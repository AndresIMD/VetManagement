using VetManagement.Application.Contracts.Persistence;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Inventory;
using DomainItemSortField = VetManagement.Domain.Enums.ItemSortField;
using DomainItemType = VetManagement.Domain.Enums.ItemType;
using DomainSortDirection = VetManagement.Domain.Enums.SortDirection;
using DomainStockAlertFilter = VetManagement.Domain.Enums.StockAlertFilter;

namespace VetManagement.Application.Services;

public class InventoryQueryService(IUnitOfWork unitOfWork)
{
    public async Task<PagedResponse<InventoryItemDto>> GetItemsPagedAsync(
        string? search,
        DomainItemType? type,
        int page,
        int pageSize,
        DomainStockAlertFilter? alertFilter = null,
        DomainItemSortField? sortBy = null,
        DomainSortDirection sortDirection = DomainSortDirection.Ascending)
    {
        var (items, totalCount) = await unitOfWork.Items.GetPagedListAsync(search, type, page, pageSize, alertFilter, sortBy, sortDirection);

        var pageItems = items.Select(i => new InventoryItemDto
        {
            Id = i.Id,
            Name = i.Name,
            Brand = i.Brand,
            Compound = i is Drug drug ? drug.Compound : null,
            Type = i.Type,
            Stock = i.Stock,
            SellPrice = i.SellPrice,
            Barcode = i.Barcode,
            BrandBarcode = i.BrandBarcode,
            LowStockThreshold = i.LowStockThreshold
        }).ToList();

        return new(pageItems, totalCount, page, pageSize);
    }
}
