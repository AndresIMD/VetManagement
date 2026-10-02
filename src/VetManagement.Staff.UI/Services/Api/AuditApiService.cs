using System.Net.Http.Json;
using System.Text.Json;
using VetManagement.Staff.UI.Constants;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Models.Audit;
using VetManagement.Staff.UI.Models.DTOs;

namespace VetManagement.Staff.UI.Services.Api;

public class AuditApiService(HttpClient http)
{
    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Audit entries are written by the API itself; the UI only reads them.
    public async Task<PagedResult<AuditLog>?> GetLogsPagedAsync(
        int page,
        int pageSize,
        string? entityName = null,
        AuditActionType? action = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null,
        string? itemName = null,
        CancellationToken ct = default)
    {
        var parameters = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(entityName))
            parameters.Add($"entityName={Uri.EscapeDataString(entityName)}");
        if (action.HasValue)
            parameters.Add($"action={action.Value}");
        if (!string.IsNullOrWhiteSpace(user))
            parameters.Add($"user={Uri.EscapeDataString(user)}");
        if (from.HasValue)
            parameters.Add($"from={from.Value:O}");
        if (to.HasValue)
            parameters.Add($"to={to.Value:O}");
        if (!string.IsNullOrWhiteSpace(itemName))
            parameters.Add($"itemName={Uri.EscapeDataString(itemName)}");

        var url = $"{ApiRouteConstants.AUDIT_LOGS_PAGED}?{string.Join("&", parameters)}";

        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<PagedResult<AuditLog>>(stream, _caseInsensitiveOptions, ct);
    }
}

