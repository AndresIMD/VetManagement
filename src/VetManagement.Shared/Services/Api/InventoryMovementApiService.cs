using System.Net.Http.Json;
using VetManagement.Contracts.Common;
using VetManagement.Contracts.Inventory;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Configuration;
using VetManagement.Shared.Models.DTOs;
using VetManagement.Shared.Models.Inventory;
using DomainInventoryMovementType = VetManagement.Domain.Enums.InventoryMovementType;

namespace VetManagement.Shared.Services.Api;

public class InventoryMovementApiService(HttpClient http)
{
    public async Task<List<InventoryMovement>?> GetAllMovementsAsync()
    {
        var response = await http.GetAsync(ApiRouteConstants.INVENTORY_MOVEMENTS_BASE);
        response.EnsureSuccessStatusCode();
        var movements = await response.Content.ReadFromJsonAsync<List<MovementDto>>();
        return movements?.Select(MapToShared).ToList();
    }

    public async Task<List<InventoryMovement>> GetMovementsByItemIdAsync(int itemId)
    {
        var response = await http.GetAsync(string.Format(ApiRouteConstants.INVENTORY_MOVEMENT_BY_ITEM, itemId));
        response.EnsureSuccessStatusCode();
        var movements = await response.Content.ReadFromJsonAsync<List<MovementDto>>();
        return movements?.Select(MapToShared).ToList() ?? [];
    }

    public async Task<List<InventoryMovement>> GetMovementsFilteredAsync(
    int? itemId = null,
    string? itemName = null,
    InventoryMovementType? type = null,
    string? responsible = null,
    DateTime? from = null,
    DateTime? to = null)
    {
        var parameters = new List<string>();
        if (itemId.HasValue)
            parameters.Add($"itemId={itemId.Value}");
        if (!string.IsNullOrWhiteSpace(itemName))
            parameters.Add($"itemName={Uri.EscapeDataString(itemName)}");
        if (type.HasValue && type.Value != InventoryMovementType.None)
            parameters.Add($"type={type}");
        if (!string.IsNullOrWhiteSpace(responsible))
            parameters.Add($"responsible={Uri.EscapeDataString(responsible)}");
        if (from.HasValue)
            parameters.Add($"from={from.Value:O}");
        if (to.HasValue)
            parameters.Add($"to={to.Value:O}");
        string url = ApiRouteConstants.INVENTORY_MOVEMENTS_BASE;
        if (parameters.Count > 0)
            url += "?" + string.Join("&", parameters);

        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var movements = await response.Content.ReadFromJsonAsync<List<MovementDto>>();
        return movements?.Select(MapToShared).ToList() ?? [];
    }

    public async Task<PagedResult<InventoryMovement>?> GetMovementsFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? itemId = null,
        InventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var parameters = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (itemId.HasValue)
            parameters.Add($"itemId={itemId.Value}");
        if (type.HasValue && type.Value != InventoryMovementType.None)
            parameters.Add($"type={type}");
        if (!string.IsNullOrWhiteSpace(responsible))
            parameters.Add($"responsible={Uri.EscapeDataString(responsible)}");
        if (from.HasValue)
            parameters.Add($"from={from.Value:O}");
        if (to.HasValue)
            parameters.Add($"to={to.Value:O}");

        string url = $"{ApiRouteConstants.INVENTORY_MOVEMENTS_BASE}/paged?" + string.Join("&", parameters);

        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResponse<MovementDto>>();
        return result is null
            ? null
            : new PagedResult<InventoryMovement>
            {
                Items = result.Items.Select(MapToShared).ToList(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
    }

    public async Task AddInventoryMovementAsync(InventoryMovement movement)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_MOVEMENTS_BASE, new MovementCreateRequest
        {
            ItemId = movement.ItemId,
            Type = (DomainInventoryMovementType)movement.Type,
            Quantity = movement.Quantity,
            Reason = movement.Reason
        });
        response.EnsureSuccessStatusCode();
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
