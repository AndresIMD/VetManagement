using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Audit;

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

    Task<(List<AuditLog> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? entityId = null,
        string? entityName = null,
        AuditActionType? action = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null);
}

