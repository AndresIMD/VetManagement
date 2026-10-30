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

    /// <summary>Optional: without one the movement reads as a quick adjustment. It keeps up to 200 characters.</summary>
    [MaxLength(200)]
    public string? Reason { get; init; }

    /// <summary>The warehouse adjusted; the daily-use one when missing.</summary>
    public int? WarehouseId { get; init; }
}
