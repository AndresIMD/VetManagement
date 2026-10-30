using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Infrastructure.Repositories;

public class WarehouseStockRepository(AppDbContext context) : Repository<WarehouseStock>(context), IWarehouseStockRepository
{
    // Find looks at the tracked rows first, so a row added or changed in this unit of work is the one returned.
    public async Task<WarehouseStock?> GetAsync(int warehouseId, int itemId)
        => await _context.Set<WarehouseStock>().FindAsync(warehouseId, itemId);

    public Task<List<WarehouseStock>> ForItemsAsync(IReadOnlyCollection<int> itemIds)
        => _context.Set<WarehouseStock>().AsNoTracking().Where(s => itemIds.Contains(s.ItemId)).ToListAsync();
}
