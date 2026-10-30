using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Localization;

namespace VetManagement.Application.Inventory;

/// <summary>
/// Every stock change, always with a traceable movement. The automatic ones (sales, supplies used in visits, vaccines
/// applied, exam kits) use the daily-use warehouse; the inventory screens move stock in a chosen warehouse or between two.
/// <see cref="Item.Stock"/> stays the clinic's total, and the daily-use warehouse holds whatever no other warehouse does.
/// </summary>
public static class Stock
{
    /// <summary>
    /// Lock held by every automatic stock change and by billing, so two of them never read the same stock and
    /// overwrite each other. ponytail: one clinic-wide key; per-item keys if a clinic ever outgrows it.
    /// Manual adjustments, transfers and warehouse changes in the inventory screens take it too.
    /// </summary>
    public const string LockKey = "stock";

    /// <summary>Name the daily-use warehouse gets when it is created (the migration uses the same text); the clinic renames it.</summary>
    public const string DailyUseName = "Daily-use warehouse";

    /// <summary>Moves <paramref name="quantity"/> units out of (Egress) or back into (Ingress) the daily-use warehouse. Null if the item no longer exists.</summary>
    public static async Task<Item?> MoveAsync(IUnitOfWork unitOfWork, int itemId, int quantity, InventoryMovementType type,
        string reason, string userName, DateTime nowUtc)
    {
        var item = await unitOfWork.Items.GetByIdAsync(itemId);
        if (item is null)
            return null;

        item.Stock += type == InventoryMovementType.Egress ? -quantity : quantity;
        await unitOfWork.InventoryMovements.AddAsync(new InventoryMovement
        {
            ItemId = item.Id,
            Type = type,
            Quantity = quantity,
            Date = nowUtc,
            Reason = reason,
            Responsible = userName
        });
        return item;
    }

    /// <summary>The daily-use warehouse. The database is created with it; this adds it if it is missing (in-memory tests).</summary>
    public static async Task<Warehouse> DailyUseAsync(IUnitOfWork unitOfWork)
    {
        if ((await unitOfWork.Warehouses.FindAsync(w => w.IsDailyUse)).FirstOrDefault() is { } daily)
            return daily;
        daily = new Warehouse { Name = Texts.T(DailyUseName), IsDailyUse = true };
        await unitOfWork.Warehouses.AddAsync(daily);
        await unitOfWork.SaveChangesAsync();
        return daily;
    }

    /// <summary>The warehouse (the daily-use one when no id is given) if it isn't archived and the user sees it; null otherwise.</summary>
    public static async Task<Warehouse?> WarehouseForAsync(IUnitOfWork unitOfWork, int? warehouseId, StockScope scope)
    {
        var warehouse = warehouseId is int id ? await unitOfWork.Warehouses.GetByIdAsNoTrackingAsync(id) : await DailyUseAsync(unitOfWork);
        return warehouse is { IsArchived: false } && scope.Sees(warehouse) ? warehouse : null;
    }

    /// <summary>Units of the item in the daily-use warehouse: its total minus what the other warehouses hold.</summary>
    public static async Task<int> InDailyUseAsync(IUnitOfWork unitOfWork, Item item)
        => item.Stock - (await unitOfWork.WarehouseStocks.ForItemsAsync([item.Id])).Sum(s => s.Quantity);

    /// <summary>Units of the item in a warehouse.</summary>
    public static async Task<int> InWarehouseAsync(IUnitOfWork unitOfWork, Item item, Warehouse warehouse)
        => warehouse.IsDailyUse
            ? await InDailyUseAsync(unitOfWork, item)
            : (await unitOfWork.WarehouseStocks.GetAsync(warehouse.Id, item.Id))?.Quantity ?? 0;

    /// <summary>
    /// Null when the daily-use warehouse has <paramref name="needed"/> units of the item besides the <paramref name="reserved"/>
    /// ones (already in the same sale); otherwise the message for the user, naming the warehouses that hold some, never how many.
    /// </summary>
    public static async Task<string?> ShortageAsync(IUnitOfWork unitOfWork, Item item, int needed, int reserved = 0)
    {
        var held = await unitOfWork.WarehouseStocks.ForItemsAsync([item.Id]);
        var left = item.Stock - held.Sum(s => s.Quantity) - reserved;
        if (needed <= left)
            return null;

        var outOfStock = left <= 0 && reserved == 0;
        var holding = held.Where(s => s.Quantity > 0).Select(s => s.WarehouseId).ToHashSet();
        if (holding.Count == 0)
            return outOfStock ? Texts.T("'{0}' is out of stock.", item.Name) : Texts.T("Only {0} of '{1}' left in stock.", Math.Max(left, 0), item.Name);

        var warehouses = await unitOfWork.Warehouses.GetAllAsync();
        var daily = warehouses.FirstOrDefault(w => w.IsDailyUse)?.Name ?? Texts.T(DailyUseName);
        var askAt = string.Join(", ", warehouses.Where(w => holding.Contains(w.Id)).Select(w => w.Name).Order(StringComparer.CurrentCultureIgnoreCase));
        return outOfStock
            ? Texts.T("'{0}' is out of stock in {1}; ask {2}.", item.Name, daily, askAt)
            : Texts.T("Only {0} of '{1}' left in {2}; ask {3} for more.", Math.Max(left, 0), item.Name, daily, askAt);
    }

    /// <summary>
    /// Adds <paramref name="change"/> units of the item to a warehouse, or takes them when negative, with its movement: the
    /// inventory screens' ingress, egress and adjustments. The caller checks that the warehouse has the units it gives.
    /// </summary>
    public static async Task ChangeAsync(IUnitOfWork unitOfWork, Item item, Warehouse warehouse, int change, InventoryMovementType type,
        string reason, string userName, DateTime nowUtc)
    {
        item.Stock += change;
        if (!warehouse.IsDailyUse)
            await AddUnitsAsync(unitOfWork, warehouse.Id, item.Id, change);
        await unitOfWork.InventoryMovements.AddAsync(Movement(item, warehouse, Math.Abs(change), type, reason, userName, nowUtc));
    }

    /// <summary>
    /// Moves units of the item from one warehouse to another: a movement out of the first and one into the second, the clinic's
    /// total unchanged. The caller checks that the first has them.
    /// </summary>
    public static async Task TransferAsync(IUnitOfWork unitOfWork, Item item, Warehouse from, Warehouse to, int quantity,
        string reason, string userName, DateTime nowUtc)
    {
        if (!from.IsDailyUse)
            await AddUnitsAsync(unitOfWork, from.Id, item.Id, -quantity);
        if (!to.IsDailyUse)
            await AddUnitsAsync(unitOfWork, to.Id, item.Id, quantity);
        await unitOfWork.InventoryMovements.AddAsync(Movement(item, from, quantity, InventoryMovementType.TransferOut, reason, userName, nowUtc));
        await unitOfWork.InventoryMovements.AddAsync(Movement(item, to, quantity, InventoryMovementType.TransferIn, reason, userName, nowUtc));
    }

    /// <summary>
    /// Tells open screens which items' stock changed, once it's saved. Never the new count: it goes to every connected user,
    /// and a user must not learn what a warehouse they don't see holds. The screens read the stock again.
    /// </summary>
    public static async Task NotifyChangedAsync(IRealtimeNotificationService? notifications, IEnumerable<int> itemIds)
    {
        if (notifications is null)
            return;
        foreach (var itemId in itemIds)
            await notifications.NotifyPropertyChangedAsync<Item>(itemId, nameof(Item.Stock), null);
    }

    private static InventoryMovement Movement(Item item, Warehouse warehouse, int quantity, InventoryMovementType type,
        string reason, string userName, DateTime nowUtc) => new()
    {
        ItemId = item.Id,
        WarehouseId = warehouse.IsDailyUse ? null : warehouse.Id,
        Type = type,
        Quantity = quantity,
        Date = nowUtc,
        Reason = reason,
        Responsible = userName
    };

    private static async Task AddUnitsAsync(IUnitOfWork unitOfWork, int warehouseId, int itemId, int change)
    {
        if (await unitOfWork.WarehouseStocks.GetAsync(warehouseId, itemId) is { } held)
            held.Quantity += change;
        else
            await unitOfWork.WarehouseStocks.AddAsync(new WarehouseStock { WarehouseId = warehouseId, ItemId = itemId, Quantity = change });
    }
}
