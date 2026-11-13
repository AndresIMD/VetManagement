using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Inventory;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Localization;
using AuditActionType = VetManagement.Domain.Enums.AuditActionType;
using InventoryMovementType = VetManagement.Domain.Enums.InventoryMovementType;
using ItemType = VetManagement.Domain.Enums.ItemType;

namespace VetManagement.Application.Services;

public enum ItemAddStatus
{
    Added,
    /// <summary>Missing data, or a warehouse the user doesn't see.</summary>
    Invalid,
    /// <summary>Another product already has the code.</summary>
    DuplicateBarcode
}

/// <summary>Whether a product was created; when it wasn't, <see cref="Error"/> says why, ready to show.</summary>
public sealed record ItemAddResult(ItemAddStatus Status, string? Error = null);

public class ItemService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    #region Query Methods

    public async Task<List<Item>> GetAllAsync()
        => await unitOfWork.Items.GetAllAsync();

    public async Task<Item?> GetByIdAsync(int id)
        => await unitOfWork.Items.GetByIdAsync(id);

    public async Task<Item?> GetByIdAsNoTrackingAsync(int id)
        => await unitOfWork.Items.GetByIdAsNoTrackingAsync(id);

    public async Task<Item?> GetByBarcodeAsync(string barcode)
        => await unitOfWork.Items.GetByBarcodeAsync(barcode);

    public async Task<IEnumerable<Item>> GetLowStockItemsAsync()
        => await unitOfWork.Items.GetLowStockAsync();

    public async Task<IEnumerable<Item>> GetOutOfStockItemsAsync()
        => await unitOfWork.Items.GetOutOfStockAsync();

    public async Task<IEnumerable<Item>> SearchAsync(ItemType? type = null, string? searchTerm = null)
        => await unitOfWork.Items.SearchAsync(type, searchTerm);

    #endregion

    #region Command Methods

    /// <summary>
    /// Creates a product. A code another product already has is refused, naming that product: the same product in two
    /// warehouses is one item, and its units are added with an ingress. Products without a code (<see cref="Item.NoBarcode"/>)
    /// are not compared. The product starts without stock;
    /// <paramref name="initialStock"/> units, if any, come in as an ingress in the warehouse (the daily-use one when no id
    /// is given), which must be one the user sees, so the first units are in the movement history like the rest.
    /// </summary>
    public async Task<ItemAddResult> AddAsync(Item item, string userName, int initialStock = 0, int? warehouseId = null, StockScope? scope = null)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name) || item.Type == ItemType.None || initialStock < 0)
            return new ItemAddResult(ItemAddStatus.Invalid, Texts.T("Invalid item data."));

        // Same lock as every stock change, and so that two people creating the same code don't both get through.
        var result = await unitOfWork.ExecuteExclusiveAsync(Stock.LockKey, async () =>
        {
            if (!string.IsNullOrWhiteSpace(item.Barcode) && item.Barcode != Item.NoBarcode && await unitOfWork.Items.GetByBarcodeAsync(item.Barcode) is { } existing)
                return new ItemAddResult(ItemAddStatus.DuplicateBarcode,
                    Texts.T("The code {0} already belongs to '{1}'. Add units to that product instead of creating it again.", item.Barcode, existing.Name));

            Warehouse? warehouse = null;
            if (initialStock > 0)
            {
                warehouse = await Stock.WarehouseForAsync(unitOfWork, warehouseId, scope ?? StockScope.All);
                if (warehouse is null)
                    return new ItemAddResult(ItemAddStatus.Invalid, Texts.T("Unknown warehouse."));
            }

            // Id back to 0: the work runs again from the start when the database retries it.
            (item.Id, item.Stock) = (0, 0);
            await unitOfWork.Items.AddAsync(item);
            await unitOfWork.SaveChangesAsync();
            await LogAuditAsync(item.Id, AuditActionType.Add, JsonSerializer.Serialize(item), userName);

            if (warehouse is not null)
            {
                // With its own audit entry, like every ingress: the one above doesn't say which warehouse got the units.
                var reason = Texts.T(InventoryReasons.INITIAL_STOCK);
                await Stock.ChangeAsync(unitOfWork, item, warehouse, initialStock, InventoryMovementType.Ingress, reason, userName, DateTime.UtcNow);
                await LogAuditAsync(item.Id, AuditActionType.Ingress, JsonSerializer.Serialize(new
                {
                    Quantity = initialStock,
                    Stock = new { old = 0, @new = initialStock },
                    Warehouse = warehouse.Name,
                    Reason = reason
                }), userName);
            }
            return new ItemAddResult(ItemAddStatus.Added);
        });
        if (result.Status != ItemAddStatus.Added)
            return result;

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Item>(item.Id, "Add", new { item.Name });
            await notificationService.NotifyCollectionChangedAsync<Item>("Add");
        }

        return result;
    }

    public async Task<bool> UpdateAsync(Item item, Item? oldData, string userName)
    {
        if (item == null || oldData == null || item.Id != oldData.Id ||
            string.IsNullOrWhiteSpace(item.Name) || item.Type == ItemType.None)
            return false;

        // Stock is a read model maintained exclusively by inventory movements.
        // A catalog edit must never become an untraceable stock adjustment.
        item.Stock = oldData.Stock;

        unitOfWork.Items.Update(item);
        await unitOfWork.SaveChangesAsync();

        await LogAuditAsync(item.Id, AuditActionType.Edit,
            $"Before: {JsonSerializer.Serialize(oldData)}\nAfter: {JsonSerializer.Serialize(item)}", userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Item>(item.Id, "Update", new { item.Name });

        }

        return true;
    }

    public async Task<bool> DeleteAsync(int id, string userName)
    {
        var item = await unitOfWork.Items.GetByIdAsync(id);
        if (item == null)
            return false;

        var snapshot = JsonSerializer.Serialize(item);
        unitOfWork.Items.Remove(item);
        await unitOfWork.SaveChangesAsync();

        await LogAuditAsync(id, AuditActionType.Delete, snapshot, userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Item>(id, "Delete");
            await notificationService.NotifyCollectionChangedAsync<Item>("Delete");
        }

        return true;
    }

    #endregion

    #region Private Helpers

    private async Task LogAuditAsync(int entityId, AuditActionType action, string changes, string userName)
    {
        await unitOfWork.AuditLogs.AddAsync(new()
        {
            EntityId = entityId,
            EntityName = nameof(Item),
            Action = action,
            User = userName,
            Date = DateTime.UtcNow,
            Changes = changes
        });
        await unitOfWork.SaveChangesAsync();
    }

    #endregion
}
