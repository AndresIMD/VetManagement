using System.Net.Http.Json;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Configuration;
using VetManagement.Shared.Models.DTOs;
using VetManagement.Shared.Models.Inventory;

namespace VetManagement.Shared.Services.Api;

public class InventoryMovementApiService(HttpClient http)
{
    public async Task<List<InventoryMovement>?> GetAllMovementsAsync()
    {
        var response = await http.GetAsync(ApiRouteConstants.INVENTORY_MOVEMENTS_BASE);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryMovement>>(ItemJsonOptions.GetPolymorphicOptions());
    }

    public async Task<List<InventoryMovement>> GetMovementsByItemIdAsync(int itemId)
    {
        var response = await http.GetAsync(string.Format(ApiRouteConstants.INVENTORY_MOVEMENT_BY_ITEM, itemId));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryMovement>>(ItemJsonOptions.GetPolymorphicOptions()) ?? [];
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
        return await response.Content.ReadFromJsonAsync<List<InventoryMovement>>(ItemJsonOptions.GetPolymorphicOptions()) ?? [];
    }


    public async Task AddInventoryMovementAsync(InventoryMovement movement)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_MOVEMENTS_BASE, movement);
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
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_MASS_INGRESS, items);
        response.EnsureSuccessStatusCode();
    }

    public async Task MassiveExitAsync(List<InventoryMassUpdateDTO> items)
    {
        var response = await http.PostAsJsonAsync(ApiRouteConstants.INVENTORY_MASS_EGRESS, items);
        response.EnsureSuccessStatusCode();
    }
}
