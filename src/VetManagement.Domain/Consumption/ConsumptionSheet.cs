using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Consumption;

/// <summary>
/// The supplies used on one patient during a stay (e.g. hospitalisation), recorded as they are given and added up at
/// discharge to charge them. The patient is a registered pet or a name typed in, like the clinic's paper sheet.
/// It never moves stock: the inventory is adjusted as usual.
/// </summary>
public class ConsumptionSheet : Entity<int>
{
    public ConsumptionSheet() : base(0) { }

    /// <summary>A registered pet; null when the patient was typed in.</summary>
    public int? PetId { get; set; }

    /// <summary>The patient's name as shown and printed (copied from the pet when registered).</summary>
    public string PatientName { get; set; } = string.Empty;

    public string? OwnerName { get; set; }

    public string? Notes { get; set; }

    public DateTime OpenedAtUtc { get; set; }
    public string? OpenedBy { get; set; }

    /// <summary>Set at discharge; a closed sheet takes no more entries until it is reopened.</summary>
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }

    public bool IsClosed => ClosedAtUtc is not null;

    public List<ConsumptionEntry> Entries { get; set; } = [];
}

/// <summary>
/// One use of a supply on the patient. How it is charged is copied from the item's pricing when recorded, so a later
/// price change doesn't alter what was already given.
/// </summary>
public class ConsumptionEntry : Entity<int>
{
    public ConsumptionEntry() : base(0) { }

    public int SheetId { get; set; }

    public int ItemId { get; set; }
    /// <summary>Copied when recorded, so the sheet stays readable if the item is renamed or deleted.</summary>
    public string ItemName { get; set; } = string.Empty;

    public ConsumptionCharge Charge { get; set; }
    /// <summary>mL in one container; only for <see cref="ConsumptionCharge.Container"/>.</summary>
    public decimal? ContainerMl { get; set; }
    /// <summary>Price of one unit, one mL or one container, depending on <see cref="Charge"/>.</summary>
    public int UnitPrice { get; set; }

    /// <summary>Units, or mL for the charges by mL and by container.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The rest of an open container thrown away (not given to the patient); it still counts towards containers used.</summary>
    public bool Discarded { get; set; }

    public string? Notes { get; set; }

    public DateTime UsedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>How one inventory item is charged on consumption sheets; items without one are charged per unit at their sale price.</summary>
public class ConsumptionPricing : Entity<int>
{
    public ConsumptionPricing() : base(0) { }

    public int ItemId { get; set; }

    public ConsumptionCharge Charge { get; set; }

    /// <summary>mL in one container (e.g. 10 for a 10 mL ampoule); required for <see cref="ConsumptionCharge.Container"/>.</summary>
    public decimal? ContainerMl { get; set; }

    /// <summary>Price of one mL; required for <see cref="ConsumptionCharge.PerMl"/>.</summary>
    public int? PricePerMl { get; set; }

    /// <summary>A controlled drug, whose every use must be accounted for.</summary>
    public bool Controlled { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
