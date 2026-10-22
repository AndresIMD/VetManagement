using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Accounts;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Each request takes the user's current roles from the database: role changes and blocking reach open sessions,
/// the token refresh hands out the new permissions, and the clinic can't lose its last active administrator.
/// </summary>
public class UserAccessTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "Valid#Pass12345";
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.ADMIN)]);

    [Fact]
    public async Task BlockedUser_CannotSignIn_AndTheirOpenSessionStops()
    {
        await CreateUserAsync(factory, Permissions.ROLES.ADMIN); // so the user below isn't the last active admin
        var user = await CreateUserAsync(factory, Permissions.ROLES.ADMIN);
        var session = await SignInAsync(factory, user.UserName!);
        (await session.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _admin.PostAsync($"/api/admin/users/{user.Id}/block", null)).EnsureSuccessStatusCode();

        (await session.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(factory, user.UserName!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ListAsync()).Single(u => u.Id == user.Id).IsBlocked.Should().BeTrue();

        (await _admin.PostAsync($"/api/admin/users/{user.Id}/unblock", null)).EnsureSuccessStatusCode();

        (await LoginAsync(factory, user.UserName!)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync()).Single(u => u.Id == user.Id).IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task RoleChanges_ApplyToOpenSessions_AndTheRefreshedTokenCarriesThem()
    {
        await CreateUserAsync(factory, Permissions.ROLES.ADMIN); // so removing the role below doesn't leave the clinic without an admin
        var user = await CreateUserAsync(factory, Permissions.ROLES.INVENTORY);
        var session = await SignInAsync(factory, user.UserName!);
        (await session.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await _admin.PostAsync($"/api/admin/users/{user.Id}/roles/{Permissions.ROLES.ADMIN}", null)).EnsureSuccessStatusCode();

        (await session.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK, "the open session gets the new role");
        var refreshed = await session.PostAsync("/api/account/refresh", null);
        refreshed.EnsureSuccessStatusCode();
        var token = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type == Permissions.CLAIM_TYPE).Select(c => c.Value).Should().Contain(Permissions.USERS.READ);

        (await _admin.DeleteAsync($"/api/admin/users/{user.Id}/roles/{Permissions.ROLES.ADMIN}")).EnsureSuccessStatusCode();

        (await session.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the removed role is gone at once");
    }

    [Fact]
    public async Task ResetPassword_SetsATemporaryPassword_ThatMustBeChangedAtSignIn()
    {
        var user = await CreateUserAsync(factory, Permissions.ROLES.LABORATORY);

        var weak = await _admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/reset-password", new ResetPasswordRequest { NewPassword = "short" });
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await weak.Content.ReadAsStringAsync()).Should().Contain("at least 10 characters");
        (await LoginAsync(factory, user.UserName!)).StatusCode.Should().Be(HttpStatusCode.OK, "a refused password leaves the old one");

        (await _admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/reset-password", new ResetPasswordRequest { NewPassword = "Temp#Pass67890" }))
            .EnsureSuccessStatusCode();

        var login = await LoginAsync(factory, user.UserName!, "Temp#Pass67890");
        login.EnsureSuccessStatusCode();
        (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_EndsOpenSessions_AndTheTemporaryPasswordCanOnlyBeChanged()
    {
        var user = await CreateUserAsync(factory, Permissions.ROLES.INVENTORY);
        var session = await SignInAsync(factory, user.UserName!);
        (await session.GetAsync("/api/items")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _admin.PostAsJsonAsync($"/api/admin/users/{user.Id}/reset-password", new ResetPasswordRequest { NewPassword = "Temp#Pass67890" }))
            .EnsureSuccessStatusCode();
        (await session.GetAsync("/api/items")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "whoever knew the old password is out");

        var temporary = await SignInAsync(factory, user.UserName!, "Temp#Pass67890");
        var refused = await temporary.GetAsync("/api/items");
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("temporary password");
        (await temporary.GetAsync("/api/account/user-info")).StatusCode.Should().Be(HttpStatusCode.OK);

        var changed = await temporary.PostAsJsonAsync("/api/account/change-password",
            new ChangePasswordRequest { CurrentPassword = "Temp#Pass67890", NewPassword = "Mine#Pass24680" });
        changed.EnsureSuccessStatusCode();
        var body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("mustChangePassword").GetBoolean().Should().BeFalse();
        (await temporary.GetAsync("/api/account/user-info")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the change ends the temporary session");

        var next = factory.CreateClient();
        next.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        (await next.GetAsync("/api/items")).StatusCode.Should().Be(HttpStatusCode.OK, "the session goes on with the token the change returned");
    }

    [Fact]
    public async Task ChangingTheOwnPassword_EndsTheOtherSessions_AndKeepsThisOne()
    {
        var user = await CreateUserAsync(factory, Permissions.ROLES.INVENTORY);
        var here = await SignInAsync(factory, user.UserName!);
        var elsewhere = await SignInAsync(factory, user.UserName!);

        var changed = await here.PostAsJsonAsync("/api/account/change-password", new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "Other#Pass13579" });
        changed.EnsureSuccessStatusCode();

        (await elsewhere.GetAsync("/api/items")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        here.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        (await here.GetAsync("/api/items")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenWithoutTheAccountsSecurityStamp_IsRefused()
    {
        var user = await CreateUserAsync(factory, Permissions.ROLES.ADMIN);
        var forged = factory.CreateClientWithClaims([new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.UserName!)]);

        (await forged.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RememberMe_KeepsTheSessionForAWeek()
    {
        var user = await CreateUserAsync(factory, Permissions.ROLES.INVENTORY);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/account/login", new { Username = user.UserName, Password, RememberMe = true });

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task EditAndDelete_ChangeTheUserList()
    {
        var user = await CreateUserAsync(factory, Permissions.ROLES.RECEPTION);
        var newName = $"renamed-{Guid.NewGuid():N}";

        (await _admin.PutAsJsonAsync($"/api/admin/users/{user.Id}", new EditUserRequest { UserName = newName, Email = $"{newName}@example.test" }))
            .EnsureSuccessStatusCode();
        (await ListAsync()).Single(u => u.Id == user.Id).Should().Match<UserDto>(u => u.UserName == newName && u.Email == $"{newName}@example.test");

        (await _admin.DeleteAsync($"/api/admin/users/{user.Id}")).EnsureSuccessStatusCode();
        (await ListAsync()).Should().NotContain(u => u.Id == user.Id);
    }

    [Fact]
    public async Task Users_CannotBlockThemselves()
    {
        var user = await CreateUserAsync(factory, Permissions.ROLES.ADMIN);
        var session = await SignInAsync(factory, user.UserName!);

        var response = await session.PostAsync($"/api/admin/users/{user.Id}/block", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("your own account");
    }

    [Fact]
    public async Task TheLastActiveAdmin_CannotBeBlockedDeletedOrDemoted()
    {
        await using var clinic = new CustomWebAppFactory(); // its own database: this admin is the only one
        var admin = await CreateUserAsync(clinic, Permissions.ROLES.ADMIN);
        var acting = clinic.CreateClientWithPermissions([.. Permissions.ForRole(Permissions.ROLES.ADMIN)]);

        (await acting.PostAsync($"/api/admin/users/{admin.Id}/block", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await acting.DeleteAsync($"/api/admin/users/{admin.Id}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var demote = await acting.DeleteAsync($"/api/admin/users/{admin.Id}/roles/{Permissions.ROLES.ADMIN}");
        demote.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await demote.Content.ReadAsStringAsync()).Should().Contain("At least one active administrator");

        var second = await CreateUserAsync(clinic, Permissions.ROLES.ADMIN);
        (await acting.PostAsync($"/api/admin/users/{admin.Id}/block", null)).EnsureSuccessStatusCode();
        (await acting.PostAsync($"/api/admin/users/{second.Id}/block", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the other admin is blocked now");
    }

    private async Task<List<UserDto>> ListAsync()
        => (await _admin.GetFromJsonAsync<List<UserDto>>("/api/admin/users"))!;

    private static async Task<IdentityUser> CreateUserAsync(CustomWebAppFactory app, params string[] roles)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var userName = $"user-{Guid.NewGuid():N}";
        var user = new IdentityUser(userName) { Email = $"{userName}@example.test" };
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        await users.AddToRolesAsync(user, roles);
        return user;
    }

    private static Task<HttpResponseMessage> LoginAsync(CustomWebAppFactory app, string userName, string password = Password)
        => app.CreateClient().PostAsJsonAsync("/api/account/login", new { Username = userName, Password = password });

    private static async Task<HttpClient> SignInAsync(CustomWebAppFactory app, string userName, string password = Password)
    {
        var response = await LoginAsync(app, userName, password);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
