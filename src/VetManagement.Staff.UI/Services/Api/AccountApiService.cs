using System.Net.Http.Json;
using System.Text.Json;
using VetManagement.Staff.UI.Constants;
using VetManagement.Contracts.Accounts;

#if !ANDROID && !IOS && !WINDOWS
using Microsoft.JSInterop;
#endif

namespace VetManagement.Staff.UI.Services.Api;

public class AccountApiService
{
    private readonly HttpClient _http;
#if !ANDROID && !IOS && !WINDOWS
    private readonly IJSRuntime? _js;
#endif
    private const string TOKEN_KEY = "authToken";
    private bool _mustChangePassword;
    public bool MustChangePassword => _mustChangePassword;

#if !ANDROID && !IOS && !WINDOWS
    public AccountApiService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }
#else
    public AccountApiService(HttpClient http)
    {
        _http = http;
    }
#endif

    public async Task<string?> LoginAsync(string username, string password, bool rememberMe)
    {
        var response = await _http.PostAsJsonAsync(ApiRouteConstants.ACCOUNT_LOGIN, new { Username = username, Password = password, RememberMe = rememberMe });
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

    public async Task SetTokenAsync(string token, bool rememberMe)
    {
#if ANDROID || IOS || WINDOWS
        await SecureStorage.Default.SetAsync(TOKEN_KEY, token);
#else
        if (_js != null)
        {
            // localStorage survives closing the browser; sessionStorage ends with the tab. Clear the other one so a
            // token from an earlier login can't win over this one.
            var (keep, clear) = rememberMe ? ("localStorage", "sessionStorage") : ("sessionStorage", "localStorage");
            await _js.InvokeVoidAsync($"{clear}.removeItem", TOKEN_KEY);
            await _js.InvokeVoidAsync($"{keep}.setItem", TOKEN_KEY, token);
        }
#endif
    }


    public async Task<string?> GetTokenAsync()
    {
#if ANDROID || IOS || WINDOWS
        return await SecureStorage.Default.GetAsync(TOKEN_KEY);
#else
        try
        {
            if (_js != null)
            {
                var sessionToken = await _js.InvokeAsync<string>("sessionStorage.getItem", TOKEN_KEY);
                if (!string.IsNullOrEmpty(sessionToken))
                    return sessionToken;

                var localToken = await _js.InvokeAsync<string>("localStorage.getItem", TOKEN_KEY);
                return localToken;
            }
            return null;
        }
        catch
        {
            return null;
        }
#endif
    }

    public async Task LogoutAsync()
    {
#if ANDROID || IOS || WINDOWS
        SecureStorage.Default.Remove(TOKEN_KEY);
#else
        if (_js != null)
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
            await _js.InvokeVoidAsync("sessionStorage.removeItem", TOKEN_KEY);
        }
#endif
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
                await SetTokenAsync(token, await IsRememberedAsync());
        }
        return result;
    }

    /// <summary>
    /// Replaces the stored token with one holding the user's current roles and permissions. False when the session is
    /// over (expired, or the user was blocked or deleted); a network error keeps the current session.
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        if (string.IsNullOrEmpty(await GetTokenAsync()))
            return false;
        HttpResponseMessage response;
        try
        {
            response = await SendAsync(HttpMethod.Post, ApiRouteConstants.ACCOUNT_REFRESH);
        }
        catch (HttpRequestException)
        {
            return true;
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return false;
        if (!response.IsSuccessStatusCode)
            return true;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (body.TryGetProperty("token", out var tokenProp) && tokenProp.GetString() is { Length: > 0 } token)
            await SetTokenAsync(token, await IsRememberedAsync());
        _mustChangePassword = body.TryGetProperty("mustChangePassword", out var mcp) && mcp.GetBoolean();
        return true;
    }

    /// <summary>Whether the session was started with "remember me" (its token is kept after closing the browser).</summary>
    private async Task<bool> IsRememberedAsync()
    {
#if ANDROID || IOS || WINDOWS
        return await Task.FromResult(true);
#else
        return _js == null || string.IsNullOrEmpty(await _js.InvokeAsync<string?>("sessionStorage.getItem", TOKEN_KEY));
#endif
    }

    /// <summary>Account calls with the session's token (this client has no JWT handler: login runs before there is a token).</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body != null)
            request.Content = JsonContent.Create(body);
        var token = await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await _http.SendAsync(request);
    }
}
