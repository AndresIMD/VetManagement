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

    public async Task<List<AuditLog>?> GetLogsForEntityAsync(string entityName, int entityId = 0)
    {
        var url = $"{ApiRouteConstants.AUDIT_LOGS}?entityName={entityName}&entityId={entityId}";
        return await http.GetFromJsonAsync<List<AuditLog>>(url);
    }

    /// <summary>
    /// Creates a new audit log entry.
    /// </summary>
    /// <param name="entityName">The name of the entity being changed.</param>
    /// <param name="entityId">The ID of the entity instance.</param>
    /// <param name="action">The action performed.</param>
    /// <param name="changes">A summary of the changes, often in JSON format.</param>
    public async Task LogAsync(string entityName, int entityId, AuditActionType action, string changes)
    {
        var log = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            Changes = changes,
            Date = DateTime.UtcNow,
        };
        await http.PostAsJsonAsync(ApiRouteConstants.AUDIT_LOGS, log);
    }

    public async Task<List<AuditLog>?> GetLogsFilteredAsync(
        string? entityName = null,
        int? entityId = null,
        AuditActionType? action = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(entityName))
            parameters.Add($"entityName={Uri.EscapeDataString(entityName)}");
        if (entityId.HasValue)
            parameters.Add($"entityId={entityId.Value}");
        if (action.HasValue)
            parameters.Add($"action={action.Value}");
        if (!string.IsNullOrWhiteSpace(user))
            parameters.Add($"user={Uri.EscapeDataString(user)}");
        if (from.HasValue)
            parameters.Add($"from={from.Value:O}");
        if (to.HasValue)
            parameters.Add($"to={to.Value:O}");

        var url = ApiRouteConstants.AUDIT_LOGS;
        if (parameters.Count > 0)
        {
            url += "?" + string.Join("&", parameters);
        }

        return await http.GetFromJsonAsync<List<AuditLog>>(url);
    }

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

        var url = $"{ApiRouteConstants.AUDIT_LOGS}/paged?{string.Join("&", parameters)}";

        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<PagedResult<AuditLog>>(stream, _caseInsensitiveOptions, ct);
    }
}

