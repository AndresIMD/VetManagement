using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Services;
using VetManagement.Shared.Models.DTOs;

namespace VetManagement.Api.Controllers.Admin;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UserManagementController(
    UserManagementService userAdminService,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager)
    : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsersAsync()
    {
        var users = await userAdminService.GetAllUsersAsync();
        var roleTasks = users.Select(async u => new
        {
            u.Id,
            u.UserName,
            u.Email,
            Roles = await userAdminService.GetUserRolesAsync(u)
        });
        var result = await Task.WhenAll(roleTasks);
        return Ok(result);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRolesAsync()
    {
        var roles = await userAdminService.GetAllRolesAsync();
        return Ok(roles);
    }

    [HttpPost("assign-role")]
    public async Task<IActionResult> AssignRoleAsync([FromBody] RoleChangeRequest req)
    {
        var success = await userAdminService.AssignRoleAsync(req.UserId, req.Role);
        if (success)
            return Ok();
        return BadRequest("Could not assign role.");
    }

    [HttpPost("remove-role")]
    public async Task<IActionResult> RemoveRoleAsync([FromBody] RoleChangeRequest req)
    {
        var success = await userAdminService.RemoveRoleAsync(req.UserId, req.Role);
        if (success)
            return Ok();
        return BadRequest("Could not remove role.");
    }

    [HttpPost("create-user")]
    public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserRequest req)
    {
        if (await userManager.FindByNameAsync(req.UserName) != null)
            return BadRequest("Username already exists.");
        if (await userManager.FindByEmailAsync(req.Email) != null)
            return BadRequest("Email is already in use.");
        if (req.Roles == null || req.Roles.Count == 0)
            return BadRequest("You must assign at least one role.");
        foreach (var role in req.Roles)
            if (!await roleManager.RoleExistsAsync(role))
                return BadRequest($"Role '{role}' does not exist.");

        var user = new IdentityUser { UserName = req.UserName, Email = req.Email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        foreach (var role in req.Roles)
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
                return BadRequest(roleResult.Errors);
        }

        return Ok("User created successfully.");
    }
}
