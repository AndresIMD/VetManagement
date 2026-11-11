using FluentAssertions;
using VetManagement.Staff.UI.Services.App;

namespace VetManagement.Tests.StaffApp;

/// <summary>The version in the user menu reads like the update package's name: its day and its commit.</summary>
public class AppVersionTests
{
    [Theory]
    [InlineData("1.0.0+cea3d43176e8c7827478e0705918494581a854a6", "2026-11-10", "2026-11-10 · cea3d43")]
    [InlineData("1.0.0+cea3d43", "2026-11-10", "2026-11-10 · cea3d43")]
    [InlineData("1.0.0", "2026-11-10", "2026-11-10")] // built without git: the day alone
    [InlineData("1.0.0+cea3d43176e8c7827478e0705918494581a854a6", null, "cea3d43")]
    [InlineData(null, null, "")] // nothing known: the menu shows no version line
    public void Describe_GivesTheDayAndTheShortCommit(string? informationalVersion, string? buildDate, string expected)
        => AppVersion.Describe(informationalVersion, buildDate).Should().Be(expected);

    [Fact]
    public void ThisBuild_HasItsDay()
        => AppVersion.Text.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}", "the staff project stamps the day it was built");
}
