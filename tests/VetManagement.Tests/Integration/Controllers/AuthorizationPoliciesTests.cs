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
    public void Policy_IsRegistered_And_CanBeResolved(string policyName)
    {
        // Arrange
        var policyProvider = _serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act & Assert
        var policy = policyProvider.GetPolicyAsync(policyName).GetAwaiter().GetResult();
        policy.Should().NotBeNull($"Policy '{policyName}' must be registered in Program.cs");
    }
}