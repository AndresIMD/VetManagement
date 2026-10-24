using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VetManagement.Api.Authorization;
using VetManagement.Application.Configuration;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Configuration;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Inventory;

namespace VetManagement.Tests.Integration;

/// <summary>"Update format": a saved report takes the exam's current template, keeping its own data.</summary>
public class ExamReportRenewTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
    private readonly HttpClient _laboratory = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.LABORATORY)]);
    private readonly HttpClient _clinical = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.CLINICAL)]);

    /// <summary>Rocky, of Ana Parker, and a T4 exam (optionally using an inventory kit) whose template has no bar yet.</summary>
    private async Task<(int PetId, int ExamId)> AddPetAndExamAsync(int? itemId = null)
    {
        var (petId, examId) = await ExamReportStockTests.WithDbAsync(factory, async db =>
        {
            var owner = new Client("Ana", "Parker", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 1", 912345678, "ana@example.test");
            var exam = new Exam { Name = $"T4 {Guid.NewGuid():N}", Brand = "BioNote", Machine = "Vcheck V200", ItemId = itemId };
            db.AddRange(owner, exam);
            await db.SaveChangesAsync();
            var pet = new Pet { OwnerId = owner.Id, Name = "Rocky", Breed = "Golden", Species = Species.Dog };
            db.Pets.Add(pet);
            await db.SaveChangesAsync();
            return (pet.Id, exam.Id);
        });
        await ChangeTemplateAsync(examId, template => template.Parameters.AddRange(
            [new ReportParameter { Code = "t4", Name = "T4", Unit = "µg/dL", Ranges = [new ReferenceRange { Min = 1, Max = 4 }] }]));
        return (petId, examId);
    }

    /// <summary>Changes the exam's template (creating it, titled "T4", the first time).</summary>
    private async Task ChangeTemplateAsync(int examId, Action<ExamReportTemplate> change)
    {
        var document = (await _admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
        var settings = document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!;
        if (settings.FindTemplate(examId) is not { } template)
            settings.Templates.Add(template = new ExamReportTemplate { ExamId = examId, Title = "T4" });
        change(template);
        var response = await _admin.PutAsJsonAsync("/api/exam-reports/settings", new ExamReportSettingsDocument
        {
            Version = document.Version,
            Settings = JsonSerializer.SerializeToElement(settings, SettingsJson.Options)
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<int> CreateReportAsync(object request)
    {
        var response = await _laboratory.PostAsJsonAsync("/api/exam-reports", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
    }

    private Task<int> CreateReportAsync(int petId, int examId) => CreateReportAsync(new
    {
        PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 1), ReferringVet = "Dra. Rojas", LotNumber = "A123",
        Interpretation = "Normal para su edad.", Values = new Dictionary<string, string> { ["t4"] = "2,1" }
    });

    private async Task<ExamReportDto> ReportAsync(int id) => (await _admin.GetFromJsonAsync<ExamReportDto>($"/api/exam-reports/{id}"))!;

    [Fact]
    public async Task Renewing_AppliesTheCurrentTemplate_AndKeepsTheReportsOwnData()
    {
        var (petId, examId) = await AddPetAndExamAsync();
        var reportId = await CreateReportAsync(petId, examId);
        var before = await ReportAsync(reportId);
        before.Results.Single().BarMin.Should().BeNull();

        await ChangeTemplateAsync(examId, template =>
        {
            template.Title = "T4 TOTAL";
            template.Parameters.Single().ShowBar = true;
        });
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/renew", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await ReportAsync(reportId);
        after.Title.Should().Be("T4 TOTAL");
        after.Results.Single().Should().BeEquivalentTo(new { Value = "2,1", BarMin = 1m, BarMax = 4m });
        after.Should().BeEquivalentTo(new
        {
            before.ReceivedOn, before.CreatedAtUtc, before.CreatedBy, before.ReferringVet, before.LotNumber, before.Interpretation, before.PetId
        });
    }

    [Fact]
    public async Task Renewing_KeepsWhatTheReportTookFromInventory()
    {
        var itemId = await ExamReportStockTests.WithDbAsync(factory, async db =>
        {
            var item = new Item($"Kit T4 {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, 5, 9000);
            db.Items.Add(item);
            await db.SaveChangesAsync();
            return item.Id;
        });
        var (petId, examId) = await AddPetAndExamAsync(itemId);
        var reportId = await CreateReportAsync(petId, examId);
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var taken = (await ReportAsync(reportId)).StockDeducted;

        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/renew", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReportAsync(reportId)).StockDeducted.Should().BeEquivalentTo(taken);
        (await ExamReportStockTests.WithDbAsync(factory, async db => (await db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId)).Stock))
            .Should().Be(4);
    }

    [Fact]
    public async Task Renewing_IsRefused_WhenAValuesParameterLeftTheTemplate_AndTheReportStaysAsItWas()
    {
        var (petId, examId) = await AddPetAndExamAsync();
        var reportId = await CreateReportAsync(petId, examId);
        await ChangeTemplateAsync(examId, template =>
        {
            template.Title = "T4 LIBRE";
            template.Parameters = [new ReportParameter { Code = "t4-libre", Name = "T4 libre", Unit = "ng/dL" }];
        });

        var response = await _laboratory.PostAsync($"/api/exam-reports/{reportId}/renew", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("t4");
        var report = await ReportAsync(reportId);
        report.Title.Should().Be("T4");
        report.Results.Single().Value.Should().Be("2,1");
    }

    [Fact]
    public async Task RenewingATypedPatientsReport_KeepsItsProvisionalPet()
    {
        var (_, examId) = await AddPetAndExamAsync();
        var reportId = await CreateReportAsync(new
        {
            ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 1), Values = new Dictionary<string, string> { ["t4"] = "2.1" },
            Patient = new { Name = "Luna", Species = Species.Cat, OwnerName = "Eva", OwnerLastName = "Rojas" }
        });
        var state = () => ExamReportStockTests.WithDbAsync(factory, async db =>
            ((await db.ExamReports.AsNoTracking().FirstAsync(r => r.Id == reportId)).PetId, await db.Clients.IgnoreQueryFilters().CountAsync()));
        var before = await state();

        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/renew", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await state()).Should().Be(before);
        (await ReportAsync(reportId)).PetName.Should().Be("LUNA");
    }

    [Fact]
    public async Task Renewing_IsACorrection()
    {
        var (petId, examId) = await AddPetAndExamAsync();
        var reportId = await CreateReportAsync(petId, examId);

        (await _clinical.PostAsync($"/api/exam-reports/{reportId}/renew", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/renew", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
