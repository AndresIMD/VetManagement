using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Billing;

/// <summary>
/// End-of-day cash count. Once a day is closed its payments are final: no new payments land on it
/// and sales paid that day can't be voided.
/// </summary>
public class CashClose : Entity<int>
{
    public CashClose() : base(0) { }

    public DateOnly BusinessDate { get; set; }
    /// <summary>Sum of the day's payments in cash methods.</summary>
    public int ExpectedCash { get; set; }
    /// <summary>Cash physically counted in the drawer.</summary>
    public int CountedCash { get; set; }
    public int Difference => CountedCash - ExpectedCash;
    /// <summary>Snapshot of the day's totals per payment method, as JSON.</summary>
    public string TotalsJson { get; set; } = "{}";
    public string? Notes { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }
}
