using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Warehouses: the daily-use one holds what no other does and serves the automatic stock changes; the others are seen,
/// and their stock moved, only by their roles. Nobody learns how many units a warehouse they don't see holds.
/// </summary>
public class WarehousesApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _admin;

    public WarehousesApiTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.ADMIN)]);
    }

    /// <summary>A user with these roles: their permissions, and the role names warehouses list.</summary>
    private HttpClient UserWith(params string[] roles) => _factory.CreateClientWithClaims([
        .. Permissions.ForRoles(roles).Select(p => new Claim(Permissions.CLAIM_TYPE, p)),
        .. roles.Select(r => new Claim(ClaimTypes.Role, r)),
        new Claim(ClaimTypes.Name, "test-user")]);

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> AddItemAsync(int stock, string name = "Lidocaína") => WithDbAsync(async db =>
    {
        var item = new Item($"{name} {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock, 3000);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    private static async Task<int> IdOf(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
    }

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private async Task<(int Id, string Name)> AddWarehouseAsync(params string[] roles)
    {
        var name = NewName("Storeroom");
        return (await IdOf(await _admin.PostAsJsonAsync("/api/inventory/warehouses", new WarehouseRequest { Name = name, Roles = [.. roles] })), name);
    }

    private async Task<int> DailyUseIdAsync()
        => (await _admin.GetFromJsonAsync<List<WarehouseDto>>("/api/inventory/warehouses"))!.Single(w => w.IsDailyUse).Id;

    private static Task<HttpResponseMessage> TransferAsync(HttpClient client, int itemId, int? from, int? to, int quantity)
        => client.PostAsJsonAsync("/api/inventory/movements/transfer",
            new TransferStockRequest { ItemId = itemId, FromWarehouseId = from, ToWarehouseId = to, Quantity = quantity });

    private static async Task<ItemDto> ItemAsync(HttpClient client, int itemId) => (await client.GetFromJsonAsync<ItemDto>($"/api/items/{itemId}"))!;

    private Task<int> UnitsAsync(int warehouseId, int itemId)
        => WithDbAsync(async db => (await db.WarehouseStocks.FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ItemId == itemId))?.Quantity ?? 0);

    private Task<int> TotalAsync(int itemId) => WithDbAsync(async db => (await db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId)).Stock);

    [Fact]
    public async Task TheDailyUseWarehouseExists_AndOnlyAdminManagesWarehouses()
    {
        var warehouses = await UserWith(Permissions.ROLES.INVENTORY).GetFromJsonAsync<List<WarehouseDto>>("/api/inventory/warehouses");
        warehouses.Should().ContainSingle(w => w.IsDailyUse);

        var inventorySupervisor = UserWith(Permissions.ROLES.INVENTORY, Permissions.ROLES.SUPERVISOR);
        (await inventorySupervisor.PostAsJsonAsync("/api/inventory/warehouses", new WarehouseRequest { Name = NewName("Mine") }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _admin.PostAsJsonAsync("/api/inventory/warehouses", new WarehouseRequest { Name = NewName("Bad"), Roles = ["Admin"] }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "Admin sees every warehouse; it isn't listed");

        var (id, name) = await AddWarehouseAsync(Permissions.ROLES.INVENTORY);
        (await _admin.PostAsJsonAsync("/api/inventory/warehouses", new WarehouseRequest { Name = name.ToUpperInvariant() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "names are unique");
        var listed = (await _admin.GetFromJsonAsync<List<WarehouseDto>>("/api/inventory/warehouses"))!.Single(w => w.Id == id);
        listed.Roles.Should().Equal(Permissions.ROLES.INVENTORY);
    }

    [Fact]
    public async Task Transfer_MovesUnits_LeavesTheTotal_AndIsNotUseInTheMetrics()
    {
        var itemId = await AddItemAsync(stock: 10);
        var daily = await DailyUseIdAsync();
        var (back, backName) = await AddWarehouseAsync(Permissions.ROLES.INVENTORY);
        var inventory = UserWith(Permissions.ROLES.INVENTORY);

        (await TransferAsync(inventory, itemId, daily, back, 8)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UnitsAsync(back, itemId)).Should().Be(8);
        (await TotalAsync(itemId)).Should().Be(10);
        var item = await ItemAsync(inventory, itemId);
        item.DailyUseStock.Should().Be(2);
        item.Warehouses.Select(w => (w.WarehouseId, w.Quantity)).Should().Equal((daily, 2), (back, 8));

        var tooMany = await TransferAsync(inventory, itemId, back, daily, 9);
        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooMany.Content.ReadAsStringAsync()).Should().Contain($"Only 8").And.Contain(backName);
        (await TransferAsync(inventory, itemId, back, back, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var movements = (await inventory.GetFromJsonAsync<List<MovementDto>>($"/api/inventory/movements/{itemId}"))!;
        movements.Select(m => (m.Type, m.WarehouseId, m.Quantity)).Should().BeEquivalentTo(
            [(InventoryMovementType.TransferOut, daily, 8), (InventoryMovementType.TransferIn, back, 8)]);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metrics = (await _admin.GetFromJsonAsync<InventoryMetricsDto>(
            $"/api/inventory/metrics?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}&itemIds={itemId}"))!;
        metrics.Series.Single().UnitsOut.Sum().Should().Be(0);
        metrics.Series.Single().UnitsIn.Sum().Should().Be(0);
        metrics.Movements.Should().Be(0);
    }

    [Fact]
    public async Task AWarehouseIsSeenOnlyByItsRoles_OthersAreToldWhereToAsk_NotHowMany()
    {
        var itemId = await AddItemAsync(stock: 10);
        var daily = await DailyUseIdAsync();
        var (locked, lockedName) = await AddWarehouseAsync(Permissions.ROLES.SUPERVISOR);
        (await TransferAsync(_admin, itemId, daily, locked, 10)).StatusCode.Should().Be(HttpStatusCode.OK);

        // A worker: the item shows as out of stock where they see it, and names the warehouse that has it.
        var worker = UserWith(Permissions.ROLES.INVENTORY);
        var seen = await ItemAsync(worker, itemId);
        seen.Stock.Should().Be(0);
        seen.Warehouses.Should().ContainSingle(w => w.WarehouseId == daily);
        seen.AskAt.Should().Equal(lockedName);
        var page = (await worker.GetFromJsonAsync<PagedResponse<InventoryItemDto>>(
            $"/api/items/paged?search={Uri.EscapeDataString(seen.Name)}&alertFilter={StockAlertFilter.OutOfStock}"))!;
        page.Items.Should().ContainSingle(i => i.Id == itemId && i.Stock == 0);
        (await worker.GetFromJsonAsync<List<WarehouseDto>>("/api/inventory/warehouses")).Should().NotContain(w => w.Id == locked);
        (await worker.GetFromJsonAsync<List<MovementDto>>($"/api/inventory/movements/{itemId}"))
            .Should().NotContain(m => m.WarehouseId == locked);
        (await worker.GetFromJsonAsync<PagedResponse<MovementDto>>($"/api/inventory/movements/paged?itemId={itemId}&warehouseId={locked}"))!
            .TotalCount.Should().Be(0);
        (await TransferAsync(worker, itemId, locked, daily, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Its roles see it, with the count.
        var supervisor = UserWith(Permissions.ROLES.INVENTORY, Permissions.ROLES.SUPERVISOR);
        var full = await ItemAsync(supervisor, itemId);
        full.Stock.Should().Be(10);
        full.Warehouses.Should().Contain(w => w.WarehouseId == locked && w.Quantity == 10);
        (await TransferAsync(supervisor, itemId, locked, daily, 2)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ItemAsync(worker, itemId)).Stock.Should().Be(2);
    }

    [Fact]
    public async Task FilteringByAWarehouse_ListsWhatItHolds_ByItsUnits_AndNothingOfOneTheUserDoesntSee()
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var (held, onlyDaily, ranOut) = (await AddItemAsync(10, tag), await AddItemAsync(5, tag), await AddItemAsync(3, tag));
        var daily = await DailyUseIdAsync();
        var (back, _) = await AddWarehouseAsync(Permissions.ROLES.SUPERVISOR);
        (await TransferAsync(_admin, held, daily, back, 6)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await TransferAsync(_admin, ranOut, daily, back, 3)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await TransferAsync(_admin, ranOut, back, daily, 3)).StatusCode.Should().Be(HttpStatusCode.OK);

        async Task<List<(int Id, int Units)>> InAsync(HttpClient client, int warehouseId, string filter = "")
        {
            var page = (await client.GetFromJsonAsync<PagedResponse<InventoryItemDto>>(
                $"/api/items/paged?search={tag}&warehouseId={warehouseId}&sortBy={ItemSortField.Stock}&sortDirection={SortDirection.Descending}{filter}"))!;
            return [.. page.Items.Select(i => (i.Id, i.Warehouses.FirstOrDefault(w => w.WarehouseId == warehouseId)?.Quantity ?? 0))];
        }

        // The back storeroom: what it holds and what ran out there, sorted by its units; never what it never had.
        (await InAsync(_admin, back)).Should().Equal((held, 6), (ranOut, 0));
        (await InAsync(_admin, back, $"&alertFilter={StockAlertFilter.OutOfStock}")).Should().Equal((ranOut, 0));
        // The daily-use one holds every item: what the others don't.
        (await InAsync(_admin, daily)).Should().Equal((onlyDaily, 5), (held, 4), (ranOut, 3));

        // A worker who doesn't see the back storeroom learns nothing of it, not even which items it holds.
        var worker = UserWith(Permissions.ROLES.INVENTORY);
        var hidden = (await worker.GetFromJsonAsync<PagedResponse<InventoryItemDto>>($"/api/items/paged?search={tag}&warehouseId={back}"))!;
        hidden.TotalCount.Should().Be(0);
        hidden.Items.Should().BeEmpty();
        (await InAsync(worker, daily)).Should().Equal((onlyDaily, 5), (held, 4), (ranOut, 3));
    }

    [Fact]
    public async Task VisitSupplies_TakeOnlyFromTheDailyUseWarehouse()
    {
        var itemId = await AddItemAsync(stock: 10);
        var daily = await DailyUseIdAsync();
        var (back, backName) = await AddWarehouseAsync(Permissions.ROLES.INVENTORY);
        (await TransferAsync(_admin, itemId, daily, back, 8)).StatusCode.Should().Be(HttpStatusCode.OK);
        var visitId = await AddVisitAsync();
        var vet = UserWith(Permissions.ROLES.CLINICAL);

        var refused = await vet.PostAsJsonAsync($"/api/clinical/visits/{visitId}/supplies", new { ItemId = itemId, Quantity = 3 });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = await refused.Content.ReadAsStringAsync();
        message.Should().Contain("Only 2").And.Contain(backName);
        var itemName = (await ItemAsync(_admin, itemId)).Name;
        message.Replace(itemName, "").Replace(backName, "").Should().NotContain("8", "the count of a warehouse the vet doesn't see stays private");

        (await vet.PostAsJsonAsync($"/api/clinical/visits/{visitId}/supplies", new { ItemId = itemId, Quantity = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await TotalAsync(itemId)).Should().Be(8);
        (await UnitsAsync(back, itemId)).Should().Be(8);
        (await ItemAsync(vet, itemId)).DailyUseStock.Should().Be(0);
    }

    [Fact]
    public async Task AdjustmentsAndBulkMoves_StayInTheirWarehouse()
    {
        var itemId = await AddItemAsync(stock: 4);
        var (back, _) = await AddWarehouseAsync(Permissions.ROLES.INVENTORY);
        var inventory = UserWith(Permissions.ROLES.INVENTORY);

        (await inventory.PostAsJsonAsync("/api/inventory/movements/adjust-stock",
            new AdjustStockRequest { ItemId = itemId, Amount = 5, Reason = "Purchase", WarehouseId = back })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await inventory.PostAsJsonAsync("/api/inventory/movements/adjust-stock",
            new AdjustStockRequest { ItemId = itemId, Amount = -6, Reason = "Count", WarehouseId = back })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await UnitsAsync(back, itemId)).Should().Be(5);
        (await TotalAsync(itemId)).Should().Be(9);

        var barcode = (await ItemAsync(inventory, itemId)).Barcode;
        var egress = await inventory.PostAsJsonAsync($"/api/inventory/movements/mass-egress?warehouseId={back}",
            new[] { new MassStockUpdateRequest { ItemId = itemId, Name = "x", Barcode = barcode, Quantity = 7 } });
        egress.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UnitsAsync(back, itemId)).Should().Be(0, "a bulk egress takes at most what the warehouse holds");
        (await TotalAsync(itemId)).Should().Be(4);

        var (locked, _) = await AddWarehouseAsync(Permissions.ROLES.SUPERVISOR);
        (await inventory.PostAsJsonAsync($"/api/inventory/movements/mass-ingress?warehouseId={locked}",
            new[] { new MassStockUpdateRequest { ItemId = itemId, Name = "x", Barcode = barcode, Quantity = 1 } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "the user doesn't see that warehouse");
    }

    private static ItemCreateRequest NewProduct(string barcode, int initialStock = 0, int? warehouseId = null) => new()
    {
        Name = $"Lidocaína {barcode}", Type = ItemType.Material, Barcode = barcode, SellPrice = 3000, InitialStock = initialStock, WarehouseId = warehouseId
    };

    private static string NewCode() => Guid.NewGuid().ToString("N")[..12];

    private Task<List<InventoryMovement>> MovementsAsync(int itemId)
        => WithDbAsync(db => db.InventoryMovements.AsNoTracking().Where(m => m.ItemId == itemId).ToListAsync());

    [Fact]
    public async Task AProductOnlyInDailyUse_TakesAnIngressStraightIntoAnotherWarehouse()
    {
        // The delivery goes to the locked storeroom while daily use has none: no need to pass it through daily use.
        var itemId = await AddItemAsync(stock: 0);
        var (locked, _) = await AddWarehouseAsync(Permissions.ROLES.SUPERVISOR);
        var barcode = (await ItemAsync(_admin, itemId)).Barcode;

        (await _admin.PostAsJsonAsync($"/api/inventory/movements/mass-ingress?warehouseId={locked}",
            new[] { new MassStockUpdateRequest { ItemId = itemId, Name = "x", Barcode = barcode, Quantity = 100 } })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await UnitsAsync(locked, itemId)).Should().Be(100);
        (await TotalAsync(itemId)).Should().Be(100);
        (await ItemAsync(_admin, itemId)).DailyUseStock.Should().Be(0);
    }

    [Fact]
    public async Task ANewProductsFirstUnits_ComeInAsAnIngress_InTheChosenWarehouse()
    {
        var (locked, storeroom) = await AddWarehouseAsync(Permissions.ROLES.SUPERVISOR);

        var created = await _admin.PostAsJsonAsync("/api/items", NewProduct(NewCode(), initialStock: 100, warehouseId: locked));
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var item = (await created.Content.ReadFromJsonAsync<ItemDto>())!;

        item.Stock.Should().Be(100);
        item.DailyUseStock.Should().Be(0);
        (await UnitsAsync(locked, item.Id)).Should().Be(100);
        (await MovementsAsync(item.Id)).Should().ContainSingle()
            .Which.Should().Match<InventoryMovement>(m => m.Type == InventoryMovementType.Ingress && m.Quantity == 100 && m.WarehouseId == locked);
        // The audit says where they went: the creation entry alone doesn't.
        (await WithDbAsync(db => db.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(Item) && a.EntityId == item.Id).ToListAsync()))
            .Should().ContainSingle(a => a.Action == AuditActionType.Ingress).Which.Changes.Should().Contain(storeroom);

        // Without a warehouse they go to daily use, and a product created without units has no movement.
        var daily = (await (await _admin.PostAsJsonAsync("/api/items", NewProduct(NewCode(), initialStock: 7))).Content.ReadFromJsonAsync<ItemDto>())!;
        daily.DailyUseStock.Should().Be(7);
        (await MovementsAsync(daily.Id)).Should().ContainSingle().Which.WarehouseId.Should().BeNull();
        var empty = (await (await _admin.PostAsJsonAsync("/api/items", NewProduct(NewCode()))).Content.ReadFromJsonAsync<ItemDto>())!;
        empty.Stock.Should().Be(0);
        (await MovementsAsync(empty.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ANewProduct_WithACodeAnotherHas_IsRefused_NamingThatProduct()
    {
        var code = NewCode();
        (await _admin.PostAsJsonAsync("/api/items", NewProduct(code))).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await _admin.PostAsJsonAsync("/api/items", NewProduct(code, initialStock: 100));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain($"Lidocaína {code}");
        (await WithDbAsync(db => db.Items.CountAsync(i => i.Barcode == code))).Should().Be(1);
    }

    [Fact]
    public async Task ProductsWithoutACode_AreNotComparedWithEachOther()
    {
        // The form's starting code is what a product without a barcode keeps: a clinic has many of them.
        (await _admin.PostAsJsonAsync("/api/items", NewProduct(Item.NoBarcode))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PostAsJsonAsync("/api/items", NewProduct(Item.NoBarcode))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ANewProductsFirstUnits_CantGoToAWarehouseTheUserDoesNotSee()
    {
        var (locked, _) = await AddWarehouseAsync(Permissions.ROLES.SUPERVISOR);
        var code = NewCode();

        var refused = await UserWith(Permissions.ROLES.INVENTORY).PostAsJsonAsync("/api/items", NewProduct(code, initialStock: 5, warehouseId: locked));

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Items.AnyAsync(i => i.Barcode == code))).Should().BeFalse("nothing is created when its units have nowhere to go");
    }

    [Fact]
    public async Task OnlyAnEmptyWarehouseIsArchived_AndOnlyAnUnusedOneDeleted()
    {
        var itemId = await AddItemAsync(stock: 3);
        var daily = await DailyUseIdAsync();
        var (used, usedName) = await AddWarehouseAsync();
        (await TransferAsync(_admin, itemId, daily, used, 3)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _admin.PutAsJsonAsync($"/api/inventory/warehouses/{daily}", new WarehouseRequest { Name = NewName("Daily"), IsArchived = true }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.DeleteAsync($"/api/inventory/warehouses/{daily}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PutAsJsonAsync($"/api/inventory/warehouses/{used}", new WarehouseRequest { Name = usedName, IsArchived = true }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "it still holds stock");

        (await TransferAsync(_admin, itemId, used, daily, 3)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.DeleteAsync($"/api/inventory/warehouses/{used}")).StatusCode.Should().Be(HttpStatusCode.BadRequest, "it has movements");
        (await _admin.PutAsJsonAsync($"/api/inventory/warehouses/{used}", new WarehouseRequest { Name = usedName, IsArchived = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await TransferAsync(_admin, itemId, daily, used, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "an archived warehouse takes no stock");

        var (unused, _) = await AddWarehouseAsync();
        (await _admin.DeleteAsync($"/api/inventory/warehouses/{unused}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.Warehouses.AnyAsync(w => w.Id == unused))).Should().BeFalse();
    }

    /// <summary>A visit of a new pet; returns its id.</summary>
    private async Task<int> AddVisitAsync()
    {
        var petId = await WithDbAsync(async db =>
        {
            var owner = new Client("Ana", "Soto", $"{Random.Shared.Next(1_000_000, 25_000_000)}-K", "Calle 1", 912345678, "ana@example.test");
            db.Clients.Add(owner);
            await db.SaveChangesAsync();
            var pet = new Pet { OwnerId = owner.Id, Name = "Toby", Breed = "Mestizo", Species = Species.Dog };
            db.Pets.Add(pet);
            await db.SaveChangesAsync();
            return pet.Id;
        });
        (await _admin.PostAsJsonAsync("/api/medical-visits", new { PatientId = petId, PatientName = "Toby", Date = DateTime.UtcNow, Reason = "Cojera" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return await WithDbAsync(db => db.MedicalVisits.Where(v => v.PatientId == petId).Select(v => v.Id).SingleAsync());
    }
}
