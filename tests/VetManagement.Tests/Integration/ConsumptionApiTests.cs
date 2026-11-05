using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Consumption;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Consumption;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Consumption sheets: supplies recorded on a patient during a stay, charged at discharge and taken from the daily-use
/// warehouse as they are used.
/// </summary>
public class ConsumptionApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _supervisor = factory.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.INVENTORY, Permissions.ROLES.SUPERVISOR])]);
    private readonly HttpClient _staff = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.CLINICAL)]);

    /// <summary>Staff who run the lab and the pharmacy: both roles, as one clinic has them.</summary>
    private readonly HttpClient _lab = factory.CreateClientWithPermissions([.. Permissions.ForRoles([Permissions.ROLES.LABORATORY, Permissions.ROLES.INVENTORY])]);

    private Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work) => ExamReportStockTests.WithDbAsync(factory, work);

    /// <summary>An item with <paramref name="stock"/> units, all in the daily-use warehouse.</summary>
    private Task<int> StockedItemAsync(string name, int stock, int sellPrice = 1000) => WithDbAsync(async db =>
    {
        var item = new Item($"{name} {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock, sellPrice);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    private Task<int> StockOfAsync(int itemId) => WithDbAsync(async db => (await db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId)).Stock);

    private Task<List<InventoryMovement>> MovementsOfAsync(int itemId)
        => WithDbAsync(db => db.InventoryMovements.AsNoTracking().Where(m => m.ItemId == itemId).OrderBy(m => m.Id).ToListAsync());

    private async Task PricingAsync(int itemId, ConsumptionCharge charge, decimal? containerMl = null, int? pricePerMl = null, bool beforehand = false)
        => (await SetPricingAsync(itemId, charge, containerMl, pricePerMl, beforehand)).EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> SetPricingAsync(int itemId, ConsumptionCharge charge, decimal? containerMl = null, int? pricePerMl = null,
        bool beforehand = false)
        => _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", new ConsumptionPricingRequest
            { ItemId = itemId, Charge = charge, ContainerMl = containerMl, PricePerMl = pricePerMl, TakenOutBeforehand = beforehand });

    private async Task<int> OpenAsync(string patient)
        => await IdAsync(await _lab.PostAsJsonAsync("/api/consumption-sheets", new OpenConsumptionSheetRequest { PatientName = patient }));

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

    private Task<HttpResponseMessage> BudgetAsync(int sheetId, int itemId, decimal quantity, string? notes = null)
        => _lab.PostAsJsonAsync($"/api/consumption-sheets/{sheetId}/budgets", new AddConsumptionBudgetRequest { ItemId = itemId, Quantity = quantity, Notes = notes });

    private async Task<ConsumptionSheetSummaryDto> ListedAsync(int sheetId)
        => (await _lab.GetFromJsonAsync<List<ConsumptionSheetSummaryDto>>("/api/consumption-sheets?closedDays=1"))!.Single(s => s.Id == sheetId);

    private async Task<ConsumptionSheetDto> SheetAsync(int id) => (await _staff.GetFromJsonAsync<ConsumptionSheetDto>($"/api/consumption-sheets/{id}"))!;

    private async Task<HttpResponseMessage> UseAsync(int sheetId, int itemId, decimal quantity, HttpClient? client = null)
        => await (client ?? _staff).PostAsJsonAsync($"/api/consumption-sheets/{sheetId}/entries", new AddConsumptionEntryRequest { ItemId = itemId, Quantity = quantity });

    [Fact]
    public async Task Sheet_TakesWholeUnitsAndEachContainerAsItStarts_AndGivesBackWhatARemovedUseNeeded()
    {
        var fentanyl = await StockedItemAsync("Fentanilo 10 mL", 5, 15000);
        var syringe = await StockedItemAsync("Jeringa 3 mL", 20, 300);
        var diazepam = await StockedItemAsync("Diazepam frasco", 3, 4000);
        await PricingAsync(fentanyl, ConsumptionCharge.Container, containerMl: 10);
        await PricingAsync(diazepam, ConsumptionCharge.PerMl, pricePerMl: 800);
        var sheet = await OpenAsync("Toby");

        (await UseAsync(sheet, fentanyl, 3, _lab)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, fentanyl, 4, _lab)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, syringe, 2, _lab)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, diazepam, 1.5m, _lab)).EnsureSuccessStatusCode();
        (await StockOfAsync(fentanyl)).Should().Be(4, "3 + 4 mL come from the one ampoule opened");
        (await StockOfAsync(syringe)).Should().Be(18);
        (await StockOfAsync(diazepam)).Should().Be(3, "a bottle charged per mL is shared, and taken out by hand when opened");

        (await _lab.PostAsync($"/api/consumption-sheets/{sheet}/items/{fentanyl}/discard-rest", null)).EnsureSuccessStatusCode();
        (await StockOfAsync(fentanyl)).Should().Be(4, "throwing away the rest of an ampoule already taken moves nothing");
        var secondAmpoule = await IdAsync(await UseAsync(sheet, fentanyl, 1, _lab));
        (await StockOfAsync(fentanyl)).Should().Be(3);

        (await _lab.DeleteAsync($"/api/consumption-sheets/{sheet}/entries/{secondAmpoule}")).EnsureSuccessStatusCode();
        (await StockOfAsync(fentanyl)).Should().Be(4, "the use that opened the second ampoule was removed");
        var movements = await MovementsOfAsync(fentanyl);
        movements.Select(m => (m.Type, m.Quantity)).Should().Equal(
            (InventoryMovementType.Egress, 1), (InventoryMovementType.Egress, 1), (InventoryMovementType.Ingress, 1));
        movements.Should().OnlyContain(m => m.Reason!.Contains($"#{sheet} – TOBY") && m.WarehouseId == null);
        (await SheetAsync(sheet)).Lines.Should().OnlyContain(l => l.StockPending == 0);
    }

    [Fact]
    public async Task AUse_IsRecordedWhenTheDailyUseWarehouseIsShort_AndTakenOnceItHasThem()
    {
        // 5 units in all, 4 of them in a locked warehouse: 1 in the daily-use one.
        var morphine = await StockedItemAsync("Morfina 1 mL", 5);
        var locked = await WithDbAsync(async db =>
        {
            var warehouse = new Warehouse { Name = $"Locked {Guid.NewGuid():N}", Roles = [Permissions.ROLES.SUPERVISOR] };
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync();
            db.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.Id, ItemId = morphine, Quantity = 4 });
            await db.SaveChangesAsync();
            return warehouse.Id;
        });
        var sheet = await OpenAsync("Max");

        (await UseAsync(sheet, morphine, 3, _lab)).EnsureSuccessStatusCode();
        (await StockOfAsync(morphine)).Should().Be(4, "only the daily-use unit is taken, never the locked ones");
        var line = (await SheetAsync(sheet)).Lines.Single();
        line.StockPending.Should().Be(2);
        var listed = (await _lab.GetFromJsonAsync<List<ConsumptionSheetSummaryDto>>("/api/consumption-sheets"))!.Single(s => s.Id == sheet);
        listed.StockPending.Should().Be(2);
        listed.Pending.Should().Equal([new ConsumptionPendingDto(line.ItemName, 2)], "the list names what is owed, item by item");

        // The head of the clinic hands out 2 from the locked warehouse; discharge takes them.
        await WithDbAsync(async db =>
        {
            (await db.WarehouseStocks.SingleAsync(s => s.WarehouseId == locked && s.ItemId == morphine)).Quantity = 2;
            return await db.SaveChangesAsync();
        });
        (await _lab.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();
        (await StockOfAsync(morphine)).Should().Be(2);
        (await SheetAsync(sheet)).Lines.Single().StockPending.Should().Be(0);
    }

    [Fact]
    public async Task ItemsFromTheQuickUseBox_AreRecordedAndCharged_WithoutTakingStockAgain()
    {
        // Filled into the box with an egress by hand: the sheet records the uses and leaves the stock alone.
        var lidocaine = await StockedItemAsync("Lidocaína 2%", 10, 900);
        var fentanyl = await StockedItemAsync("Fentanilo 10 mL", 10, 15000);
        await PricingAsync(lidocaine, ConsumptionCharge.Unit, beforehand: true);
        await PricingAsync(fentanyl, ConsumptionCharge.Container, containerMl: 10, beforehand: true);
        (await _supervisor.GetFromJsonAsync<List<ConsumptionPricingDto>>("/api/consumption-sheets/pricing"))!
            .Where(p => p.ItemId == lidocaine || p.ItemId == fentanyl).Should().OnlyContain(p => p.TakenOutBeforehand);
        var sheet = await OpenAsync("Kiara");

        (await UseAsync(sheet, lidocaine, 3, _lab)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, fentanyl, 12, _lab)).EnsureSuccessStatusCode();

        (await StockOfAsync(lidocaine)).Should().Be(10);
        (await StockOfAsync(fentanyl)).Should().Be(10);
        (await MovementsOfAsync(lidocaine)).Should().BeEmpty();
        (await MovementsOfAsync(fentanyl)).Should().BeEmpty();
        var dto = await SheetAsync(sheet);
        dto.Lines.Should().OnlyContain(l => l.StockPending == 0, "nothing is owed for what was already out");
        dto.Total.Should().Be(3 * 900 + 2 * 15000, "they are still charged");
        (await _lab.GetFromJsonAsync<List<ConsumptionSheetSummaryDto>>("/api/consumption-sheets"))!.Single(s => s.Id == sheet)
            .Should().Match<ConsumptionSheetSummaryDto>(s => s.StockPending == 0 && s.Pending.Count == 0);
    }

    [Fact]
    public async Task TheBoxSetting_WaitsForOpenSheets_AndNeverChangesWhatRecordedUsesTook()
    {
        // One unit in stock, two used: one taken, one owed.
        var ketamine = await StockedItemAsync("Ketamina 10 mL", 1);
        var sheet = await OpenAsync("Bruno");
        (await UseAsync(sheet, ketamine, 2, _lab)).EnsureSuccessStatusCode();
        (await StockOfAsync(ketamine)).Should().Be(0);

        (await SetPricingAsync(ketamine, ConsumptionCharge.Unit, beforehand: true)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "a sheet not discharged yet uses it");
        (await _lab.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();
        await PricingAsync(ketamine, ConsumptionCharge.Unit, beforehand: true);

        // Stock arrives; reopening takes what the two earlier uses owe, and a use recorded now comes from the box.
        await WithDbAsync(async db =>
        {
            (await db.Items.SingleAsync(i => i.Id == ketamine)).Stock = 5;
            return await db.SaveChangesAsync();
        });
        (await _supervisor.PostAsync($"/api/consumption-sheets/{sheet}/reopen", null)).EnsureSuccessStatusCode();
        (await UseAsync(sheet, ketamine, 1, _lab)).EnsureSuccessStatusCode();
        (await _supervisor.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();

        (await StockOfAsync(ketamine)).Should().Be(4, "the owed unit was taken; the use from the box took nothing");
        (await MovementsOfAsync(ketamine)).Select(m => (m.Type, m.Quantity)).Should().Equal(
            (InventoryMovementType.Egress, 1), (InventoryMovementType.Egress, 1));
        (await SheetAsync(sheet)).Lines.Single().Should().Match<ConsumptionLineDto>(l => l.Given == 3 && l.StockPending == 0);
    }

    [Fact]
    public async Task ABudget_CountsDownAsUsesAreRecorded_AndMovesNoStock()
    {
        // Lidocaine comes from the quick-use box; the guardian approves 5. The catheters are approved before any is used.
        var lidocaine = await StockedItemAsync("Lidocaína 2%", 20, 900);
        await PricingAsync(lidocaine, ConsumptionCharge.Unit, beforehand: true);
        var catheter = await StockedItemAsync("Catéter 22G", 10);
        var sheet = await OpenAsync("Nala");

        var first = await IdAsync(await BudgetAsync(sheet, lidocaine, 5, "Presupuesto 123"));
        (await BudgetAsync(sheet, catheter, 2)).EnsureSuccessStatusCode();
        (await StockOfAsync(catheter)).Should().Be(10, "a budget moves no stock");
        var dto = await SheetAsync(sheet);
        dto.Lines.Should().BeEmpty("nothing was used yet");
        dto.Balances.Select(b => (b.ItemId, b.Left)).Should().BeEquivalentTo(new[] { (lidocaine, 5m), (catheter, 2m) });

        for (var i = 0; i < 5; i++)
            (await UseAsync(sheet, lidocaine, 1, _lab)).EnsureSuccessStatusCode();
        (await ListedAsync(sheet)).UsedUp.Should().ContainSingle().Which.Should().Match<ConsumptionBalanceDto>(b => b.ItemId == lidocaine && b.Left == 0);
        (await StockOfAsync(lidocaine)).Should().Be(20, "it comes from the box");

        // A new approval adds to the first.
        (await BudgetAsync(sheet, lidocaine, 2)).EnsureSuccessStatusCode();
        dto = await SheetAsync(sheet);
        dto.Balances.Single(b => b.ItemId == lidocaine).Should().Match<ConsumptionBalanceDto>(b =>
            b.Budgeted == 7 && b.Used == 5 && b.Left == 2 && !b.UsedUp && b.ToRefund == 2);
        dto.Budgets.Select(b => b.Quantity).Should().Equal([2m, 2m, 5m], "newest first");
        dto.Budgets.Last().Notes.Should().Be("Presupuesto 123");
        (await ListedAsync(sheet)).UsedUp.Should().BeEmpty();

        // An approval recorded by mistake is removed.
        (await _lab.DeleteAsync($"/api/consumption-sheets/{sheet}/budgets/{first}")).EnsureSuccessStatusCode();
        (await SheetAsync(sheet)).Balances.Single(b => b.ItemId == lidocaine).Should().Match<ConsumptionBalanceDto>(b =>
            b.Budgeted == 2 && b.Left == -3 && b.UsedUp && b.ToRefund == 0);

        // Whole units only, and none once discharged.
        (await BudgetAsync(sheet, catheter, 1.5m)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _lab.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();
        (await BudgetAsync(sheet, catheter, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SheetAsync(sheet)).Balances.Single(b => b.ItemId == catheter).ToRefund.Should().Be(2, "never used: to give back");
    }

    [Fact]
    public async Task ABudgetAlone_KeepsHowTheItemIsChargedFromChanging()
    {
        var propofol = await StockedItemAsync("Propofol 20 mL", 10);
        var sheet = await OpenAsync("Coco");
        (await BudgetAsync(sheet, propofol, 2)).EnsureSuccessStatusCode();

        (await SetPricingAsync(propofol, ConsumptionCharge.Container, containerMl: 20)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "the budget counts units");
        (await _lab.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();
        await PricingAsync(propofol, ConsumptionCharge.Container, containerMl: 20);
    }

    [Fact]
    public async Task DeletingASheet_ReturnsWhatItTook()
    {
        var catheter = await StockedItemAsync("Catéter 22G", 10);
        var sheet = await OpenAsync("Luna");
        (await UseAsync(sheet, catheter, 3, _lab)).EnsureSuccessStatusCode();
        (await StockOfAsync(catheter)).Should().Be(7);

        (await _supervisor.DeleteAsync($"/api/consumption-sheets/{sheet}")).EnsureSuccessStatusCode();
        (await StockOfAsync(catheter)).Should().Be(10);
        (await MovementsOfAsync(catheter)).Last().Should().Match<InventoryMovement>(m => m.Type == InventoryMovementType.Ingress && m.Quantity == 3);
    }

    [Fact]
    public async Task ASheetOpenedBeforeSheetsTookStock_MovesNothing()
    {
        var gauze = await StockedItemAsync("Gasa", 10);
        var sheet = await WithDbAsync(async db =>
        {
            var old = new ConsumptionSheet { PatientName = "Kira", OpenedAtUtc = DateTime.UtcNow.AddDays(-1), MovesStock = false };
            db.ConsumptionSheets.Add(old);
            await db.SaveChangesAsync();
            return old.Id;
        });

        (await UseAsync(sheet, gauze, 2, _lab)).EnsureSuccessStatusCode();
        (await StockOfAsync(gauze)).Should().Be(10);
        var dto = await SheetAsync(sheet);
        dto.MovesStock.Should().BeFalse();
        dto.Lines.Single().StockPending.Should().Be(0, "nothing is pending on a sheet that doesn't take stock");
    }

    [Fact]
    public async Task ADischargedSheetThatStillOwesStock_StaysInTheList_UntilASupervisorSettlesIt()
    {
        var ketamine = await StockedItemAsync("Ketamina 10 mL", 1);
        var sheet = await OpenAsync("Rocky");
        (await UseAsync(sheet, ketamine, 2, _lab)).EnsureSuccessStatusCode();
        (await _lab.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();

        var listed = (await _lab.GetFromJsonAsync<List<ConsumptionSheetSummaryDto>>("/api/consumption-sheets"))!.Single(s => s.Id == sheet);
        listed.Should().Match<ConsumptionSheetSummaryDto>(s => s.ClosedAtUtc != null && s.StockPending == 1, "it owes one unit");

        await WithDbAsync(async db =>
        {
            (await db.Items.SingleAsync(i => i.Id == ketamine)).Stock = 3;
            return await db.SaveChangesAsync();
        });
        (await _supervisor.PostAsync($"/api/consumption-sheets/{sheet}/reopen", null)).EnsureSuccessStatusCode();
        (await _supervisor.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();
        (await StockOfAsync(ketamine)).Should().Be(2);
        (await _lab.GetFromJsonAsync<List<ConsumptionSheetSummaryDto>>("/api/consumption-sheets?closedDays=1"))!.Single(s => s.Id == sheet)
            .Pending.Should().BeEmpty("it took what it owed");
        (await _lab.GetFromJsonAsync<List<ConsumptionSheetSummaryDto>>("/api/consumption-sheets"))!.Should().NotContain(s => s.Id == sheet,
            "a discharged sheet that owes nothing leaves the default list");
    }

    [Fact]
    public async Task HowAnItemIsCharged_WaitsForTheSheetsUsingItToBeDischarged()
    {
        var propofol = await StockedItemAsync("Propofol 20 mL", 10);
        var sheet = await OpenAsync("Nala");
        (await UseAsync(sheet, propofol, 1, _lab)).EnsureSuccessStatusCode();

        var byContainer = new ConsumptionPricingRequest { ItemId = propofol, Charge = ConsumptionCharge.Container, ContainerMl = 20 };
        var refused = await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", byContainer);
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the open sheet would add up its unit as mL");
        (await refused.Content.ReadAsStringAsync()).Should().Contain("not discharged yet");

        (await _lab.PostAsync($"/api/consumption-sheets/{sheet}/close", null)).EnsureSuccessStatusCode();
        (await _supervisor.PutAsJsonAsync("/api/consumption-sheets/pricing", byContainer)).EnsureSuccessStatusCode();
        (await StockOfAsync(propofol)).Should().Be(9, "the discharged sheet keeps what it took");
    }

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
