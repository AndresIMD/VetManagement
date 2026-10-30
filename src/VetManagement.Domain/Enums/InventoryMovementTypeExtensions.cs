namespace VetManagement.Domain.Enums;

public static class InventoryMovementTypeExtensions
{
    /// <summary>
    /// Converts an InventoryMovementType to its corresponding AuditActionType.
    /// </summary>
    public static AuditActionType ToAuditActionType(this InventoryMovementType movementType) => movementType switch
    {
        InventoryMovementType.Ingress => AuditActionType.Ingress,
        InventoryMovementType.Egress => AuditActionType.Egress,
        InventoryMovementType.Adjustment => AuditActionType.Adjustment,
        InventoryMovementType.MassiveStockIngress => AuditActionType.MassiveStockIngress,
        InventoryMovementType.MassiveStockEgress => AuditActionType.MassiveStockEgress,
        InventoryMovementType.TransferOut or InventoryMovementType.TransferIn => AuditActionType.Transfer,
        _ => AuditActionType.None
    };

    /// <summary>A move between warehouses: the clinic's stock neither grows nor shrinks.</summary>
    public static bool IsTransfer(this InventoryMovementType movementType)
        => movementType is InventoryMovementType.TransferOut or InventoryMovementType.TransferIn;
}
