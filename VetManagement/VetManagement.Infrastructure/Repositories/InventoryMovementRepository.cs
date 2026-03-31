using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Inventory;

namespace VetManagement.Infrastructure.Repositories;

public class InventoryMovementRepository(AppDbContext context) : Repository<InventoryMovement>(context), IInventoryMovementRepository
{
    public async Task<IEnumerable<InventoryMovement>> GetFilteredAsync(
        string? itemName = null,
        InventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        IQueryable<InventoryMovement> query = _context.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item);

        if (!string.IsNullOrWhiteSpace(itemName))
        {
            var term = itemName.Trim().ToLower();
            query = query.Where(m => m.Item != null && m.Item.Name.ToLower().Contains(term));
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

        return await query.OrderByDescending(m => m.Date).ToListAsync();
    }

    public async Task<IEnumerable<InventoryMovement>> GetByItemIdAsync(int itemId)
    {
        return await _context.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Item)
            .Where(m => m.ItemId == itemId)
            .OrderByDescending(m => m.Date)
            .ToListAsync();
    }
}
