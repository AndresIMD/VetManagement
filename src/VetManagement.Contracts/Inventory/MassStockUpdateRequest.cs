using System.ComponentModel.DataAnnotations;

namespace VetManagement.Contracts.Inventory;

/// <summary>
/// One item in a bulk ingress or egress operation.
/// </summary>
public sealed class MassStockUpdateRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string Barcode { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
