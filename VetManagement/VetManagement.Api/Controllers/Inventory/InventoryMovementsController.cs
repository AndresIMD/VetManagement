using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
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
    public async Task<IActionResult> AdjustStockAsync([FromBody] InventoryAdjustStockDTO request)
    {
        var result = await movementService.AdjustStockAsync(request.ItemId, request.Amount, request.Reason, GetUserName());
        if (!result)
            return BadRequest("Could not adjust stock.");
        return Ok();
    }

    [HttpPost("mass-ingress")]
    [Authorize(Policy = "Inventory.UPDATE")]
    public async Task<IActionResult> MassIngressAsync([FromBody] List<InventoryMassUpdateDTO> items)
    {
        var updated = await movementService.MassStockUpdateAsync(items, true, GetUserName());
        if (updated == 0)
            return NotFound("No items updated.");
        return Ok(new { updated });
    }

    [HttpPost("mass-egress")]
    [Authorize(Policy = "Inventory.UPDATE")]
    public async Task<IActionResult> MassEgressAsync([FromBody] List<InventoryMassUpdateDTO> items)
    {
        var updated = await movementService.MassStockUpdateAsync(items, false, GetUserName());
        if (updated == 0)
            return NotFound("No items updated.");
        return Ok(new { updated });
    }
}
