using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Shared.Models.DTOs;

public class InventoryItemDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Brand { get; set; } = "";
    public string? Compound { get; set; } = "";
    public ItemType Type { get; set; } = ItemType.None;
    public int Stock { get; set; }
    public int SellPrice { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? BrandBarcode { get; set; } = string.Empty;
    public int LowStockThreshold { get; set; } = 5;
}

public class InventoryMassUpdateDTO
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class InventoryAdjustStockDTO
{
    public int ItemId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public static class InventoryItemDTOExtensions
{
    /// <summary>
    /// Converts an Item or Drug to InventoryItemDTO.
    /// </summary>
    public static InventoryItemDTO ToInventoryItemDTO(this Item item)
    {
        return new InventoryItemDTO
        {
            Id = item.Id,
            Name = item.Name ?? string.Empty,
            Brand = item.Brand ?? string.Empty,
            Compound = item is Drug drug ? drug.Compound : null,
            Type = item.Type,
            Stock = item.Stock,
            SellPrice = item.SellPrice,
            Barcode = item.Barcode ?? string.Empty,
            BrandBarcode = item.BrandBarcode ?? string.Empty,
            LowStockThreshold = item.LowStockThreshold
        };
    }
}
