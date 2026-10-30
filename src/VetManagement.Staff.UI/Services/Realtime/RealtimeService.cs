using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace VetManagement.Staff.UI.Services.Realtime;

public class RealtimeService : IAsyncDisposable
{
  private HubConnection? _inventoryHub;
  private HubConnection? _dataHub;
  private readonly string _baseUrl;
  private readonly Func<Task<string?>> _getAccessToken;
  private readonly Action<HttpConnectionOptions>? _configure;

  // Generic events
  public event Action<EntityChangedEventArgs>? OnEntityChanged;
  public event Action<PropertyChangedEventArgs>? OnPropertyChanged;
  public event Action<CollectionChangedEventArgs>? OnCollectionChanged;

  public bool IsConnected =>
      _inventoryHub?.State == HubConnectionState.Connected ||
      _dataHub?.State == HubConnectionState.Connected;

  /// <param name="configure">Extra connection options: tests connect through their in-memory server.</param>
  public RealtimeService(string baseUrl, Func<Task<string?>> getAccessToken, Action<HttpConnectionOptions>? configure = null)
  {
    _baseUrl = baseUrl.TrimEnd('/');
    _getAccessToken = getAccessToken;
    _configure = configure;
  }

  public async Task InitializeAsync()
  {
    try
    {
      var token = await _getAccessToken();
      if (string.IsNullOrWhiteSpace(token))
        return;

      _inventoryHub = new HubConnectionBuilder()
          .WithUrl($"{_baseUrl}/hubs/inventory", Configure)
          .WithAutomaticReconnect()
          .Build();

      RegisterHubEvents(_inventoryHub);

      _dataHub = new HubConnectionBuilder()
          .WithUrl($"{_baseUrl}/hubs/data", Configure)
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

  private void Configure(HttpConnectionOptions options)
  {
    options.AccessTokenProvider = async () => await _getAccessToken();
    _configure?.Invoke(options);
  }

  // Typed, so SignalR reads each notice with the same settings the hubs write it with (camelCase names).
  private void RegisterHubEvents(HubConnection hub)
  {
    hub.On<EntityChangedEventArgs>("EntityChanged", args => OnEntityChanged?.Invoke(args));
    hub.On<PropertyChangedEventArgs>("PropertyChanged", args => OnPropertyChanged?.Invoke(args));
    hub.On<CollectionChangedEventArgs>("CollectionChanged", args => OnCollectionChanged?.Invoke(args));
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
