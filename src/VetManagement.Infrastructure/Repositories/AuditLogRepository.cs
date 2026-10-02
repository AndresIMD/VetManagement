using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Audit;

namespace VetManagement.Infrastructure.Repositories;

public class AuditLogRepository(AppDbContext context) : Repository<AuditLog>(context), IAuditLogRepository
{
    public async Task<IEnumerable<AuditLog>> GetFilteredAsync(string? entityName = null, AuditActionType? action = null, string? user = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName == entityName);
        }

        if (action.HasValue)
        {
            query = query.Where(a => a.Action == action.Value);
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            var term = user.Trim();
            query = query.Where(a => a.User != null && a.User.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(a => a.Date <= to.Value);
        }

        return await query.OrderByDescending(a => a.Date).ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(int entityId, string entityName)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityId == entityId && a.EntityName == entityName)
            .OrderByDescending(a => a.Date)
            .ToListAsync();
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? entityId = null,
        string? entityName = null,
        AuditActionType? action = null,
        string? user = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (entityId.HasValue)
        {
            query = query.Where(a => a.EntityId == entityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName == entityName);
        }

        if (action.HasValue)
        {
            query = query.Where(a => a.Action == action.Value);
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            var term = user.Trim();
            query = query.Where(a => a.User != null && a.User.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(a => a.Date <= to.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.Date)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}

