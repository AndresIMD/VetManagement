using Microsoft.AspNetCore.SignalR.Client;

namespace VetManagement.Shared.Services.Realtime;

public class RealtimeService : IAsyncDisposable
{
  private HubConnection? _inventoryHub;
  private HubConnection? _dataHub;
  private readonly string _baseUrl;
  private readonly Func<Task<string?>> _getAccessToken;

  // Generic events
  public event Action<EntityChangedEventArgs>? OnEntityChanged;
  public event Action<PropertyChangedEventArgs>? OnPropertyChanged;
  public event Action<CollectionChangedEventArgs>? OnCollectionChanged;

  public bool IsConnected =>
      _inventoryHub?.State == HubConnectionState.Connected ||
      _dataHub?.State == HubConnectionState.Connected;

  public RealtimeService(string baseUrl, Func<Task<string?>> getAccessToken)
  {
    _baseUrl = baseUrl.TrimEnd('/');
    _getAccessToken = getAccessToken;
  }

  public async Task InitializeAsync()
  {
    try
    {
      var token = await _getAccessToken();
      if (string.IsNullOrWhiteSpace(token))
        return;

      _inventoryHub = new HubConnectionBuilder()
          .WithUrl($"{_baseUrl}/hubs/inventory", options =>
          {
            options.AccessTokenProvider = async () => await _getAccessToken();
          })
          .WithAutomaticReconnect()
          .Build();

      RegisterHubEvents(_inventoryHub);

      _dataHub = new HubConnectionBuilder()
          .WithUrl($"{_baseUrl}/hubs/data", options =>
          {
            options.AccessTokenProvider = async () => await _getAccessToken();
          })
          .WithAutomaticReconnect()
          .Build();

      RegisterHubEvents(_dataHub);

      await _inventoryHub.StartAsync();
      await _dataHub.StartAsync();
    }
    catch (Exception)
    {
      // -- Placeholder for logging errors --
    }
  }

  private void RegisterHubEvents(HubConnection hub)
  {
    hub.On<object>("EntityChanged", payload =>
    {
      var json = System.Text.Json.JsonSerializer.Serialize(payload);
      var eventArgs = System.Text.Json.JsonSerializer.Deserialize<EntityChangedEventArgs>(json);
      if (eventArgs != null)
        OnEntityChanged?.Invoke(eventArgs);
    });

    hub.On<object>("PropertyChanged", payload =>
    {
      var json = System.Text.Json.JsonSerializer.Serialize(payload);
      var eventArgs = System.Text.Json.JsonSerializer.Deserialize<PropertyChangedEventArgs>(json);
      if (eventArgs != null)
        OnPropertyChanged?.Invoke(eventArgs);
    });

    hub.On<object>("CollectionChanged", payload =>
    {
      var json = System.Text.Json.JsonSerializer.Serialize(payload);
      var eventArgs = System.Text.Json.JsonSerializer.Deserialize<CollectionChangedEventArgs>(json);
      if (eventArgs != null)
        OnCollectionChanged?.Invoke(eventArgs);
    });
  }

  public async Task DisconnectAsync()
  {
    if (_inventoryHub != null)
      await _inventoryHub.StopAsync();
    if (_dataHub != null)
      await _dataHub.StopAsync();
  }

  public async ValueTask DisposeAsync()
  {
    await DisconnectAsync();

    if (_inventoryHub != null)
      await _inventoryHub.DisposeAsync();
    if (_dataHub != null)
      await _dataHub.DisposeAsync();
  }
}

public class EntityChangedEventArgs
{
  public string EntityType { get; set; } = string.Empty;
  public int EntityId { get; set; }
  public string Action { get; set; } = string.Empty;
  public DateTime Timestamp { get; set; }
  public object? AdditionalData { get; set; }
}

public class PropertyChangedEventArgs
{
  public string EntityType { get; set; } = string.Empty;
  public int EntityId { get; set; }
  public string PropertyName { get; set; } = string.Empty;
  public object? NewValue { get; set; }
  public DateTime Timestamp { get; set; }
}

public class CollectionChangedEventArgs
{
  public string EntityType { get; set; } = string.Empty;
  public string Action { get; set; } = string.Empty;
  public DateTime Timestamp { get; set; }
}
