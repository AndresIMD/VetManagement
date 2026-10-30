using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Localization;

namespace VetManagement.Application.Inventory;

/// <summary>Units of an item in one warehouse.</summary>
public sealed record WarehouseUnits(int WarehouseId, string Name, int Quantity);

/// <summary>An item's stock as one user sees it.</summary>
/// <param name="Seen">Units in the warehouses the user sees: the clinic's total for whoever sees them all.</param>
/// <param name="InDailyUse">Units in the daily-use warehouse, the ones sales, visits, doses and exam kits take.</param>
/// <param name="Warehouses">Units per warehouse the user sees: the daily-use one first, then those holding some.</param>
/// <param name="AskAt">The other warehouses holding some, by name only: where to ask for more.</param>
public sealed record StockView(int Seen, int InDailyUse, IReadOnlyList<WarehouseUnits> Warehouses, IReadOnlyList<string> AskAt);

public enum WarehouseStatus
{
    Done,
    NotFound,
    Invalid
}

public sealed record WarehouseResult(WarehouseStatus Status, int? Id = null, string? Error = null);

/// <summary>
/// The clinic's warehouses and who sees each one, and the stock as each user sees it. Changing a warehouse takes the stock
/// lock, so it can't be archived or deleted while a transfer puts units into it.
/// </summary>
public class WarehouseService(IUnitOfWork unitOfWork, AuditService audit)
{
    public const int NameMaxLength = 100;

    /// <summary>The warehouses the user sees, the daily-use one first, then by name; archived ones only for whoever sees them all.</summary>
    public async Task<List<Warehouse>> ListAsync(StockScope scope)
    {
        await Stock.DailyUseAsync(unitOfWork);
        return [.. (await unitOfWork.Warehouses.GetAllAsync())
            .Where(w => scope.Sees(w) && (!w.IsArchived || scope.SeesAll))
            .OrderByDescending(w => w.IsDailyUse)
            .ThenBy(w => w.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Ids of the warehouses the user doesn't see.</summary>
    public async Task<List<int>> HiddenFromAsync(StockScope scope)
        => scope.SeesAll ? [] : scope.HiddenAmong(await unitOfWork.Warehouses.GetAllAsync());

    /// <summary>How each item's stock looks to the user: never a count of a warehouse they don't see.</summary>
    public async Task<Dictionary<int, StockView>> ViewAsync(IReadOnlyCollection<Item> items, StockScope scope)
    {
        var daily = await Stock.DailyUseAsync(unitOfWork);
        var warehouses = (await unitOfWork.Warehouses.GetAllAsync()).ToDictionary(w => w.Id);
        var held = (await unitOfWork.WarehouseStocks.ForItemsAsync([.. items.Select(i => i.Id)]))
            .Where(s => s.Quantity != 0 && warehouses.ContainsKey(s.WarehouseId))
            .ToLookup(s => s.ItemId);

        var views = new Dictionary<int, StockView>();
        foreach (var item in items)
        {
            var elsewhere = held[item.Id].ToList();
            var inDailyUse = item.Stock - elsewhere.Sum(s => s.Quantity);
            var seen = elsewhere.Where(s => scope.Sees(warehouses[s.WarehouseId])).ToList();
            views[item.Id] = new StockView(
                inDailyUse + seen.Sum(s => s.Quantity),
                inDailyUse,
                [new WarehouseUnits(daily.Id, daily.Name, inDailyUse),
                 .. seen.Select(s => new WarehouseUnits(s.WarehouseId, warehouses[s.WarehouseId].Name, s.Quantity))
                     .OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase)],
                [.. elsewhere.Where(s => s.Quantity > 0).Select(s => warehouses[s.WarehouseId].Name).Order(StringComparer.CurrentCultureIgnoreCase)]);
        }
        return views;
    }

    public async Task<WarehouseResult> CreateAsync(string name, IReadOnlyCollection<string> roles, string userName)
    {
        name = name.Trim();
        if (await NameErrorAsync(name, null) is { } error)
            return new WarehouseResult(WarehouseStatus.Invalid, Error: error);

        var warehouse = new Warehouse { Name = name, Roles = Distinct(roles) };
        await unitOfWork.Warehouses.AddAsync(warehouse);
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(nameof(Warehouse), warehouse.Id, AuditActionType.Add, JsonSerializer.Serialize(warehouse), userName);
        return new WarehouseResult(WarehouseStatus.Done, warehouse.Id);
    }

    /// <summary>Renames a warehouse, sets who sees it and archives or restores it. The daily-use one is only renamed.</summary>
    public async Task<WarehouseResult> UpdateAsync(int id, string name, IReadOnlyCollection<string> roles, bool archived, string userName)
    {
        name = name.Trim();
        string? before = null, after = null;
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var warehouse = await unitOfWork.Warehouses.GetByIdAsync(id);
            if (warehouse is null)
                return new WarehouseResult(WarehouseStatus.NotFound);
            if (await NameErrorAsync(name, id) is { } error)
                return new WarehouseResult(WarehouseStatus.Invalid, Error: error);
            if (warehouse.IsDailyUse && archived)
                return new WarehouseResult(WarehouseStatus.Invalid, Error: Texts.T("The daily-use warehouse can't be archived."));
            if (archived && !warehouse.IsArchived && await HoldsUnitsAsync(id))
                return new WarehouseResult(WarehouseStatus.Invalid, Error: Texts.T("'{0}' still holds stock; transfer it out first.", warehouse.Name));

            before = JsonSerializer.Serialize(warehouse);
            warehouse.Name = name;
            warehouse.Roles = warehouse.IsDailyUse ? [] : Distinct(roles);
            warehouse.IsArchived = archived;
            await unitOfWork.SaveChangesAsync();
            after = JsonSerializer.Serialize(warehouse);
            return new WarehouseResult(WarehouseStatus.Done, id);
        });

        if (result.Status == WarehouseStatus.Done)
            await audit.LogAsync(nameof(Warehouse), id, AuditActionType.Edit, $"Before: {before}\nAfter: {after}", userName);
        return result;
    }

    /// <summary>Deletes a warehouse created by mistake: one that never had movements. A warehouse that was used is archived instead.</summary>
    public async Task<WarehouseResult> DeleteAsync(int id, string userName)
    {
        string? snapshot = null;
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var warehouse = await unitOfWork.Warehouses.GetByIdAsync(id);
            if (warehouse is null)
                return new WarehouseResult(WarehouseStatus.NotFound);
            if (warehouse.IsDailyUse)
                return new WarehouseResult(WarehouseStatus.Invalid, Error: Texts.T("The daily-use warehouse can't be deleted."));
            if (await HoldsUnitsAsync(id) || await unitOfWork.InventoryMovements.AnyInWarehouseAsync(id))
                return new WarehouseResult(WarehouseStatus.Invalid, Error: Texts.T("'{0}' has movements; archive it instead.", warehouse.Name));

            snapshot = JsonSerializer.Serialize(warehouse);
            foreach (var row in await unitOfWork.WarehouseStocks.FindAsync(s => s.WarehouseId == id))
                unitOfWork.WarehouseStocks.Remove(row);
            unitOfWork.Warehouses.Remove(warehouse);
            await unitOfWork.SaveChangesAsync();
            return new WarehouseResult(WarehouseStatus.Done, id);
        });

        if (result.Status == WarehouseStatus.Done)
            await audit.LogAsync(nameof(Warehouse), id, AuditActionType.Delete, snapshot!, userName);
        return result;
    }

    private async Task<string?> NameErrorAsync(string name, int? id)
    {
        if (name.Length == 0)
            return Texts.T("Enter the warehouse name.");
        if (name.Length > NameMaxLength)
            return Texts.T("The name can have up to {0} characters.", NameMaxLength);
        var taken = (await unitOfWork.Warehouses.GetAllAsync())
            .Any(w => w.Id != id && string.Equals(w.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
        return taken ? Texts.T("There is already a warehouse named '{0}'.", name) : null;
    }

    private async Task<bool> HoldsUnitsAsync(int warehouseId)
        => (await unitOfWork.WarehouseStocks.FindAsync(s => s.WarehouseId == warehouseId && s.Quantity != 0)).Any();

    private static List<string> Distinct(IEnumerable<string> roles)
        => [.. roles.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
}
