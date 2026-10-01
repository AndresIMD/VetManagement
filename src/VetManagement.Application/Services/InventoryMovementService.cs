using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Inventory;
using VetManagement.Shared.Constants;
using VetManagement.Shared.Helpers;
using VetManagement.Shared.Models.DTOs;
using AuditActionType = VetManagement.Shared.Enums.AuditActionType;
using DomainInventoryMovementType = VetManagement.Domain.Enums.InventoryMovementType;

namespace VetManagement.Application.Services;

public class InventoryMovementService(IUnitOfWork unitOfWork, IRealtimeNotificationService? notificationService = null)
{
    #region Query Methods

    public async Task<List<InventoryMovement>> GetAllMovementsAsync()
        => await unitOfWork.InventoryMovements.GetAllAsync();

    public async Task<List<InventoryMovement>> GetMovementsByItemIdAsync(int itemId)
    {
        var movements = await unitOfWork.InventoryMovements.GetByItemIdAsync(itemId);
        return [.. movements];
    }

    public async Task<List<InventoryMovement>> GetMovementsFilteredAsync(
        string? itemName = null,
        DomainInventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var (items, _) = await unitOfWork.InventoryMovements.GetFilteredPagedAsync(
            page: 0,
            pageSize: int.MaxValue,
            itemId: null,
            type: type,
            responsible: responsible,
            from: from,
            to: to);

        return items;
    }

    public async Task<PagedResult<InventoryMovement>> GetMovementsFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? itemId = null,
        DomainInventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        var (items, totalCount) = await unitOfWork.InventoryMovements.GetFilteredPagedAsync(
            page: page,
            pageSize: pageSize,
            itemId: itemId,
            type: type,
            responsible: responsible,
            from: from,
            to: to);

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

    public async Task AddMovementAsync(InventoryMovement movement, string userName)
    {
        movement.Responsible = userName;
        if (movement.Date == default)
            movement.Date = DateTime.UtcNow;

        await unitOfWork.InventoryMovements.AddAsync(movement);
        await unitOfWork.SaveChangesAsync();

        await LogAuditAsync(nameof(InventoryMovement), movement.Id,
            ((VetManagement.Shared.Enums.InventoryMovementType)movement.Type).ToAuditActionType(),
            JsonSerializer.Serialize(movement), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<InventoryMovement>(movement.Id, "Add", new { movement.ItemId, movement.Type });
            await notificationService.NotifyCollectionChangedAsync<InventoryMovement>("Add");
        }
    }

    public async Task<bool> AdjustStockAsync(int itemId, int amount, string reason, string userName)
    {
        if (amount == 0)
            return false;

        var item = await unitOfWork.Items.GetByIdAsync(itemId);
        if (item == null)
            return false;

        int oldStock = item.Stock;
        item.Stock += amount;
        var moveType = amount > 0 ? DomainInventoryMovementType.Ingress : DomainInventoryMovementType.Egress;

        unitOfWork.Items.Update(item);

        await unitOfWork.InventoryMovements.AddAsync(new()
        {
            ItemId = item.Id,
            Type = moveType,
            Quantity = Math.Abs(amount),
            Date = DateTime.UtcNow,
            Reason = string.IsNullOrWhiteSpace(reason) ? InventoryReasons.QUICK_ADJUSTMENT : reason,
            Responsible = userName
        });

        await unitOfWork.SaveChangesAsync();
        await LogAuditAsync(nameof(Item), item.Id, AuditActionType.Adjustment,
            $"Stock before: {oldStock}, Stock after: {item.Stock}, Amount: {amount}, Reason: {reason}", userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyPropertyChangedAsync<Item>(item.Id, nameof(Item.Stock), item.Stock);
            await notificationService.NotifyEntityChangedAsync<InventoryMovement>(item.Id, "Add", new { itemId, amount, reason });
        }

        return true;
    }

    public async Task<int> MassStockUpdateAsync(List<InventoryMassUpdateDTO> items, bool isIngress, string userName)
    {
        int updatedCount = 0;
        List<(int ItemId, int NewStock)> changedItems = [];

        foreach (var dto in items)
        {
            Item? item = dto.ItemId > 0
                ? await unitOfWork.Items.GetByIdAsync(dto.ItemId)
                : await unitOfWork.Items.GetByBarcodeAsync(dto.Barcode ?? string.Empty);

            if (item == null || dto.Quantity <= 0)
                continue;

            int oldStock = item.Stock;
            int appliedQuantity;

            if (isIngress)
            {
                appliedQuantity = dto.Quantity;
                item.Stock += appliedQuantity;
            }
            else
            {
                if (oldStock == 0)
                    continue;
                appliedQuantity = Math.Min(oldStock, dto.Quantity);
                item.Stock -= appliedQuantity;
            }

            unitOfWork.Items.Update(item);

            await unitOfWork.InventoryMovements.AddAsync(new()
            {
                ItemId = item.Id,
                Type = isIngress ? DomainInventoryMovementType.MassiveStockIngress : DomainInventoryMovementType.MassiveStockEgress,
                Quantity = appliedQuantity,
                Date = DateTime.UtcNow,
                Reason = isIngress ? InventoryReasons.MASSIVE_INGRESS : InventoryReasons.MASSIVE_EGRESS,
                Responsible = userName
            });

            var movementType = isIngress ? DomainInventoryMovementType.MassiveStockIngress : DomainInventoryMovementType.MassiveStockEgress;
            await LogAuditAsync(nameof(Item), item.Id,
                ((VetManagement.Shared.Enums.InventoryMovementType)movementType).ToAuditActionType(),
                $"Stock before: {oldStock}, Stock after: {item.Stock}, Applied quantity: {appliedQuantity}, ID: {item.Id}, Barcode: {item.Barcode}", userName);

            changedItems.Add((item.Id, item.Stock));
            updatedCount++;
        }

        await unitOfWork.SaveChangesAsync();

        if (changedItems.Count > 0 && notificationService is not null)
        {
            foreach (var (itemId, newStock) in changedItems)
            {
                await notificationService.NotifyPropertyChangedAsync<Item>(itemId, nameof(Item.Stock), newStock);
            }

            await notificationService.NotifyCollectionChangedAsync<InventoryMovement>("MassUpdate");
        }

        return updatedCount;
    }

    #endregion

    #region Private Helpers

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
