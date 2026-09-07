using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Contracts.Inventory;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.DTOs;
using VetManagement.Shared.Models.Inventory;

namespace VetManagement.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/movements")]
[Authorize(Policy = "Inventory.READ")]
public class InventoryMovementsController(InventoryMovementService movementService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMovementsFilteredAsync(
        [FromQuery] string? itemName = null,
        [FromQuery] InventoryMovementType? type = null,
        [FromQuery] string? responsible = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var movements = await movementService.GetMovementsFilteredAsync(itemName, type, responsible, from, to);
        return Ok(movements);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetMovementsFilteredPagedAsync(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] int? itemId = null,
        [FromQuery] InventoryMovementType? type = null,
        [FromQuery] string? responsible = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await movementService.GetMovementsFilteredPagedAsync(page, pageSize, itemId, type, responsible, from, to);
        return Ok(result);
    }

    [HttpGet("{itemId}")]
    public async Task<IActionResult> GetMovementsByItemIdAsync(int itemId)
    {
        var movements = await movementService.GetMovementsByItemIdAsync(itemId);
        return Ok(movements);
    }

    [HttpPost]
    [Authorize(Policy = "Inventory.UPDATE")]
    public async Task<IActionResult> AddMovementAsync([FromBody] InventoryMovement movement)
    {
        await movementService.AddMovementAsync(movement, GetUserName());
        return Ok();
    }

    [HttpPost("adjust-stock")]
    [Authorize(Policy = "Inventory.UPDATE")]
    public async Task<IActionResult> AdjustStockAsync([FromBody] AdjustStockRequest request)
    {
        var result = await movementService.AdjustStockAsync(request.ItemId, request.Amount, request.Reason, GetUserName());
        if (!result)
            return BadRequest("Could not adjust stock.");
        return Ok();
    }

    [HttpPost("mass-ingress")]
    [Authorize(Policy = "Inventory.UPDATE")]
    public async Task<IActionResult> MassIngressAsync([FromBody] List<MassStockUpdateRequest> items)
    {
        var updated = await movementService.MassStockUpdateAsync(items.Select(MapToLegacyDto).ToList(), true, GetUserName());
        if (updated == 0)
            return NotFound("No items updated.");
        return Ok(new { updated });
    }

    [HttpPost("mass-egress")]
    [Authorize(Policy = "Inventory.UPDATE")]
    public async Task<IActionResult> MassEgressAsync([FromBody] List<MassStockUpdateRequest> items)
    {
        var updated = await movementService.MassStockUpdateAsync(items.Select(MapToLegacyDto).ToList(), false, GetUserName());
        if (updated == 0)
            return NotFound("No items updated.");
        return Ok(new { updated });
    }

    private static InventoryMassUpdateDTO MapToLegacyDto(MassStockUpdateRequest request) => new()
    {
        ItemId = request.ItemId,
        Name = request.Name,
        Barcode = request.Barcode,
        Quantity = request.Quantity
    };
}
