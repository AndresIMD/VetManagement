using System.Text.Json;
using InventoryReasons = VetManagement.Domain.Inventory.InventoryReasons;
using VetManagement.Domain.Enums;
using VetManagement.Staff.UI.Models.Core;
using VetManagement.Staff.UI.Models.Inventory;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Staff.UI.Commands;

public sealed class DeleteItemCommand(
    ItemsApiService itemsApi,
    InventoryMovementApiService inventoryMovementsApi,
    AuditApiService auditApi,
    Item itemToDelete,
    string? performedBy = null) : ICommand
{
    public string Description => $"Delete Item: {itemToDelete.Name}";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public string? PerformedBy { get; } = performedBy;

    public async Task ExecuteAsync()
    {
        await inventoryMovementsApi.AddInventoryMovementAsync(new InventoryMovement
        {
            ItemId = itemToDelete.Id,
            Type = InventoryMovementType.Egress,
            Quantity = itemToDelete.Stock,
            Date = Timestamp,
            Reason = InventoryReasons.ITEM_DELETION,
            Responsible = PerformedBy ?? "System"
        });

        await auditApi.LogAsync(
            entityName: "Item",
            entityId: itemToDelete.Id,
            action: AuditActionType.Delete,
            changes: JsonSerializer.Serialize(itemToDelete)
        );

        await itemsApi.DeleteItemAsync(itemToDelete.Id);
    }

    public async Task UndoAsync()
    {
        var reverseCommand = new AddItemCommand(itemsApi, itemToDelete, $"Undo by {PerformedBy ?? "System"}");
        await reverseCommand.ExecuteAsync();
    }
}

