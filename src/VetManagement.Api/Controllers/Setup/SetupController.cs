using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VetManagement.Api.Authorization;
using VetManagement.Api.Services;
using VetManagement.Application.Services;
using VetManagement.Shared.Enums;

namespace VetManagement.Api.Controllers.Setup;

/// <summary>
/// One-time application bootstrap for creating the first admin.
/// </summary>
[ApiController]
[Route("api/setup")]
[EnableRateLimiting(policyName: "fixed-window")]
public class SetupController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, UserManagementService userService, IConfiguration config, AuditService audit) : ApiControllerBase
{
    [HttpPost("bootstrap")]
    public async Task<IActionResult> BootstrapAsync()
    {
        // Expose only if no admin exists or feature flag is set
        var enabled = config.GetValue<bool?>("Setup:Enabled") ?? false;
        if (!enabled)
            return NotFound();

        var existingAdmins = await userManager.GetUsersInRoleAsync("Admin");
        if (existingAdmins.Count > 0)
            return BadRequest("Admin already exists.");

        // Validate bootstrap token header (X-Setup-Token)
        if (!Request.Headers.TryGetValue("X-Setup-Token", out var tokenHeader))
        { await audit.LogSecurityAsync(AuditActionType.SetupBootstrapFailed, null, "Missing token"); return Unauthorized(); }
        var provided = tokenHeader.ToString();
        var expected = config["Setup:AdminBootstrapToken"];
        if (string.IsNullOrWhiteSpace(expected))
        { await audit.LogSecurityAsync(AuditActionType.SetupBootstrapFailed, null, "No expected token configured"); return Unauthorized(); }
        if (!TimeConstantEquals(provided, expected))
        { await audit.LogSecurityAsync(AuditActionType.SetupBootstrapFailed, null, "Invalid token"); return Unauthorized(); }

        var username = config["Setup:AdminUser:UserName"] ?? "admin";
        var email = config["Setup:AdminUser:Email"] ?? "admin@example.com";
        var password = config["Setup:AdminUser:Password"] ?? "ChangeMe!123";

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            user = new IdentityUser { UserName = username, Email = email, EmailConfirmed = true };
            var res = await userManager.CreateAsync(user, password);
            if (!res.Succeeded)
                return BadRequest(res.Errors);
            await userManager.AddToRoleAsync(user, "Admin");
            // Force password change on first login
            await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(SystemClaims.CLAIM_TYPE, SystemClaims.MUST_CHANGE_PASSWORD));
        }

        await userService.SyncClaimsForUserAsync(user);
        await audit.LogSecurityAsync(AuditActionType.SetupBootstrap, username, "Admin created via bootstrap");

        return Ok(new { ok = true });
    }

    private static bool TimeConstantEquals(string a, string b)
    {
        var aa = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aa, bb);
    }
}

