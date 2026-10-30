using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Inventory;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Inventory;
using DomainItemSortField = VetManagement.Domain.Enums.ItemSortField;
using DomainItemType = VetManagement.Domain.Enums.ItemType;
using DomainSortDirection = VetManagement.Domain.Enums.SortDirection;
using DomainStockAlertFilter = VetManagement.Domain.Enums.StockAlertFilter;

namespace VetManagement.Application.Services;

public class InventoryQueryService(IUnitOfWork unitOfWork, WarehouseService warehouses)
{
    /// <summary>A page of items with the stock the user sees; the stock filters and sort use it too.</summary>
    public async Task<PagedResponse<InventoryItemDto>> GetItemsPagedAsync(
        StockScope scope,
        string? search,
        DomainItemType? type,
        int page,
        int pageSize,
        DomainStockAlertFilter? alertFilter = null,
        DomainItemSortField? sortBy = null,
        DomainSortDirection sortDirection = DomainSortDirection.Ascending)
    {
        var (items, totalCount) = await unitOfWork.Items.GetPagedListAsync(search, type, page, pageSize, alertFilter, sortBy, sortDirection,
            await warehouses.HiddenFromAsync(scope));
        var views = await warehouses.ViewAsync(items, scope);

        var pageItems = items.Select(i => new InventoryItemDto
        {
            Id = i.Id,
            Name = i.Name,
            Brand = i.Brand,
            Compound = i is Drug drug ? drug.Compound : null,
            Type = i.Type,
            Stock = views[i.Id].Seen,
            DailyUseStock = views[i.Id].InDailyUse,
            Warehouses = ToDtos(views[i.Id]),
            AskAt = [.. views[i.Id].AskAt],
            SellPrice = i.SellPrice,
            Barcode = i.Barcode,
            BrandBarcode = i.BrandBarcode,
            LowStockThreshold = i.LowStockThreshold
        }).ToList();

        return new(pageItems, totalCount, page, pageSize);
    }

    public static List<WarehouseUnitsDto> ToDtos(StockView view)
        => [.. view.Warehouses.Select(w => new WarehouseUnitsDto { WarehouseId = w.WarehouseId, Name = w.Name, Quantity = w.Quantity })];
}
