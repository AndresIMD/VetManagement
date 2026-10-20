using Microsoft.Extensions.DependencyInjection;

namespace VetManagement.Staff.UI.Services.Auth;

/// <summary>
/// Authorization policies used by pages (<c>[Authorize(Policy = ...)]</c>). Names and permission claims match
/// the API's policies (Program.cs); <c>UiPolicyParityTests</c> fails if they drift. A page whose policy is
/// missing here crashes with "AuthorizationPolicy ... was not found".
/// </summary>
public static class StaffAuthorization
{
    public const string PermissionClaimType = "perm";

    public static readonly IReadOnlyDictionary<string, string> Policies = new Dictionary<string, string>
    {
        ["Users.Read"] = "users.read",
        ["Users.Create"] = "users.create",
        ["Users.Update"] = "users.update",
        ["Users.Delete"] = "users.delete",
        ["Inventory.Read"] = "inventory.read",
        ["Inventory.Delete"] = "inventory.delete",
        ["Inventory.Metrics"] = "inventory.metrics",
        ["ClientsPets.Read"] = "clientspets.read",
        ["ClientsPets.Create"] = "clientspets.create",
        ["Audit.Read"] = "audit.read",
        ["Exams.Read"] = "exams.read",
        ["Exams.Update"] = "exams.update",
        ["ExamsPerformed.Read"] = "examsperformed.read",
        ["ExamsPerformed.Create"] = "examsperformed.create",
        ["ExamsPerformed.Update"] = "examsperformed.update",
        ["ExamsPerformed.Delete"] = "examsperformed.delete",
        ["ExternalLabs.Read"] = "externallabs.read",
        ["ExternalLabs.Update"] = "externallabs.update",
        ["Scheduling.Read"] = "scheduling.read",
        ["Scheduling.Book"] = "scheduling.book",
        ["Scheduling.Manage"] = "scheduling.manage",
        ["Billing.Read"] = "billing.read",
        ["Billing.Charge"] = "billing.charge",
        ["Billing.Manage"] = "billing.manage",
        ["Medical.Read"] = "medical.read",
        ["Clinical.Manage"] = "clinical.manage",
        ["Reports.Read"] = "reports.read",
        ["System.SendEmail"] = "system.send-email",
        ["System.TestData"] = "system.test-data"
    };

    public static IServiceCollection AddStaffAuthorization(this IServiceCollection services)
        => services.AddAuthorizationCore(options =>
        {
            foreach (var (name, permission) in Policies)
                options.AddPolicy(name, policy => policy.RequireClaim(PermissionClaimType, permission));
        });
}
