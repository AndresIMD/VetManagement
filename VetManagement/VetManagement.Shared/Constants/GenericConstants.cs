namespace VetManagement.Shared.Constants;

public static class GenericConstants
{
    // Error Messages
    public const string REQUIRED_FIELD_ERROR = "This field is required.";

    // Month Abbreviations
    public static readonly string[] MONTH_ABBREVIATIONS =
    [
        "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    ];
    public static readonly string[] MONTH_ABBREVIATIONS_ES =
    [
        "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"
    ];
}

/// <summary>
/// Centralized reasons for inventory movements to ensure consistency across the application.
/// </summary>
public static class InventoryReasons
{
    public const string QUICK_ADJUSTMENT = "Quick stock adjustment";
    public const string ITEM_EDIT = "Update via item edit";
    public const string ITEM_DELETION = "Item deletion";
    public const string MASSIVE_INGRESS = "Massive Ingress";
    public const string MASSIVE_EGRESS = "Massive Egress";
}
