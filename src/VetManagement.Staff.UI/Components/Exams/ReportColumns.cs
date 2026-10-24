namespace VetManagement.Staff.UI.Components.Exams;

/// <summary>
/// How wide each column of a report's results table is. A column starts as wide as its widest text plus the cell's padding,
/// and the room left over is shared out so the space between the texts of any two neighbouring columns is the same, whatever
/// the exam: short names (blood gases) leave room to the other columns, long ones (a blood count) keep theirs. The first
/// column (parameter names) always aligns left. With the others left-aligned too, each column takes one share after its text
/// and the last none; centered, each takes one share split around its text, and the first half a share. A column is never
/// narrower than its header: one that would be takes the header's width and no share. When the texts don't fit, the first
/// column (names, which wrap well) gives way first, down to its header; past that every column shrinks alike.
/// </summary>
public static class ReportColumns
{
    /// <param name="Percents">Each column's width, in % of the table.</param>
    /// <param name="Text">Room for text in each column, in mm: its width without the cell's padding.</param>
    public sealed record Layout(double[] Percents, double[] Text);

    /// <param name="widest">Widest text of each column, in mm.</param>
    /// <param name="headers">Width of each column's header, in mm: the narrowest the column gets (0 = no header of its own).</param>
    /// <param name="table">Width of the table, in mm.</param>
    /// <param name="padding">A cell's padding, left and right together, in mm.</param>
    /// <param name="centered">The columns after the first center their texts; otherwise they align left.</param>
    public static Layout Lay(IReadOnlyList<double> widest, IReadOnlyList<double> headers, double table, double padding, bool centered)
    {
        var count = widest.Count;
        var cells = widest.Select(w => w + padding).ToArray();
        var floors = headers.Select(h => h + padding).ToArray();
        // Shares of the room left that each column takes, so the texts end up evenly spaced.
        double Shares(int i) => centered ? (i == 0 ? 0.5 : 1) : (i < count - 1 ? 1 : 0);

        var atHeader = new bool[count];
        double share;
        while (true)
        {
            var open = Enumerable.Range(0, count).Where(i => !atHeader[i]).ToList();
            var shares = open.Sum(Shares);
            var free = table - open.Sum(i => cells[i]) - Enumerable.Range(0, count).Where(i => atHeader[i]).Sum(i => floors[i]);
            share = shares == 0 || free < 0 ? 0 : free / shares;
            var narrow = open.Where(i => cells[i] + Shares(i) * share < floors[i]).ToList();
            if (narrow.Count == 0)
                break;
            narrow.ForEach(i => atHeader[i] = true);
        }
        var widths = Enumerable.Range(0, count).Select(i => atHeader[i] ? floors[i] : cells[i] + Shares(i) * share).ToArray();

        var over = widths.Sum() - table;
        if (over > 0)
        {
            widths[0] -= Math.Min(over, Math.Max(0, widths[0] - floors[0]));
            var scale = Math.Min(1, table / widths.Sum());
            for (var i = 0; i < count; i++)
                widths[i] *= scale;
        }
        widths[^1] += Math.Max(0, table - widths.Sum());

        return new Layout(widths.Select(w => w / table * 100).ToArray(), widths.Select(w => w - padding).ToArray());
    }
}
