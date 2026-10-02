using System.Reflection;
using FluentAssertions;

namespace VetManagement.Tests;

/// <summary>
/// Guards the dependency direction: Domain ← Application ← Infrastructure ← Api.
/// The staff UI (VetManagement.Staff.UI) may depend on Domain/Contracts, never the other way around.
/// </summary>
public class ArchitectureTests
{
    private static IEnumerable<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("VetManagement."));

    [Theory]
    [InlineData(typeof(VetManagement.Api.Controllers.ApiControllerBase))]
    [InlineData(typeof(VetManagement.Application.Common.PagedResult<>))]
    [InlineData(typeof(VetManagement.Infrastructure.Data.AppDbContext))]
    public void Backend_DoesNotReference_StaffUi(Type typeInProject)
    {
        ReferencesOf(typeInProject.Assembly).Should().NotContain("VetManagement.Staff.UI");
    }

    [Fact]
    public void Domain_ReferencesNoOtherProject()
    {
        ReferencesOf(typeof(VetManagement.Domain.Primitives.Entity<>).Assembly).Should().BeEmpty();
    }
}
