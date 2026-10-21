using System.Net.Http.Json;
using VetManagement.Contracts.Consumption;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

/// <summary>Consumption sheets: supplies used on each patient during a stay, charged at discharge.</summary>
public class ConsumptionApiService(HttpClient http)
{
    public async Task<List<ConsumptionSheetSummaryDto>> ListAsync(int closedDays)
        => await http.GetFromJsonAsync<List<ConsumptionSheetSummaryDto>>($"{ApiRouteConstants.CONSUMPTION_SHEETS}?closedDays={closedDays}") ?? [];

    public async Task<ConsumptionSheetDto?> GetAsync(int id)
    {
        var response = await http.GetAsync(string.Format(ApiRouteConstants.CONSUMPTION_SHEET_BY_ID, id));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ConsumptionSheetDto>() : null;
    }

    public async Task<ApiResult> OpenAsync(OpenConsumptionSheetRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.CONSUMPTION_SHEETS, request));

    public async Task<ApiResult> AddEntryAsync(int sheetId, AddConsumptionEntryRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.CONSUMPTION_SHEET_ENTRIES, sheetId), request));

    public async Task<ApiResult> RemoveEntryAsync(int sheetId, int entryId)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync($"{string.Format(ApiRouteConstants.CONSUMPTION_SHEET_ENTRIES, sheetId)}/{entryId}"));

    public async Task<ApiResult> DiscardRestAsync(int sheetId, int itemId)
        => await ApiResult.FromResponseAsync(await http.PostAsync(string.Format(ApiRouteConstants.CONSUMPTION_SHEET_DISCARD_REST, sheetId, itemId), null));

    public async Task<ApiResult> CloseAsync(int id)
        => await ApiResult.FromResponseAsync(await http.PostAsync($"{string.Format(ApiRouteConstants.CONSUMPTION_SHEET_BY_ID, id)}/close", null));

    public async Task<ApiResult> ReopenAsync(int id)
        => await ApiResult.FromResponseAsync(await http.PostAsync($"{string.Format(ApiRouteConstants.CONSUMPTION_SHEET_BY_ID, id)}/reopen", null));

    public async Task<ApiResult> DeleteAsync(int id)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.CONSUMPTION_SHEET_BY_ID, id)));

    public async Task<List<ConsumptionPricingDto>> GetPricingsAsync()
        => await http.GetFromJsonAsync<List<ConsumptionPricingDto>>(ApiRouteConstants.CONSUMPTION_PRICING) ?? [];

    public async Task<ApiResult> SetPricingAsync(ConsumptionPricingRequest request)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(ApiRouteConstants.CONSUMPTION_PRICING, request));

    public async Task<ApiResult> DeletePricingAsync(int itemId)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync($"{ApiRouteConstants.CONSUMPTION_PRICING}/{itemId}"));
}
