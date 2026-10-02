using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetManagement.Api.Authorization;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;

namespace VetManagement.Api.Services;

public class UserManagementService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, AuditService? audit = null)
{
    public async Task<List<IdentityUser>> GetAllUsersAsync()
        => await userManager.Users.ToListAsync();

    public async Task<IList<string>> GetUserRolesAsync(IdentityUser user)
        => await userManager.GetRolesAsync(user);

    public async Task<List<string>> GetAllRolesAsync()
        => await roleManager.Roles.Select(r => r.Name!).ToListAsync();

    public async Task<IdentityResult> CreateUserAsync(string username, string email, string password, List<string> roles)
    {
        var user = new IdentityUser { UserName = username, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            return result;

        await userManager.AddToRolesAsync(user, roles);
        await SyncClaimsForUserAsync(user);

        if (audit != null)
            await audit.LogSecurityAsync(AuditActionType.ClaimsSynced, null, $"TargetUserId={user.Id}; AfterCreateRoles=[{string.Join(',', roles)}]");

        return result;
    }

    public async Task<bool> AssignRoleAsync(string userId, string role)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null || !await roleManager.RoleExistsAsync(role))
            return false;

        var result = await userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
            return false;

        await SyncClaimsForUserAsync(user);

        if (audit != null)
            await audit.LogSecurityAsync(AuditActionType.ClaimsSynced, null, $"TargetUserId={user.Id}; RoleAdded={role}");

        return true;
    }

    public async Task<bool> RemoveRoleAsync(string userId, string role)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null || !await roleManager.RoleExistsAsync(role))
            return false;

        var result = await userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
            return false;

        await SyncClaimsForUserAsync(user);

        if (audit != null)
            await audit.LogSecurityAsync(AuditActionType.ClaimsSynced, null, $"TargetUserId={user.Id}; RoleRemoved={role}");

        return true;
    }

    public async Task SyncClaimsForUserAsync(IdentityUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var targetClaims = GetClaimsForRoles(roles);
        var existingClaims = await userManager.GetClaimsAsync(user);
        var existingPerms = existingClaims.Where(c => c.Type == Permissions.CLAIM_TYPE).ToList();
        var added = new List<string>();
        var removed = new List<string>();

        foreach (var perm in targetClaims)
            if (!existingPerms.Any(c => c.Value == perm))
            {
                await userManager.AddClaimAsync(user, new Claim(Permissions.CLAIM_TYPE, perm));
                added.Add(perm);
            }

        foreach (var stale in existingPerms.Where(c => !targetClaims.Contains(c.Value)))
        {
            await userManager.RemoveClaimAsync(user, stale);
            removed.Add(stale.Value);
        }

        if ((added.Count > 0 || removed.Count > 0) && audit != null)
        {
            var correlationId = System.Guid.NewGuid().ToString("D");
            await audit.LogSecurityAsync(AuditActionType.ClaimsSynced, correlationId, $"TargetUserId={user.Id}; Added=[{string.Join(',', added)}]; Removed=[{string.Join(',', removed)}]");
        }
    }

    private static HashSet<string> GetClaimsForRoles(IEnumerable<string> roles)
    {
        HashSet<string> set = [];
        foreach (var role in roles)
        {
            switch (role)
            {
                case "Admin":
                    foreach (var p in Permissions.ALL())
                        set.Add(p);
                    break;
                case "Manager":
                    set.Add(Permissions.INVENTORY.READ);
                    set.Add(Permissions.INVENTORY.CREATE);
                    set.Add(Permissions.INVENTORY.UPDATE);
                    set.Add(Permissions.EXAMS.READ);
                    set.Add(Permissions.EXAMS.UPDATE);
                    set.Add(Permissions.EXTERNAL_LABS.READ);
                    set.Add(Permissions.EXTERNAL_LABS.UPDATE);
                    set.Add(Permissions.EXAMS_PERFORMED.READ);
                    set.Add(Permissions.EXAMS_PERFORMED.CREATE);
                    set.Add(Permissions.EXAMS_PERFORMED.UPDATE);
                    set.Add(Permissions.MEDICAL.READ);
                    set.Add(Permissions.MEDICAL.CREATE);
                    set.Add(Permissions.MEDICAL.UPDATE);
                    set.Add(Permissions.CLIENTS_PETS.READ);
                    set.Add(Permissions.CLIENTS_PETS.CREATE);
                    set.Add(Permissions.CLIENTS_PETS.UPDATE);
                    set.Add(Permissions.AUDIT.READ);
                    set.Add(Permissions.SYSTEM.SEND_EMAIL);
                    break;
                case "Employee":
                    set.Add(Permissions.INVENTORY.READ);
                    set.Add(Permissions.INVENTORY.CREATE);
                    set.Add(Permissions.INVENTORY.UPDATE);
                    set.Add(Permissions.EXAMS.READ);
                    set.Add(Permissions.EXTERNAL_LABS.READ);
                    set.Add(Permissions.EXAMS_PERFORMED.READ);
                    set.Add(Permissions.EXAMS_PERFORMED.CREATE);
                    set.Add(Permissions.MEDICAL.READ);
                    set.Add(Permissions.MEDICAL.CREATE);
                    set.Add(Permissions.CLIENTS_PETS.READ);
                    set.Add(Permissions.CLIENTS_PETS.CREATE);
                    break;
            }
        }
        return set;
    }
}

