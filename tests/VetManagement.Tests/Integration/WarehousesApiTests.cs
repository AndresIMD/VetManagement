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

    private Task<int> AddItemAsync(int stock) => WithDbAsync(async db =>
    {
        var item = new Item($"Lidocaína {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock, 3000);
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
