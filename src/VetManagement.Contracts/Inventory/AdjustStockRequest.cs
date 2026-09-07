using System.ComponentModel.DataAnnotations;

namespace VetManagement.Contracts.Inventory;

/// <summary>
/// Request to record a single stock adjustment. The adjustment is always persisted as a movement.
/// </summary>
public sealed class AdjustStockRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    [Range(int.MinValue, int.MaxValue)]
    public int Amount { get; init; }

    [Required, MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
