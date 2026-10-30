using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Tests.Integration;

/// <summary>
/// The staff UI's inventory clients against the real API: a query parameter or a rule the two sides disagree on otherwise
/// only shows in the browser (a filter that filters nothing, a 400 for a field the form calls optional).
/// </summary>
public class InventoryClientsTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _inventory = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.INVENTORY)]);

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<int> AddItemAsync(string name, int stock) => WithDbAsync(async db =>
    {
        var item = new Item(name, ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, stock, 1000, lowStockThreshold: 5);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    });

    [Fact]
    public async Task TheItemsPagesAlertCodes_FilterTheList()
    {
        var search = $"Alert {Guid.NewGuid():N}"[..16];
        var empty = await AddItemAsync($"{search} empty", 0);
        var negative = await AddItemAsync($"{search} negative", -1);
        var low = await AddItemAsync($"{search} low", 2);
        var plenty = await AddItemAsync($"{search} plenty", 50);
        var items = new ItemsApiService(_inventory);

        async Task<List<int>> IdsAsync(string? alert)
            => [.. (await items.GetItemsPagedAsync(search, ItemType.None, 0, 25, alert: alert))!.Items.Select(i => i.Id)];

        (await IdsAsync("zero")).Should().BeEquivalentTo([empty, negative], "the stock alerts card counts both as out of stock");
        (await IdsAsync("low")).Should().Equal(low);
        (await IdsAsync(null)).Should().BeEquivalentTo([empty, negative, low, plenty]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AQuickAdjustmentWithoutAReason_IsSavedAsOne(string? reason)
    {
        var itemId = await AddItemAsync($"Adjust {Guid.NewGuid():N}", 3);

        var result = await new InventoryMovementApiService(_inventory).AdjustStockAsync(itemId, 2, reason);

        result.Ok.Should().BeTrue(result.Error);
        (await WithDbAsync(db => db.Items.AsNoTracking().Where(i => i.Id == itemId).Select(i => i.Stock).SingleAsync())).Should().Be(5);
        (await WithDbAsync(db => db.InventoryMovements.Where(m => m.ItemId == itemId).Select(m => m.Reason).SingleAsync()))
            .Should().Be(InventoryReasons.QUICK_ADJUSTMENT);
    }

    [Fact]
    public async Task AQuickAdjustmentWithAReason_KeepsIt()
    {
        var itemId = await AddItemAsync($"Adjust {Guid.NewGuid():N}", 3);

        (await new InventoryMovementApiService(_inventory).AdjustStockAsync(itemId, -1, "Vencido")).Ok.Should().BeTrue();

        (await WithDbAsync(db => db.InventoryMovements.Where(m => m.ItemId == itemId).Select(m => m.Reason).SingleAsync())).Should().Be("Vencido");
    }
}
