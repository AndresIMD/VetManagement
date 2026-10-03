namespace VetManagement.Domain.Enums;

public enum SaleStatus
{
    /// <summary>Lines can change and payments are taken until the balance reaches zero.</summary>
    Open,
    Paid,
    /// <summary>Cancelled by an administrator; its payments no longer count in the cash totals.</summary>
    Voided
}

public enum SaleLineKind
{
    Service,
    /// <summary>An inventory item; its stock goes down when the sale is paid (if the clinic enables it).</summary>
    Product,
    Other
}
