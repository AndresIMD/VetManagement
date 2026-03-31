using System.ComponentModel.DataAnnotations;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Enums;

namespace VetManagement.Shared.Models.Core;

public class Drug : Item
{
    public Drug() : base() { }

    public Drug(
        ItemType type,
        string name,
        string barcode,
        string? description,
        string compound,
        float ml,
        float concentration,
        DosageRange? dosageDog,
        DosageRange? dosageCat,
        int stock = 0,
        int sellPrice = 0,
        string? brand = "Unknown",
        string? brandBarcode = null)
        : base(name, type, barcode, description, stock, sellPrice, brand, brandBarcode: brandBarcode)
    {
        Compound = compound;
        ML = ml;
        Concentration = concentration;
        DosageDog = dosageDog ?? new DosageRange(0, 0);
        DosageCat = dosageCat ?? new DosageRange(0, 0);
    }

    [Required(ErrorMessage = GenericConstants.REQUIRED_FIELD_ERROR)]
    public string Compound { get; set; } = string.Empty;

    [Range(0, float.MaxValue, ErrorMessage = "The amount (mL) must be 0 or greater.")]
    public float ML { get; set; }

    [Range(0, 100.0, ErrorMessage = "Concentration must be between 0 and 100.")]
    public float Concentration { get; set; }

    public DosageRange DosageDog { get; set; } = new(0, 0);

    public DosageRange DosageCat { get; set; } = new(0, 0);

    public static new Drug GetEmpty()
    {
        return new Drug(
            type: ItemType.Drug,
            name: string.Empty,
            barcode: "00000",
            description: null,
            compound: string.Empty,
            ml: 0.0f,
            concentration: 0.0f,
            dosageDog: null,
            dosageCat: null,
            stock: 0,
            sellPrice: 0,
            brand: "Unknown",
            brandBarcode: null);
    }
}
