using MudBlazor;

namespace VetManagement.Staff.UI.Layout;

/// <summary>The figures a home page card loads besides its links.</summary>
public enum HomeData
{
    None,
    /// <summary>Items out of stock and with low stock; for whoever sees the metrics, what runs out soon.</summary>
    Stock,
    /// <summary>Open consumption sheets, and how many need something.</summary>
    PatientConsumption,
    /// <summary>No figures yet: the card offers a new report when the user may write one.</summary>
    Exams
}

/// <summary>A card on the home page: an area, where its name leads, and its other pages as links.</summary>
public sealed record HomeArea(string Title, string? Icon, string Href, HomeData Data, IReadOnlyList<MenuItem> Links);

/// <summary>
/// The home page's cards, in the menu's order of use, each one only when the user can open something in it. Patient
/// consumption gets a card of its own: clinical staff use it daily without seeing the rest of the inventory.
/// </summary>
public static class HomePage
{
    /// <summary>Links under a card's name; the menu holds the rest.</summary>
    public const int MaxLinks = 3;

    private const string ConsumptionHref = "consumption";
    private const string InventoryHref = "inventory";
    private const string ExamReportsHref = "exam-reports";

    public static IReadOnlyList<HomeArea> AreasFor(Func<string, bool> has)
    {
        var cards = new List<HomeArea>();
        foreach (var area in AppNavigation.Areas.Where(area => area.Visible(has)))
        {
            var pages = area.Children?.Where(page => page.Visible(has)).ToList() ?? [];
            var consumption = pages.FirstOrDefault(page => page.Href == ConsumptionHref);
            if (consumption is not null)
                pages.Remove(consumption);

            var data = area.Children?.Any(page => page.Href == InventoryHref) == true && has("inventory.read") ? HomeData.Stock
                : area.Children?.Any(page => page.Href == ExamReportsHref) == true ? HomeData.Exams
                : HomeData.None;
            if (area.Href is { } href)
                cards.Add(new(area.Title, area.Icon, href, data, []));
            else if (pages.Count > 0)
                cards.Add(new(area.Title, area.Icon, pages[0].Href!, data, [.. pages.Skip(1).Take(MaxLinks)]));

            if (consumption is not null)
                cards.Add(new(consumption.Title, Icons.Material.Filled.Assignment, ConsumptionHref, HomeData.PatientConsumption, []));
        }
        return cards;
    }
}
