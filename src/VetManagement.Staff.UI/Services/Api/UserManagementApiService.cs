using System.Net.Http.Json;
using VetManagement.Staff.UI.Constants;
using VetManagement.Contracts.Accounts;

namespace VetManagement.Staff.UI.Services.Api;

public class UserManagementApiService(HttpClient http)
{
    public async Task<List<UserDto>> GetUsersAsync()
        => await http.GetFromJsonAsync<List<UserDto>>(ApiRouteConstants.USERS_BASE) ?? new();

    public async Task<List<string>> GetRolesAsync()
        => await http.GetFromJsonAsync<List<string>>(ApiRouteConstants.USERS_ROLES_ALL) ?? new();

    public async Task<ApiResult> AssignRoleAsync(string userId, string role)
        => await ApiResult.FromResponseAsync(await http.PostAsync(string.Format(ApiRouteConstants.USER_ROLES, userId) + $"/{role}", null));

    /// <summary>Can be refused (e.g. removing Admin from the last active Admin); the result says why.</summary>
    public async Task<ApiResult> RemoveRoleAsync(string userId, string role)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.USER_ROLES, userId) + $"/{role}"));

    public async Task<ApiResult> CreateUserAsync(CreateUserRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.USERS_BASE, request));

    public async Task<ApiResult> UpdateUserAsync(string userId, EditUserRequest request)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(string.Format(ApiRouteConstants.USER_BY_ID, userId), request));

    public async Task<ApiResult> SetBlockedAsync(string userId, bool blocked)
        => await ApiResult.FromResponseAsync(await http.PostAsync(string.Format(ApiRouteConstants.USER_BY_ID, userId) + (blocked ? "/block" : "/unblock"), null));

    public async Task<ApiResult> ResetPasswordAsync(string userId, ResetPasswordRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.USER_BY_ID, userId) + "/reset-password", request));

    public async Task<ApiResult> DeleteUserAsync(string userId)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.USER_BY_ID, userId)));
}
