using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Contracts.Persistence;

/// <summary>The part of a movement the inventory metrics add up.</summary>
public sealed record MovementQuantity(int ItemId, InventoryMovementType Type, int Quantity, DateTime Date);

public interface IInventoryMovementRepository : IRepository<InventoryMovement>
{
    Task<IEnumerable<InventoryMovement>> GetByItemIdAsync(int itemId);

    /// <summary>Movements between two UTC instants (from inclusive, to exclusive), without the item or the texts.</summary>
    Task<List<MovementQuantity>> GetQuantitiesAsync(DateTime fromUtc, DateTime toUtc);

    Task<(List<InventoryMovement> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? itemId = null,
        InventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null);
}
