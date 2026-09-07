using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Inventory;

/// <summary>
/// Inventory item DTO for list/table display.
/// </summary>
public sealed class InventoryItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? Compound { get; set; }

    public ItemType Type { get; set; }

    public int Stock { get; set; }

    public int SellPrice { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string? BrandBarcode { get; set; }

    public int LowStockThreshold { get; set; } = 5;
}

/// <summary>
/// Request to create a new inventory item.
/// </summary>
public sealed class ItemCreateRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public ItemType Type { get; init; }

    [MaxLength(200)]
    public string? Brand { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Range(0, int.MaxValue)]
    public int SellPrice { get; init; }

    [Range(0, int.MaxValue)]
    public int BuyPrice { get; init; }

    [Required, MaxLength(100)]
    public string Barcode { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? BrandBarcode { get; init; }

    [Range(0, int.MaxValue)]
    public int LowStockThreshold { get; init; } = 5;

    // Drug-specific properties (only used when Type == Drug)
    [MaxLength(500)]
    public string? Compound { get; init; }

    [Range(0, float.MaxValue)]
    public float ML { get; init; }

    [Range(0, 100)]
    public float Concentration { get; init; }

    public double DosageDogMin { get; init; }

    public double DosageDogMax { get; init; }

    public double DosageCatMin { get; init; }

    public double DosageCatMax { get; init; }
}

/// <summary>
/// Request to update an inventory item. Stock is deliberately absent —
/// stock changes must go through inventory movements.
/// </summary>
public sealed class ItemUpdateRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? Brand { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Range(0, int.MaxValue)]
    public int SellPrice { get; init; }

    [Range(0, int.MaxValue)]
    public int BuyPrice { get; init; }

    [Required, MaxLength(100)]
    public string Barcode { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? BrandBarcode { get; init; }

    [Range(0, int.MaxValue)]
    public int LowStockThreshold { get; init; } = 5;

    // Drug-specific properties (only used when the item Type is Drug)
    [MaxLength(500)]
    public string? Compound { get; init; }

    [Range(0, float.MaxValue)]
    public float ML { get; init; }

    [Range(0, 100)]
    public float Concentration { get; init; }

    public double DosageDogMin { get; init; }

    public double DosageDogMax { get; init; }

    public double DosageCatMin { get; init; }

    public double DosageCatMax { get; init; }
}

/// <summary>
/// Full item DTO including drug-specific properties.
/// </summary>
public sealed class ItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ItemType Type { get; set; }

    public string? Brand { get; set; }

    public string? Description { get; set; }

    public int Stock { get; set; }

    public int SellPrice { get; set; }

    public int BuyPrice { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string? BrandBarcode { get; set; }

    public int LowStockThreshold { get; set; } = 5;

    // Drug-specific
    public string? Compound { get; set; }

    public float ML { get; set; }

    public float Concentration { get; set; }

    public double DosageDogMin { get; set; }

    public double DosageDogMax { get; set; }

    public double DosageCatMin { get; set; }

    public double DosageCatMax { get; set; }
}