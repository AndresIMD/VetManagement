using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Contracts.Persistence;

public interface IWarehouseStockRepository : IRepository<WarehouseStock>
{
    /// <summary>The units of an item in a warehouse, tracked so they can be changed; null if it never held any.</summary>
    Task<WarehouseStock?> GetAsync(int warehouseId, int itemId);

    /// <summary>What warehouses other than the daily-use one hold of these items (read-only).</summary>
    Task<List<WarehouseStock>> ForItemsAsync(IReadOnlyCollection<int> itemIds);
}
