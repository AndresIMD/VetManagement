using FluentAssertions;
using VetManagement.Staff.UI.Components.Exams;

namespace VetManagement.Tests.Application;

/// <summary>How wide the columns of a report's results table are.</summary>
public class ReportColumnsTests
{
    private const double Table = 172;
    private const double Padding = 3;
    // Parameter, result, unit, reference value, previous result date.
    private static readonly double[] Headers = [21, 20.5, 12.5, 36, 18];

    [Fact]
    public void ShortNames_LeaveRoomToTheOtherColumns()
    {
        // Blood gases ("pCO2", "HCO3-"): the fixed widths gave the names 30 % of the table.
        var layout = ReportColumns.Lay([15, 11.5, 14.5, 40.5], Headers[..4], Table, Padding, barColumn: 3);

        layout.Percents[0].Should().BeLessThan(30);
    }

    [Fact]
    public void LongNames_KeepTheirRoom()
    {
        // A blood count's "Neutrófilos segmentados" with the previous result column, where the fixed widths gave 26 %.
        var layout = ReportColumns.Lay([46, 17, 18, 54, 14], Headers, Table, Padding, barColumn: 3);

        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        layout.Text[0].Should().BeGreaterThanOrEqualTo(46);
        layout.Percents[0].Should().BeGreaterThanOrEqualTo(26);
    }

    [Fact]
    public void RoomLeft_IsSharedOutEqually_BetweenTheColumns_AndBeforeTheBars()
    {
        double[] widest = [22, 20, 15, 42];
        var layout = ReportColumns.Lay(widest, [10, 10, 10, 10], Table, Padding, barColumn: 3);

        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        // One gap after each of the first three columns, and the one before the bars in the last column.
        layout.BarGap.Should().BeApproximately((Table - widest.Sum() - 4 * Padding) / 4, 1e-9);
        layout.Text.Select((text, i) => text - widest[i]).Should().AllSatisfy(gap => gap.Should().BeApproximately(layout.BarGap, 1e-9));
    }

    [Fact]
    public void WithoutBars_TheLastColumnGetsNoGap()
    {
        double[] widest = [30, 20, 15];
        var layout = ReportColumns.Lay(widest, [10, 10, 10], Table, Padding);

        layout.BarGap.Should().Be(0);
        layout.Text[2].Should().BeApproximately(15, 1e-9);
        layout.Text[0].Should().BeApproximately(30 + (Table - widest.Sum() - 3 * Padding) / 2, 1e-9);
    }

    [Fact]
    public void AColumnNarrowerThanItsHeader_TakesTheHeadersWidth_AndTheOthersShareTheRest()
    {
        var layout = ReportColumns.Lay([30, 5, 15], [10, 80, 10], Table, Padding);

        layout.Text[1].Should().BeApproximately(80, 1e-9);
        layout.Text[2].Should().BeApproximately(15, 1e-9);
        layout.Text[0].Should().BeApproximately(Table - 80 - 15 - 3 * Padding, 1e-9);
    }

    [Fact]
    public void TextsThatDontFit_TakeTheRoomFromTheNamesFirst()
    {
        var layout = ReportColumns.Lay([80, 20, 18, 54, 18], [10, 10, 10, 10, 10], Table, Padding, barColumn: 3);

        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        layout.Text[1..].Should().Equal([20, 18, 54, 18], (a, b) => Math.Abs(a - b) < 1e-9);
        layout.Text[0].Should().BeApproximately(Table - (20 + 18 + 54 + 18) - 5 * Padding, 1e-9);
        layout.BarGap.Should().Be(0);
    }

    [Fact]
    public void WhenTheNamesCantGiveWayEnough_EveryColumnShrinksAlike()
    {
        double[] widest = [60, 40, 40, 80, 30];
        var layout = ReportColumns.Lay(widest, [20, 10, 10, 10, 10], Table, Padding, barColumn: 3);

        // The names went down to their header first; then every column lost the same share.
        var scale = Table / (20 + Padding + widest[1..].Sum(w => w + Padding));
        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        layout.Text[0].Should().BeApproximately((20 + Padding) * scale - Padding, 1e-9);
        layout.Text[1..].Should().Equal(widest[1..].Select(w => (w + Padding) * scale - Padding), (a, b) => Math.Abs(a - b) < 1e-9);
    }
}
