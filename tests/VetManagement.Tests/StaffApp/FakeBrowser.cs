using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using VetManagement.Staff.UI.Services.Api;
using VetManagement.Staff.UI.Services.Auth;

namespace VetManagement.Tests.StaffApp;

/// <summary>
/// One browser profile running the staff app: its tabs share localStorage and cookies, each tab has its own
/// sessionStorage. A change to localStorage reaches the other tabs as a "storage" event when
/// <see cref="DeliverStorageEventsAsync"/> runs, the way a real browser queues it on each tab's own event loop.
/// </summary>
internal sealed class FakeBrowser(FakeAccountApi api)
{
    private readonly List<FakeTab> _tabs = [];
    private readonly List<FakeTab> _toNotify = [];

    public Dictionary<string, string> LocalStorage { get; } = [];

    /// <summary>Cookies without an expiry date: they last until the browser closes.</summary>
    public HashSet<string> SessionCookies { get; } = [];

    public bool CookiesEnabled { get; set; } = true;

    /// <summary>Whether the page loads js/session.js (an index.html cached from an earlier version doesn't).</summary>
    public bool HasSessionScript { get; set; } = true;

    public FakeTab OpenTab(string path = "")
    {
        var tab = new FakeTab(this, api, path);
        _tabs.Add(tab);
        return tab;
    }

    /// <summary>Closes the browser: its tabs and the cookies without an expiry date go, localStorage stays.</summary>
    public void Close()
    {
        _tabs.Clear();
        _toNotify.Clear();
        SessionCookies.Clear();
    }

    internal void LocalStorageChangedBy(FakeTab writer)
    {
        foreach (var tab in _tabs.Where(t => t != writer && !_toNotify.Contains(t)))
            _toNotify.Add(tab);
    }

    /// <summary>The other tabs hear about the localStorage changes made so far.</summary>
    public async Task DeliverStorageEventsAsync()
    {
        while (_toNotify.Count > 0)
        {
            var tab = _toNotify[0];
            _toNotify.RemoveAt(0);
            await tab.StorageEventAsync();
        }
    }
}

/// <summary>A tab of <see cref="FakeBrowser"/>: the JavaScript side of the staff app's session for that tab.</summary>
internal sealed class FakeTab : IJSRuntime
{
    private const string BrowserOpenCookie = "staffBrowserOpen";
    private readonly FakeBrowser _browser;
    private readonly HttpClient _http;
    private DotNetObjectReference<SessionRefresher>? _watcher;
    /// <summary>One per tab: each tab is its own running app.</summary>
    private readonly SessionToken _session = new();

    public FakeTab(FakeBrowser browser, FakeAccountApi api, string path)
    {
        _browser = browser;
        _http = new HttpClient(api, disposeHandler: false) { BaseAddress = new Uri("http://api/") };
        Navigation = new FakeNavigation(path);
        Auth = new JwtAuthenticationStateProvider(Account());
    }

    public Dictionary<string, string> SessionStorage { get; } = [];
    public FakeNavigation Navigation { get; }
    public JwtAuthenticationStateProvider Auth { get; }

    /// <summary>A fresh account service per use, as the app's DI hands them out (a typed HttpClient is transient).</summary>
    public AccountApiService Account() => new(_http, this, _session);

    public SessionRefresher CreateRefresher() => new(Account(), Auth, Navigation, this);

    public Task<string?> TokenAsync() => Account().GetTokenAsync();

    internal Task StorageEventAsync() => _watcher is null ? Task.CompletedTask : _watcher.Value.OnSessionChangedElsewhereAsync();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        object? result = null;
        switch (identifier)
        {
            case "localStorage.getItem":
                result = _browser.LocalStorage.GetValueOrDefault(Text(args, 0));
                break;
            case "localStorage.setItem":
                // Like a browser: writing the value already there changes nothing and tells no one.
                if (_browser.LocalStorage.GetValueOrDefault(Text(args, 0)) != Text(args, 1))
                {
                    _browser.LocalStorage[Text(args, 0)] = Text(args, 1);
                    _browser.LocalStorageChangedBy(this);
                }
                break;
            case "localStorage.removeItem":
                if (_browser.LocalStorage.Remove(Text(args, 0)))
                    _browser.LocalStorageChangedBy(this);
                break;
            case "sessionStorage.getItem":
                result = SessionStorage.GetValueOrDefault(Text(args, 0));
                break;
            case "sessionStorage.setItem":
                SessionStorage[Text(args, 0)] = Text(args, 1);
                break;
            case "sessionStorage.removeItem":
                SessionStorage.Remove(Text(args, 0));
                break;
            case "staffSession.markBrowserOpen":
                RequireScript(identifier);
                if (_browser.CookiesEnabled)
                    _browser.SessionCookies.Add(BrowserOpenCookie);
                break;
            case "staffSession.browserWasClosed":
                RequireScript(identifier);
                result = _browser.CookiesEnabled && !_browser.SessionCookies.Contains(BrowserOpenCookie);
                break;
            case "staffSession.clearBrowserMark":
                RequireScript(identifier);
                _browser.SessionCookies.Remove(BrowserOpenCookie);
                break;
            case "staffSession.watch":
                RequireScript(identifier);
                _watcher = (DotNetObjectReference<SessionRefresher>)args![0]!;
                break;
            default:
                throw new JSException($"Could not find '{identifier}' ('{identifier.Split('.')[0]}' was undefined).");
        }
        return ValueTask.FromResult((TValue)result!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);

    private void RequireScript(string identifier)
    {
        if (!_browser.HasSessionScript)
            throw new JSException($"Could not find '{identifier}' ('staffSession' was undefined).");
    }

    private static string Text(object?[]? args, int index) => (string)args![index]!;
}

/// <summary>Where a tab is, and where the app sent it.</summary>
internal sealed class FakeNavigation : NavigationManager
{
    public FakeNavigation(string path) => Initialize("http://staff/", "http://staff/" + path);

    public List<(string Uri, bool ForceLoad)> Visits { get; } = [];

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        var absolute = ToAbsoluteUri(uri).ToString();
        Visits.Add((absolute, options.ForceLoad));
        Uri = absolute;
    }
}

/// <summary>The API's account endpoints as the staff app sees them: sign-in and refresh hand out tokens for the user.</summary>
internal sealed class FakeAccountApi : HttpMessageHandler
{
    private int _refreshes;

    /// <summary>Users whose sessions the API ends (blocked, deleted, password reset by an admin).</summary>
    public HashSet<string> Ended { get; } = [];

    /// <summary>Roles each user's next token carries.</summary>
    public Dictionary<string, string[]> Roles { get; } = [];

    /// <summary>When set, a refresh waits for it before answering, so a test can act in another tab meanwhile.</summary>
    public TaskCompletionSource? HoldRefresh { get; set; }

    /// <summary>Set when a refresh has reached the API (and may be waiting on <see cref="HoldRefresh"/>).</summary>
    public TaskCompletionSource RefreshArrived { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Refreshes => _refreshes;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/api/account/login", StringComparison.Ordinal))
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var user = body.EnumerateObject().First(p => p.Name.Equals("username", StringComparison.OrdinalIgnoreCase)).Value.GetString()!;
            return Json(new { token = Token(user), mustChangePassword = false });
        }
        if (path.EndsWith("/api/account/refresh", StringComparison.Ordinal))
        {
            var user = new JwtSecurityTokenHandler().ReadJwtToken(request.Headers.Authorization!.Parameter).Subject;
            Interlocked.Increment(ref _refreshes);
            RefreshArrived.TrySetResult();
            if (HoldRefresh is { } hold)
                await hold.Task;
            RefreshArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return Ended.Contains(user)
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json(new { token = Token(user), mustChangePassword = false });
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    public string Token(string user) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        claims: [
            new Claim(JwtRegisteredClaimNames.Sub, user),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            .. Roles.GetValueOrDefault(user, []).Select(role => new Claim(ClaimTypes.Role, role))
        ],
        expires: DateTime.UtcNow.AddHours(8)));

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
