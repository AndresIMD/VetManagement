using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Staff.UI.Services.Auth;

namespace VetManagement.Tests.Integration;

/// <summary>The staff UI's authorization policies must exist for its pages and match the API's.</summary>
public class UiPolicyParityTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public UiPolicyParityTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public void EveryPolicyUsedByAUiPage_IsRegisteredInTheUi()
    {
        var usedByPages = typeof(StaffAuthorization).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Select(a => a.Policy)
            .OfType<string>()
            .Distinct();

        usedByPages.Should().BeSubsetOf(StaffAuthorization.Policies.Keys,
            "a page whose policy isn't registered crashes the app with 'AuthorizationPolicy ... was not found'");
    }

    [Fact]
    public async Task EveryUiPolicy_RequiresTheSamePermissionAsTheApi()
    {
        var apiPolicies = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var (name, permission) in StaffAuthorization.Policies)
        {
            var apiPolicy = await apiPolicies.GetPolicyAsync(name);
            apiPolicy.Should().NotBeNull($"the API has no policy '{name}'");
            apiPolicy!.Requirements.OfType<ClaimsAuthorizationRequirement>()
                .Should().ContainSingle(r => r.ClaimType == StaffAuthorization.PermissionClaimType && r.AllowedValues!.Contains(permission),
                    $"'{name}' must require the '{permission}' permission in both UI and API");
        }
    }
}
