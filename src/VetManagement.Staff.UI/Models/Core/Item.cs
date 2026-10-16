using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using VetManagement.Staff.UI.Constants;
using VetManagement.Domain.Enums;

namespace VetManagement.Staff.UI.Models.Core;

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

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public ItemType Type { get; set; }

    public string? Brand { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    public int Stock { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    [Range(0, int.MaxValue, ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.SellPriceNotNegative))]
    public int SellPrice { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    [Range(0, int.MaxValue, ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.BuyPriceNotNegative))]
    public int BuyPrice { get; set; }

    [Required(ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.Required))]
    [MaxLength(100)]
    public string Barcode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? BrandBarcode { get; set; }

    [Range(0, int.MaxValue, ErrorMessageResourceType = typeof(VetManagement.Staff.UI.Resources.ValidationTexts), ErrorMessageResourceName = nameof(VetManagement.Staff.UI.Resources.ValidationTexts.LowStockThresholdNotNegative))]
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
