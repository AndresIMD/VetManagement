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
        ["Exams.Read"] = "exams.read",
        ["ExamsPerformed.Read"] = "examsperformed.read",
        ["ExternalLabs.Read"] = "externallabs.read",
        ["Scheduling.Read"] = "scheduling.read",
        ["Scheduling.Book"] = "scheduling.book",
        ["Scheduling.Manage"] = "scheduling.manage",
        ["Billing.Read"] = "billing.read",
        ["Billing.Charge"] = "billing.charge",
        ["Billing.Manage"] = "billing.manage",
        ["Medical.Read"] = "medical.read",
        ["Clinical.Manage"] = "clinical.manage"
    };

    public static IServiceCollection AddStaffAuthorization(this IServiceCollection services)
        => services.AddAuthorizationCore(options =>
        {
            foreach (var (name, permission) in Policies)
                options.AddPolicy(name, policy => policy.RequireClaim(PermissionClaimType, permission));
        });
}
