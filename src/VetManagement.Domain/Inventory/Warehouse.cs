namespace VetManagement.Domain.Inventory;

/// <summary>
/// A place where the clinic keeps stock (a storeroom, a locked cabinet). The daily-use warehouse is the one sales,
/// visit supplies, doses and exam kits take from and everyone sees; the others are seen, and their stock moved, only
/// by their roles and by whoever manages warehouses. Stock no other warehouse holds is in the daily-use one, so that
/// one has no <see cref="WarehouseStock"/> rows: its units are the item's total minus the other warehouses'.
/// </summary>
public class Warehouse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The single warehouse the automatic stock changes use. Created with the database; it can only be renamed.</summary>
    public bool IsDailyUse { get; set; }

    /// <summary>Roles that see this warehouse and may move its stock (with the inventory permissions). Ignored on the daily-use one.</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>Hidden from the lists and the stock screens; only an empty warehouse is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Whether a user with these roles sees the warehouse (managing warehouses sees them all, checked by the caller).</summary>
    public bool IsSeenBy(IEnumerable<string> roles)
        => IsDailyUse || Roles.Intersect(roles, StringComparer.OrdinalIgnoreCase).Any();
}

/// <summary>Units of an item kept in a warehouse other than the daily-use one.</summary>
public class WarehouseStock
{
    public int WarehouseId { get; set; }

    public int ItemId { get; set; }

    public int Quantity { get; set; }
}
