using System.Net.Http.Json;
using VetManagement.Shared.Constants;
using VetManagement.Contracts.Accounts;

namespace VetManagement.Shared.Services.Api;

public class UserManagementApiService(HttpClient http)
{
    public async Task<List<UserDto>> GetUsersAsync()
        => await http.GetFromJsonAsync<List<UserDto>>(ApiRouteConstants.USERS_BASE) ?? new();

    public async Task<List<string>> GetRolesAsync()
        => await http.GetFromJsonAsync<List<string>>(ApiRouteConstants.USERS_ROLES_ALL) ?? new();

    public async Task<List<string>> GetUserRolesAsync(string userId)
        => await http.GetFromJsonAsync<List<string>>(string.Format(ApiRouteConstants.USER_ROLES, userId)) ?? new();

    public async Task<List<(string Type, string Value)>> GetUserClaimsAsync(string userId)
    {
        var data = await http.GetFromJsonAsync<List<ClaimDto>>(string.Format(ApiRouteConstants.USER_CLAIMS, userId)) ?? new();
        return data.Select(c => (c.Type, c.Value)).ToList();
    }

    public async Task<bool> AssignRoleAsync(string userId, string role)
    {
        var uri = string.Format(ApiRouteConstants.USER_ROLES, userId) + $"/{role}";
        var response = await http.PostAsync(uri, null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveRoleAsync(string userId, string role)
    {
        var uri = string.Format(ApiRouteConstants.USER_ROLES, userId) + $"/{role}";
        var response = await http.DeleteAsync(uri);
        return response.IsSuccessStatusCode;
    }

    public async Task<(bool Success, string? Error)> CreateUserAsync(CreateUserRequest request)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.USERS_BASE, request);
        if (response.IsSuccessStatusCode)
            return (true, null);
        var error = await response.Content.ReadAsStringAsync();
        return (false, error);
    }

    public record ClaimDto(string Type, string Value);
}
