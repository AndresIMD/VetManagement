using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Application.Configuration;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Audit;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>
/// Text searches must be translatable to SQL: the in-memory provider of the other tests runs any LINQ, so a search that
/// SQL Server can't run (e.g. string.Contains with a StringComparison) only failed in production, with a 500.
/// </summary>
public class SearchSqlServerTests
{
    [SqlServerFact]
    public async Task Searches_RunOnSqlServer_IgnoringCase()
    {
        await using var factory = new SqlServerAppFactory();
        int itemId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            var owner = new Client("Ana", "López", "11111111-1", "Calle 1", 912345678, "ana@example.test");
            db.Clients.Add(owner);
            var item = new Item("Jeringa 3 mL", ItemType.Material, "JER-3", null, 10, 300);
            db.Items.Add(item);
            await db.SaveChangesAsync();
            db.Pets.Add(new Pet { OwnerId = owner.Id, Name = "Gala", Breed = "Bulldog francés", Species = Species.Dog });
            db.InventoryMovements.Add(new InventoryMovement { ItemId = item.Id, Responsible = "Recepción", Quantity = 2, Type = InventoryMovementType.Egress });
            db.AuditLogs.Add(new AuditLog { EntityName = "Item", EntityId = item.Id, Action = AuditActionType.Edit, User = "Recepción", Date = DateTime.UtcNow });
            await db.SaveChangesAsync();
            itemId = item.Id;
        }
        try
        {
            var admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);

            async Task<JsonNode> GetAsync(string url)
            {
                var response = await admin.GetAsync(url);
                response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {await response.Content.ReadAsStringAsync()}");
                return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
            }

            (await GetAsync("/api/clientpet/clients/search?searchTerm=lópez")).AsArray().Should().ContainSingle();
            (await GetAsync("/api/clientpet/pets/search?searchTerm=GAL")).AsArray().Should().ContainSingle();
            (await GetAsync("/api/audit/logs/paged?user=recep"))["items"]!.AsArray().Should().ContainSingle();
            (await GetAsync("/api/inventory/movements/paged?responsible=RECEP"))["items"]!.AsArray().Should().ContainSingle();
            (await GetAsync("/api/items/paged?search=jeringa"))["items"]!.AsArray().Should().ContainSingle();
            (await GetAsync($"/api/audit/logs/paged?entityName=Item&action=Add&action=Edit&entityId=0&entityId={itemId}"))["items"]!.AsArray()
                .Should().ContainSingle();
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    /// <summary>The report list and the search of patients typed before are queries, not reads of every report.</summary>
    [SqlServerFact]
    public async Task ReportList_AndTypedPatientSearch_RunOnSqlServer()
    {
        await using var factory = new SqlServerAppFactory();
        int examId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            var exam = new Exam { Name = "Potasio", Brand = "IDEXX", Machine = "Catalyst One" };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();
            examId = exam.Id;
        }
        try
        {
            var admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
            var document = (await admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
            var settings = document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!;
            settings.Templates.Add(new ExamReportTemplate { ExamId = examId, Title = "POTASIO", Parameters = [new ReportParameter { Code = "k", Name = "K+" }] });
            (await admin.PutAsJsonAsync("/api/exam-reports/settings", new ExamReportSettingsDocument
            {
                Version = document.Version, Settings = JsonSerializer.SerializeToElement(settings, SettingsJson.Options)
            })).EnsureSuccessStatusCode();
            foreach (var (pet, received) in new[] { ("Toby", 1), ("Toby", 5), ("Luna", 3) })
                (await admin.PostAsJsonAsync("/api/exam-reports", new
                {
                    ExamId = examId, ReceivedOn = new DateOnly(2026, 10, received), Values = new Dictionary<string, string> { ["k"] = "4.5" },
                    Patient = new { Name = pet, Species = Species.Dog, OwnerName = "Ana", OwnerLastName = "Rojas" }
                })).EnsureSuccessStatusCode();

            var reports = (await admin.GetFromJsonAsync<List<ExamReportDto>>("/api/exam-reports"))!;
            reports.Select(r => r.ReceivedOn.Day).Should().Equal(5, 3, 1);
            var patients = (await admin.GetFromJsonAsync<List<ExamReportPatientDto>>("/api/exam-reports/provisional-patients?search=ROJ"))!;
            patients.Select(p => p.Name).Should().Equal("LUNA", "TOBY");
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
