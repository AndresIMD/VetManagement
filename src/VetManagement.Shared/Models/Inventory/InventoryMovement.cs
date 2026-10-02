using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Shared.Models.Inventory;

/// <summary>
/// Records a single change in the stock of an inventory item.
/// This is used for auditing and tracking all additions and subtractions.
/// </summary>
public class InventoryMovement
{
    public int Id { get; set; }

    [Required]
    public int ItemId { get; set; }

    [ForeignKey(nameof(ItemId))]
    public Item Item { get; set; } = null!;

    [MaxLength(100)]
    public string Responsible { get; set; } = "System";

    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The number of units moved. Positive for additions, negative for subtractions.
    /// </summary>
    [Required]
    public int Quantity { get; set; }

    /// <summary>
    /// The type of movement (Ingress, Egress, Adjustment).
    /// </summary>
    [Required]
    public InventoryMovementType Type { get; set; }

    /// <summary>
    /// An optional reason for the movement, especially for manual adjustments.
    /// </summary>
    [MaxLength(200)]
    public string? Reason { get; set; }
}
