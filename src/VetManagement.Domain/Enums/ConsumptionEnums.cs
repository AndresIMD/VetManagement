namespace VetManagement.Domain.Enums;

/// <summary>How a supply used on a patient is charged on the consumption sheet.</summary>
public enum ConsumptionCharge
{
    /// <summary>Whole units at the item's sale price (syringes, catheters...).</summary>
    [DisplayString("Per unit")]
    Unit = 0,
    /// <summary>The mL given, at a price per mL (e.g. diazepam from a shared vial).</summary>
    [DisplayString("Per mL used")]
    PerMl = 1,
    /// <summary>
    /// Whole containers at the item's sale price, used little by little on the same patient (e.g. a 10 mL fentanyl
    /// ampoule): each container started is charged.
    /// </summary>
    [DisplayString("Whole container")]
    Container = 2
}
