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

    public async Task AdjustStockAsync(int itemId, int amount, string reason)
    {
        var payload = new
        {
            ItemId = itemId,
            Amount = amount,
            Reason = reason,
        };
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_ADJUST_STOCK, payload);
        response.EnsureSuccessStatusCode();
    }

    public async Task MassiveEntryAsync(List<InventoryMassUpdateDTO> items)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_MASS_INGRESS, items.Select(MapToContract).ToList());
        response.EnsureSuccessStatusCode();
    }

    public async Task MassiveExitAsync(List<InventoryMassUpdateDTO> items)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_MASS_EGRESS, items.Select(MapToContract).ToList());
        response.EnsureSuccessStatusCode();
    }

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
        Reason = movement.Reason
    };

    private static MassStockUpdateRequest MapToContract(InventoryMassUpdateDTO item) => new()
    {
        ItemId = item.ItemId,
        Name = item.Name,
        Barcode = item.Barcode,
        Quantity = item.Quantity
    };
}
