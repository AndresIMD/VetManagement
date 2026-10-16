using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Accounts;

namespace VetManagement.Tests.Integration;

public class UsersApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    [Fact]
    public async Task UserList_ShowsEachUsersRoles_AndNeverThePasswordHash()
    {
        var userName = $"lab-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(Permissions.ROLES.LABORATORY))
                await roles.CreateAsync(new IdentityRole(Permissions.ROLES.LABORATORY));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser(userName) { Email = $"{userName}@example.test" };
            (await users.CreateAsync(user, "Valid#Pass12345")).Succeeded.Should().BeTrue();
            await users.AddToRoleAsync(user, Permissions.ROLES.LABORATORY);
        }
        var admin = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.ADMIN)]);

        var response = await admin.GetAsync("/api/admin/users");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        body.Should().NotContainEquivalentOf("passwordHash").And.NotContainEquivalentOf("securityStamp");
        (await response.Content.ReadFromJsonAsync<List<UserDto>>())!.Single(u => u.UserName == userName)
            .Roles.Should().Equal(Permissions.ROLES.LABORATORY);
    }
}
