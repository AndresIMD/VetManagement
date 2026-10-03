using System.Net.Http.Json;
using VetManagement.Contracts.Reports;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

public class ReportsApiService(HttpClient http)
{
    /// <summary>The summary, or the API's message (e.g. range too long).</summary>
    public async Task<(ReportSummaryDto? Report, string? Error)> GetSummaryAsync(DateOnly from, DateOnly to)
    {
        var response = await http.GetAsync($"{ApiRouteConstants.REPORTS_SUMMARY}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ReportSummaryDto>(), null);
        return (null, (await ApiResult.FromResponseAsync(response)).Error);
    }
}
