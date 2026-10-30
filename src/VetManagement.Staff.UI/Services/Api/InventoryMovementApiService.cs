using System.Net.Http.Json;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using VetManagement.Staff.UI.Constants;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Models.Configuration;
using VetManagement.Staff.UI.Models.DTOs;
using VetManagement.Staff.UI.Models.Inventory;
using DomainInventoryMovementType = VetManagement.Domain.Enums.InventoryMovementType;

namespace VetManagement.Staff.UI.Services.Api;

public class InventoryMovementApiService(HttpClient http)
{
    /// <summary>The stock flow of a period (clinic-local dates), or the API's message (e.g. range too long).</summary>
    public async Task<(InventoryMetricsDto? Metrics, string? Error)> GetMetricsAsync(DateOnly from, DateOnly to, IEnumerable<int> itemIds,
        CancellationToken cancellationToken = default)
    {
        var query = $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" + string.Concat(itemIds.Select(id => $"&itemIds={id}"));
        var response = await http.GetAsync($"{ApiRouteConstants.INVENTORY_METRICS}?{query}", cancellationToken);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<InventoryMetricsDto>(cancellationToken), null);
        return (null, (await ApiResult.FromResponseAsync(response)).Error);
    }

    public async Task<List<InventoryMovement>> GetMovementsByItemIdAsync(int itemId)
    {
        var response = await http.GetAsync(string.Format(ApiRouteConstants.INVENTORY_MOVEMENT_BY_ITEM, itemId));
        response.EnsureSuccessStatusCode();
        var movements = await response.Content.ReadFromJsonAsync<List<MovementDto>>();
        return movements?.Select(MapToShared).ToList() ?? [];
    }

    /// <summary>Adds or takes units in a warehouse (the daily-use one when none is given).</summary>
    public async Task<ApiResult> AdjustStockAsync(int itemId, int amount, string? reason, int? warehouseId = null)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_ADJUST_STOCK,
            new AdjustStockRequest { ItemId = itemId, Amount = amount, Reason = reason, WarehouseId = warehouseId }));

    public async Task<ApiResult> MassiveEntryAsync(List<InventoryMassUpdateDTO> items, int? warehouseId = null)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(InWarehouse(ApiRouteConstants.INVENTORY_MASS_INGRESS, warehouseId),
            items.Select(MapToContract).ToList()));

    public async Task<ApiResult> MassiveExitAsync(List<InventoryMassUpdateDTO> items, int? warehouseId = null)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(InWarehouse(ApiRouteConstants.INVENTORY_MASS_EGRESS, warehouseId),
            items.Select(MapToContract).ToList()));

    /// <summary>Moves units of an item between two warehouses; refused when the first doesn't hold them.</summary>
    public async Task<ApiResult> TransferAsync(int itemId, int fromWarehouseId, int toWarehouseId, int quantity, string? reason)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_TRANSFER, new TransferStockRequest
        {
            ItemId = itemId, FromWarehouseId = fromWarehouseId, ToWarehouseId = toWarehouseId, Quantity = quantity, Reason = reason
        }));

    /// <summary>Moves several items between two warehouses; each moves at most what the first one holds.</summary>
    public async Task<ApiResult> MassTransferAsync(List<InventoryMassUpdateDTO> items, int fromWarehouseId, int toWarehouseId)
        => await ApiResult.FromResponseAsync(await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_MASS_TRANSFER, new MassTransferRequest
        {
            FromWarehouseId = fromWarehouseId, ToWarehouseId = toWarehouseId, Items = [.. items.Select(MapToContract)]
        }));

    private static string InWarehouse(string route, int? warehouseId) => warehouseId is int id ? $"{route}?warehouseId={id}" : route;

    private static InventoryMovement MapToShared(MovementDto movement) => new()
    {
        Id = movement.Id,
        ItemId = movement.ItemId,
        Item = new()
        {
            Id = movement.ItemId,
            Name = movement.ItemName
        },
        Type = (InventoryMovementType)movement.Type,
        Quantity = movement.Quantity,
        Date = movement.Date,
        Responsible = movement.Responsible,
        Reason = movement.Reason,
        WarehouseId = movement.WarehouseId,
        WarehouseName = movement.WarehouseName
    };

    private static MassStockUpdateRequest MapToContract(InventoryMassUpdateDTO item) => new()
    {
        ItemId = item.ItemId,
        Name = item.Name,
        Barcode = item.Barcode,
        Quantity = item.Quantity
    };
}
