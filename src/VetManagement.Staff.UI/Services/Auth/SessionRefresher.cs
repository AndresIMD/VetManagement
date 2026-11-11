using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Staff.UI.Services.Auth;

/// <summary>
/// Keeps this tab in step with the browser's session, which every tab shares (see <see cref="AccountApiService"/>).
/// Refreshes the token every minute (and on demand, e.g. after changing roles), so menus and pages follow role changes
/// without signing in again, and signs out when the API says the session is over (user blocked or deleted, token
/// expired). When another tab signs out, this one goes to the sign-in page; when another user signs in, this one reloads,
/// so it neither shows nor saves anything under the previous user's name.
/// </summary>
public sealed class SessionRefresher(AccountApiService account, AuthenticationStateProvider auth, NavigationManager navigation, IJSRuntime js) : IDisposable
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(1);
    private static readonly HashSet<string> ChangesEveryTime = [JwtRegisteredClaimNames.Jti, JwtRegisteredClaimNames.Exp, JwtRegisteredClaimNames.Iat, JwtRegisteredClaimNames.Nbf];

    private PeriodicTimer? _timer;
    private DotNetObjectReference<SessionRefresher>? _self;

    // The user, roles and permissions this tab shows: the session's when the tab last drew its menus.
    private string? _shownUser;
    private string _shownAccess = string.Empty;

    public void Start()
    {
        if (_timer != null)
            return;
        _timer = new PeriodicTimer(Every);
        _ = RunAsync(_timer);
    }

    private async Task RunAsync(PeriodicTimer timer)
    {
        try { await WatchAsync(); }
        catch (Exception) { } // the refreshes below still run
        do
        {
            try { await RefreshAsync(); }
            catch (Exception) { } // a failed refresh keeps the current session; the next tick tries again
        }
        while (await timer.WaitForNextTickAsync());
    }

    /// <summary>Starts following the browser's session from what this tab shows now.</summary>
    internal async Task WatchAsync()
    {
        auth.AuthenticationStateChanged += OnAuthenticationStateChanged;
        Show((await auth.GetAuthenticationStateAsync()).User);
        try
        {
            _self ??= DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("staffSession.watch", _self);
        }
        catch (JSException)
        {
            // A page without js/session.js (cached from an earlier version) hears of other tabs at its next refresh.
        }
    }

    public async Task RefreshAsync()
    {
        if (!string.IsNullOrEmpty(await account.GetTokenAsync()) && !await account.RefreshAsync())
        {
            // The API ended this session; the account cleared it, which signs the other tabs out too.
            NotifyChanged();
            navigation.NavigateTo("/login", forceLoad: true);
            return;
        }
        await CheckAsync();
    }

    /// <summary>Another tab signed in or out, or refreshed the session; or this tab came back into view.</summary>
    [JSInvokable]
    public Task OnSessionChangedElsewhereAsync() => CheckAsync();

    /// <summary>
    /// The browser's session against what this tab shows: another user, or none, reloads the tab; the same user with
    /// other roles or permissions updates its menus.
    /// </summary>
    private async Task CheckAsync()
    {
        var now = (await auth.GetAuthenticationStateAsync()).User;
        var user = UserOf(now);
        if (user != _shownUser)
        {
            var onSignInPage = navigation.ToBaseRelativePath(navigation.Uri).StartsWith("login", StringComparison.OrdinalIgnoreCase);
            navigation.NavigateTo(user is null ? "/login" : onSignInPage ? "/" : navigation.Uri, forceLoad: true);
            return;
        }
        if (Access(now) != _shownAccess)
            NotifyChanged();
    }

    // Signing in or out in this tab, and NotifyChanged: the tab now shows the session as it is.
    private async void OnAuthenticationStateChanged(Task<AuthenticationState> state)
    {
        try
        {
            Show((await state).User);
        }
        catch (Exception)
        {
            // The next check compares again.
        }
    }

    private void Show(ClaimsPrincipal user)
    {
        _shownUser = UserOf(user);
        _shownAccess = Access(user);
    }

    private void NotifyChanged()
    {
        if (auth is JwtAuthenticationStateProvider jwt)
            jwt.NotifyUserAuthentication();
    }

    private static string? UserOf(ClaimsPrincipal user) => user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    /// <summary>The user, roles and permissions, without the claims that change on every refresh.</summary>
    private static string Access(ClaimsPrincipal user)
        => string.Join('\n', user.Claims
            .Where(c => !ChangesEveryTime.Contains(c.Type))
            .Select(c => $"{c.Type}={c.Value}")
            .Order(StringComparer.Ordinal));

    public void Dispose()
    {
        _timer?.Dispose();
        auth.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        _self?.Dispose();
    }
}
