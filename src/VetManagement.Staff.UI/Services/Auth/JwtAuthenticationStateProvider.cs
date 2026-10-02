using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Staff.UI.Services.Auth;

/// <summary>
/// A custom AuthenticationStateProvider that uses a JWT to determine the user's authentication state.
/// It retrieves the token from the AccountApiService and parses it to create a ClaimsPrincipal.
/// </summary>
public class JwtAuthenticationStateProvider(AccountApiService accountService) : AuthenticationStateProvider
{

    /// <summary>
    /// The core method that determines the current authentication state.
    /// It is called by the framework to get the current user.
    /// </summary>
    /// <returns>An AuthenticationState representing the user.</returns>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await accountService.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            // User is not authenticated
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var jwtToken = handler.ReadJwtToken(token);

            // Check if the token is expired
            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                // Token is expired, treat as unauthenticated
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var claims = jwtToken.Claims;
            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            // Token is invalid or malformed
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    /// <summary>
    /// Notifies the framework that the authentication state has changed,
    /// typically after a user logs in. This triggers a re-render of authorized content.
    /// </summary>
    public void NotifyUserAuthentication()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Notifies the framework that the authentication state has changed,
    /// typically after a user logs out.
    /// </summary>
    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Get the current JWT token.
    /// </summary>
    public async Task<string?> GetTokenAsync()
        => await accountService.GetTokenAsync();
}
