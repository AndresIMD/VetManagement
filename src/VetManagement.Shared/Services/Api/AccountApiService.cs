using System.Net.Http.Json;
using System.Text.Json;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Models.DTOs;

#if !ANDROID && !IOS && !WINDOWS
using Microsoft.JSInterop;
#endif

namespace VetManagement.Shared.Services.Api;

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
            if (rememberMe)
                await _js.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, token);
            else
                await _js.InvokeVoidAsync("sessionStorage.setItem", TOKEN_KEY, token);
        }
#endif
    }

    public async Task SetTokenAsync(string token)
    {
        await SetTokenAsync(token, false);
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
            return await _http.GetFromJsonAsync<UserInfo>(ApiRouteConstants.ACCOUNT_USER_INFO);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateUserInfoAsync(UpdateUserRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync(ApiRouteConstants.ACCOUNT_USER_INFO, request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(ApiRouteConstants.ACCOUNT_CHANGE_PASSWORD, request);
            if (response.IsSuccessStatusCode)
                _mustChangePassword = false;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
