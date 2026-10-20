using VetManagement.Domain.Enums;

namespace VetManagement.Api.Authorization;

/// <summary>
/// Application permission claims.
/// </summary>
public static class Permissions
{
    public const string CLAIM_TYPE = "perm";

    public static class INVENTORY
    {
        public const string READ = "inventory.read";
        public const string CREATE = "inventory.create";
        public const string UPDATE = "inventory.update";
        public const string DELETE = "inventory.delete";
        /// <summary>Stock metrics: consumption, value and trends (management data).</summary>
        public const string METRICS = "inventory.metrics";
    }

    public static class EXAMS
    {
        public const string READ = "exams.read";
        public const string CREATE = "exams.create";
        public const string UPDATE = "exams.update";
        public const string DELETE = "exams.delete";
    }

    public static class EXTERNAL_LABS
    {
        public const string READ = "externallabs.read";
        public const string CREATE = "externallabs.create";
        public const string UPDATE = "externallabs.update";
        public const string DELETE = "externallabs.delete";
    }

    public static class EXAMS_PERFORMED
    {
        public const string READ = "examsperformed.read";
        public const string CREATE = "examsperformed.create";
        public const string UPDATE = "examsperformed.update";
        public const string DELETE = "examsperformed.delete";
    }

    public static class MEDICAL
    {
        public const string READ = "medical.read";
        public const string CREATE = "medical.create";
        public const string UPDATE = "medical.update";
        public const string DELETE = "medical.delete";
    }

    public static class CLIENTS_PETS
    {
        public const string READ = "clientspets.read";
        public const string CREATE = "clientspets.create";
        public const string UPDATE = "clientspets.update";
        public const string DELETE = "clientspets.delete";
    }

    public static class AUDIT
    {
        public const string READ = "audit.read";
    }

    public static class USERS
    {
        public const string READ = "users.read";
        public const string CREATE = "users.create";
        public const string MANAGE_ROLES = "users.roles.manage";
        /// <summary>Edit, block and reset the password of other users.</summary>
        public const string UPDATE = "users.update";
        public const string DELETE = "users.delete";
    }

    public static class SYSTEM
    {
        /// <summary>Email sending and email alerts: Admin only until a clinic has its mail set up and tested.</summary>
        public const string SEND_EMAIL = "system.send-email";
        /// <summary>Create sample data (development/test environments only).</summary>
        public const string TEST_DATA = "system.test-data";
    }

    public static class SCHEDULING
    {
        public const string READ = "scheduling.read";
        /// <summary>Create, cancel and reassign appointments.</summary>
        public const string BOOK = "scheduling.book";
        /// <summary>Edit the clinic's agenda configuration.</summary>
        public const string MANAGE = "scheduling.manage";
    }

    public static class BILLING
    {
        public const string READ = "billing.read";
        /// <summary>Open sales, add lines and payments, close the day's cash.</summary>
        public const string CHARGE = "billing.charge";
        /// <summary>Void sales, discounts above the clinic's limit, billing settings.</summary>
        public const string MANAGE = "billing.manage";
    }

    public static class REPORTS
    {
        /// <summary>Management dashboard: agenda use, income, stock value, clinical activity.</summary>
        public const string READ = "reports.read";
    }

    public static class CLINICAL
    {
        /// <summary>Edit the clinic's preventive protocols and reminders (records use the medical.* permissions).</summary>
        public const string MANAGE = "clinical.manage";
    }

    /// <summary>Roles the system knows. A user may have several; their permissions add up.</summary>
    public static class ROLES
    {
        public const string ADMIN = "Admin";
        public const string INVENTORY = "Inventory";
        public const string LABORATORY = "Laboratory";
        public const string RECEPTION = "Reception";
        public const string CLINICAL = "Clinical";
        public const string CASHIER = "Cashier";
        /// <summary>Adds deleting, settings and management data (e.g. inventory metrics) in the areas the user already has, plus audit and the reports dashboard.</summary>
        public const string SUPERVISOR = "Supervisor";

        public static readonly string[] ALL = [ADMIN, INVENTORY, LABORATORY, RECEPTION, CLINICAL, CASHIER, SUPERVISOR];
    }

    /// <summary>An area role: what its staff do every day, and what a supervisor of the area may also do.</summary>
    private sealed record Area(string Role, string[] Work, string[] Supervise);

    private static readonly Area[] AREAS =
    [
        new(ROLES.INVENTORY,
            [INVENTORY.READ, INVENTORY.CREATE, INVENTORY.UPDATE],
            [INVENTORY.DELETE, INVENTORY.METRICS]),
        new(ROLES.LABORATORY,
            [EXAMS.READ, EXTERNAL_LABS.READ, EXAMS_PERFORMED.READ, EXAMS_PERFORMED.CREATE, EXAMS_PERFORMED.UPDATE,
             CLIENTS_PETS.READ, CLIENTS_PETS.CREATE, CLIENTS_PETS.UPDATE],
            [EXAMS.CREATE, EXAMS.UPDATE, EXAMS.DELETE, EXTERNAL_LABS.CREATE, EXTERNAL_LABS.UPDATE, EXTERNAL_LABS.DELETE, EXAMS_PERFORMED.DELETE]),
        new(ROLES.RECEPTION,
            [CLIENTS_PETS.READ, CLIENTS_PETS.CREATE, CLIENTS_PETS.UPDATE, SCHEDULING.READ, SCHEDULING.BOOK],
            [CLIENTS_PETS.DELETE, SCHEDULING.MANAGE]),
        new(ROLES.CLINICAL,
            [MEDICAL.READ, MEDICAL.CREATE, MEDICAL.UPDATE, CLIENTS_PETS.READ, CLIENTS_PETS.CREATE, CLIENTS_PETS.UPDATE,
             EXAMS.READ, EXAMS_PERFORMED.READ, EXAMS_PERFORMED.CREATE, SCHEDULING.READ, INVENTORY.READ],
            [MEDICAL.DELETE, CLINICAL.MANAGE]),
        new(ROLES.CASHIER,
            [BILLING.READ, BILLING.CHARGE, CLIENTS_PETS.READ, SCHEDULING.READ, INVENTORY.READ],
            [BILLING.MANAGE])
    ];

    /// <summary>
    /// Single source of the permissions roles grant (seeding and user management both use it): Admin has all; each
    /// area role its daily work; Supervisor adds the supervision permissions of the user's areas, audit and reports.
    /// </summary>
    public static HashSet<string> ForRoles(IEnumerable<string> roles)
    {
        var set = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (set.Contains(ROLES.ADMIN))
            return [.. ALL()];

        var areas = AREAS.Where(a => set.Contains(a.Role)).ToList();
        var permissions = areas.SelectMany(a => a.Work).ToHashSet();
        if (set.Contains(ROLES.SUPERVISOR) && areas.Count > 0)
        {
            permissions.UnionWith(areas.SelectMany(a => a.Supervise));
            permissions.UnionWith([AUDIT.READ, REPORTS.READ]);
        }
        return permissions;
    }

    public static HashSet<string> ForRole(string role) => ForRoles([role]);

    /// <summary>The module a permission belongs to; null for the ones every clinic has (users, audit, system).</summary>
    public static AppModule? ModuleOf(string permission) => permission.Split('.')[0] switch
    {
        "inventory" => AppModule.Inventory,
        "exams" or "externallabs" or "examsperformed" => AppModule.Exams,
        "clientspets" => AppModule.Clients,
        "medical" or "clinical" => AppModule.Clinical,
        "scheduling" => AppModule.Scheduling,
        "billing" => AppModule.Billing,
        "reports" => AppModule.Reports,
        _ => null
    };

    public static IEnumerable<string> ALL()
    {
        yield return INVENTORY.READ;
        yield return INVENTORY.CREATE;
        yield return INVENTORY.UPDATE;
        yield return INVENTORY.DELETE;
        yield return INVENTORY.METRICS;
        yield return EXAMS.READ;
        yield return EXAMS.CREATE;
        yield return EXAMS.UPDATE;
        yield return EXAMS.DELETE;
        yield return EXTERNAL_LABS.READ;
        yield return EXTERNAL_LABS.CREATE;
        yield return EXTERNAL_LABS.UPDATE;
        yield return EXTERNAL_LABS.DELETE;
        yield return EXAMS_PERFORMED.READ;
        yield return EXAMS_PERFORMED.CREATE;
        yield return EXAMS_PERFORMED.UPDATE;
        yield return EXAMS_PERFORMED.DELETE;
        yield return MEDICAL.READ;
        yield return MEDICAL.CREATE;
        yield return MEDICAL.UPDATE;
        yield return MEDICAL.DELETE;
        yield return CLIENTS_PETS.READ;
        yield return CLIENTS_PETS.CREATE;
        yield return CLIENTS_PETS.UPDATE;
        yield return CLIENTS_PETS.DELETE;
        yield return AUDIT.READ;
        yield return USERS.READ;
        yield return USERS.CREATE;
        yield return USERS.MANAGE_ROLES;
        yield return USERS.UPDATE;
        yield return USERS.DELETE;
        yield return SYSTEM.SEND_EMAIL;
        yield return SCHEDULING.READ;
        yield return SCHEDULING.BOOK;
        yield return SCHEDULING.MANAGE;
        yield return BILLING.READ;
        yield return BILLING.CHARGE;
        yield return BILLING.MANAGE;
        yield return CLINICAL.MANAGE;
        yield return REPORTS.READ;
        yield return SYSTEM.TEST_DATA;
    }
}
