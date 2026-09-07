using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Application.Contracts.Persistence;

public interface IItemRepository : IRepository<Item>
{
    Task<Item?> GetByBarcodeAsync(string barcode);

    Task<IEnumerable<Item>> GetLowStockAsync();

    Task<IEnumerable<Item>> GetOutOfStockAsync();

    Task<IEnumerable<Item>> SearchAsync(ItemType? type = null, string? searchTerm = null);

    Task<(List<Item> Items, int TotalCount)> GetPagedListAsync(
        string? searchTerm,
        ItemType? type,
        int page,
        int pageSize,
        StockAlertFilter? alertFilter = null,
        ItemSortField? sortBy = null,
        SortDirection sortDirection = SortDirection.Ascending);
}
