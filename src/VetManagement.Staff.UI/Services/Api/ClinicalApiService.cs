using System.Net.Http.Json;
using System.Text.Json;
using VetManagement.Contracts.Clinical;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

public sealed record ProtocolOption(string Code, string Name, PreventiveKind Kind, Species? Species, int IntervalDays, bool Enabled);

public class ClinicalApiService(HttpClient http)
{
    public Task<PetHistoryDto?> GetHistoryAsync(int petId)
        => http.GetFromJsonAsync<PetHistoryDto>(string.Format(ApiRouteConstants.CLINICAL_PET_HISTORY, petId));

    public async Task<ApiResult> RecordDoseAsync(int petId, RecordDoseRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.CLINICAL_PET_DOSES, petId), request));

    public async Task<ApiResult> DeleteDoseAsync(int id)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.CLINICAL_DOSE, id)));

    public async Task<ApiResult> AddSupplyAsync(int visitId, AddVisitSupplyRequest request)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.CLINICAL_VISIT_SUPPLIES, visitId), request));

    public async Task<ApiResult> RemoveSupplyAsync(int supplyId)
        => await ApiResult.FromResponseAsync(await http.DeleteAsync(string.Format(ApiRouteConstants.CLINICAL_SUPPLY, supplyId)));

    public async Task<List<DueDoseDto>> GetDueAsync(int days)
        => await http.GetFromJsonAsync<List<DueDoseDto>>($"{ApiRouteConstants.CLINICAL_DUE}?days={days}") ?? [];

    public Task<ClinicalSettingsDocument?> GetSettingsAsync()
        => http.GetFromJsonAsync<ClinicalSettingsDocument>(ApiRouteConstants.CLINICAL_SETTINGS);

    public async Task<ApiResult> SaveSettingsAsync(ClinicalSettingsDocument document)
        => await ApiResult.FromResponseAsync(await http.PutAsJsonAsync(ApiRouteConstants.CLINICAL_SETTINGS, document));

    /// <summary>Preventive protocols from the settings, for the dose form.</summary>
    public async Task<List<ProtocolOption>> GetProtocolsAsync()
    {
        if ((await GetSettingsAsync())?.Settings is not { ValueKind: JsonValueKind.Object } s)
            return [];
        return s.GetProperty("protocols").EnumerateArray().Select(p => new ProtocolOption(
            p.GetProperty("code").GetString()!,
            p.GetProperty("name").GetString()!,
            Enum.Parse<PreventiveKind>(p.GetProperty("kind").GetString()!),
            p.GetProperty("species").ValueKind == JsonValueKind.String ? Enum.Parse<Species>(p.GetProperty("species").GetString()!) : null,
            p.GetProperty("intervalDays").GetInt32(),
            p.GetProperty("enabled").GetBoolean())).ToList();
    }
}
