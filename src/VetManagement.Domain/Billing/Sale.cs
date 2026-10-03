using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Billing;

/// <summary>
/// What a client is charged at the clinic (the account of a visit, an appointment or a counter sale) and
/// how it was paid. Amounts are whole Chilean pesos. Rule methods return an error message, or null when applied.
/// </summary>
public class Sale : Entity<int>
{
    public Sale() : base(0) { }

    public SaleStatus Status { get; set; } = SaleStatus.Open;
    public int? ClientId { get; set; }
    public int? PetId { get; set; }
    /// <summary>At most one sale per appointment; it carries the deposit paid online.</summary>
    public int? AppointmentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Clinic-local date the sale was opened (lists and reports filter by it).</summary>
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    /// <summary>Set when paying took the product lines out of stock, so a void puts back exactly that.</summary>
    public bool StockDeducted { get; set; }

    public List<SaleLine> Lines { get; set; } = [];
    public List<SalePayment> Payments { get; set; } = [];

    public int Total => Lines.Sum(l => l.Total);
    public int PaidAmount => Payments.Sum(p => p.Amount);
    public int Balance => Total - PaidAmount;

    public string? AddLine(SaleLine line)
    {
        if (Status != SaleStatus.Open) return "Only open sales can be changed.";
        if (string.IsNullOrWhiteSpace(line.Description)) return "The line needs a description.";
        if (line.Quantity < 1) return "Quantity must be at least 1.";
        if (line.UnitPrice < 0) return "The price can't be negative.";
        if (line.Discount < 0 || line.Discount > line.Gross) return "The discount must be between 0 and the line amount.";
        if (line.Kind == SaleLineKind.Product && line.ItemId is null) return "A product line needs an inventory item.";

        Lines.Add(line);
        return null;
    }

    public string? RemoveLine(int lineId)
    {
        if (Status != SaleStatus.Open) return "Only open sales can be changed.";
        var line = Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return "Line not found.";
        if (Total - line.Total < PaidAmount) return "The total can't go below what was already paid.";

        Lines.Remove(line);
        return null;
    }

    /// <summary>Takes a payment; the sale becomes <see cref="SaleStatus.Paid"/> when nothing is left to pay.</summary>
    public string? AddPayment(SalePayment payment)
    {
        if (Status != SaleStatus.Open) return "Only open sales take payments.";
        if (payment.Amount < 1) return "The amount must be greater than 0.";
        if (payment.Amount > Balance) return $"The amount exceeds the balance ({Balance}).";

        Payments.Add(payment);
        if (Balance == 0)
        {
            Status = SaleStatus.Paid;
            PaidAtUtc = payment.ReceivedAtUtc;
        }
        return null;
    }

    public string? Void(string reason, string userName, DateTime nowUtc)
    {
        if (Status == SaleStatus.Voided) return "The sale is already voided.";
        if (string.IsNullOrWhiteSpace(reason)) return "A reason is required to void a sale.";

        Status = SaleStatus.Voided;
        VoidReason = reason;
        VoidedBy = userName;
        VoidedAtUtc = nowUtc;
        return null;
    }
}

public class SaleLine
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public SaleLineKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Service code from the agenda settings, for service lines.</summary>
    public string? ServiceCode { get; set; }
    /// <summary>Inventory item, for product lines.</summary>
    public int? ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public int UnitPrice { get; set; }
    /// <summary>Discount in pesos on the whole line.</summary>
    public int Discount { get; set; }

    public int Gross => Quantity * UnitPrice;
    public int Total => Gross - Discount;
}

public class SalePayment
{
    /// <summary>Method code of a deposit paid online when booking (not money at the front desk).</summary>
    public const string OnlineDepositMethod = "OnlineDeposit";

    public int Id { get; set; }
    public int SaleId { get; set; }
    /// <summary>Code of a payment method from the billing settings, or <see cref="OnlineDepositMethod"/>.</summary>
    public string Method { get; set; } = string.Empty;
    public int Amount { get; set; }
    /// <summary>Voucher, transfer or transaction number.</summary>
    public string? Reference { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    /// <summary>Clinic-local date the payment counts for in the daily cash close.</summary>
    public DateOnly BusinessDate { get; set; }
    public string? ReceivedBy { get; set; }
}
