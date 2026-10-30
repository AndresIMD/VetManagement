using System.Net.Http.Json;
using VetManagement.Contracts.Inventory;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

/// <summary>The clinic's warehouses: the ones the user sees, and (for whoever manages them) creating and changing them.</summary>
public class WarehousesApiService(HttpClient http)
{
    /// <summary>The warehouses the user sees, the daily-use one first; archived ones too for whoever manages them.</summary>
    public async Task<List<WarehouseDto>> GetAsync()
        => await http.GetFromJsonAsync<List<WarehouseDto>>(ApiRouteConstants.WAREHOUSES) ?? [];

    /// <summary>The roles a warehouse can list (Admin sees every warehouse).</summary>
    public async Task<List<string>> GetRolesAsync()
        => await http.GetFromJsonAsync<List<string>>(ApiRouteConstants.WAREHOUSE_ROLES) ?? [];

    public async Task<ApiResult> CreateAsync(WarehouseRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.WAREHOUSES, request));

    public async Task<ApiResult> UpdateAsync(int id, WarehouseRequest request)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(string.Format(ApiRouteConstants.WAREHOUSE_BY_ID, id), request));

    public async Task<ApiResult> DeleteAsync(int id)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.WAREHOUSE_BY_ID, id)));
}
