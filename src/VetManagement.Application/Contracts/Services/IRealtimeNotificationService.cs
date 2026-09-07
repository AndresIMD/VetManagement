namespace VetManagement.Application.Contracts.Services;

public interface IRealtimeNotificationService
{
    /// <summary>
    /// Notify clients about an entity change.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="entityId">Entity ID</param>
    /// <param name="action">Action performed (Add, Update, Delete)</param>
    /// <param name="additionalData">Optional additional data</param>
    Task NotifyEntityChangedAsync<TEntity>(int entityId, string action, object? additionalData = null) where TEntity : class;

    /// <summary>
    /// Notify clients about a property change in an entity.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="entityId">Entity ID</param>
    /// <param name="propertyName">Property that changed</param>
    /// <param name="newValue">New value</param>
    Task NotifyPropertyChangedAsync<TEntity>(int entityId, string propertyName, object? newValue) where TEntity : class;

    /// <summary>
    /// Notify clients about a collection change.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="action">Action performed (Add, Update, Delete, Refresh)</param>
    Task NotifyCollectionChangedAsync<TEntity>(string action) where TEntity : class;

    /// <summary>
    /// Send a custom notification to a specific group.
    /// </summary>
    /// <param name="groupName">Target group</param>
    /// <param name="eventName">Event name</param>
    /// <param name="data">Event data</param>
    Task NotifyGroupAsync(string groupName, string eventName, object? data = null);

    /// <summary>
    /// Send a custom notification to all clients.
    /// </summary>
    /// <param name="eventName">Event name</param>
    /// <param name="data">Event data</param>
    Task NotifyAllAsync(string eventName, object? data = null);
}
