using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace VetManagement.Tests.Integration.Controllers;

/// <summary>
/// Tests to verify that authorization policies are correctly registered and can be resolved.
/// This catches the policy name mismatch bug (e.g. Inventory.READ vs Inventory.Read)
/// at startup time — if a policy is not registered, the DI container throws.
/// </summary>
public class AuthorizationPoliciesTests : IClassFixture<CustomWebAppFactory>
{
    private readonly IServiceProvider _serviceProvider;

    public AuthorizationPoliciesTests(CustomWebAppFactory factory)
    {
        _serviceProvider = factory.Services;
    }

    [Theory]
    [InlineData("Inventory.Read")]
    [InlineData("Inventory.Create")]
    [InlineData("Inventory.Update")]
    [InlineData("Inventory.Delete")]
    [InlineData("Exams.Read")]
    [InlineData("Exams.Create")]
    [InlineData("Exams.Update")]
    [InlineData("Exams.Delete")]
    [InlineData("ExternalLabs.Read")]
    [InlineData("ExternalLabs.Create")]
    [InlineData("ExternalLabs.Update")]
    [InlineData("ExternalLabs.Delete")]
    [InlineData("ExamsPerformed.Read")]
    [InlineData("ExamsPerformed.Create")]
    [InlineData("ExamsPerformed.Update")]
    [InlineData("ExamsPerformed.Delete")]
    [InlineData("Medical.Read")]
    [InlineData("Medical.Create")]
    [InlineData("Medical.Update")]
    [InlineData("Medical.Delete")]
    [InlineData("ClientsPets.Read")]
    [InlineData("ClientsPets.Create")]
    [InlineData("ClientsPets.Update")]
    [InlineData("ClientsPets.Delete")]
    [InlineData("Audit.Read")]
    [InlineData("Users.Read")]
    [InlineData("Users.Create")]
    [InlineData("Users.ManageRoles")]
    [InlineData("System.SendEmail")]
    public async Task Policy_IsRegistered_And_CanBeResolved(string policyName)
    {
        // Arrange
        var policyProvider = _serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act & Assert
        var policy = await policyProvider.GetPolicyAsync(policyName);
        policy.Should().NotBeNull($"Policy '{policyName}' must be registered in Program.cs");
    }

    // Every [Authorize(Policy = ...)] actually used by a controller must resolve; a typo would
    // otherwise only surface as a 500 when that endpoint is called.
    [Fact]
    public async Task EveryPolicyUsedByControllers_IsRegistered()
    {
        var policyProvider = _serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var usedPolicies = controllers
            .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(t.GetMethods().SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true))))
            .Select(a => a.Policy)
            .OfType<string>()
            .Distinct()
            .ToList();

        usedPolicies.Should().NotBeEmpty();
        var missing = new List<string>();
        foreach (var name in usedPolicies)
            if (await policyProvider.GetPolicyAsync(name) is null)
                missing.Add(name);

        missing.Should().BeEmpty("every policy referenced by [Authorize] must be registered in Program.cs");
    }
}