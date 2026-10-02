using System.Text.Json;
using InventoryReasons = VetManagement.Domain.Inventory.InventoryReasons;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Models.Core;
using VetManagement.Staff.UI.Models.Inventory;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Staff.UI.Commands;

public sealed class EditItemCommand(
    ItemsApiService itemsApi,
    InventoryMovementApiService inventoryMovementsApi,
    AuditApiService auditApi,
    Item originalData,
    Item updatedData,
    string? performedBy = null) : ICommand
{
    public string Description => $"Edit Item: {updatedData.Name}";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string? PerformedBy { get; } = performedBy;

    public async Task ExecuteAsync()
    {
        await itemsApi.UpdateItemAsync(updatedData);

        if (originalData.Stock != updatedData.Stock)
        {
            int difference = updatedData.Stock - originalData.Stock;
            await inventoryMovementsApi.AddInventoryMovementAsync(new InventoryMovement
            {
                ItemId = updatedData.Id,
                Type = difference > 0 ? InventoryMovementType.Ingress : InventoryMovementType.Egress,
                Quantity = Math.Abs(difference),
                Date = Timestamp,
                Reason = InventoryReasons.ITEM_EDIT,
                Responsible = PerformedBy ?? "System"
            });
        }

        await auditApi.LogAsync(
            entityName: "Item",
            entityId: updatedData.Id,
            action: AuditActionType.Edit,
            changes: $"Before: {JsonSerializer.Serialize(originalData)}\nAfter: {JsonSerializer.Serialize(updatedData)}"
        );
    }

    public async Task UndoAsync()
    {
        // Simply reverse the operation by swapping original and updated data.
        var reverseCommand = new EditItemCommand(
            itemsApi,
            inventoryMovementsApi,
            auditApi,
            originalData: updatedData,
            updatedData: originalData,
            performedBy: $"Undo by {PerformedBy ?? "System"}"
        );
        await reverseCommand.ExecuteAsync();
    }
}

