using System.Net.Http.Json;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

public class InventoryAlertsApiService(HttpClient http)
{
    public record ScheduleDto(
        bool Enabled,
        int DayOfWeek,
        int Hour,
        int Minute,
        string To,
        string? Cc,
        string Subject,
        bool IncludeLowStock,
        bool IncludeOutOfStock,
        DateTime? LastSentUtc
    );

    public async Task<ScheduleDto?> GetScheduleAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<ScheduleDto>(ApiRouteConstants.INVENTORY_ALERTS_SCHEDULE, ct);

    public async Task SaveScheduleAsync(ScheduleDto schedule, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_ALERTS_SCHEDULE, schedule, ct);
        response.EnsureSuccessStatusCode();
    }
}
