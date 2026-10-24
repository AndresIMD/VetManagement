using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Application.Configuration;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Configuration;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>
/// An exam can name the inventory product one exam uses up (e.g. its test kit): its reports deduct it once, with a movement
/// naming the report and the patient, and put it back when returned or deleted.
/// </summary>
public class ExamReportStockTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
    private readonly HttpClient _laboratory = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.LABORATORY)]);
    private readonly HttpClient _clinical = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.CLINICAL)]);

    internal static async Task<T> WithDbAsync<T>(CustomWebAppFactory factory, Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work) => WithDbAsync(factory, work);

    private Task<int> AddItemAsync(int stock) => WithDbAsync(async db =>
    {
        var item = new Item($"Kit T4 {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock, 9000);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    private Task<int> StockOfAsync(int itemId) => WithDbAsync(async db => (await db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId)).Stock);

    private Task<List<InventoryMovement>> MovementsOfAsync(int itemId)
        => WithDbAsync(db => db.InventoryMovements.AsNoTracking().Where(m => m.ItemId == itemId).OrderBy(m => m.Id).ToListAsync());

    /// <summary>Rocky, of Ana Parker, and a T4 exam with a one-line template; the exam uses <paramref name="quantity"/> of the item.</summary>
    private async Task<(int PetId, int ExamId)> AddPetAndExamAsync(int? itemId, int quantity = 1)
    {
        var (petId, examId) = await WithDbAsync(async db =>
        {
            var owner = new Client("Ana", "Parker", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 1", 912345678, "ana@example.test");
            db.Clients.Add(owner);
            var exam = new Exam { Name = $"T4 {Guid.NewGuid():N}", Brand = "BioNote", Machine = "Vcheck V200", ItemId = itemId, ItemQuantity = quantity };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();
            var pet = new Pet { OwnerId = owner.Id, Name = "Rocky", Breed = "Golden", Species = Species.Dog };
            db.Pets.Add(pet);
            await db.SaveChangesAsync();
            return (pet.Id, exam.Id);
        });
        await AddTemplateAsync(examId);
        return (petId, examId);
    }

    private async Task AddTemplateAsync(int examId)
    {
        var document = (await _admin.GetFromJsonAsync<ExamReportSettingsDocument>("/api/exam-reports/settings"))!;
        var settings = document.Settings.Deserialize<ExamReportSettings>(SettingsJson.Options)!;
        settings.Templates.Add(new ExamReportTemplate
        {
            ExamId = examId,
            Title = "T4",
            Parameters = [new ReportParameter { Code = "t4", Name = "T4", Unit = "µg/dL" }]
        });
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
        PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 20), Values = new Dictionary<string, string> { ["t4"] = "2.1" }
    });

    private async Task<ExamReportDto> ReportAsync(int id) => (await _admin.GetFromJsonAsync<ExamReportDto>($"/api/exam-reports/{id}"))!;

    [Fact]
    public async Task Deduct_TakesTheExamsProduct_WithAMovementNamingTheReportAndPatient()
    {
        var itemId = await AddItemAsync(stock: 10);
        var (petId, examId) = await AddPetAndExamAsync(itemId, quantity: 2);
        var reportId = await CreateReportAsync(petId, examId);

        var before = await ReportAsync(reportId);
        before.StockToDeduct.Should().BeEquivalentTo(new { Quantity = 2, InStock = 10 });
        before.StockDeducted.Should().BeNull();

        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await StockOfAsync(itemId)).Should().Be(8);
        var movement = (await MovementsOfAsync(itemId)).Single();
        movement.Type.Should().Be(InventoryMovementType.Egress);
        movement.Quantity.Should().Be(2);
        movement.Reason.Should().Be($"Exam report #{reportId} – ROCKY PARKER");
        var after = await ReportAsync(reportId);
        after.StockToDeduct.Should().BeNull();
        after.StockDeducted.Should().BeEquivalentTo(new { Quantity = 2, By = "test-user" });
        after.StockDeducted!.ItemName.Should().StartWith("Kit T4");
    }

    [Fact]
    public async Task ASecondDeduct_IsRefused_SoTheProductIsTakenOnce()
    {
        var itemId = await AddItemAsync(stock: 10);
        var (petId, examId) = await AddPetAndExamAsync(itemId);
        var reportId = await CreateReportAsync(petId, examId);

        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await StockOfAsync(itemId)).Should().Be(9);
        (await MovementsOfAsync(itemId)).Should().ContainSingle();
    }

    [Fact]
    public async Task NotEnoughStock_IsRefused_AndNothingMoves()
    {
        var itemId = await AddItemAsync(stock: 1);
        var (petId, examId) = await AddPetAndExamAsync(itemId, quantity: 2);
        var reportId = await CreateReportAsync(petId, examId);

        var response = await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Only 1 of");
        (await StockOfAsync(itemId)).Should().Be(1);
        (await ReportAsync(reportId)).StockDeducted.Should().BeNull();
    }

    [Fact]
    public async Task AnExamWithoutAProduct_HasNothingToDeduct()
    {
        var (petId, examId) = await AddPetAndExamAsync(itemId: null);
        var reportId = await CreateReportAsync(petId, examId);

        (await ReportAsync(reportId)).StockToDeduct.Should().BeNull();
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Return_PutsTheProductBack_AndItCanBeDeductedAgain()
    {
        var itemId = await AddItemAsync(stock: 5);
        var (petId, examId) = await AddPetAndExamAsync(itemId);
        var reportId = await CreateReportAsync(petId, examId);
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _laboratory.DeleteAsync($"/api/exam-reports/{reportId}/stock")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await StockOfAsync(itemId)).Should().Be(5);
        var returned = (await MovementsOfAsync(itemId)).Last();
        returned.Type.Should().Be(InventoryMovementType.Ingress);
        returned.Reason.Should().Be($"Exam report #{reportId} – ROCKY PARKER (returned)");
        (await ReportAsync(reportId)).StockToDeduct.Should().NotBeNull();
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await StockOfAsync(itemId)).Should().Be(4);
    }

    [Fact]
    public async Task DeletingAReportThatTookTheProduct_PutsItBack()
    {
        var itemId = await AddItemAsync(stock: 5);
        var (petId, examId) = await AddPetAndExamAsync(itemId);
        var reportId = await CreateReportAsync(petId, examId);
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _admin.DeleteAsync($"/api/exam-reports/{reportId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await StockOfAsync(itemId)).Should().Be(5);
        (await MovementsOfAsync(itemId)).Last().Reason.Should().Be($"Exam report #{reportId} – ROCKY PARKER (deleted)");
    }

    [Fact]
    public async Task CorrectingTheReport_KeepsWhatItTook()
    {
        var itemId = await AddItemAsync(stock: 5);
        var (petId, examId) = await AddPetAndExamAsync(itemId);
        var reportId = await CreateReportAsync(petId, examId);
        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _laboratory.PutAsJsonAsync($"/api/exam-reports/{reportId}", new
        {
            PetId = petId, ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 20), Values = new Dictionary<string, string> { ["t4"] = "3.4" }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await ReportAsync(reportId);
        report.Results.Single().Value.Should().Be("3.4");
        report.StockDeducted.Should().NotBeNull();
        (await StockOfAsync(itemId)).Should().Be(4);
    }

    [Fact]
    public async Task ATypedPatient_IsNamedAsTyped()
    {
        var itemId = await AddItemAsync(stock: 5);
        var (_, examId) = await AddPetAndExamAsync(itemId);
        var reportId = await CreateReportAsync(new
        {
            ExamId = examId, ReceivedOn = new DateOnly(2026, 10, 20), Values = new Dictionary<string, string> { ["t4"] = "2.1" },
            Patient = new { Name = "Luna", Species = Species.Cat, OwnerName = "Eva", OwnerLastName = "Rojas" }
        });

        (await _laboratory.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await MovementsOfAsync(itemId)).Single().Reason.Should().Be($"Exam report #{reportId} – LUNA ROJAS");
    }

    [Fact]
    public async Task WhoeverMakesReports_CanDeduct_ButReturningIsACorrection()
    {
        var itemId = await AddItemAsync(stock: 5);
        var (petId, examId) = await AddPetAndExamAsync(itemId);
        var reportId = await CreateReportAsync(petId, examId);

        // The Clinical role makes reports (no inventory permissions needed) but doesn't correct them.
        (await _clinical.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _clinical.DeleteAsync($"/api/exam-reports/{reportId}/stock")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _laboratory.DeleteAsync($"/api/exam-reports/{reportId}/stock")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheCatalog_LinksAnExistingProduct_AndShowsItsName()
    {
        var itemId = await AddItemAsync(stock: 7);
        var itemName = await WithDbAsync(async db => (await db.Items.FindAsync(itemId))!.Name);
        var exam = new { Name = $"SNAP {Guid.NewGuid():N}", Brand = "IDEXX", Machine = "SNAP", SampleType = SampleType.Blood, SampleContainer = SampleContainer.Tube };

        (await _admin.PostAsJsonAsync("/api/exams", new { exam.Name, exam.Brand, exam.Machine, exam.SampleType, exam.SampleContainer, ItemId = 999_999 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PostAsJsonAsync("/api/exams", new { exam.Name, exam.Brand, exam.Machine, exam.SampleType, exam.SampleContainer, ItemId = itemId, ItemQuantity = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = (await _admin.GetFromJsonAsync<List<ExamDto>>("/api/exams"))!.Single(e => e.Name == exam.Name);
        saved.Should().BeEquivalentTo(new { ItemId = itemId, ItemName = itemName, ItemQuantity = 2 });
        var found = (await _admin.GetFromJsonAsync<List<ExamStockItemDto>>($"/api/exams/stock-items?search={Uri.EscapeDataString(itemName)}"))!;
        found.Should().ContainSingle(i => i.Id == itemId && i.Stock == 7);
    }
}

/// <summary>A clinic without the Inventory module: exams and reports work, but nothing about stock shows or moves.</summary>
public sealed class NoInventoryAppFactory : CustomWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Modules:Enabled:0", "Clients");
        builder.UseSetting("Modules:Enabled:1", "Exams");
    }
}

public class ExamReportStockWithoutInventoryTests(NoInventoryAppFactory factory) : IClassFixture<NoInventoryAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);

    [Fact]
    public async Task NothingAboutStockShowsOrMoves_AndSavingAnExamKeepsItsLink()
    {
        var (itemId, examId, reportId) = await ExamReportStockTests.WithDbAsync(factory, async db =>
        {
            var item = new Item("Kit cPL", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, 10, 9000);
            db.Items.Add(item);
            var owner = new Client("Ana", "Parker", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 1", 912345678, "ana@example.test");
            db.Clients.Add(owner);
            await db.SaveChangesAsync();
            var exam = new Exam { Name = "cPL", Brand = "BioNote", Machine = "Vcheck V200", ItemId = item.Id, ItemQuantity = 1 };
            var pet = new Pet { OwnerId = owner.Id, Name = "Rocky", Breed = "Golden", Species = Species.Dog };
            db.AddRange(exam, pet);
            await db.SaveChangesAsync();
            var report = new ExamReport { PetId = pet.Id, ExamId = exam.Id, Title = "cPL", ReceivedOn = new DateOnly(2026, 10, 20), CreatedBy = "test" };
            db.ExamReports.Add(report);
            await db.SaveChangesAsync();
            return (item.Id, exam.Id, report.Id);
        });

        (await _admin.GetFromJsonAsync<ExamReportDto>($"/api/exam-reports/{reportId}"))!.StockToDeduct.Should().BeNull();
        (await _admin.PostAsync($"/api/exam-reports/{reportId}/stock", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _admin.DeleteAsync($"/api/exam-reports/{reportId}/stock")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _admin.GetAsync("/api/exams/stock-items")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _admin.GetFromJsonAsync<List<ExamDto>>("/api/exams"))!.Single(e => e.Id == examId).ItemId.Should().BeNull();

        // The form can't show the link, so saving the exam doesn't drop it.
        (await _admin.PutAsJsonAsync($"/api/exams/{examId}", new { Name = "cPL", Brand = "BioNote", Machine = "Vcheck V200", ItemId = (int?)null }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await ExamReportStockTests.WithDbAsync(factory, async db => (await db.Exams.AsNoTracking().FirstAsync(e => e.Id == examId)).ItemId);
        link.Should().Be(itemId);
    }
}
