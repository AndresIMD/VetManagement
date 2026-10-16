using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Services;
using VetManagement.Application.Services;
using VetManagement.Contracts.Accounts;
using VetManagement.Domain.Enums;

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
                Roles = [.. await userService.GetUserRolesAsync(user)]
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

    public record CreateUserRequest(string UserName, string Email, string Password, List<string> Roles);

    [HttpPost]
    [Authorize(Policy = "Users.Create")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateUserRequest request)
    {
        var result = await userService.CreateUserAsync(request.UserName, request.Email, request.Password, request.Roles);
        if (!result.Succeeded)
            return BadRequest(result.Errors);
        await audit.LogSecurityAsync(AuditActionType.UserCreated, User.Identity?.Name, $"Target={request.UserName}; Roles=[{string.Join(',', request.Roles)}]");
        return Ok();
    }

    [HttpPost("{id}/roles/{role}")]
    [Authorize(Policy = "Users.ManageRoles")]
    public async Task<IActionResult> AssignRoleAsync(string id, string role)
    {
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
        var ok = await userService.RemoveRoleAsync(id, role);
        if (!ok)
            return BadRequest();
        await audit.LogSecurityAsync(AuditActionType.RoleRemoved, User.Identity?.Name, $"TargetUserId={id}; Role={role}");
        return Ok();
    }
}

