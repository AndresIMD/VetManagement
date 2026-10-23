using FluentAssertions;
using VetManagement.Staff.UI.Components.Exams;

namespace VetManagement.Tests.Application;

/// <summary>How a report's result lines are spread over pages.</summary>
public class ReportPaginationTests
{
    private static List<List<int>> Split(int lines, double room, double closing, params int[] sections)
        => ReportPagination.Split(Enumerable.Repeat(10.0, lines).ToList(), Enumerable.Range(0, lines).Select(sections.Contains).ToList(), room, room, closing);

    [Fact]
    public void Lines_FillEachPage_AndTheClosingStaysOnTheLastOne_WhenItFits()
        => Split(lines: 25, room: 100, closing: 20).Should().BeEquivalentTo(
            new[] { Enumerable.Range(0, 10), Enumerable.Range(10, 10), Enumerable.Range(20, 5) }, o => o.WithStrictOrdering());

    [Fact]
    public void Closing_ThatDoesntFit_TakesTheLastLinesWithIt_InsteadOfGoingAlone()
    {
        // 30 lines fill three pages exactly; the closing would be alone on a fourth.
        var pages = Split(lines: 30, room: 100, closing: 15);

        pages.Should().HaveCount(4);
        pages[2].Should().Equal(Enumerable.Range(20, 8));
        pages[3].Should().Equal(28, 29);
    }

    [Fact]
    public void SectionTitle_NeverStaysAloneAtTheFootOfAPage()
    {
        var pages = Split(lines: 12, room: 100, closing: 0, sections: 9);

        pages[0].Should().Equal(Enumerable.Range(0, 9));
        pages[1].Should().Equal(9, 10, 11);
    }

    [Fact]
    public void Title_RightAboveTheLinesThatMoveWithTheClosing_MovesToo()
    {
        var pages = Split(lines: 10, room: 100, closing: 15, sections: 7);

        pages[0].Should().Equal(Enumerable.Range(0, 7));
        pages[1].Should().Equal(7, 8, 9);
    }

    [Fact]
    public void Closing_GoesAlone_OnlyWhenThePageHasASingleLine()
        => Split(lines: 1, room: 15, closing: 10).Should().BeEquivalentTo(new[] { new[] { 0 }, Array.Empty<int>() }, o => o.WithStrictOrdering());

    [Fact]
    public void PageBreak_StartsANewPage_AndIsOnNoPage()
    {
        var breaks = Enumerable.Range(0, 6).Select(i => i == 3).ToList();

        ReportPagination.Split(Enumerable.Repeat(10.0, 6).ToList(), new bool[6], 100, 100, 0, breaks).Should().BeEquivalentTo(
            new[] { new[] { 0, 1, 2 }, new[] { 4, 5 } }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void PageBreaks_NeverLeaveAnEmptyPage()
        // A break before any line, and two breaks in a row.
        => ReportPagination.Split(Enumerable.Repeat(10.0, 5).ToList(), new bool[5], 100, 100, 0, [true, false, true, true, false])
            .Should().BeEquivalentTo(new[] { new[] { 1 }, new[] { 4 } }, o => o.WithStrictOrdering());

    [Fact]
    public void SectionTitle_RightAboveAPageBreak_MovesOnWithTheLinesAfterIt()
        => ReportPagination.Split(Enumerable.Repeat(10.0, 5).ToList(), [false, false, true, false, false], 100, 100, 0,
                [false, false, false, true, false])
            .Should().BeEquivalentTo(new[] { new[] { 0, 1 }, new[] { 2, 4 } }, o => o.WithStrictOrdering());

    [Theory]
    [InlineData(40, 16, 124)]  // the slim box replaces the first page's patient box
    [InlineData(40, 0, 145)]   // no box on later pages: their lines take its place and the gap under it
    [InlineData(0, 16, 79)]    // no patient box on the first page, the slim one on later pages
    [InlineData(0, 0, 100)]
    public void LaterPages_GainOrLoseTheRoomOfThePatientBoxes(double patientBox, double laterPatientBox, double expected)
        => ReportPagination.LaterRoom(firstRoom: 100, patientBox, laterPatientBox, gap: 5).Should().Be(expected);
}
