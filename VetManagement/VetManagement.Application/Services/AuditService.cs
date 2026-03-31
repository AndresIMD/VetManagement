using VetManagement.Application.Contracts.Persistence;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Audit;
using VetManagement.Shared.Models.DTOs;

namespace VetManagement.Application.Services;

public class AuditService(IUnitOfWork unitOfWork)
{
    public async Task<List<AuditLog>> GetLogsFilteredAsync(
        int? entityId = null,
        string? entityName = null,
        DateTime? from = null,
        DateTime? to = null,
        AuditActionType? action = null,
        string? user = null)
    {
        var allLogs = await unitOfWork.AuditLogs.GetAllAsync();
        var query = allLogs.AsEnumerable();

        if (entityId.HasValue)
            query = query.Where(l => l.EntityId == entityId.Value);
        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(l => l.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase));
        if (from.HasValue)
            query = query.Where(l => l.Date >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.Date <= to.Value);
        if (action.HasValue)
            query = query.Where(l => l.Action == action.Value);
        if (!string.IsNullOrWhiteSpace(user))
            query = query.Where(l => (l.User ?? string.Empty).Contains(user, StringComparison.OrdinalIgnoreCase));

        return query.OrderByDescending(l => l.Date).ToList();
    }

    public async Task<PagedResult<AuditLog>> GetLogsPagedAsync(
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
        var allLogs = await unitOfWork.AuditLogs.GetAllAsync();
        var query = allLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(entityName))
            query = query.Where(l => l.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase));
        if (from.HasValue)
            query = query.Where(l => l.Date >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.Date <= to.Value);
        if (action.HasValue)
            query = query.Where(l => l.Action == action.Value);
        if (!string.IsNullOrWhiteSpace(user))
            query = query.Where(l => (l.User ?? string.Empty).Contains(user, StringComparison.OrdinalIgnoreCase));

        query = query.OrderByDescending(l => l.Date);

        var total = query.Count();
        var items = query.Skip(page * pageSize).Take(pageSize).ToList();

        return new PagedResult<AuditLog>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task LogAsync(string entityName, int entityId, AuditActionType action, string changes, string? user)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = entityName,
            Date = DateTime.UtcNow,
            Action = action,
            Changes = changes,
            User = user
        });
        await unitOfWork.SaveChangesAsync();
    }

    public Task LogSecurityAsync(AuditActionType action, string? user, string details)
        => LogAsync("Security", 0, action, details, user);
}

