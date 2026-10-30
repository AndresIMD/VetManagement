using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;
using VetManagement.Staff.UI.Services.Realtime;

namespace VetManagement.Tests.Integration;

/// <summary>
/// The staff app's realtime client connected to the API's real hubs. A test of either side alone can't see whether the
/// other understands it: the notices arrived for months, and the app read every field as empty.
/// </summary>
public class RealtimeTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private readonly HttpClient _inventory = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.INVENTORY)]);

    private async Task<int> AddItemAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = new Item($"Realtime {Guid.NewGuid():N}", ItemType.Material, Guid.NewGuid().ToString("N")[..12], null, 5, 1000);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    [Fact]
    public async Task AStockChange_ReachesOpenScreens_NamingTheItemNotTheCount()
    {
        var itemId = await AddItemAsync();
        var received = new TaskCompletionSource<PropertyChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var realtime = factory.CreateRealtimeClient();
        realtime.OnPropertyChanged += args => received.TrySetResult(args);
        await realtime.InitializeAsync();
        realtime.IsConnected.Should().BeTrue();

        (await _inventory.PostAsJsonAsync("/api/inventory/movements/adjust-stock", new AdjustStockRequest { ItemId = itemId, Amount = 2 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var notice = await received.Task.WaitAsync(Wait);
        notice.EntityType.Should().Be(nameof(Item));
        notice.EntityId.Should().Be(itemId);
        notice.PropertyName.Should().Be(nameof(Item.Stock));
        notice.NewValue.Should().BeNull("every connected user gets it, and the count of a warehouse they don't see stays private");
    }

    [Fact]
    public async Task ANewItem_ReachesOpenScreens_WithoutItsStock()
    {
        var changed = new TaskCompletionSource<EntityChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var collection = new TaskCompletionSource<CollectionChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var realtime = factory.CreateRealtimeClient();
        realtime.OnEntityChanged += args => changed.TrySetResult(args);
        realtime.OnCollectionChanged += args => collection.TrySetResult(args);
        await realtime.InitializeAsync();

        var name = $"Realtime {Guid.NewGuid():N}";
        (await _inventory.PostAsJsonAsync("/api/items", new ItemCreateRequest { Name = name, Type = ItemType.Material, Barcode = Guid.NewGuid().ToString("N")[..12] }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var notice = await changed.Task.WaitAsync(Wait);
        notice.EntityType.Should().Be(nameof(Item));
        notice.Action.Should().Be("Add");
        notice.AdditionalData?.ToString().Should().Contain(name).And.NotContainEquivalentOf("stock");
        (await collection.Task.WaitAsync(Wait)).EntityType.Should().Be(nameof(Item));
    }
}
