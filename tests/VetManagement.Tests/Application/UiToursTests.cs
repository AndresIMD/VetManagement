using System.Text.RegularExpressions;
using FluentAssertions;

namespace VetManagement.Tests.Application;

/// <summary>
/// A tutorial step points at an element marked data-tour="anchor" in the same page; a step whose element is missing is
/// skipped without a word in the browser, so a renamed or removed anchor would silently shorten the tutorial.
/// </summary>
public partial class UiToursTests
{
    private static readonly string StaffUi = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "VetManagement.Staff.UI");

    public static IEnumerable<object[]> PagesWithTours() => Directory
        .EnumerateFiles(StaffUi, "*.razor", SearchOption.AllDirectories)
        .Where(f => File.ReadAllText(f).Contains("TourId="))
        .Select(f => new object[] { Path.GetRelativePath(StaffUi, f) });

    [Fact]
    public void SomePageHasATour() => PagesWithTours().Should().NotBeEmpty();

    [Theory]
    [MemberData(nameof(PagesWithTours))]
    public void EveryStep_PointsAtAnElementOfItsPage(string page)
    {
        var markup = File.ReadAllText(Path.Combine(StaffUi, page));
        var steps = StepAnchor().Matches(markup).Select(m => m.Groups[1].Value).ToList();
        var anchors = DataTour().Matches(markup).Select(m => m.Groups[1].Value).ToList();

        steps.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
        anchors.Should().OnlyHaveUniqueItems();
        steps.Should().BeSubsetOf(anchors, $"each tutorial step of {page} needs an element with that data-tour");
    }

    [GeneratedRegex("""new\("([^"]+)", L\[""")]
    private static partial Regex StepAnchor();

    [GeneratedRegex("data-tour=\"([^\"]+)\"")]
    private static partial Regex DataTour();
}
