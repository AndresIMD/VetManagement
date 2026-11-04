using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace VetManagement.Staff.UI.Services.Realtime;

/// <summary>What the "Live" chip shows.</summary>
public enum RealtimeState
{
  Offline,
  Connecting,
  Connected
}

/// <summary>
/// The staff app's connection to the API's hubs. It never gives up: a clinic's server or Wi-Fi can be down for minutes,
/// and SignalR's default reconnection stops after four tries (about 42 seconds), which left the app without notices, and
/// its chip "Offline", until the page was reloaded. A server that is down when the app starts is retried the same way.
/// </summary>
public class RealtimeService : IAsyncDisposable
{
  /// <summary>Waits between tries, the last one repeated for as long as the server stays away.</summary>
  internal static readonly TimeSpan[] RetryDelays = [TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

  internal static TimeSpan RetryDelay(long attempt) => RetryDelays[(int)Math.Min(attempt, RetryDelays.Length - 1)];

  private HubConnection[] _hubs = [];
  private CancellationTokenSource? _stop;
  private bool _firstAttempt;
  private int _starting;
  private readonly string _baseUrl;
  private readonly Func<Task<string?>> _getAccessToken;
  private readonly Action<HttpConnectionOptions>? _configure;

  // Generic events
  public event Action<EntityChangedEventArgs>? OnEntityChanged;
  public event Action<PropertyChangedEventArgs>? OnPropertyChanged;
  public event Action<CollectionChangedEventArgs>? OnCollectionChanged;

  /// <summary>The connection was lost, came back or was closed: <see cref="State"/> may have changed.</summary>
  public event Action? OnStateChanged;

  public bool IsConnected => _hubs.Length > 0 && _hubs.All(h => h.State == HubConnectionState.Connected);

  /// <summary>Connecting only while the app first starts; a lost connection shows as offline while it's retried.</summary>
  public RealtimeState State => IsConnected ? RealtimeState.Connected
      : _firstAttempt ? RealtimeState.Connecting
      : RealtimeState.Offline;

  /// <param name="configure">Extra connection options: tests connect through their in-memory server.</param>
  public RealtimeService(string baseUrl, Func<Task<string?>> getAccessToken, Action<HttpConnectionOptions>? configure = null)
  {
    _baseUrl = baseUrl.TrimEnd('/');
    _getAccessToken = getAccessToken;
    _configure = configure;
  }

  /// <summary>
  /// Connects to the hubs, once: calling it again (another sign-in notice) keeps the same connections. If the server
  /// doesn't answer, it keeps trying in the background and <see cref="OnStateChanged"/> says when it's connected.
  /// </summary>
  public async Task InitializeAsync()
  {
    if (_hubs.Length > 0)
      return;
    var token = await _getAccessToken();
    if (string.IsNullOrWhiteSpace(token) || _hubs.Length > 0)
      return;

    var stop = _stop = new CancellationTokenSource();
    _hubs = [CreateHub("inventory"), CreateHub("data")];
    _firstAttempt = true;
    NotifyState();
    try
    {
      await StartDownHubsAsync(stop.Token);
    }
    catch (OperationCanceledException)
    {
      return; // signed out meanwhile
    }
    finally
    {
      _firstAttempt = false;
      NotifyState();
    }
    if (!stop.IsCancellationRequested && !AllUp)
      _ = StartUntilConnectedAsync(stop.Token);
  }

  /// <summary>No hub is down: each is connected, or reconnecting on its own.</summary>
  private bool AllUp => _hubs.All(h => h.State != HubConnectionState.Disconnected);

  /// <summary>One try at starting the hubs that are down; a server or network that doesn't answer is left for the next.</summary>
  private async Task StartDownHubsAsync(CancellationToken stop)
  {
    foreach (var hub in _hubs.Where(h => h.State == HubConnectionState.Disconnected))
    {
      try
      {
        await hub.StartAsync(stop);
      }
      catch (Exception) when (!stop.IsCancellationRequested)
      {
      }
    }
  }

  private HubConnection CreateHub(string name)
  {
    var hub = new HubConnectionBuilder()
        .WithUrl($"{_baseUrl}/hubs/{name}", Configure)
        .WithAutomaticReconnect(new KeepTrying())
        .Build();
    RegisterHubEvents(hub);
    hub.Reconnecting += _ => NotifyStateAsync();
    hub.Reconnected += _ => NotifyStateAsync();
    hub.Closed += error =>
    {
      NotifyState();
      // Closed without being asked to (the server ended it): start over, unless the app is signing out.
      if (_stop is { IsCancellationRequested: false } stop)
        _ = StartUntilConnectedAsync(stop.Token);
      return Task.CompletedTask;
    };
    return hub;
  }

  /// <summary>Starts the hubs that are down, again and again, until none is down or the app disconnects.</summary>
  private async Task StartUntilConnectedAsync(CancellationToken stop)
  {
    // One loop at a time: a hub closing while the loop runs is left to that loop.
    if (Interlocked.Exchange(ref _starting, 1) == 1)
      return;
    try
    {
      for (long attempt = 1; !stop.IsCancellationRequested && !AllUp; attempt++)
      {
        await Task.Delay(RetryDelay(attempt), stop);
        await StartDownHubsAsync(stop);
        NotifyState();
      }
    }
    catch (OperationCanceledException)
    {
      // Disconnected on purpose (sign-out).
    }
    finally
    {
      Interlocked.Exchange(ref _starting, 0);
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

  private void NotifyState() => OnStateChanged?.Invoke();

  private Task NotifyStateAsync()
  {
    NotifyState();
    return Task.CompletedTask;
  }

  /// <summary>Stops trying and closes the connections (sign-out); <see cref="InitializeAsync"/> connects again.</summary>
  public async Task DisconnectAsync()
  {
    var hubs = _hubs;
    _stop?.Cancel();
    _hubs = [];
    foreach (var hub in hubs)
    {
      try
      {
        await hub.StopAsync();
      }
      finally
      {
        await hub.DisposeAsync();
      }
    }
    _stop?.Dispose();
    _stop = null;
    _firstAttempt = false;
    NotifyState();
  }

  public async ValueTask DisposeAsync()
  {
    await DisconnectAsync();
    GC.SuppressFinalize(this);
  }

  /// <summary>Reconnects for as long as it takes, with the same waits as the first start.</summary>
  private sealed class KeepTrying : IRetryPolicy
  {
    public TimeSpan? NextRetryDelay(RetryContext retryContext) => RetryDelay(retryContext.PreviousRetryCount);
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
