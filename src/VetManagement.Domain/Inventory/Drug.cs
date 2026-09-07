using VetManagement.Domain.Enums;

namespace VetManagement.Domain.Inventory;

/// <summary>
/// Drug inventory item. Domain entity.
/// </summary>
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

    public string Compound { get; set; } = string.Empty;

    public float ML { get; set; }

    public float Concentration { get; set; }

    public DosageRange DosageDog { get; set; } = new(0, 0);

    public DosageRange DosageCat { get; set; } = new(0, 0);
}