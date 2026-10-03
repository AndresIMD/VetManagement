using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Clinical;

/// <summary>A vaccine or antiparasitic applied to a pet, with the date the next one is due.</summary>
public class PreventiveDose : Entity<int>
{
    public PreventiveDose() : base(0) { }

    public int PetId { get; set; }
    /// <summary>Protocol from the clinical settings (e.g. "rabies"); null for a one-off product.</summary>
    public string? ProtocolCode { get; set; }
    public PreventiveKind Kind { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateOnly AppliedOn { get; set; }
    /// <summary>When the next dose is due; null when no repeat is needed.</summary>
    public DateOnly? NextDueOn { get; set; }
    public int? VisitId { get; set; }
    /// <summary>Inventory item applied (one unit), when the clinic tracks its vaccines in stock.</summary>
    public int? ItemId { get; set; }
    /// <summary>Set when the unit was taken out of stock, so deleting the dose puts it back.</summary>
    public bool StockDeducted { get; set; }
    public string? Notes { get; set; }
    public string? AppliedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Set once the owner was reminded of <see cref="NextDueOn"/>, so it is sent only once.</summary>
    public DateTime? ReminderSentAtUtc { get; set; }

    /// <summary>Doses of the same protocol (or product) replace each other: only the latest one is due.</summary>
    public string SeriesKey => ProtocolCode ?? ProductName.Trim().ToUpperInvariant();

    public PreventiveStatus StatusOn(DateOnly today, int dueSoonDays, bool superseded) =>
        superseded ? PreventiveStatus.Superseded
        : NextDueOn is not { } due ? PreventiveStatus.NoRepeat
        : due < today ? PreventiveStatus.Overdue
        : due <= today.AddDays(dueSoonDays) ? PreventiveStatus.DueSoon
        : PreventiveStatus.UpToDate;
}
