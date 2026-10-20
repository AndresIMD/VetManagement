using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace VetManagement.Api.Authorization;

/// <summary>
/// What a signed-in user may do right now. A token proves who the user is; the roles and permissions of each request
/// come from the database (cached for a minute), so role changes apply without signing in again and a blocked or
/// deleted user's open sessions stop working.
/// </summary>
public sealed class UserAccess(IMemoryCache cache)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private sealed record Entry(string[] Roles, string[] Permissions);

    /// <summary>
    /// Blocked by an administrator (e.g. staff who left the clinic): locked out with no end. The temporary lockout
    /// after failed logins ends within minutes, so it doesn't count and doesn't close open sessions.
    /// </summary>
    public static bool IsBlocked(IdentityUser user) => user.LockoutEnd > DateTimeOffset.UtcNow.AddYears(100);

    /// <summary>Puts the user's current roles and permissions on the principal; false if the user may no longer sign in.</summary>
    public async Task<bool> ApplyAsync(ClaimsPrincipal principal, IServiceProvider services)
    {
        // Tokens this API issues always carry the user id; one without it has no user to check (tests sign those).
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || principal.Identity is not ClaimsIdentity identity)
            return true;

        var entry = await cache.GetOrCreateAsync(Key(userId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheFor;
            e.Size = 1;
            var users = services.GetRequiredService<UserManager<IdentityUser>>();
            var user = await users.FindByIdAsync(userId);
            if (user is null || IsBlocked(user))
                return null;
            var roles = await users.GetRolesAsync(user);
            var permissions = services.GetRequiredService<EnabledModules>().Filter(Permissions.ForRoles(roles));
            return new Entry([.. roles], [.. permissions]);
        });
        if (entry is null)
            return false;

        foreach (var claim in identity.FindAll(c => c.Type is ClaimTypes.Role or Permissions.CLAIM_TYPE).ToList())
            identity.RemoveClaim(claim);
        identity.AddClaims(entry.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        identity.AddClaims(entry.Permissions.Select(p => new Claim(Permissions.CLAIM_TYPE, p)));
        return true;
    }

    /// <summary>Call after changing a user's roles, block or account, so the next request reads them again.</summary>
    public void Forget(string userId) => cache.Remove(Key(userId));

    private static string Key(string userId) => $"user-access:{userId}";
}
