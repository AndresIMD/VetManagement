namespace VetManagement.Domain.Enums;

/// <summary>
/// Payment status of a medical visit or sale.
/// </summary>
public enum PaymentStatus
{
    [DisplayString("Pending")]
    Pending,
    [DisplayString("Paid")]
    Paid,
    [DisplayString("Partial")]
    Partial,
    [DisplayString("Cancelled")]
    Cancelled
}

/// <summary>
/// Method of payment used.
/// </summary>
public enum PaymentMethod
{
    [DisplayString("Cash")]
    Cash,
    [DisplayString("Card")]
    Card,
    [DisplayString("Transfer")]
    Transfer,
    [DisplayString("Other")]
    Other
}

/// <summary>
/// Unified audit action type supporting CRUD, inventory, and security operations.
/// Organized by numeric ranges: 0-99 (CRUD), 100-199 (Inventory), 200-299 (Security).
/// </summary>
public enum AuditActionType
{
    [DisplayString("None")]
    None = 0,

    [DisplayString("Add")]
    Add = 1,
    [DisplayString("Edit")]
    Edit = 2,
    [DisplayString("Delete")]
    Delete = 3,

    [DisplayString("Ingress")]
    Ingress = 100,
    [DisplayString("Egress")]
    Egress = 101,
    [DisplayString("Adjustment")]
    Adjustment = 102,
    [DisplayString("Massive Stock Ingress")]
    MassiveStockIngress = 103,
    [DisplayString("Massive Stock Egress")]
    MassiveStockEgress = 104,

    [DisplayString("User Login")]
    UserLogin = 200,
    [DisplayString("User Login Failed")]
    UserLoginFailed = 201,
    [DisplayString("Password Changed")]
    PasswordChanged = 202,
    [DisplayString("User Created")]
    UserCreated = 203,
    [DisplayString("Role Assigned")]
    RoleAssigned = 204,
    [DisplayString("Role Removed")]
    RoleRemoved = 205,
    [DisplayString("Claims Synced")]
    ClaimsSynced = 206,
    [DisplayString("Setup Bootstrap")]
    SetupBootstrap = 207,
    [DisplayString("Setup Bootstrap Failed")]
    SetupBootstrapFailed = 208
}
