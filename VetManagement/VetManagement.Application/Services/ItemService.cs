using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Application.Services;

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

    public async Task<bool> AddAsync(Item item, string userName)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name) || item.Type == ItemType.None)
            return false;

        await unitOfWork.Items.AddAsync(item);
        await unitOfWork.SaveChangesAsync();

        await LogAuditAsync(item.Id, AuditActionType.Add, JsonSerializer.Serialize(item), userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Item>(item.Id, "Add", new { item.Name, item.Stock });
            await notificationService.NotifyCollectionChangedAsync<Item>("Add");
        }

        return true;
    }

    public async Task<bool> UpdateAsync(Item item, Item? oldData, string userName)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name) || item.Type == ItemType.None)
            return false;

        unitOfWork.Items.Update(item);
        await unitOfWork.SaveChangesAsync();

        await LogAuditAsync(item.Id, AuditActionType.Edit,
            $"Before: {JsonSerializer.Serialize(oldData)}\nAfter: {JsonSerializer.Serialize(item)}", userName);

        if (notificationService is not null)
        {
            await notificationService.NotifyEntityChangedAsync<Item>(item.Id, "Update", new { item.Name, item.Stock });

            if (oldData?.Stock != item.Stock)
                await notificationService.NotifyPropertyChangedAsync<Item>(item.Id, nameof(Item.Stock), item.Stock);
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
