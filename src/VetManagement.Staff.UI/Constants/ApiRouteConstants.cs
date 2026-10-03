namespace VetManagement.Staff.UI.Constants;

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
    public const string ACCOUNT_USER_INFO = "api/account/user-info";
    public const string ACCOUNT_CHANGE_PASSWORD = "api/account/change-password";

    // User Management (Admin)
    public const string USERS_BASE = "api/admin/users";
    public const string USER_ROLES = "api/admin/users/{0}/roles";
    public const string USER_CLAIMS = "api/admin/users/{0}/claims";
    public const string USERS_ROLES_ALL = "api/admin/users/roles";

    // Audit
    public const string AUDIT_LOGS_PAGED = "api/audit/logs/paged";

    // External Labs
    public const string EXTERNAL_LABS_BASE = "api/external-labs";
    public const string EXTERNAL_LAB_BY_ID = "api/external-labs/{0}";

    // Exams Performed
    public const string EXAMS_PERFORMED_BASE = "api/exams-performed";
    public const string EXAM_PERFORMED_BY_ID = "api/exams-performed/{0}";

    // Inventory Alerts
    public const string INVENTORY_ALERTS_SCHEDULE = "api/inventory-alerts/schedule";
    public const string SEND_EMAIL = "api/email/send";

    // Scheduling (agenda)
    public const string SCHEDULING_SETTINGS = "api/scheduling/settings";
    public const string SCHEDULING_AVAILABILITY = "api/scheduling/availability";
    public const string SCHEDULING_APPOINTMENTS = "api/scheduling/appointments";
    public const string SCHEDULING_APPOINTMENT_RESCHEDULE = "api/scheduling/appointments/{0}/reschedule";
    public const string SCHEDULING_APPOINTMENT_CANCEL = "api/scheduling/appointments/{0}/cancel";

    // Billing
    public const string BILLING_SALES = "api/billing/sales";
    public const string BILLING_SALE_BY_ID = "api/billing/sales/{0}";
    public const string BILLING_SALE_LINES = "api/billing/sales/{0}/lines";
    public const string BILLING_SALE_PAYMENTS = "api/billing/sales/{0}/payments";
    public const string BILLING_SALE_VOID = "api/billing/sales/{0}/void";
    public const string BILLING_CASH_DAY = "api/billing/cash/{0}";
    public const string BILLING_CASH_CLOSE = "api/billing/cash/close";
    public const string BILLING_SETTINGS = "api/billing/settings";
    public const string BILLING_VISIT_CHARGE = "api/billing/visits/{0}/charge";

    // Clinical record
    public const string CLINICAL_PET_HISTORY = "api/clinical/pets/{0}/history";
    public const string CLINICAL_PET_DOSES = "api/clinical/pets/{0}/doses";
    public const string CLINICAL_DOSE = "api/clinical/doses/{0}";
    public const string CLINICAL_VISIT_SUPPLIES = "api/clinical/visits/{0}/supplies";
    public const string CLINICAL_SUPPLY = "api/clinical/supplies/{0}";
    public const string CLINICAL_DUE = "api/clinical/due";
    public const string CLINICAL_SETTINGS = "api/clinical/settings";

    // Sample data (development only)
    public const string TEST_DATA = "api/dev/test-data";

    // Reports
    public const string REPORTS_SUMMARY = "api/reports/summary";
}
