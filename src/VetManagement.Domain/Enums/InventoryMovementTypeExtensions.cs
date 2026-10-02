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
        _ => AuditActionType.None
    };
}
