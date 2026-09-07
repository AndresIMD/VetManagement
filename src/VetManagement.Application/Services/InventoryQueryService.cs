using VetManagement.Application.Contracts.Persistence;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Models.DTOs;

namespace VetManagement.Application.Services;

public class InventoryQueryService(IUnitOfWork unitOfWork)
{
    public async Task<PagedResult<InventoryItemDTO>> GetItemsPagedAsync(
        string? search,
        ItemType? type,
        int page,
        int pageSize,
        StockAlertFilter? alertFilter = null,
        ItemSortField? sortBy = null,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        var (items, totalCount) = await unitOfWork.Items.GetPagedListAsync(search, type, page, pageSize, alertFilter, sortBy, sortDirection);

        var pageItems = items.Select(i => new InventoryItemDTO
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

        return new()
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
