using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Inventory;
using VetManagement.Application.Services;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using DomainInventoryMovement = VetManagement.Domain.Inventory.InventoryMovement;
using DomainInventoryMovementType = VetManagement.Domain.Enums.InventoryMovementType;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Inventory;

[ApiController]
[Route("api/inventory/movements")]
[Authorize(Policy = "Inventory.Read")]
public class InventoryMovementsController(InventoryMovementService movementService, WarehouseService warehouses) : ApiControllerBase
{
    [HttpGet("paged")]
    public async Task<IActionResult> GetMovementsFilteredPagedAsync(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] int? itemId = null,
        [FromQuery] DomainInventoryMovementType? type = null,
        [FromQuery] string? responsible = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? warehouseId = null)
    {
        (page, pageSize) = Paging(page, pageSize);
        var result = await movementService.GetMovementsFilteredPagedAsync(GetStockScope(), page, pageSize, itemId, type, responsible, from, to, warehouseId);
        var names = await WarehouseNamesAsync();
        return Ok(new PagedResponse<MovementDto>(
            result.Items.Select(m => MapToDto(m, names)).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    [HttpGet("{itemId}")]
    public async Task<IActionResult> GetMovementsByItemIdAsync(int itemId)
    {
        var movements = await movementService.GetMovementsByItemIdAsync(itemId, GetStockScope());
        var names = await WarehouseNamesAsync();
        return Ok(movements.Select(m => MapToDto(m, names)).ToList());
    }

    [HttpPost("adjust-stock")]
    [Authorize(Policy = "Inventory.Update")]
    public async Task<IActionResult> AdjustStockAsync([FromBody] AdjustStockRequest request)
    {
        var result = await movementService.AdjustStockAsync(request.ItemId, request.Amount, request.Reason, GetUserName(), GetStockScope(), request.WarehouseId);
        return result.Status switch
        {
            StockMoveStatus.Done => Ok(),
            StockMoveStatus.Invalid => BadRequestMessage(result.Error!),
            _ => BadRequest(Texts.T("Could not adjust stock."))
        };
    }

    [HttpPost("mass-ingress")]
    [Authorize(Policy = "Inventory.Update")]
    public Task<IActionResult> MassIngressAsync([FromBody] List<MassStockUpdateRequest> items, [FromQuery] int? warehouseId = null)
        => MassUpdateAsync(items, true, warehouseId);

    [HttpPost("mass-egress")]
    [Authorize(Policy = "Inventory.Update")]
    public Task<IActionResult> MassEgressAsync([FromBody] List<MassStockUpdateRequest> items, [FromQuery] int? warehouseId = null)
        => MassUpdateAsync(items, false, warehouseId);

    /// <summary>Moves units of an item between two warehouses the user sees; refused when the first doesn't hold them.</summary>
    [HttpPost("transfer")]
    [Authorize(Policy = "Inventory.Update")]
    public async Task<IActionResult> TransferAsync([FromBody] TransferStockRequest request)
    {
        var result = await movementService.TransferAsync(request.ItemId, request.FromWarehouseId, request.ToWarehouseId, request.Quantity,
            request.Reason, GetUserName(), GetStockScope());
        return result.Status switch
        {
            StockMoveStatus.Done => Ok(),
            StockMoveStatus.NotFound => NotFound(),
            _ => BadRequestMessage(result.Error!)
        };
    }

    /// <summary>Moves several items between two warehouses; each moves at most what the first one holds.</summary>
    [HttpPost("mass-transfer")]
    [Authorize(Policy = "Inventory.Update")]
    public async Task<IActionResult> MassTransferAsync([FromBody] MassTransferRequest request)
    {
        var result = await movementService.MassTransferAsync(request.Items, request.FromWarehouseId, request.ToWarehouseId, request.Reason,
            GetUserName(), GetStockScope());
        if (result.Status != StockMoveStatus.Done)
            return BadRequestMessage(result.Error!);
        if (result.Count == 0)
            return NotFound(Texts.T("No items updated."));
        return Ok(new { updated = result.Count });
    }

    private async Task<IActionResult> MassUpdateAsync(List<MassStockUpdateRequest> items, bool isIngress, int? warehouseId)
    {
        var result = await movementService.MassStockUpdateAsync(items, isIngress, GetUserName(), GetStockScope(), warehouseId);
        if (result.Status != StockMoveStatus.Done)
            return BadRequestMessage(result.Error!);
        if (result.Count == 0)
            return NotFound(Texts.T("No items updated."));
        return Ok(new { updated = result.Count });
    }

    /// <summary>Every warehouse's name by id; the daily-use one's under 0, for the movements without a warehouse.</summary>
    private async Task<Dictionary<int, (int Id, string Name)>> WarehouseNamesAsync()
    {
        var all = await warehouses.ListAsync(StockScope.All);
        var names = all.ToDictionary(w => w.Id, w => (w.Id, w.Name));
        var daily = all.First(w => w.IsDailyUse);
        names[0] = (daily.Id, daily.Name);
        return names;
    }

    private static MovementDto MapToDto(DomainInventoryMovement movement, Dictionary<int, (int Id, string Name)> warehouseNames)
    {
        var warehouse = warehouseNames.TryGetValue(movement.WarehouseId ?? 0, out var found) ? found : warehouseNames[0];
        return new()
        {
            Id = movement.Id,
            ItemId = movement.ItemId,
            ItemName = movement.Item?.Name ?? string.Empty,
            Type = movement.Type,
            Quantity = movement.Quantity,
            Date = movement.Date,
            Responsible = movement.Responsible,
            Reason = movement.Reason,
            WarehouseId = warehouse.Id,
            WarehouseName = warehouse.Name
        };
    }
}
