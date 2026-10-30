using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Inventory;

/// <summary>
/// The warehouses a user sees: the daily-use one, those that list one of the user's roles, or all of them for whoever
/// manages warehouses. A user moves stock by hand (with the inventory permissions) only in the warehouses they see, and
/// never learns how many units the others hold.
/// </summary>
public sealed record StockScope(bool SeesAll, IReadOnlySet<string> Roles)
{
    /// <summary>Every warehouse: work no user asked for, and tests.</summary>
    public static readonly StockScope All = new(true, new HashSet<string>());

    public bool Sees(Warehouse warehouse) => SeesAll || warehouse.IsSeenBy(Roles);

    /// <summary>Ids of the warehouses the user doesn't see: their units and movements are left out of what the user is shown.</summary>
    public List<int> HiddenAmong(IEnumerable<Warehouse> warehouses) => [.. warehouses.Where(w => !Sees(w)).Select(w => w.Id)];
}
