using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Contracts.Persistence;

public interface IInventoryMovementRepository : IRepository<InventoryMovement>
{
    Task<IEnumerable<InventoryMovement>> GetFilteredAsync(
        string? itemName = null,
        InventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null);

    Task<IEnumerable<InventoryMovement>> GetByItemIdAsync(int itemId);

    Task<(List<InventoryMovement> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? itemId = null,
        InventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null);
}
