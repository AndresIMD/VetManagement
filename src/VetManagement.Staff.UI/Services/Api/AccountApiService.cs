using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using VetManagement.Staff.UI.Constants;
using VetManagement.Contracts.Accounts;
using VetManagement.Staff.UI.Services.Auth;

namespace VetManagement.Staff.UI.Services.Api;

/// <summary>
/// Sign-in, refresh and sign-out, and where the session's token is kept: ONE session per browser, in localStorage, so
/// every tab uses it. A new tab is already signed in, and signing in or out in one tab does it in all of them. Without
/// "Remember me" the session still ends when the browser closes: it is marked, and js/session.js keeps a cookie that the
/// browser drops when it closes, so the first read after that ends it.
/// Before, a session without "Remember me" lived in its tab (sessionStorage): a new tab asked to sign in again, and at
/// its next refresh it wiped the session of a tab signed in with "Remember me", which then loaded no data (production,
/// 2026-11).
/// The app hands out a new instance for every use (typed HttpClient), so nothing is kept here between calls: the last
/// token read is kept in <see cref="SessionToken"/>, for the code that can't reach the browser's storage.
/// </summary>
public class AccountApiService(HttpClient http, IJSRuntime js, SessionToken session)
{
    private const string TOKEN_KEY = "authToken";
    /// <summary>Set while the session lasts only until the browser closes ("Remember me" not ticked).</summary>
    private const string UNTIL_BROWSER_CLOSES_KEY = "authUntilBrowserCloses";

    private bool _mustChangePassword;
    public bool MustChangePassword => _mustChangePassword;

    public async Task<string?> LoginAsync(string username, string password, bool rememberMe)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.ACCOUNT_LOGIN, new { Username = username, Password = password, RememberMe = rememberMe });
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Too many login attempts.", null, response.StatusCode);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var token = root.TryGetProperty("token", out var tProp) ? tProp.GetString() : null;
        _mustChangePassword = root.TryGetProperty("mustChangePassword", out var mcp) && mcp.GetBoolean();
        if (!string.IsNullOrEmpty(token))
        {
            await SetTokenAsync(token, rememberMe);
        }
        return token;
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        return await LoginAsync(username, password, false);
    }

    /// <summary>Makes this token the browser's session, for every tab.</summary>
    public async Task SetTokenAsync(string token, bool rememberMe)
    {
        // The mark goes before the token, so no tab finds a token kept until the browser closes without its cookie.
        if (rememberMe)
            await js.InvokeVoidAsync("localStorage.removeItem", UNTIL_BROWSER_CLOSES_KEY);
        else
        {
            await TrySessionScriptAsync("staffSession.markBrowserOpen");
            await js.InvokeVoidAsync("localStorage.setItem", UNTIL_BROWSER_CLOSES_KEY, "1");
        }
        await StoreAsync(token);
        await js.InvokeVoidAsync("sessionStorage.removeItem", TOKEN_KEY); // where earlier versions kept it
    }

    /// <summary>
    /// The browser's session token. Null when signed out, and once the browser has closed since a sign-in without
    /// "Remember me" (the session is then cleared).
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            await AdoptTabSessionAsync();
            var token = await js.InvokeAsync<string?>("localStorage.getItem", TOKEN_KEY);
            if (string.IsNullOrEmpty(token))
                return session.Value = null;
            if (await js.InvokeAsync<string?>("localStorage.getItem", UNTIL_BROWSER_CLOSES_KEY) == "1" && await BrowserWasClosedAsync())
            {
                await ClearAsync();
                return null;
            }
            return session.Value = token;
        }
        catch
        {
            // JavaScript can't run here (the desktop app's HTTP handlers): the last token read stands.
            return session.Value;
        }
    }

    /// <summary>Signs the browser out: every tab.</summary>
    public async Task LogoutAsync()
    {
        await ClearAsync();
        _mustChangePassword = false;
    }

    public async Task<UserInfo?> GetUserInfoAsync()
    {
        try
        {
            var response = await SendAsync(HttpMethod.Get, ApiRouteConstants.ACCOUNT_USER_INFO);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<UserInfo>() : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResult> UpdateUserInfoAsync(UpdateUserRequest request)
        => await ApiResult.FromResponseAsync(await SendAsync(HttpMethod.Put, ApiRouteConstants.ACCOUNT_USER_INFO, request));

    public async Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var result = await ApiResult.FromResponseAsync(await SendAsync(HttpMethod.Post, ApiRouteConstants.ACCOUNT_CHANGE_PASSWORD, request));
        if (result.Ok)
        {
            _mustChangePassword = false;
            // The new password ends the sessions opened with the old one, this one included: the API sends its new token.
            if (result.Body is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("token", out var tokenProp)
                && tokenProp.GetString() is { Length: > 0 } token)
                await StoreAsync(token); // same session: "Remember me" stays as it was
        }
        return result;
    }

    /// <summary>
    /// Replaces the browser's token with one holding the user's current roles and permissions. False when the API ended
    /// this session (expired, or the user was blocked or deleted): it is cleared, in every tab. A network error keeps it.
    /// An answer that comes after another tab signed in or out is dropped, so it can't undo that.
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        var sent = await GetTokenAsync();
        if (string.IsNullOrEmpty(sent))
            return false;
        HttpResponseMessage response;
        try
        {
            response = await SendAsync(HttpMethod.Post, ApiRouteConstants.ACCOUNT_REFRESH, token: sent);
        }
        catch (HttpRequestException)
        {
            return true;
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (await GetTokenAsync() != sent)
                return true; // another tab signed in or out meanwhile: that session isn't the one the API ended
            await ClearAsync();
            return false;
        }
        if (!response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        _mustChangePassword = body.TryGetProperty("mustChangePassword", out var mcp) && mcp.GetBoolean();
        if (body.TryGetProperty("token", out var tokenProp) && tokenProp.GetString() is { Length: > 0 } token
            && await GetTokenAsync() == sent)
            await StoreAsync(token); // same session: "Remember me" stays as it was
        return true;
    }

    /// <summary>
    /// After an update, a session that an earlier version kept in this tab becomes the browser's, unless the browser has
    /// one already. New sign-ins never write there.
    /// </summary>
    private async Task AdoptTabSessionAsync()
    {
        var tabToken = await js.InvokeAsync<string?>("sessionStorage.getItem", TOKEN_KEY);
        if (string.IsNullOrEmpty(tabToken))
            return;
        if (string.IsNullOrEmpty(await js.InvokeAsync<string?>("localStorage.getItem", TOKEN_KEY)))
            await SetTokenAsync(tabToken, rememberMe: false);
        else
            await js.InvokeVoidAsync("sessionStorage.removeItem", TOKEN_KEY);
    }

    /// <summary>The token, in memory and in the browser's storage.</summary>
    private async Task StoreAsync(string token)
    {
        session.Value = token;
        await js.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, token);
    }

    private async Task ClearAsync()
    {
        session.Value = null;
        await js.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
        await js.InvokeVoidAsync("localStorage.removeItem", UNTIL_BROWSER_CLOSES_KEY);
        await js.InvokeVoidAsync("sessionStorage.removeItem", TOKEN_KEY);
        await TrySessionScriptAsync("staffSession.clearBrowserMark");
    }

    /// <summary>
    /// Only a "yes" counts: a page without js/session.js (an index.html cached from an earlier version) keeps the
    /// session, which then lasts until its token expires.
    /// </summary>
    private async Task<bool> BrowserWasClosedAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("staffSession.browserWasClosed");
        }
        catch (JSException)
        {
            return false;
        }
    }

    private async Task TrySessionScriptAsync(string identifier)
    {
        try
        {
            await js.InvokeVoidAsync(identifier);
        }
        catch (JSException)
        {
            // No js/session.js on this page: see BrowserWasClosedAsync.
        }
    }

    /// <summary>
    /// Account calls with the session's token, or the one given (this client has no JWT handler: login runs before there
    /// is a token).
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body != null)
            request.Content = JsonContent.Create(body);
        token ??= await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request);
    }
}
