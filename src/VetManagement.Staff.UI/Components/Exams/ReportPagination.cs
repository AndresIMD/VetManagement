namespace VetManagement.Staff.UI.Components.Exams;

/// <summary>
/// How a report's result lines are spread over pages, from the height of each line (measured on the rendered sheet, or
/// estimated until then) and the room for lines on the first and on later pages. Rules: a page takes lines while they fit;
/// a page break starts a new page (unless the page is still empty) and is on no page itself; a section title never stays
/// alone at the foot of a page; the closing blocks (legend, criteria, interpretation, method) go on the last page and
/// never alone: when they don't fit, the last lines (and a title just above them) move on with them.
/// </summary>
public static class ReportPagination
{
    /// <summary>Lines carried over with the closing blocks when these don't fit on the last page.</summary>
    public const int LinesWithClosing = 2;

    /// <summary>
    /// Room for lines on a later page, worked out from the first one (when no later page has been measured): the first
    /// page's patient box and the gap under it make way for the slim box of later pages and its gap. A height of 0 = no box.
    /// </summary>
    public static double LaterRoom(double firstRoom, double patientBox, double laterPatientBox, double gap)
        => firstRoom + (patientBox > 0 ? patientBox + gap : 0) - (laterPatientBox > 0 ? laterPatientBox + gap : 0);

    /// <returns>For each page, the indexes of its lines; the last page also holds the closing blocks.</returns>
    public static List<List<int>> Split(IReadOnlyList<double> heights, IReadOnlyList<bool> isSection, double firstRoom, double nextRoom, double closing,
        IReadOnlyList<bool>? isPageBreak = null)
    {
        var pages = new List<List<int>> { new() };
        double Room(int page) => page == 0 ? firstRoom : nextRoom;
        double Height(IEnumerable<int> lines) => lines.Sum(i => heights[i]);

        var used = 0.0;
        // A new page; a section title at the foot of the current one moves on with it, to stay over its first parameter.
        void NextPage()
        {
            var page = pages[^1];
            var title = isSection[page[^1]] && page.Count > 1 ? page[^1] : -1;
            if (title >= 0)
                page.RemoveAt(page.Count - 1);
            pages.Add(title >= 0 ? [title] : []);
            used = title >= 0 ? heights[title] : 0;
        }

        for (var i = 0; i < heights.Count; i++)
        {
            if (isPageBreak?[i] == true)
            {
                if (pages[^1].Count > 0)
                    NextPage();
                continue;
            }
            if (used + heights[i] > Room(pages.Count - 1) && pages[^1].Count > 0)
                NextPage();
            pages[^1].Add(i);
            used += heights[i];
        }

        if (closing > 0 && used + closing > Room(pages.Count - 1))
        {
            var page = pages[^1];
            // Keep at least one line on this page; a title right above the moved lines goes with them.
            var take = Math.Min(LinesWithClosing, page.Count - 1);
            if (take > 0 && page.Count - take > 1 && isSection[page[^(take + 1)]])
                take++;
            var moved = page.GetRange(page.Count - take, take);
            if (take > 0 && Height(moved) + closing <= nextRoom)
            {
                page.RemoveRange(page.Count - take, take);
                pages.Add(moved);
            }
            else
                pages.Add([]);
        }
        return pages;
    }
}
