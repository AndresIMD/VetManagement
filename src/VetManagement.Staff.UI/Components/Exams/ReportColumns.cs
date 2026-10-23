namespace VetManagement.Staff.UI.Components.Exams;

/// <summary>
/// How wide each column of a report's results table is. A column starts as wide as its widest text plus the cell's padding,
/// and the room left over is shared out equally between the gaps, so the spaces between columns look even whatever the
/// exam: short names (blood gases) leave room to the other columns, long ones (a blood count) keep theirs. With bars, the
/// space between the widest range and the bars counts as one more gap; there is none after the last column. A column is
/// never narrower than its header: one that would be takes the header's width and no gap. When the texts don't fit, the
/// first column (parameter names, which wrap well) gives way first, down to its header; past that every column shrinks
/// alike.
/// </summary>
public static class ReportColumns
{
    /// <param name="Percents">Each column's width, in % of the table.</param>
    /// <param name="Text">Room for text in each column, in mm: its width without the cell's padding.</param>
    /// <param name="BarGap">Space added between the widest range and the bars, in mm.</param>
    public sealed record Layout(double[] Percents, double[] Text, double BarGap);

    /// <param name="widest">Widest text of each column, in mm; for the column with bars, at least its widest range, the gap
    /// before the bar and the bar.</param>
    /// <param name="headers">Width of each column's header, in mm: the narrowest the column gets.</param>
    /// <param name="table">Width of the table, in mm.</param>
    /// <param name="padding">A cell's padding, left and right together, in mm.</param>
    /// <param name="barColumn">Index of the column whose lines carry bars, or -1 when there are none.</param>
    public static Layout Lay(IReadOnlyList<double> widest, IReadOnlyList<double> headers, double table, double padding, int barColumn = -1)
    {
        var count = widest.Count;
        var cells = widest.Select(w => w + padding).ToArray();
        var floors = headers.Select(h => h + padding).ToArray();
        // The gaps a column takes: the one after it (not after the last) and, for the column with bars, the one before them.
        int Gaps(int i) => (i < count - 1 ? 1 : 0) + (i == barColumn ? 1 : 0);

        var atHeader = new bool[count];
        double gap;
        while (true)
        {
            var open = Enumerable.Range(0, count).Where(i => !atHeader[i]).ToList();
            var gaps = open.Sum(Gaps);
            var free = table - open.Sum(i => cells[i]) - Enumerable.Range(0, count).Where(i => atHeader[i]).Sum(i => floors[i]);
            gap = gaps == 0 || free < 0 ? 0 : free / gaps;
            var narrow = open.Where(i => cells[i] + Gaps(i) * gap < floors[i]).ToList();
            if (narrow.Count == 0)
                break;
            narrow.ForEach(i => atHeader[i] = true);
        }
        var widths = Enumerable.Range(0, count).Select(i => atHeader[i] ? floors[i] : cells[i] + Gaps(i) * gap).ToArray();

        var over = widths.Sum() - table;
        if (over > 0)
        {
            widths[0] -= Math.Min(over, Math.Max(0, widths[0] - floors[0]));
            var scale = Math.Min(1, table / widths.Sum());
            for (var i = 0; i < count; i++)
                widths[i] *= scale;
        }
        widths[^1] += Math.Max(0, table - widths.Sum());

        return new Layout(widths.Select(w => w / table * 100).ToArray(), widths.Select(w => w - padding).ToArray(),
            barColumn >= 0 && !atHeader[barColumn] ? gap : 0);
    }
}
