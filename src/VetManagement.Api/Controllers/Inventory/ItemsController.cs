using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Models.DTOs;

namespace VetManagement.Api.Controllers.Inventory;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Inventory.Read")]
public class ItemsController(ItemService itemService, InventoryQueryService queryService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Item>>> GetAllItemsAsync()
    {
        var items = await itemService.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Item>> GetItemByIdAsync(int id)
    {
        var item = await itemService.GetByIdAsync(id);
        if (item == null)
            return NotFound();
        return Ok(item);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<Item>> GetItemByBarcodeAsync(string barcode)
    {
        var item = await itemService.GetByBarcodeAsync(barcode);
        if (item == null)
            return NotFound();
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "Inventory.Create")]
    public async Task<IActionResult> AddItemAsync([FromBody] Item item)
    {
        if (item == null)
            return BadRequest("Item cannot be null.");

        var result = await itemService.AddAsync(item, GetUserName());
        if (!result)
            return BadRequest("Invalid item data.");

        return Ok(item);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Inventory.Update")]
    public async Task<IActionResult> EditItemAsync(int id, [FromBody] Item item)
    {
        if (item == null)
            return BadRequest("Item cannot be null.");

        var oldData = await itemService.GetByIdAsNoTrackingAsync(id);
        if (oldData == null)
            return NotFound();

        item.Id = id;
        item.Type = oldData.Type;
        item.Stock = oldData.Stock;

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
    public async Task<ActionResult<PagedResult<InventoryItemDTO>>> GetItemsPagedAsync(
        [FromQuery] string? search = null,
        [FromQuery] ItemType? type = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] StockAlertFilter? alertFilter = null,
        [FromQuery] ItemSortField? sortBy = null,
        [FromQuery] SortDirection sortDirection = SortDirection.Ascending)
    {
        var result = await queryService.GetItemsPagedAsync(search, type, page, pageSize, alertFilter, sortBy, sortDirection);
        return Ok(result);
    }
}
