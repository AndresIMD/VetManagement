using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetManagement.Api.Authorization;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;

namespace VetManagement.Api.Services;

public class UserManagementService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, AuditService? audit = null, EnabledModules? modules = null, UserAccess? access = null)
{
    /// <summary>True when the user is the only Admin who can still sign in, so blocking, deleting or demoting them would lock everyone out.</summary>
    public async Task<bool> IsLastActiveAdminAsync(IdentityUser user)
    {
        if (UserAccess.IsBlocked(user) || !await userManager.IsInRoleAsync(user, Permissions.ROLES.ADMIN))
            return false;
        var admins = await userManager.GetUsersInRoleAsync(Permissions.ROLES.ADMIN);
        return !admins.Any(a => a.Id != user.Id && !UserAccess.IsBlocked(a));
    }

    public async Task<IdentityResult> UpdateUserAsync(IdentityUser user, string userName, string email)
    {
        user.UserName = userName.Trim();
        user.Email = email.Trim();
        var result = await userManager.UpdateAsync(user);
        access?.Forget(user.Id);
        return result;
    }

    /// <summary>Blocks (no end date) or unblocks the account; a blocked user can't sign in and their open sessions stop working.</summary>
    public async Task<IdentityResult> SetBlockedAsync(IdentityUser user, bool blocked)
    {
        var result = await userManager.SetLockoutEnabledAsync(user, true);
        if (result.Succeeded)
            result = await userManager.SetLockoutEndDateAsync(user, blocked ? DateTimeOffset.MaxValue : null);
        if (result.Succeeded && !blocked)
            await userManager.ResetAccessFailedCountAsync(user);
        access?.Forget(user.Id);
        return result;
    }

    /// <summary>Sets a temporary password that the user must change at their next sign-in.</summary>
    public async Task<IdentityResult> ResetPasswordAsync(IdentityUser user, string newPassword)
    {
        // Validate first: RemovePassword would otherwise leave the account without a password if the new one is refused.
        var errors = new List<IdentityError>();
        foreach (var validator in userManager.PasswordValidators)
            errors.AddRange((await validator.ValidateAsync(userManager, user, newPassword)).Errors);
        if (errors.Count > 0)
            return IdentityResult.Failed([.. errors]);

        var result = await userManager.RemovePasswordAsync(user);
        if (result.Succeeded)
            result = await userManager.AddPasswordAsync(user, newPassword);
        if (!result.Succeeded)
            return result;

        var claims = await userManager.GetClaimsAsync(user);
        if (!claims.Any(c => c.Type == SystemClaims.CLAIM_TYPE && c.Value == SystemClaims.MUST_CHANGE_PASSWORD))
            await userManager.AddClaimAsync(user, new Claim(SystemClaims.CLAIM_TYPE, SystemClaims.MUST_CHANGE_PASSWORD));
        await userManager.ResetAccessFailedCountAsync(user);
        access?.Forget(user.Id); // the new password came with a new security stamp: open sessions end now
        return result;
    }

    /// <summary>Deletes the account. The audit trail keeps the user name; blocking is the usual choice for staff who leave.</summary>
    public async Task<IdentityResult> DeleteUserAsync(IdentityUser user)
    {
        var result = await userManager.DeleteAsync(user);
        access?.Forget(user.Id);
        return result;
    }

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
        access?.Forget(user.Id);

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
        access?.Forget(user.Id);

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

    private HashSet<string> GetClaimsForRoles(IEnumerable<string> roles)
        => modules?.Filter(Permissions.ForRoles(roles)) ?? Permissions.ForRoles(roles);
}

