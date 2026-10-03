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
    }

    public static class SYSTEM
    {
        public const string SEND_EMAIL = "system.send-email";
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

    /// <summary>Single source of the permissions each role grants (seeding and user management both use it).</summary>
    public static IEnumerable<string> ForRole(string role) => role switch
    {
        "Admin" => ALL(),
        "Manager" =>
        [
            INVENTORY.READ, INVENTORY.CREATE, INVENTORY.UPDATE,
            EXAMS.READ, EXAMS.UPDATE,
            EXTERNAL_LABS.READ, EXTERNAL_LABS.UPDATE,
            EXAMS_PERFORMED.READ, EXAMS_PERFORMED.CREATE, EXAMS_PERFORMED.UPDATE,
            MEDICAL.READ, MEDICAL.CREATE, MEDICAL.UPDATE,
            CLIENTS_PETS.READ, CLIENTS_PETS.CREATE, CLIENTS_PETS.UPDATE,
            AUDIT.READ,
            SYSTEM.SEND_EMAIL,
            SCHEDULING.READ, SCHEDULING.BOOK,
            BILLING.READ, BILLING.CHARGE
        ],
        "Employee" =>
        [
            INVENTORY.READ, INVENTORY.CREATE, INVENTORY.UPDATE,
            EXAMS.READ,
            EXTERNAL_LABS.READ,
            EXAMS_PERFORMED.READ, EXAMS_PERFORMED.CREATE,
            MEDICAL.READ, MEDICAL.CREATE,
            CLIENTS_PETS.READ, CLIENTS_PETS.CREATE,
            SCHEDULING.READ, SCHEDULING.BOOK,
            BILLING.READ, BILLING.CHARGE
        ],
        _ => []
    };

    public static IEnumerable<string> ALL()
    {
        yield return INVENTORY.READ;
        yield return INVENTORY.CREATE;
        yield return INVENTORY.UPDATE;
        yield return INVENTORY.DELETE;
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
        yield return SYSTEM.SEND_EMAIL;
        yield return SCHEDULING.READ;
        yield return SCHEDULING.BOOK;
        yield return SCHEDULING.MANAGE;
        yield return BILLING.READ;
        yield return BILLING.CHARGE;
        yield return BILLING.MANAGE;
    }
}
