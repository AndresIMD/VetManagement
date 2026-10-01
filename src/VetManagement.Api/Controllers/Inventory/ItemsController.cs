using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Inventory;
using DomainItem = VetManagement.Domain.Inventory.Item;
using DomainItemSortField = VetManagement.Domain.Enums.ItemSortField;
using DomainItemType = VetManagement.Domain.Enums.ItemType;
using DomainSortDirection = VetManagement.Domain.Enums.SortDirection;
using DomainStockAlertFilter = VetManagement.Domain.Enums.StockAlertFilter;

namespace VetManagement.Api.Controllers.Inventory;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Inventory.Read")]
public class ItemsController(ItemService itemService, InventoryQueryService queryService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ItemDto>>> GetAllItemsAsync()
    {
        var items = await itemService.GetAllAsync();
        return Ok(items.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ItemDto>> GetItemByIdAsync(int id)
    {
        var item = await itemService.GetByIdAsync(id);
        if (item == null)
            return NotFound();
        return Ok(MapToDto(item));
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<ItemDto>> GetItemByBarcodeAsync(string barcode)
    {
        var item = await itemService.GetByBarcodeAsync(barcode);
        if (item == null)
            return NotFound();
        return Ok(MapToDto(item));
    }

    [HttpPost]
    [Authorize(Policy = "Inventory.Create")]
    public async Task<IActionResult> AddItemAsync([FromBody] ItemCreateRequest request)
    {
        var item = MapToDomain(request);

        var result = await itemService.AddAsync(item, GetUserName());
        if (!result)
            return BadRequest("Invalid item data.");

        return Ok(MapToDto(item));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Inventory.Update")]
    public async Task<IActionResult> EditItemAsync(int id, [FromBody] ItemUpdateRequest request)
    {
        var oldData = await itemService.GetByIdAsNoTrackingAsync(id);
        if (oldData == null)
            return NotFound();

        var item = MapToDomain(request, oldData);

        var result = await itemService.UpdateAsync(item, oldData, GetUserName());
        if (!result)
            return BadRequest("Invalid update data.");

        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Inventory.Delete")]
    public async Task<IActionResult> DeleteItemAsync(int id)
    {
        var result = await itemService.DeleteAsync(id, GetUserName());
        if (!result)
            return NotFound();

        return Ok();
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResponse<InventoryItemDto>>> GetItemsPagedAsync(
        [FromQuery] string? search = null,
        [FromQuery] DomainItemType? type = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] DomainStockAlertFilter? alertFilter = null,
        [FromQuery] DomainItemSortField? sortBy = null,
        [FromQuery] DomainSortDirection sortDirection = DomainSortDirection.Ascending)
    {
        var result = await queryService.GetItemsPagedAsync(search, type, page, pageSize, alertFilter, sortBy, sortDirection);
        return Ok(result);
    }

    private static ItemDto MapToDto(DomainItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Type = item.Type,
        Brand = item.Brand,
        Description = item.Description,
        Stock = item.Stock,
        SellPrice = item.SellPrice,
        BuyPrice = item.BuyPrice,
        Barcode = item.Barcode,
        BrandBarcode = item.BrandBarcode,
        LowStockThreshold = item.LowStockThreshold,
        Compound = item is Drug drug ? drug.Compound : null,
        ML = item is Drug drugItem ? drugItem.ML : 0,
        Concentration = item is Drug drugDetails ? drugDetails.Concentration : 0,
        DosageDogMin = item is Drug dogDrug ? dogDrug.DosageDog.Min : 0,
        DosageDogMax = item is Drug dogDrugMax ? dogDrugMax.DosageDog.Max : 0,
        DosageCatMin = item is Drug catDrug ? catDrug.DosageCat.Min : 0,
        DosageCatMax = item is Drug catDrugMax ? catDrugMax.DosageCat.Max : 0
    };

    private static DomainItem MapToDomain(ItemCreateRequest request)
        => request.Type == DomainItemType.Drug
            ? new Drug(
                request.Type,
                request.Name,
                request.Barcode,
                request.Description,
                request.Compound ?? string.Empty,
                request.ML,
                request.Concentration,
                new((float)request.DosageDogMin, (float)request.DosageDogMax),
                new((float)request.DosageCatMin, (float)request.DosageCatMax),
                brand: request.Brand,
                brandBarcode: request.BrandBarcode)
            {
                BuyPrice = request.BuyPrice,
                LowStockThreshold = request.LowStockThreshold,
                SellPrice = request.SellPrice
            }
            : new DomainItem(
                request.Name,
                request.Type,
                request.Barcode,
                request.Description,
                0,
                request.SellPrice,
                request.Brand,
                brandBarcode: request.BrandBarcode,
                lowStockThreshold: request.LowStockThreshold,
                buyPrice: request.BuyPrice);

    private static DomainItem MapToDomain(ItemUpdateRequest request, DomainItem existing)
    {
        var item = request.Compound is not null && existing is Drug
            ? MapToDomain(new ItemCreateRequest
            {
                Name = request.Name,
                Type = existing.Type,
                Brand = request.Brand,
                Description = request.Description,
                SellPrice = request.SellPrice,
                BuyPrice = request.BuyPrice,
                Barcode = request.Barcode,
                BrandBarcode = request.BrandBarcode,
                LowStockThreshold = request.LowStockThreshold,
                Compound = request.Compound,
                ML = request.ML,
                Concentration = request.Concentration,
                DosageDogMin = request.DosageDogMin,
                DosageDogMax = request.DosageDogMax,
                DosageCatMin = request.DosageCatMin,
                DosageCatMax = request.DosageCatMax
            })
            : new DomainItem(
                request.Name,
                existing.Type,
                request.Barcode,
                request.Description,
                existing.Stock,
                request.SellPrice,
                request.Brand,
                existing.Id,
                request.BrandBarcode,
                request.LowStockThreshold,
                request.BuyPrice);

        item.Id = existing.Id;
        item.Stock = existing.Stock;
        return item;
    }
}
