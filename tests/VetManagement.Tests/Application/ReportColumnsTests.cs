using System.Text.Json;
using FluentAssertions;
using VetManagement.Application.Configuration;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Staff.UI.Components.Exams;

namespace VetManagement.Tests.Application;

/// <summary>How wide the columns of a report's results table are.</summary>
public class ReportColumnsTests
{
    private const double Table = 172;
    private const double Padding = 3;

    [Fact]
    public void ShortNames_LeaveRoomToTheOtherColumns()
    {
        // Blood gases ("pCO2", "HCO3-"), with the range and its bar: the fixed widths gave the names 30 % of the table.
        var layout = ReportColumns.Lay([15, 11.5, 14.5, 18, 25], [21, 20.5, 12.5, 0, 0], Table, Padding, centered: true);

        layout.Percents[0].Should().BeLessThan(30);
    }

    [Fact]
    public void LongNames_KeepTheirRoom()
    {
        // A blood count's "Neutrófilos segmentados" stays on one line.
        var layout = ReportColumns.Lay([46, 17, 18, 31, 25], [21, 20.5, 12.5, 0, 0], Table, Padding, centered: true);

        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        layout.Text[0].Should().BeGreaterThanOrEqualTo(46);
    }

    [Fact]
    public void LeftAligned_EachColumnTakesOneShareAfterItsText_AndTheLastNone()
    {
        double[] widest = [30, 20, 15];
        var layout = ReportColumns.Lay(widest, [10, 10, 10], Table, Padding, centered: false);

        var share = (Table - widest.Sum() - 3 * Padding) / 2;
        layout.Text.Should().Equal([30 + share, 20 + share, 15], (a, b) => Math.Abs(a - b) < 1e-9);
    }

    [Fact]
    public void Centered_TheSpaceBetweenAnyTwoNeighbouringTexts_IsTheSame()
    {
        double[] widest = [22, 20, 15, 18, 25];
        var layout = ReportColumns.Lay(widest, [5, 5, 5, 5, 5], Table, Padding, centered: true);

        // Where each text sits: the names at the left of their column, the others centered in theirs.
        var start = 0.0;
        var texts = new List<(double Left, double Right)>();
        for (var i = 0; i < widest.Length; i++)
        {
            var width = layout.Percents[i] / 100 * Table;
            var left = i == 0 ? start + Padding / 2 : start + (width - widest[i]) / 2;
            texts.Add((left, left + widest[i]));
            start += width;
        }
        var spaces = texts.Zip(texts.Skip(1), (a, b) => b.Left - a.Right).ToList();

        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        spaces.Should().AllSatisfy(space => space.Should().BeApproximately(spaces[0], 1e-9));
        spaces[0].Should().BeGreaterThan(Padding);
    }

    [Fact]
    public void AColumnNarrowerThanItsHeader_TakesTheHeadersWidth_AndTheOthersShareTheRest()
    {
        var layout = ReportColumns.Lay([30, 5, 15], [10, 80, 10], Table, Padding, centered: false);

        layout.Text[1].Should().BeApproximately(80, 1e-9);
        layout.Text[2].Should().BeApproximately(15, 1e-9);
        layout.Text[0].Should().BeApproximately(Table - 80 - 15 - 3 * Padding, 1e-9);
    }

    [Fact]
    public void TextsThatDontFit_TakeTheRoomFromTheNamesFirst()
    {
        var layout = ReportColumns.Lay([80, 20, 18, 31, 25, 18], [10, 10, 10, 0, 0, 10], Table, Padding, centered: true);

        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        layout.Text[1..].Should().Equal([20, 18, 31, 25, 18], (a, b) => Math.Abs(a - b) < 1e-9);
        layout.Text[0].Should().BeApproximately(Table - (20 + 18 + 31 + 25 + 18) - 6 * Padding, 1e-9);
    }

    [Fact]
    public void WhenTheNamesCantGiveWayEnough_EveryColumnShrinksAlike()
    {
        double[] widest = [60, 40, 40, 80, 30];
        var layout = ReportColumns.Lay(widest, [20, 10, 10, 10, 10], Table, Padding, centered: true);

        // The names went down to their header first; then every column lost the same share.
        var scale = Table / (20 + Padding + widest[1..].Sum(w => w + Padding));
        layout.Percents.Sum().Should().BeApproximately(100, 1e-9);
        layout.Text[0].Should().BeApproximately((20 + Padding) * scale - Padding, 1e-9);
        layout.Text[1..].Should().Equal(widest[1..].Select(w => (w + Padding) * scale - Padding), (a, b) => Math.Abs(a - b) < 1e-9);
    }

    [Fact]
    public void ASavedDesignWithoutTheSetting_CentersTheColumns()
    {
        var settings = JsonSerializer.Deserialize<ExamReportSettings>("""{ "paper": "Letter", "fontFamily": "Segoe UI" }""", SettingsJson.Options)!;

        settings.ColumnAlign.Should().Be(ColumnAlign.Center);
    }
}
