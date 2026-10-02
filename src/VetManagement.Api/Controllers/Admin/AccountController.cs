using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using VetManagement.Shared.Models.DTOs;
using VetManagement.Api.Authorization;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;

namespace VetManagement.Api.Controllers.Admin;

[ApiController]
[Route("api/[controller]")]
public class AccountController(
    UserManager<IdentityUser> userManager,
    IConfiguration configuration,
    AuditService audit) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.Username);
        if (user != null && await userManager.CheckPasswordAsync(user, request.Password))
        {
            var userRoles = await userManager.GetRolesAsync(user);
            var userClaims = await userManager.GetClaimsAsync(user);
            var token = GenerateJwtToken(user, userRoles, userClaims, request.RememberMe);
            var mustChange = userClaims.Any(c => c.Type == SystemClaims.CLAIM_TYPE && c.Value == SystemClaims.MUST_CHANGE_PASSWORD);
            await audit.LogSecurityAsync(AuditActionType.UserLogin, user.UserName, "Login successful");
            return Ok(new { token, mustChangePassword = mustChange });
        }
        await audit.LogSecurityAsync(AuditActionType.UserLoginFailed, request.Username, "Invalid credentials");
        return Unauthorized("Invalid credentials.");
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
        return BadRequest(result.Errors);
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
            return BadRequest(result.Errors);
        var claims = await userManager.GetClaimsAsync(user);
        var mustChange = claims.FirstOrDefault(c => c.Type == SystemClaims.CLAIM_TYPE && c.Value == SystemClaims.MUST_CHANGE_PASSWORD);
        if (mustChange != null)
        {
            await userManager.RemoveClaimAsync(user, mustChange);
        }
        await audit.LogSecurityAsync(AuditActionType.PasswordChanged, user.UserName, "User changed own password");
        return Ok();
    }

    private string GenerateJwtToken(IdentityUser user, IList<string> roles, IList<Claim> extraClaims, bool rememberMe)
    {
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var jwtAudience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.UserName ?? string.Empty),
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

        var hasMustChange = extraClaims.Any(c => c.Type == SystemClaims.CLAIM_TYPE && c.Value == SystemClaims.MUST_CHANGE_PASSWORD);
        var expires = hasMustChange ? DateTime.UtcNow.AddMinutes(30) : (rememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(8));

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

