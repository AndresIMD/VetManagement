using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using VetManagement.Contracts.Accounts;
using VetManagement.Api.Authorization;
using VetManagement.Api.Services;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Admin;

[ApiController]
[Route("api/[controller]")]
public class AccountController(
    UserManager<IdentityUser> userManager,
    IConfiguration configuration,
    AuditService audit,
    UserManagementService userManagement,
    UserAccess access) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.Username);

        // Same 401 for unknown user, locked account and wrong password, so responses don't reveal which accounts exist.
        if (user != null && await userManager.IsLockedOutAsync(user))
        {
            await audit.LogSecurityAsync(AuditActionType.UserLoginFailed, request.Username, "Account locked out");
            return Unauthorized(Texts.T("Invalid credentials."));
        }

        if (user != null && await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.ResetAccessFailedCountAsync(user);
            // Permissions derive from roles; syncing here gives existing users any permission added since their last login.
            await userManagement.SyncClaimsForUserAsync(user);
            var userRoles = await userManager.GetRolesAsync(user);
            var userClaims = await userManager.GetClaimsAsync(user);
            var mustChange = MustChangePassword(userClaims);
            var expires = mustChange ? DateTime.UtcNow.AddMinutes(30) : (request.RememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(8));
            var token = GenerateJwtToken(user, userRoles, userClaims, expires);
            await audit.LogSecurityAsync(AuditActionType.UserLogin, user.UserName, "Login successful");
            return Ok(new { token, mustChangePassword = mustChange });
        }
        if (user != null)
            await userManager.AccessFailedAsync(user); // locks the account after MaxFailedAccessAttempts

        await audit.LogSecurityAsync(AuditActionType.UserLoginFailed, request.Username, "Invalid credentials");
        return Unauthorized(Texts.T("Invalid credentials."));
    }

    /// <summary>
    /// A new token with the user's current roles and permissions, so the staff app's menus follow role changes without
    /// signing in again. It expires when the current one does (the 30-minute password-change token gets the normal
    /// 8 hours once the password is changed). Blocked or deleted users get 401 (see UserAccess).
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    public async Task<IActionResult> RefreshAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null || !long.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Exp), out var exp))
            return Unauthorized();
        await userManagement.SyncClaimsForUserAsync(user);
        access.Forget(user.Id); // the next requests use the roles this token carries
        var userRoles = await userManager.GetRolesAsync(user);
        var userClaims = await userManager.GetClaimsAsync(user);
        var mustChange = MustChangePassword(userClaims);
        var expires = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
        if (!mustChange && User.HasClaim(SystemClaims.CLAIM_TYPE, SystemClaims.MUST_CHANGE_PASSWORD))
            expires = DateTime.UtcNow.AddHours(8);
        return Ok(new { token = GenerateJwtToken(user, userRoles, userClaims, expires), mustChangePassword = mustChange });
    }

    [HttpGet("user-info")]
    [Authorize]
    public async Task<IActionResult> GetUserInfoAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();
        return Ok(new UserInfo { Username = user.UserName!, Email = user.Email! });
    }

    [HttpPut("user-info")]
    [Authorize]
    public async Task<IActionResult> UpdateUserInfoAsync([FromBody] UpdateUserRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();
        user.Email = request.Email;
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
            return Ok();
        return IdentityProblem(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);
        var claims = await userManager.GetClaimsAsync(user);
        var mustChange = claims.FirstOrDefault(c => c.Type == SystemClaims.CLAIM_TYPE && c.Value == SystemClaims.MUST_CHANGE_PASSWORD);
        if (mustChange != null)
        {
            await userManager.RemoveClaimAsync(user, mustChange);
        }
        await audit.LogSecurityAsync(AuditActionType.PasswordChanged, user.UserName, "User changed own password");
        return Ok();
    }

    private static bool MustChangePassword(IEnumerable<Claim> claims)
        => claims.Any(c => c.Type == SystemClaims.CLAIM_TYPE && c.Value == SystemClaims.MUST_CHANGE_PASSWORD);

    private string GenerateJwtToken(IdentityUser user, IList<string> roles, IList<Claim> extraClaims, DateTime expires)
    {
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var jwtAudience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            // The user id: ASP.NET reads "sub" as the user identifier (GetUserAsync, UserAccess), so it must not be the name.
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        ];

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var c in extraClaims)
        {
            if (!claims.Any(x => x.Type == c.Type && x.Value == c.Value))
                claims.Add(c);
        }

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

