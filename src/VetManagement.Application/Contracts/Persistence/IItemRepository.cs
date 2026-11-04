using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Contracts.Persistence;

public interface IItemRepository : IRepository<Item>
{
    Task<Item?> GetByBarcodeAsync(string barcode);

    Task<IEnumerable<Item>> GetLowStockAsync();

    Task<IEnumerable<Item>> GetOutOfStockAsync();

    Task<IEnumerable<Item>> SearchAsync(ItemType? type = null, string? searchTerm = null);

    /// <param name="hiddenWarehouseIds">Warehouses the user doesn't see: the stock filters and sort leave their units out.</param>
    /// <param name="onlyIn">
    /// Only the items this warehouse holds, with its units as the stock the filters and sort use: every item for the daily-use
    /// one (its units are what the others don't hold), the items it has a row for in another one, at 0 too once they ran out.
    /// </param>
    Task<(List<Item> Items, int TotalCount)> GetPagedListAsync(
        string? searchTerm,
        ItemType? type,
        int page,
        int pageSize,
        StockAlertFilter? alertFilter = null,
        ItemSortField? sortBy = null,
        SortDirection sortDirection = SortDirection.Ascending,
        IReadOnlyCollection<int>? hiddenWarehouseIds = null,
        Warehouse? onlyIn = null);
}
