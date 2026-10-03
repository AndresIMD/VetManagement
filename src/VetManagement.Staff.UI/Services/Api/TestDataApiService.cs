using System.Net;
using System.Net.Http.Json;
using VetManagement.Contracts.TestData;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

public class TestDataApiService(HttpClient http)
{
    /// <summary>Null when the API doesn't offer sample data (any environment other than development/test).</summary>
    public async Task<List<TestDataStatusDto>?> GetStatusAsync()
    {
        var response = await http.GetAsync(ApiRouteConstants.TEST_DATA);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TestDataStatusDto>>() ?? [];
    }

    /// <summary>How many were created, or the message explaining why not (e.g. what to create first).</summary>
    public async Task<(int Created, string? Error)> CreateAsync(string key, int count)
    {
        var response = await http.PostAsync($"{ApiRouteConstants.TEST_DATA}/{Uri.EscapeDataString(key)}?count={count}", null);
        if (response.IsSuccessStatusCode)
            return ((await response.Content.ReadFromJsonAsync<TestDataCreatedDto>())?.Created ?? 0, null);
        return (0, (await ApiResult.FromResponseAsync(response)).Error);
    }
}
