using VetManagement.Domain.Enums;
using VetManagement.Domain.Audit;

namespace VetManagement.Application.Contracts.Persistence;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetFilteredAsync(
        string? entityName = null,
        AuditActionType? action = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null);

    Task<IEnumerable<AuditLog>> GetByEntityAsync(int entityId, string entityName);

    /// <summary>A page of the log, newest first; <paramref name="entityIds"/> and <paramref name="actions"/> match any of their values.</summary>
    Task<(List<AuditLog> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        IReadOnlyCollection<int>? entityIds = null,
        string? entityName = null,
        IReadOnlyCollection<AuditActionType>? actions = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null);
}

