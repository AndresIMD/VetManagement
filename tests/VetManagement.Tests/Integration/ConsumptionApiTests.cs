using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Consumption;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Enums;

namespace VetManagement.Tests.Integration;

/// <summary>Consumption sheets: supplies recorded on a patient during a stay and charged at discharge.</summary>
public class ConsumptionApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _supervisor = factory.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.INVENTORY, Permissions.ROLES.SUPERVISOR])]);
    private readonly HttpClient _staff = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.CLINICAL)]);

    private async Task<int> ItemAsync(string name, ItemType type, int sellPrice)
    {
        var response = await _supervisor.PostAsJsonAsync("/api/items", new ItemCreateRequest
        {
            Name = name, Type = type, SellPrice = sellPrice, Barcode = Guid.NewGuid().ToString("N")[..16], Compound = type == ItemType.Drug ? name : null, ML = 10
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ItemDto>())!.Id;
    }

    private static async Task<int> IdAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<ConsumptionSheetDto> SheetAsync(int id) => (await _staff.GetFromJsonAsync<ConsumptionSheetDto>($"/api/consumption-sheets/{id}"))!;

    private async Task<HttpResponseMessage> UseAsync(int sheetId, int itemId, decimal quantity)
        => await _staff.PostAsJsonAsync($"/api/consumption-sheets/{sheetId}/entries", new AddConsumptionEntryRequest { ItemId = itemId, Quantity = quantity });

    [Fact]
    public async Task Sheet_AddsUpUnitsMlAndAmpoules_AndClosesAtDischarge()
    {
        var fentanyl = await ItemAsync("Fentanilo 10 mL", ItemType.Drug, 15000);
        var diazepam = await ItemAsync("Diazepam", ItemType.Drug, 4000);
        var syringe = await ItemAsync("Jeringa 3 mL", ItemType.Material, 300);
        (await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest
            { ItemId = fentanyl, Charge = ConsumptionCharge.Container, ContainerMl = 10, Controlled = true })).EnsureSuccessStatusCode();
        (await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest
            { ItemId = diazepam, Charge = ConsumptionCharge.PerMl, PricePerMl = 800, Controlled = true })).EnsureSuccessStatusCode();

        var sheet = await IdAsync(await _staff.PostAsJsonAsync("/api/consumption-sheets", new OpenConsumptionSheetRequest { PatientName = " Toby ", OwnerName = "Ana Pérez" }));
        (await UseAsync(sheet, fentanyl, 3)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, fentanyl, 4)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, diazepam, 1.5m)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, syringe, 2)).EnsureSuccessStatusCode();

        var halfSyringe = await UseAsync(sheet, syringe, 1.5m);
        halfSyringe.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await halfSyringe.Content.ReadAsStringAsync()).Should().Contain("whole numbers");

        var open = await SheetAsync(sheet);
        open.PatientName.Should().Be("Toby");
        open.Lines.Single(l => l.ItemId == fentanyl).Should().Match<ConsumptionLineDto>(l => l.Containers == 1 && l.LeftMl == 3 && l.Total == 15000);
        open.Lines.Single(l => l.ItemId == diazepam).Total.Should().Be(1200);
        open.Lines.Single(l => l.ItemId == syringe).Total.Should().Be(600);
        open.Total.Should().Be(16800);

        (await _staff.PostAsync($"/api/consumption-sheets/{sheet}/items/{fentanyl}/discard-rest", null)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, fentanyl, 1)).EnsureSuccessStatusCode();
        (await SheetAsync(sheet)).Lines.Single(l => l.ItemId == fentanyl).Should().Match<ConsumptionLineDto>(l => l.Containers == 2 && l.Discarded == 3 && l.LeftMl == 9);

        (await _staff.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();
        var closed = await SheetAsync(sheet);
        closed.ClosedAtUtc.Should().NotBeNull();
        closed.Lines.Single(l => l.ItemId == fentanyl).Should().Match<ConsumptionLineDto>(l => l.LeftMl == 0 && l.Discarded == 12);
        closed.Total.Should().Be(31800);

        var late = await UseAsync(sheet, syringe, 1);
        late.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await late.Content.ReadAsStringAsync()).Should().Contain("closed");
        (await _staff.PostAsync($"/api/consumption-sheets/{sheet}/reopen", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _supervisor.PostAsync($"/api/consumption-sheets/{sheet}/reopen", null)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, syringe, 1)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PriceChanges_DontAlterUsesAlreadyRecorded_AndAMistakeCanBeRemovedWhileOpen()
    {
        var item = await ItemAsync("Midazolam", ItemType.Drug, 3000);
        (await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest
            { ItemId = item, Charge = ConsumptionCharge.PerMl, PricePerMl = 500 })).EnsureSuccessStatusCode();
        var sheet = await IdAsync(await _staff.PostAsJsonAsync("/api/consumption-sheets", new OpenConsumptionSheetRequest { PatientName = "Luna" }));
        (await UseAsync(sheet, item, 2)).EnsureSuccessStatusCode();
        var mistake = await IdAsync(await UseAsync(sheet, item, 5));

        (await _staff.DeleteAsync($"/api/consumption-sheets/{sheet}/entries/{mistake}")).EnsureSuccessStatusCode();
        (await SheetAsync(sheet)).Total.Should().Be(1000);

        (await _staff.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest
            { ItemId = item, Charge = ConsumptionCharge.PerMl, PricePerMl = 900 })).StatusCode.Should().Be(HttpStatusCode.Forbidden, "only supervisors set prices");
        (await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest
            { ItemId = item, Charge = ConsumptionCharge.PerMl, PricePerMl = 900 })).EnsureSuccessStatusCode();
        (await SheetAsync(sheet)).Entries.Single().Should().Match<ConsumptionEntryDto>(e => e.Quantity == 2);
        (await UseAsync(sheet, item, 1)).EnsureSuccessStatusCode();
        var line = (await SheetAsync(sheet)).Lines.Single();
        line.UnitPrice.Should().Be(900, "the sheet shows the current price");
        line.Total.Should().Be(2 * 500 + 900, "each use keeps the price it was recorded with");
    }

    [Fact]
    public async Task Pricing_ByContainer_NeedsItsSize_AndPerMl_ItsPrice()
    {
        var item = await ItemAsync("Ketamina", ItemType.Drug, 9000);

        var noSize = await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest { ItemId = item, Charge = ConsumptionCharge.Container });
        noSize.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var noPrice = await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest { ItemId = item, Charge = ConsumptionCharge.PerMl });
        noPrice.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _supervisor.GetFromJsonAsync<List<ConsumptionPricingDto>>("/api/consumption-sheets/pricing"))!.Should().NotContain(p => p.ItemId == item);
    }

    [Fact]
    public async Task Laboratory_HasNoAccess()
    {
        var lab = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.LABORATORY)]);

        (await lab.GetAsync("/api/consumption-sheets")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
