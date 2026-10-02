using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;

namespace VetManagement.Tests.Integration;

/// <summary>Permissions come from roles and are refreshed at login, so new permissions reach existing users.</summary>
public class LoginPermissionSyncTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public LoginPermissionSyncTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_GrantsTheRolesCurrentPermissions_ToAUserWithoutStoredClaims()
    {
        const string password = "Valid#Pass12345";
        var userName = $"employee-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Employee"))
                await roles.CreateAsync(new IdentityRole("Employee"));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser(userName);
            (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            await users.AddToRoleAsync(user, "Employee"); // existing user: role but no permission claims yet
        }

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/account/login", new { Username = userName, Password = password });
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

        var permissions = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type == Permissions.CLAIM_TYPE).Select(c => c.Value);
        permissions.Should().BeEquivalentTo(Permissions.ForRole("Employee"));
        permissions.Should().Contain(Permissions.SCHEDULING.BOOK).And.NotContain(Permissions.SCHEDULING.MANAGE);
    }
}
