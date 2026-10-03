namespace VetManagement.Domain.Billing;

/// <summary>Per-clinic billing policy, edited by the admin (see docs/architecture/BILLING.md).</summary>
public class BillingSettings
{
    public bool Enabled { get; set; } = true;

    public List<PaymentMethodDefinition> PaymentMethods { get; set; } =
    [
        new() { Code = "Cash", Name = "Efectivo", IsCash = true },
        new() { Code = "Debit", Name = "Tarjeta de débito" },
        new() { Code = "Credit", Name = "Tarjeta de crédito" },
        new() { Code = "Transfer", Name = "Transferencia" }
    ];

    /// <summary>Largest discount (% of a line) staff can give; above it needs billing management permission.</summary>
    public int MaxDiscountPercent { get; set; } = 10;

    /// <summary>Paying a sale takes its product lines out of inventory (and a void puts them back).</summary>
    public bool DeductStockOnSale { get; set; } = true;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (MaxDiscountPercent is < 0 or > 100) errors.Add("MaxDiscountPercent must be between 0 and 100.");
        if (PaymentMethods.Count(m => m.Enabled) == 0) errors.Add("Enable at least one payment method.");

        foreach (var m in PaymentMethods)
        {
            if (string.IsNullOrWhiteSpace(m.Code) || string.IsNullOrWhiteSpace(m.Name))
                errors.Add("Every payment method needs a Code and a Name.");
            if (string.Equals(m.Code, SalePayment.OnlineDepositMethod, StringComparison.OrdinalIgnoreCase))
                errors.Add($"'{SalePayment.OnlineDepositMethod}' is reserved for deposits paid online.");
        }
        foreach (var code in PaymentMethods.GroupBy(m => m.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add($"Duplicate payment method code '{code}'.");

        return errors;
    }

    public PaymentMethodDefinition? FindMethod(string code)
        => PaymentMethods.FirstOrDefault(m => string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase));
}

public class PaymentMethodDefinition
{
    /// <summary>Stable code stored on payments; rename <see cref="Name"/> freely, never the code.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    /// <summary>Counted in the drawer at the daily cash close.</summary>
    public bool IsCash { get; set; }
}
