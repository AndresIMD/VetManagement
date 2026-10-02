using VetManagement.Domain.Enums;

namespace VetManagement.Shared.Helpers;

/// <summary>
/// Helper methods for filtering audit action types by domain category.
/// </summary>
public static class AuditActionHelper
{
    /// <summary>
    /// Gets audit actions related to inventory operations (100-199 range).
    /// </summary>
    public static List<AuditActionType> GetInventoryActions() =>
    [
        AuditActionType.Add,
        AuditActionType.Edit,
        AuditActionType.Delete,
        AuditActionType.Ingress,
        AuditActionType.Egress,
        AuditActionType.Adjustment,
        AuditActionType.MassiveStockIngress,
        AuditActionType.MassiveStockEgress
    ];

    /// <summary>
    /// Gets audit actions related to security operations (200-299 range).
    /// </summary>
    public static List<AuditActionType> GetSecurityActions() =>
    [
        AuditActionType.UserLogin,
        AuditActionType.UserLoginFailed,
        AuditActionType.PasswordChanged,
        AuditActionType.UserCreated,
        AuditActionType.RoleAssigned,
        AuditActionType.RoleRemoved,
        AuditActionType.ClaimsSynced,
        AuditActionType.SetupBootstrap,
        AuditActionType.SetupBootstrapFailed
    ];

    /// <summary>
    /// Gets basic CRUD actions (0-99 range).
    /// </summary>
    public static List<AuditActionType> GetCrudActions() =>
    [
        AuditActionType.Add,
        AuditActionType.Edit,
        AuditActionType.Delete
    ];

    /// <summary>
    /// Gets actions for a specific entity type by category.
    /// </summary>
    public static List<AuditActionType> GetActionsForEntity(string entityName) => entityName switch
    {
        "Item" => GetInventoryActions(),
        "User" or "Role" => GetSecurityActions(),
        "Client" or "Pet" or "Visit" or "Exam" => GetCrudActions(),
        _ => GetCrudActions()
    };

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
