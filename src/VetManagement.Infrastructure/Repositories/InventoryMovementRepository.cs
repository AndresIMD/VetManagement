using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

namespace VetManagement.Infrastructure.Repositories;

public class InventoryMovementRepository(AppDbContext context) : Repository<InventoryMovement>(context), IInventoryMovementRepository
{
    public async Task<IEnumerable<InventoryMovement>> GetByItemIdAsync(int itemId, IReadOnlyCollection<int>? hiddenWarehouseIds = null)
    {
        return await Visible(_context.InventoryMovements.AsNoTracking(), hiddenWarehouseIds)
            .Include(m => m.Item)
            .Where(m => m.ItemId == itemId)
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .ToListAsync();
    }

    public Task<List<MovementQuantity>> GetQuantitiesAsync(DateTime fromUtc, DateTime toUtc)
        => _context.InventoryMovements
            .AsNoTracking()
            .Where(m => m.Date >= fromUtc && m.Date < toUtc)
            .Select(m => new MovementQuantity(m.ItemId, m.Type, m.Quantity, m.Date))
            .ToListAsync();

    public Task<bool> AnyInWarehouseAsync(int warehouseId)
        => _context.InventoryMovements.AnyAsync(m => m.WarehouseId == warehouseId);

    public async Task<(List<InventoryMovement> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? itemId = null,
        InventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null,
        IReadOnlyCollection<int>? hiddenWarehouseIds = null,
        int? warehouseId = null,
        bool onlyDailyUse = false)
    {
        IQueryable<InventoryMovement> query = Visible(_context.InventoryMovements.AsNoTracking(), hiddenWarehouseIds)
            .Include(m => m.Item);

        if (itemId.HasValue)
        {
            query = query.Where(m => m.ItemId == itemId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(m => m.WarehouseId == warehouseId.Value);
        }
        else if (onlyDailyUse)
        {
            query = query.Where(m => m.WarehouseId == null);
        }

        if (type.HasValue && type.Value != InventoryMovementType.None)
        {
            query = query.Where(m => m.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(responsible))
        {
            var term = responsible.Trim().ToLower();
            query = query.Where(m => m.Responsible != null && m.Responsible.ToLower().Contains(term));
        }

        if (from.HasValue)
        {
            query = query.Where(m => m.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(m => m.Date <= to.Value);
        }

        var totalCount = await query.CountAsync();

        // A transfer's two movements share their date: the id keeps them in order and pages stable.
        var items = await query
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>Leaves out the movements of hidden warehouses; the daily-use warehouse's (no warehouse) always stay.</summary>
    private static IQueryable<InventoryMovement> Visible(IQueryable<InventoryMovement> query, IReadOnlyCollection<int>? hiddenWarehouseIds)
        => hiddenWarehouseIds is { Count: > 0 }
            ? query.Where(m => m.WarehouseId == null || !hiddenWarehouseIds.Contains(m.WarehouseId.Value))
            : query;
}
