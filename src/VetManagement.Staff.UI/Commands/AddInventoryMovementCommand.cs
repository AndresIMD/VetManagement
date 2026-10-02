using VetManagement.Staff.UI.Models.Inventory;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Staff.UI.Commands;

public sealed class AddInventoryMovementCommand : ICommand
{
    private readonly InventoryMovementApiService _inventoryMovementsApi;
    private readonly InventoryMovement _movement;

    public string Description { get; }
    public DateTime Timestamp { get; }
    public string? PerformedBy { get; }

    public AddInventoryMovementCommand(InventoryMovementApiService inventoryMovementsApi, InventoryMovement movement, string performedBy)
    {
        _inventoryMovementsApi = inventoryMovementsApi;
        _movement = movement;
        PerformedBy = performedBy;
        Timestamp = DateTime.UtcNow;
        Description = $"Add inventory movement: {_movement.Type} of {_movement.Quantity} for Item ID {_movement.ItemId}";
    }

    /// <summary>
    /// Executes the command by setting the timestamp and responsible user, then calling the API.
    /// </summary>
    public async Task ExecuteAsync()
    {
        _movement.Date = Timestamp;
        _movement.Responsible = PerformedBy ?? "System";
        await _inventoryMovementsApi.AddInventoryMovementAsync(_movement);
    }

    // -- Placeholder for undo functionality --
    public Task UndoAsync()
    {
        return Task.CompletedTask;
    }
}
