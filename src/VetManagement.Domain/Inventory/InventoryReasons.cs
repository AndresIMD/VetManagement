namespace VetManagement.Domain.Inventory;

/// <summary>
/// Standard reasons recorded on inventory movements, so the same operation is always labeled the same way.
/// </summary>
public static class InventoryReasons
{
    public const string QUICK_ADJUSTMENT = "Quick stock adjustment";
    public const string ITEM_EDIT = "Update via item edit";
    public const string ITEM_DELETION = "Item deletion";
    public const string MASSIVE_INGRESS = "Massive Ingress";
    public const string MASSIVE_EGRESS = "Massive Egress";
}
