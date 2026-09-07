using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Inventory;

/// <summary>
/// Base class for inventory items. Domain entity — no validation or JSON attributes.
/// </summary>
public class Item : Entity<int>
{
    protected Item() : base(0) { }

    public Item(
        string name,
        ItemType type,
        string barcode,
        string? description,
        int stock,
        int sellPrice,
        string? brand = "Unknown",
        int id = 0,
        string? brandBarcode = null,
        int lowStockThreshold = 5,
        int buyPrice = 0)
        : base(id)
    {
        Name = name;
        Type = type;
        Brand = brand;
        Description = description;
        Stock = stock;
        SellPrice = sellPrice;
        BuyPrice = buyPrice;
        Barcode = barcode;
        BrandBarcode = brandBarcode;
        LowStockThreshold = lowStockThreshold;
    }

    public string Name { get; set; } = string.Empty;

    public ItemType Type { get; set; }

    public string? Brand { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Read model maintained exclusively by inventory movements.
    /// A catalog edit must never become an untraceable stock adjustment.
    /// </summary>
    public int Stock { get; set; }

    public int SellPrice { get; set; }

    public int BuyPrice { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string? BrandBarcode { get; set; }

    public int LowStockThreshold { get; set; } = 5;
}