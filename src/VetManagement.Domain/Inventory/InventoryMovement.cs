using VetManagement.Domain.Enums;

namespace VetManagement.Domain.Inventory;

/// <summary>
/// Records a single change in the stock of an inventory item.
/// This is used for auditing and tracking all additions and subtractions.
/// </summary>
public class InventoryMovement
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public string Responsible { get; set; } = "System";

    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The number of units moved. Positive for additions, negative for subtractions.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// The type of movement (Ingress, Egress, Adjustment, ...).
    /// </summary>
    public InventoryMovementType Type { get; set; }

    /// <summary>
    /// An optional reason for the movement, especially for manual adjustments.
    /// </summary>
    public string? Reason { get; set; }
}