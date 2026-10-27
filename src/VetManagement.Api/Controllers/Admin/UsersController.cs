using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Authorization;
using VetManagement.Api.Services;
using VetManagement.Application.Services;
using VetManagement.Contracts.Accounts;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize]
public class UsersController(
    UserManagementService userService,
    UserManager<IdentityUser> userManager,
    AuditService audit) : ApiControllerBase
{
    /// <summary>Users with their roles; never the Identity record itself (it carries the password hash).</summary>
    [HttpGet]
    [Authorize(Policy = "Users.Read")]
    public async Task<ActionResult<List<UserDto>>> GetAllAsync()
    {
        var result = new List<UserDto>();
        // One at a time: the user manager shares one DbContext.
        foreach (var user in await userService.GetAllUsersAsync())
            result.Add(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Roles = [.. await userService.GetUserRolesAsync(user)],
                IsBlocked = UserAccess.IsBlocked(user)
            });
        return Ok(result.OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase).ToList());
    }

    [HttpGet("roles")]
    [Authorize(Policy = "Users.Read")]
    public async Task<ActionResult<List<string>>> GetAllRolesAsync()
    {
        var roles = await userService.GetAllRolesAsync();
        return Ok(roles);
    }

    [HttpGet("{id}/roles")]
    [Authorize(Policy = "Users.Read")]
    public async Task<ActionResult<IList<string>>> GetUserRolesAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        var roles = await userService.GetUserRolesAsync(user);
        return Ok(roles);
    }

    [HttpPost]
    [Authorize(Policy = "Users.Create")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateUserRequest request)
    {
        var result = await userService.CreateUserAsync(request.UserName, request.Email, request.Password, request.Roles);
        if (!result.Succeeded)
            return IdentityProblem(result);
        await audit.LogSecurityAsync(AuditActionType.UserCreated, User.Identity?.Name, $"Target={request.UserName}; Roles=[{string.Join(',', request.Roles)}]");
        return Ok();
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Users.Update")]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] EditUserRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        var before = $"{user.UserName} <{user.Email}>";
        var result = await userService.UpdateUserAsync(user, request.UserName, request.Email);
        if (!result.Succeeded)
            return IdentityProblem(result);
        await audit.LogSecurityAsync(AuditActionType.UserUpdated, User.Identity?.Name, $"TargetUserId={id}; Before={before}; After={user.UserName} <{user.Email}>");
        return Ok();
    }

    /// <summary>
    /// Blocks the account (e.g. staff who left the clinic): no sign-in, open sessions stop working and every role is
    /// taken away; unblocking doesn't give them back, so the audit entry keeps the roles the user had.
    /// </summary>
    [HttpPost("{id}/block")]
    [Authorize(Policy = "Users.Update")]
    public Task<IActionResult> BlockAsync(string id) => SetBlockedAsync(id, true);

    [HttpPost("{id}/unblock")]
    [Authorize(Policy = "Users.Update")]
    public Task<IActionResult> UnblockAsync(string id) => SetBlockedAsync(id, false);

    private async Task<IActionResult> SetBlockedAsync(string id, bool blocked)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        if (blocked && await ProtectedAccountProblemAsync(user) is { } problem)
            return problem;
        var result = await userService.SetBlockedAsync(user, blocked);
        if (!result.Succeeded)
            return IdentityProblem(result);
        // Roles go after the block: if removing them fails, the account is still blocked.
        var rolesResult = IdentityResult.Success;
        IList<string> removedRoles = [];
        if (blocked)
            (rolesResult, removedRoles) = await userService.RemoveAllRolesAsync(user);
        var details = $"TargetUserId={id}; Target={user.UserName}";
        if (removedRoles.Count > 0)
            details += $"; RolesRemoved=[{string.Join(',', removedRoles)}]";
        await audit.LogSecurityAsync(blocked ? AuditActionType.UserBlocked : AuditActionType.UserUnblocked, User.Identity?.Name, details);
        return rolesResult.Succeeded ? Ok() : IdentityProblem(rolesResult);
    }

    [HttpPost("{id}/reset-password")]
    [Authorize(Policy = "Users.Update")]
    public async Task<IActionResult> ResetPasswordAsync(string id, [FromBody] ResetPasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        var result = await userService.ResetPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);
        await audit.LogSecurityAsync(AuditActionType.PasswordReset, User.Identity?.Name, $"TargetUserId={id}; Target={user.UserName}");
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Users.Delete")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        if (await ProtectedAccountProblemAsync(user) is { } problem)
            return problem;
        var result = await userService.DeleteUserAsync(user);
        if (!result.Succeeded)
            return IdentityProblem(result);
        await audit.LogSecurityAsync(AuditActionType.UserDeleted, User.Identity?.Name, $"TargetUserId={id}; Target={user.UserName}");
        return Ok();
    }

    [HttpPost("{id}/roles/{role}")]
    [Authorize(Policy = "Users.ManageRoles")]
    public async Task<IActionResult> AssignRoleAsync(string id, string role)
    {
        // A blocked account keeps no roles (see BlockAsync).
        if (await userManager.FindByIdAsync(id) is { } user && UserAccess.IsBlocked(user))
            return BadRequestMessage(Texts.T("Unblock the account before assigning it roles."));
        var ok = await userService.AssignRoleAsync(id, role);
        if (!ok)
            return BadRequest();
        await audit.LogSecurityAsync(AuditActionType.RoleAssigned, User.Identity?.Name, $"TargetUserId={id}; Role={role}");
        return Ok();
    }

    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Policy = "Users.ManageRoles")]
    public async Task<IActionResult> RemoveRoleAsync(string id, string role)
    {
        if (string.Equals(role, Permissions.ROLES.ADMIN, StringComparison.OrdinalIgnoreCase))
        {
            // Without it they couldn't manage users any more: only another administrator could give it back.
            if (id == User.FindFirstValue(ClaimTypes.NameIdentifier))
                return BadRequestMessage(Texts.T("You can't remove your own administrator role."));
            if (await userManager.FindByIdAsync(id) is { } user && await userService.IsLastActiveAdminAsync(user))
                return BadRequestMessage(Texts.T("At least one active administrator must remain."));
        }
        var ok = await userService.RemoveRoleAsync(id, role);
        if (!ok)
            return BadRequest();
        await audit.LogSecurityAsync(AuditActionType.RoleRemoved, User.Identity?.Name, $"TargetUserId={id}; Role={role}");
        return Ok();
    }

    /// <summary>Your own account and the last active Admin can't be blocked or deleted.</summary>
    private async Task<ObjectResult?> ProtectedAccountProblemAsync(IdentityUser user)
    {
        if (user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return BadRequestMessage(Texts.T("You can't block or delete your own account."));
        if (await userService.IsLastActiveAdminAsync(user))
            return BadRequestMessage(Texts.T("At least one active administrator must remain."));
        return null;
    }
}
