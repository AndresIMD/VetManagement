using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Domain.Audit;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
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
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
