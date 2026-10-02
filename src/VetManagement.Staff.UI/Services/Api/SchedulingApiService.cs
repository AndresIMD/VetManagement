using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VetManagement.Contracts.Scheduling;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

/// <summary>Outcome of a write call: success, or a message the page can show as-is.</summary>
public sealed record ApiResult(bool Ok, string? Error = null, int? Id = null, JsonElement? Body = null);

public sealed record CatalogService(string Code, string Name, bool Enabled, int DurationMinutes, IReadOnlyList<string> ResourceCodes);

/// <summary>Display names from the agenda settings, so pages show "Consulta general" instead of codes.</summary>
public sealed record AgendaCatalog(bool AgendaEnabled, IReadOnlyList<CatalogService> Services, IReadOnlyDictionary<string, string> ResourceNames)
{
    public string ServiceName(string code) => Services.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))?.Name ?? code;
    public string ResourceName(string code) => ResourceNames.TryGetValue(code, out var name) ? name : code;
}

public class SchedulingApiService(HttpClient http)
{
    public Task<SchedulingSettingsDocument?> GetSettingsAsync()
        => http.GetFromJsonAsync<SchedulingSettingsDocument>(ApiRouteConstants.SCHEDULING_SETTINGS);

    public async Task<AgendaCatalog> GetCatalogAsync()
    {
        var settings = (await GetSettingsAsync())?.Settings;
        if (settings is not { ValueKind: JsonValueKind.Object } s)
            return new AgendaCatalog(false, [], new Dictionary<string, string>());

        var services = s.GetProperty("services").EnumerateArray().Select(x => new CatalogService(
            x.GetProperty("code").GetString()!,
            x.GetProperty("name").GetString()!,
            x.GetProperty("enabled").GetBoolean(),
            x.GetProperty("durationMinutes").GetInt32(),
            x.GetProperty("resourceCodes").EnumerateArray().Select(r => r.GetString()!).ToList())).ToList();
        var resources = s.GetProperty("resources").EnumerateArray().ToDictionary(
            x => x.GetProperty("code").GetString()!, x => x.GetProperty("name").GetString()!, StringComparer.OrdinalIgnoreCase);

        return new AgendaCatalog(s.GetProperty("enabled").GetBoolean(), services, resources);
    }

    public async Task<ApiResult> SaveSettingsAsync(SchedulingSettingsDocument document)
        => await ToResultAsync(await http.PutAsJsonAsync(ApiRouteConstants.SCHEDULING_SETTINGS, document));

    public async Task<List<AvailableSlotDto>> GetAvailabilityAsync(string serviceCode, DateOnly from, DateOnly to)
        => await http.GetFromJsonAsync<List<AvailableSlotDto>>(
               $"{ApiRouteConstants.SCHEDULING_AVAILABILITY}?service={Uri.EscapeDataString(serviceCode)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}")
           ?? [];

    public async Task<List<AppointmentDto>> GetAppointmentsAsync(DateTime fromUtc, DateTime toUtc, AppointmentStatus? status = null)
    {
        var url = $"{ApiRouteConstants.SCHEDULING_APPOINTMENTS}?fromUtc={fromUtc:O}&toUtc={toUtc:O}";
        if (status is not null)
            url += $"&status={status}";
        return await http.GetFromJsonAsync<List<AppointmentDto>>(url) ?? [];
    }

    public async Task<ApiResult> BookAsync(CreateAppointmentRequest request)
        => await ToResultAsync(await http.PostAsJsonAsync(ApiRouteConstants.SCHEDULING_APPOINTMENTS, request));

    public async Task<ApiResult> RescheduleAsync(int id, RescheduleAppointmentRequest request)
        => await ToResultAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.SCHEDULING_APPOINTMENT_RESCHEDULE, id), request));

    public async Task<ApiResult> CancelAsync(int id, string? reason)
        => await ToResultAsync(await http.PostAsJsonAsync(string.Format(ApiRouteConstants.SCHEDULING_APPOINTMENT_CANCEL, id), new CancelAppointmentRequest { Reason = reason }));

    /// <summary>Turns ProblemDetails (title, detail, validation errors) into one readable message.</summary>
    private static async Task<ApiResult> ToResultAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        JsonElement? body = string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text).RootElement.Clone();

        if (response.IsSuccessStatusCode)
        {
            int? id = body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt32() : null;
            return new ApiResult(true, Id: id, Body: body);
        }

        var messages = new List<string>();
        if (body is { ValueKind: JsonValueKind.Object } problem)
        {
            if (problem.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                foreach (var field in errors.EnumerateObject())
                    messages.AddRange(field.Value.EnumerateArray().Select(e => e.GetString() ?? ""));
            if (messages.Count == 0 && problem.TryGetProperty("title", out var title))
                messages.Add(title.GetString() ?? "");
            if (problem.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d)
                messages.Add(d);
        }
        if (messages.Count == 0)
            messages.Add(response.StatusCode == HttpStatusCode.Forbidden ? "You don't have permission for this action." : $"Request failed ({(int)response.StatusCode}).");

        return new ApiResult(false, string.Join(Environment.NewLine, messages), Body: body);
    }
}
