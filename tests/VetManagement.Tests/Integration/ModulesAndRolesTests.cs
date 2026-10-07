using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using static VetManagement.Api.Authorization.Permissions;

namespace VetManagement.Tests.Integration;

/// <summary>Area roles add up, Supervisor only raises the user's own areas, and a module that is off is gone for everyone.</summary>
public class ModulesAndRolesTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    [Fact]
    public void AreaRoles_AddUp_AndSupervisorOnlyRaisesTheUsersAreas()
    {
        ForRole(ROLES.LABORATORY).Should().Contain(EXAMS_PERFORMED.CREATE).And.NotContain([MEDICAL.READ, BILLING.READ, EXAMS.UPDATE]);
        ForRoles([ROLES.LABORATORY, ROLES.SUPERVISOR]).Should().Contain([EXAMS.UPDATE, EXAMS_PERFORMED.DELETE, AUDIT.READ]).And.NotContain(INVENTORY.DELETE);
        ForRoles([ROLES.RECEPTION, ROLES.CASHIER]).Should().Contain([SCHEDULING.BOOK, BILLING.CHARGE]).And.NotContain(BILLING.MANAGE);
        ForRole(ROLES.SUPERVISOR).Should().BeEmpty("supervising needs an area");
        ForRole(ROLES.ADMIN).Should().BeEquivalentTo(ALL());
    }

    [Fact]
    public void ModulesThatNeedPatients_CannotBeOnWithoutClients()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Modules:Enabled:0"] = "Inventory",
            ["Modules:Enabled:1"] = "Exams"
        }).Build();

        FluentActions.Invoking(() => new EnabledModules(config)).Should().Throw<InvalidOperationException>().WithMessage("*Exams*Clients*");
    }

    [Fact]
    public async Task ModuleOff_GivesNoPermissionsEvenToAdmins_AndItsPublicEndpointsDisappear()
    {
        var limited = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Modules:Enabled:0", "Inventory");
            b.UseSetting("Modules:Enabled:1", "Clients");
            b.UseSetting("Modules:Enabled:2", "Exams");
        });
        const string password = "Valid#Pass12345";
        var userName = $"admin-{Guid.NewGuid():N}";
        using (var scope = limited.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(ROLES.ADMIN))
                await roles.CreateAsync(new IdentityRole(ROLES.ADMIN));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser(userName);
            (await users.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            await users.AddToRoleAsync(user, ROLES.ADMIN);
        }

        var client = limited.CreateClient();
        var login = await client.PostAsJsonAsync("/api/account/login", new { Username = userName, Password = password });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var permissions = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Where(c => c.Type == CLAIM_TYPE).Select(c => c.Value).ToList();

        permissions.Should().Contain([INVENTORY.READ, CLIENTS_PETS.READ, EXAMS_PERFORMED.CREATE, USERS.READ]);
        permissions.Should().NotContain([BILLING.READ, SCHEDULING.READ, MEDICAL.READ, REPORTS.READ]);
        (await client.GetAsync("/api/public/booking/info")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await factory.CreateClient().GetAsync("/api/public/booking/info")).StatusCode.Should().NotBe(HttpStatusCode.NotFound, "with every module on it answers");
    }
}
