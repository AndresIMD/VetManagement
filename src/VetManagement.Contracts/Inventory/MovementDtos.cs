using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Inventory;

/// <summary>
/// Request to record a new stock movement.
/// </summary>
public sealed class MovementCreateRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    [Required]
    public InventoryMovementType Type { get; init; }

    public int Quantity { get; init; }

    [MaxLength(200)]
    public string? Reason { get; init; }
}

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
}