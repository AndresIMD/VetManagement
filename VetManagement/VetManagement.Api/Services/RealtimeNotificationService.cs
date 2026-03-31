using Microsoft.AspNetCore.SignalR;
using VetManagement.Api.Hubs;
using VetManagement.Application.Contracts.Services;

namespace VetManagement.Api.Services;

public class RealtimeNotificationService(
    IHubContext<InventoryHub> inventoryHub,
    IHubContext<DataHub> dataHub) : IRealtimeNotificationService
{
    public async Task NotifyEntityChangedAsync<TEntity>(int entityId, string action, object? additionalData = null) where TEntity : class
    {
        var entityType = typeof(TEntity).Name;
        var payload = new
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Timestamp = DateTime.UtcNow,
            AdditionalData = additionalData
        };

        // Route to appropriate hub based on entity type
        var hub = DetermineHub(entityType);
        await hub.Clients.All.SendAsync("EntityChanged", payload);
    }

    public async Task NotifyPropertyChangedAsync<TEntity>(int entityId, string propertyName, object? newValue) where TEntity : class
    {
        var entityType = typeof(TEntity).Name;
        var payload = new
        {
            EntityType = entityType,
            EntityId = entityId,
            PropertyName = propertyName,
            NewValue = newValue,
            Timestamp = DateTime.UtcNow
        };

        var hub = DetermineHub(entityType);
        await hub.Clients.All.SendAsync("PropertyChanged", payload);
    }

    public async Task NotifyCollectionChangedAsync<TEntity>(string action) where TEntity : class
    {
        var entityType = typeof(TEntity).Name;
        var payload = new
        {
            EntityType = entityType,
            Action = action,
            Timestamp = DateTime.UtcNow
        };

        var hub = DetermineHub(entityType);
        await hub.Clients.All.SendAsync("CollectionChanged", payload);
    }

    public async Task NotifyGroupAsync(string groupName, string eventName, object? data = null)
    {
        await dataHub.Clients.Group(groupName).SendAsync(eventName, data);
    }

    public async Task NotifyAllAsync(string eventName, object? data = null)
    {
        await dataHub.Clients.All.SendAsync(eventName, data);
    }

    private IHubContext<Hub> DetermineHub(string entityType)
    {
        // Inventory-related entities use InventoryHub
        var inventoryEntities = new[] { "Item", "Drug", "InventoryMovement", "InventoryAlert" };

        if (inventoryEntities.Contains(entityType))
            return (IHubContext<Hub>)(object)inventoryHub;

        // All other entities use DataHub
        return (IHubContext<Hub>)(object)dataHub;
    }
}
