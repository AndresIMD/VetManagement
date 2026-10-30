using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Contracts.Persistence;

/// <summary>The part of a movement the inventory metrics add up.</summary>
public sealed record MovementQuantity(int ItemId, InventoryMovementType Type, int Quantity, DateTime Date);

public interface IInventoryMovementRepository : IRepository<InventoryMovement>
{
    /// <summary>An item's movements, newest first, leaving out those of the hidden warehouses.</summary>
    Task<IEnumerable<InventoryMovement>> GetByItemIdAsync(int itemId, IReadOnlyCollection<int>? hiddenWarehouseIds = null);

    /// <summary>Movements between two UTC instants (from inclusive, to exclusive), without the item or the texts.</summary>
    Task<List<MovementQuantity>> GetQuantitiesAsync(DateTime fromUtc, DateTime toUtc);

    /// <summary>Whether any movement happened in the warehouse.</summary>
    Task<bool> AnyInWarehouseAsync(int warehouseId);

    /// <param name="hiddenWarehouseIds">Warehouses whose movements are left out.</param>
    /// <param name="warehouseId">Only this warehouse's movements (not the daily-use one: see <paramref name="onlyDailyUse"/>).</param>
    /// <param name="onlyDailyUse">Only the daily-use warehouse's movements, the ones without a warehouse.</param>
    Task<(List<InventoryMovement> Items, int TotalCount)> GetFilteredPagedAsync(
        int page = 0,
        int pageSize = 25,
        int? itemId = null,
        InventoryMovementType? type = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null,
        IReadOnlyCollection<int>? hiddenWarehouseIds = null,
        int? warehouseId = null,
        bool onlyDailyUse = false);
}
