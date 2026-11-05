using MudBlazor;

namespace VetManagement.Staff.UI.Layout;

/// <summary>A menu entry: a page (<see cref="Href"/>) or a group of pages (<see cref="Children"/>). Titles are text keys.</summary>
public class MenuItem
{
    public string Title { get; set; } = string.Empty;
    public string? Href { get; set; }
    public string? Icon { get; set; }
    public string[] RequiredPerms { get; set; } = [];
    public List<MenuItem>? Children { get; set; }

    /// <summary>A page needs any of its permissions; a group shows when one of its pages does.</summary>
    public bool Visible(Func<string, bool> has)
        => Children is null ? RequiredPerms.Length == 0 || RequiredPerms.Any(has) : Children.Any(c => c.Visible(has));

    /// <summary>Where the entry leads: its page, or the first page of the group the user can see.</summary>
    public string? MainHref(Func<string, bool> has) => Href ?? Children?.FirstOrDefault(c => c.Visible(has))?.Href;
}

/// <summary>
/// The staff menu, also used by the home page. Areas go in order of use, and what is set up once or rarely lives in
/// <see cref="Settings"/>, so it doesn't get the same weight as the daily work (DESIGN.md).
/// </summary>
public static class AppNavigation
{
    public static IReadOnlyList<MenuItem> Areas { get; } =
    [
        new() { Title = "Medical Visits", Href = "medical-visits", Icon = Icons.Material.Filled.MedicalServices, RequiredPerms = ["medical.read"] },
        new()
        {
            Title = "Billing",
            Icon = Icons.Material.Filled.PointOfSale,
            Children =
            [
                new() { Title = "Sales", Href = "billing", RequiredPerms = ["billing.read"] },
                new() { Title = "Cash Close", Href = "billing/cash", RequiredPerms = ["billing.read"] }
            ]
        },
        new()
        {
            Title = "Exams",
            Icon = Icons.Material.Filled.Science,
            Children =
            [
                // Lab staff work on Lab Reports; requests are made by clinicians and followed up by the lab supervisor.
                new() { Title = "Lab Reports", Href = "exam-reports", RequiredPerms = ["examsperformed.read"] },
                new() { Title = "Requests", Href = "exam-requests", RequiredPerms = ["medical.read", "exams.update"] }
            ]
        },
        new()
        {
            Title = "Inventory",
            Icon = Icons.Material.Filled.Inventory,
            Children =
            [
                new() { Title = "Dashboard", Href = "inventory", RequiredPerms = ["inventory.read"] },
                new() { Title = "Items", Href = "inventory-items", RequiredPerms = ["inventory.read"] },
                new() { Title = "Patient consumption", Href = "consumption", RequiredPerms = ["consumption.read"] },
                new() { Title = "Audit", Href = "inventory-audit", RequiredPerms = ["audit.read"] },
                new() { Title = "Metrics", Href = "inventory-metrics", RequiredPerms = ["inventory.metrics"] }
            ]
        },
        new()
        {
            Title = "Clients",
            Icon = Icons.Material.Filled.Pets,
            Children =
            [
                new() { Title = "Search", Href = "client-search", RequiredPerms = ["clientspets.list"] },
                new() { Title = "Vaccines Due", Href = "clinical/due", RequiredPerms = ["medical.read"] }
            ]
        },
        new() { Title = "Agenda", Href = "agenda", Icon = Icons.Material.Filled.CalendarMonth, RequiredPerms = ["scheduling.read"] },
        new() { Title = "Reports", Href = "reports", Icon = Icons.Material.Filled.Insights, RequiredPerms = ["reports.read"] }
    ];

    public static MenuItem Settings { get; } = new()
    {
        Title = "Settings",
        Icon = Icons.Material.Filled.Settings,
        Children =
        [
            new() { Title = "Clinical Settings", Href = "clinical/settings", RequiredPerms = ["clinical.manage"] },
            new() { Title = "Billing Settings", Href = "billing/settings", RequiredPerms = ["billing.manage"] },
            new() { Title = "Exam Catalog", Href = "exams", RequiredPerms = ["exams.update"] },
            new() { Title = "Lab Machines", Href = "lab-machines", RequiredPerms = ["exams.update"] },
            new() { Title = "External Labs", Href = "external-labs", RequiredPerms = ["externallabs.update"] },
            new() { Title = "Report Design", Href = "exam-reports/settings", RequiredPerms = ["exams.update"] },
            new() { Title = "Warehouses", Href = "inventory/warehouses", RequiredPerms = ["inventory.warehouses"] },
            new() { Title = "Email Alerts", Href = "inventory-alerts", RequiredPerms = ["system.send-email"] },
            new() { Title = "Agenda Settings", Href = "agenda/settings", RequiredPerms = ["scheduling.manage"] },
            new() { Title = "Users", Href = "admin/users", RequiredPerms = ["users.read"] },
            new() { Title = "Test Data", Href = "admin/test-data", RequiredPerms = ["system.test-data"] }
        ]
    };

    public static IEnumerable<MenuItem> All => Areas.Append(Settings);
}
