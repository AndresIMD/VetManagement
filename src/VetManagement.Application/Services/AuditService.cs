using VetManagement.Application.Contracts.Persistence;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Audit;
using VetManagement.Shared.Models.DTOs;

namespace VetManagement.Application.Services;

public class AuditService(IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Gets audit logs with server-side filtering and pagination.
    /// Efficient for large datasets - only requested page loaded from database.
    /// </summary>
    public async Task<PagedResult<AuditLog>> GetLogsPagedAsync(
        int page = 0,
        int pageSize = 25,
        string? entityName = null,
        AuditActionType? action = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null,
        int? entityId = null,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await unitOfWork.AuditLogs.GetFilteredPagedAsync(
            page: page,
            pageSize: pageSize,
            entityId: entityId,
            entityName: entityName,
            action: action,
            user: user,
            from: from,
            to: to);

        return new PagedResult<AuditLog>
        {
            Items = items,
            TotalCount = totalCount,
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

