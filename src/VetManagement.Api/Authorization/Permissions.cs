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
    }
}
