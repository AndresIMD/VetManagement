using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Staff.UI.Services.Auth;

/// <summary>
/// Keeps the session's token in step with the user's roles: refreshes it every minute (and on demand, e.g. after
/// changing roles), so menus and pages follow role changes without signing in again; signs out when the API says the
/// session is over (user blocked or deleted, token expired).
/// </summary>
public sealed class SessionRefresher(AccountApiService account, AuthenticationStateProvider auth, NavigationManager navigation) : IDisposable
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(1);
    private static readonly HashSet<string> ChangesEveryTime = [JwtRegisteredClaimNames.Jti, JwtRegisteredClaimNames.Exp, JwtRegisteredClaimNames.Iat, JwtRegisteredClaimNames.Nbf];

    private PeriodicTimer? _timer;

    public void Start()
    {
        if (_timer != null)
            return;
        _timer = new PeriodicTimer(Every);
        _ = RunAsync(_timer);
    }

    private async Task RunAsync(PeriodicTimer timer)
    {
        do
        {
            try { await RefreshAsync(); }
            catch (Exception) { } // a failed refresh keeps the current session; the next tick tries again
        }
        while (await timer.WaitForNextTickAsync());
    }

    public async Task RefreshAsync()
    {
        var before = await account.GetTokenAsync();
        if (string.IsNullOrEmpty(before))
            return;

        if (!await account.RefreshAsync())
        {
            await account.LogoutAsync();
            NotifyChanged();
            navigation.NavigateTo("/login", forceLoad: true);
            return;
        }
        if (Access(before) != Access(await account.GetTokenAsync()))
            NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (auth is JwtAuthenticationStateProvider jwt)
            jwt.NotifyUserAuthentication();
    }

    /// <summary>The token's user, roles and permissions, without the claims that change on every refresh.</summary>
    private static string Access(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return string.Empty;
        try
        {
            return string.Join('\n', new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
                .Where(c => !ChangesEveryTime.Contains(c.Type))
                .Select(c => $"{c.Type}={c.Value}")
                .Order(StringComparer.Ordinal));
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    public void Dispose() => _timer?.Dispose();
}
