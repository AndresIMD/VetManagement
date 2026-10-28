using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Exams;

/// <summary>
/// Lab exam offered by the clinic (catalog entry). Domain entity — no validation or UI attributes.
/// </summary>
public class Exam : Entity<int>
{
    public Exam() : base(0) { }

    public string Name { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Machine { get; set; } = string.Empty;

    public SampleType SampleType { get; set; }

    public SampleContainer SampleContainer { get; set; }

    public int BuyPrice { get; set; }

    public int SellPrice { get; set; }

    /// <summary>Inventory product one exam uses up (e.g. its test kit), which its reports can deduct; null = none.</summary>
    public int? ItemId { get; set; }

    /// <summary>Units of <see cref="ItemId"/> one exam uses.</summary>
    public int ItemQuantity { get; set; } = 1;

    /// <summary>
    /// A product of its own for some species (e.g. the feline pancreatic lipase kit), used by their reports instead of
    /// <see cref="ItemId"/>; at most one per species.
    /// </summary>
    public List<ExamSpeciesItem> SpeciesItems { get; set; } = [];

    /// <summary>What a report of <paramref name="species"/> takes from inventory: its species' own product, or else the exam's; null = nothing.</summary>
    public (int ItemId, int Quantity)? ItemFor(Species species)
        => SpeciesItems.FirstOrDefault(s => s.Species == species) is { } own ? (own.ItemId, own.Quantity)
            : ItemId is int itemId ? (itemId, ItemQuantity) : null;
}

/// <summary>An exam's own inventory product for one species, kept like <see cref="Exam.ItemId"/>: by id, without a key to the item.</summary>
public class ExamSpeciesItem
{
    public Species Species { get; set; }

    public int ItemId { get; set; }

    /// <summary>Units one exam of this species uses.</summary>
    public int Quantity { get; set; } = 1;
}
