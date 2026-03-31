namespace VetManagement.Shared.Constants;

public static class ApiRouteConstants
{
    // Items
    public const string ITEMS_BASE = "api/items";
    public const string ITEMS_BY_ID = "api/items/{0}";
    public const string ITEMS_BY_BARCODE = "api/items/barcode/{0}";
    public const string ITEMS_PAGED = "api/items/paged";

    // Inventory Movements
    public const string INVENTORY_MOVEMENTS_BASE = "api/inventory/movements";
    public const string INVENTORY_MOVEMENT_BY_ITEM = "api/inventory/movements/{0}";
    public const string INVENTORY_ADJUST_STOCK = "api/inventory/movements/adjust-stock";
    public const string INVENTORY_MASS_INGRESS = "api/inventory/movements/mass-ingress";
    public const string INVENTORY_MASS_EGRESS = "api/inventory/movements/mass-egress";

    // Clients & Pets
    public const string CLIENTS = "api/clientpet/clients";
    public const string CLIENT_BY_ID = "api/clientpet/clients/{0}";
    public const string PETS = "api/clientpet/pets";
    public const string PET_BY_ID = "api/clientpet/pets/{0}";

    // Exams
    public const string EXAMS = "api/exams";
    public const string EXAM_BY_ID = "api/exams/{0}";

    // Medical Visits
    public const string MEDICAL_VISITS = "api/medical-visits";
    public const string MEDICAL_VISIT_BY_ID = "api/medical-visits/{0}";

    // Account
    public const string ACCOUNT_LOGIN = "api/account/login";
    public const string ACCOUNT_REGISTER = "api/account/register";
    public const string ACCOUNT_CONFIRM_EMAIL = "api/account/confirm-email";
    public const string ACCOUNT_USER_INFO = "api/account/user-info";
    public const string ACCOUNT_CHANGE_PASSWORD = "api/account/change-password";

    // User Management (Admin)
    public const string USERS_BASE = "api/admin/users";
    public const string USER_BY_ID = "api/admin/users/{0}";
    public const string USER_ROLES = "api/admin/users/{0}/roles";
    public const string USER_CLAIMS = "api/admin/users/{0}/claims";
    public const string USERS_ROLES_ALL = "api/admin/users/roles";

    // Audit
    public const string AUDIT_LOGS = "api/audit/logs";

    // External Labs
    public const string EXTERNAL_LABS_BASE = "api/external-labs";
    public const string EXTERNAL_LAB_BY_ID = "api/external-labs/{0}";

    // Exams Performed
    public const string EXAMS_PERFORMED_BASE = "api/exams-performed";
    public const string EXAM_PERFORMED_BY_ID = "api/exams-performed/{0}";

    // Inventory Alerts
    public const string INVENTORY_ALERTS_BASE = "api/inventory-alerts";
    public const string INVENTORY_ALERTS_EMAIL = "api/inventory-alerts/send-email";
}
