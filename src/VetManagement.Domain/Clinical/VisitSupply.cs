using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Clinical;

/// <summary>A drug or material from inventory used during a visit; it leaves stock when the clinic enables it.</summary>
public class VisitSupply : Entity<int>
{
    public VisitSupply() : base(0) { }

    public int VisitId { get; set; }
    public int ItemId { get; set; }
    /// <summary>Copied when used, so the record stays readable if the item is renamed or deleted.</summary>
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    /// <summary>Set when the quantity was taken out of stock, so removing it puts back exactly that.</summary>
    public bool StockDeducted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
}
