using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Inventory;

/// <summary>
/// Movement DTO for list/history display.
/// </summary>
public sealed class MovementDto
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public InventoryMovementType Type { get; set; }

    public int Quantity { get; set; }

    public DateTime Date { get; set; }

    public string Responsible { get; set; } = "System";

    public string? Reason { get; set; }

    /// <summary>The warehouse whose stock moved (the daily-use one's id for the movements that have none).</summary>
    public int WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;
}
