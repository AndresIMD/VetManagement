using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Inventory;

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
}
