using VetManagement.Domain.Enums;

namespace VetManagement.Staff.UI.Models.DTOs;

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

