using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;

namespace VetManagement.Application.Inventory;

/// <summary>Automatic stock changes (sales, supplies used in visits, vaccines applied), always with a traceable movement.</summary>
public static class Stock
{
    /// <summary>
    /// Lock held by every automatic stock change and by billing, so two of them never read the same stock and
    /// overwrite each other. ponytail: one clinic-wide key; per-item keys if a clinic ever outgrows it.
    /// Manual adjustments in the inventory screens don't take it yet.
    /// </summary>
    public const string LockKey = "stock";

    /// <summary>Moves <paramref name="quantity"/> units out of (Egress) or back into (Ingress) stock. Null if the item no longer exists.</summary>
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
}
