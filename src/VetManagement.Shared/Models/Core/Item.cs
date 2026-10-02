using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using VetManagement.Shared.Constants;
using VetManagement.Domain.Enums;

namespace VetManagement.Shared.Models.Core;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Drug))]
public class Item
{
    public Item() { }
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
    {
        Id = id;
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

    public int Id { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public ItemType Type { get; set; }

    public string? Brand { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public int Stock { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    [Range(0, int.MaxValue, ErrorMessage = "The selling price must be 0 or greater.")]
    public int SellPrice { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    [Range(0, int.MaxValue, ErrorMessage = "The purchase price must be 0 or greater.")]
    public int BuyPrice { get; set; }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    [MaxLength(100)]
    public string Barcode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? BrandBarcode { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "The low stock threshold must be 0 or greater.")]
    public int LowStockThreshold { get; set; } = 5;

    public static Item GetEmpty()
    {
        return new Item(
            name: string.Empty,
            type: ItemType.Material,
            description: null,
            stock: 0,
            sellPrice: 0,
            brand: "Unknown",
            barcode: "00000",
            brandBarcode: null,
            lowStockThreshold: 5,
            buyPrice: 0);
    }
}
