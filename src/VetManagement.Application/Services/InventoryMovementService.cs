using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Inventory;
using VetManagement.Domain.Localization;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Application.Common;
using VetManagement.Contracts.Inventory;
using AuditActionType = VetManagement.Domain.Enums.AuditActionType;
using DomainInventoryMovementType = VetManagement.Domain.Enums.InventoryMovementType;

namespace VetManagement.Application.Services;

public enum StockMoveStatus
{
    Done,
    NotFound,
    Invalid
}

/// <param name="Count">Items changed, for the bulk operations.</param>
public sealed record StockMoveResult(StockMoveStatus Status, string? Error = null, int Count = 0);

/// <summary>
/// The inventory screens' stock changes: adjustments, bulk ingress and egress, and transfers between warehouses. Each one
/// happens in warehouses the user sees, and the realtime notices say what changed, never how many units are left.
/// </summary>
public class InventoryMovementService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    #region Query Methods

    /// <summary>An item's movements in the warehouses the user sees, newest first.</summary>
    public async Task<List<InventoryMovement>> GetMovementsByItemIdAsync(int itemId, StockScope scope)
    {
        var movements = await unitOfWork.InventoryMovements.GetByItemIdAsync(itemId, await HiddenFromAsync(scope));
        return [.. movements];
    }

    public async Task<PagedResult<InventoryMovement>> GetMovementsFilteredPagedAsync(
        StockScope scope,
        int page = 0,
        int pageSize = 25,
        int? itemId = null,
        DomainInventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null,
        int? warehouseId = null)
    {
        // The daily-use warehouse's movements carry no warehouse.
        int? onlyWarehouse = null;
        var onlyDailyUse = false;
        if (warehouseId is int id)
        {
            if (await Stock.WarehouseForAsync(unitOfWork, id, scope) is not { } warehouse)
                return new PagedResult<InventoryMovement> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
            (onlyWarehouse, onlyDailyUse) = warehouse.IsDailyUse ? ((int?)null, true) : (warehouse.Id, false);
        }

        var (items, totalCount) = await unitOfWork.InventoryMovements.GetFilteredPagedAsync(
            page: page,
            pageSize: pageSize,
            itemId: itemId,
            type: type,
            responsible: responsible,
            from: from,
            to: to,
            hiddenWarehouseIds: await HiddenFromAsync(scope),
            warehouseId: onlyWarehouse,
            onlyDailyUse: onlyDailyUse);

        return new PagedResult<InventoryMovement>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Command Methods

    /// <summary>Adds or takes units of an item in a warehouse (the daily-use one when none is given).</summary>
    public async Task<StockMoveResult> AdjustStockAsync(int itemId, int amount, string? reason, string userName, StockScope scope, int? warehouseId = null)
    {
        if (amount == 0)
            return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Could not adjust stock."));
        var note = string.IsNullOrWhiteSpace(reason) ? Texts.T(InventoryReasons.QUICK_ADJUSTMENT) : reason;

        // Same lock as sales and clinical use, so a manual adjustment never overwrites their stock change.
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            var item = await unitOfWork.Items.GetByIdAsync(itemId);
            if (item == null)
                return new StockMoveResult(StockMoveStatus.NotFound);
            if (await Stock.WarehouseForAsync(unitOfWork, warehouseId, scope) is not { } warehouse)
                return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Unknown warehouse."));

            var before = await Stock.InWarehouseAsync(unitOfWork, item, warehouse);
            if (before + amount < 0)
                return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Only {0} of '{1}' in {2}.", before, item.Name, warehouse.Name));

            var oldStock = item.Stock;
            await Stock.ChangeAsync(unitOfWork, item, warehouse, amount,
                amount > 0 ? DomainInventoryMovementType.Ingress : DomainInventoryMovementType.Egress,
                note, userName, DateTime.UtcNow);
            unitOfWork.Items.Update(item);

            await unitOfWork.SaveChangesAsync();
            await LogAuditAsync(nameof(Item), item.Id, AuditActionType.Adjustment,
                $"Stock before: {oldStock}, Stock after: {item.Stock}, Amount: {amount}, Reason: {note}, Warehouse: {warehouse.Name} ({before} → {before + amount})", userName);
            return new StockMoveResult(StockMoveStatus.Done, Count: 1);
        });

        if (result.Status == StockMoveStatus.Done)
            await NotifyStockChangedAsync([itemId]);
        return result;
    }

    /// <summary>Bulk ingress or egress in one warehouse; an egress takes at most what the warehouse holds of each item.</summary>
    public async Task<StockMoveResult> MassStockUpdateAsync(IEnumerable<MassStockUpdateRequest> items, bool isIngress, string userName,
        StockScope scope, int? warehouseId = null)
    {
        var requests = items.ToList();
        List<int> changedItems = [];

        // Same lock as sales and clinical use, so a bulk update never overwrites their stock changes.
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            changedItems.Clear();
            if (await Stock.WarehouseForAsync(unitOfWork, warehouseId, scope) is not { } warehouse)
                return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Unknown warehouse."));
            var movementType = isIngress ? DomainInventoryMovementType.MassiveStockIngress : DomainInventoryMovementType.MassiveStockEgress;

            foreach (var dto in requests)
            {
                Item? item = dto.ItemId > 0
                    ? await unitOfWork.Items.GetByIdAsync(dto.ItemId)
                    : await TrackedByBarcodeAsync(dto.Barcode);

                if (item == null || dto.Quantity <= 0)
                    continue;

                int oldStock = item.Stock;
                var held = await Stock.InWarehouseAsync(unitOfWork, item, warehouse);
                int appliedQuantity = isIngress ? dto.Quantity : Math.Min(held, dto.Quantity);
                if (appliedQuantity <= 0)
                    continue;

                await Stock.ChangeAsync(unitOfWork, item, warehouse, isIngress ? appliedQuantity : -appliedQuantity, movementType,
                    Texts.T(isIngress ? InventoryReasons.MASSIVE_INGRESS : InventoryReasons.MASSIVE_EGRESS), userName, DateTime.UtcNow);
                unitOfWork.Items.Update(item);

                // Saved item by item, so the next lookup of a warehouse's units sees this change.
                await LogAuditAsync(nameof(Item), item.Id,
                    movementType.ToAuditActionType(),
                    $"Stock before: {oldStock}, Stock after: {item.Stock}, Applied quantity: {appliedQuantity}, ID: {item.Id}, Barcode: {item.Barcode}, Warehouse: {warehouse.Name}", userName);

                changedItems.Add(item.Id);
            }

            await unitOfWork.SaveChangesAsync();
            return new StockMoveResult(StockMoveStatus.Done, Count: changedItems.Count);
        });

        if (changedItems.Count > 0)
            await NotifyStockChangedAsync(changedItems);
        return result;
    }

    /// <summary>Moves units of an item from one warehouse to another (the daily-use one when an id is missing); both must be the user's.</summary>
    public Task<StockMoveResult> TransferAsync(int itemId, int? fromWarehouseId, int? toWarehouseId, int quantity, string? reason,
        string userName, StockScope scope)
        => MassTransferAsync([new MassStockUpdateRequest { ItemId = itemId, Quantity = quantity }], fromWarehouseId, toWarehouseId, reason, userName, scope,
            refuseShortage: true);

    /// <summary>
    /// Moves several items from one warehouse to another. With <paramref name="refuseShortage"/> an item the first warehouse
    /// doesn't have enough of refuses the whole transfer; otherwise as many units as it holds move.
    /// </summary>
    public async Task<StockMoveResult> MassTransferAsync(IEnumerable<MassStockUpdateRequest> items, int? fromWarehouseId, int? toWarehouseId,
        string? reason, string userName, StockScope scope, bool refuseShortage = false)
    {
        var requests = items.ToList();
        if (requests.Any(r => r.Quantity < 1))
            return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Quantity must be at least 1."));
        List<int> changedItems = [];

        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            changedItems.Clear();
            var from = await Stock.WarehouseForAsync(unitOfWork, fromWarehouseId, scope);
            var to = await Stock.WarehouseForAsync(unitOfWork, toWarehouseId, scope);
            if (from is null || to is null)
                return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Unknown warehouse."));
            if (from.Id == to.Id)
                return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Pick two different warehouses."));

            var note = $"{from.Name} → {to.Name}{(string.IsNullOrWhiteSpace(reason) ? "" : $": {reason.Trim()}")}";
            if (note.Length > 200)
                note = note[..200];
            var now = DateTime.UtcNow;
            foreach (var dto in requests)
            {
                var item = dto.ItemId > 0 ? await unitOfWork.Items.GetByIdAsync(dto.ItemId) : await TrackedByBarcodeAsync(dto.Barcode);
                if (item is null)
                {
                    if (refuseShortage)
                        return new StockMoveResult(StockMoveStatus.NotFound);
                    continue;
                }

                var held = await Stock.InWarehouseAsync(unitOfWork, item, from);
                if (held < dto.Quantity && refuseShortage)
                    return new StockMoveResult(StockMoveStatus.Invalid, Texts.T("Only {0} of '{1}' in {2}.", Math.Max(held, 0), item.Name, from.Name));
                var quantity = Math.Min(held, dto.Quantity);
                if (quantity <= 0)
                    continue;

                await Stock.TransferAsync(unitOfWork, item, from, to, quantity, note, userName, now);
                await LogAuditAsync(nameof(Item), item.Id, AuditActionType.Transfer, $"{note} ({quantity}), ID: {item.Id}", userName);
                changedItems.Add(item.Id);
            }

            await unitOfWork.SaveChangesAsync();
            return new StockMoveResult(StockMoveStatus.Done, Count: changedItems.Count);
        });

        if (changedItems.Count > 0)
            await NotifyStockChangedAsync(changedItems);
        return result;
    }

    #endregion

    #region Private Helpers

    private async Task<List<int>> HiddenFromAsync(StockScope scope)
        => scope.SeesAll ? [] : scope.HiddenAmong(await unitOfWork.Warehouses.GetAllAsync());

    /// <summary>The item with this code, tracked so its stock can change (the barcode lookup itself reads without tracking).</summary>
    private async Task<Item?> TrackedByBarcodeAsync(string? barcode)
        => await unitOfWork.Items.GetByBarcodeAsync(barcode ?? string.Empty) is { } found ? await unitOfWork.Items.GetByIdAsync(found.Id) : null;

    private async Task NotifyStockChangedAsync(IReadOnlyCollection<int> itemIds)
    {
        await Stock.NotifyChangedAsync(notificationService, itemIds);
        if (notificationService is not null)
            await notificationService.NotifyCollectionChangedAsync<InventoryMovement>("Add");
    }

    private async Task LogAuditAsync(string entityName, int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            Changes = changes,
            User = userName,
            Date = DateTime.UtcNow
        });
        await unitOfWork.SaveChangesAsync();
    }

    #endregion
}
