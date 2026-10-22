using System.Text.Json;
using VetManagement.Application.Configuration;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Services;

/// <summary>When the inventory alert email goes out and to whom. The time is the clinic's local time.</summary>
public class InventoryEmailSchedule
{
    public bool Enabled { get; set; }
    public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday, 6=Saturday
    public int Hour { get; set; }
    public int Minute { get; set; }
    public string To { get; set; } = string.Empty; // Comma-separated emails
    public string? Cc { get; set; } // Comma-separated emails
    public string Subject { get; set; } = "Inventory Alerts";
    public bool IncludeLowStock { get; set; } = true;
    public bool IncludeOutOfStock { get; set; } = true;
    public DateTime? LastSentUtc { get; set; }

    public static string[] Addresses(string? list)
        => (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (DayOfWeek is < 0 or > 6 || Hour is < 0 or > 23 || Minute is < 0 or > 59)
            errors.Add(Texts.T("Choose a valid day and time."));
        if (Enabled && Addresses(To).Length == 0)
            errors.Add(Texts.T("Write at least one email address to send the alerts to."));
        return errors;
    }
}

/// <summary>
/// The schedule is a clinic settings document in the database. It used to be a file next to the API
/// (App_Data/inventory_email_schedule.json), which a redeploy could lose; when present, that file starts the document.
/// </summary>
public class InventoryEmailScheduleService(IUnitOfWork unitOfWork, AuditService audit, IHostEnvironment env)
    : ClinicSettingsService<InventoryEmailSchedule>(unitOfWork, audit)
{
    protected override string SettingsKey => "inventory-email-schedule";
    protected override InventoryEmailSchedule CreateDefaults() => ReadFormerFile() ?? new InventoryEmailSchedule();
    protected override List<string> Validate(InventoryEmailSchedule settings) => settings.Validate();

    /// <summary>Saves over the stored schedule (the screen keeps no version: the last save wins, as with the file).</summary>
    public async Task<SettingsUpdateResult> SaveAsync(InventoryEmailSchedule schedule, string userName)
        => await UpdateAsync(schedule, (await GetAsync()).Version, userName);

    private InventoryEmailSchedule? ReadFormerFile()
    {
        var path = Path.Combine(env.ContentRootPath, "App_Data", "inventory_email_schedule.json");
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<InventoryEmailSchedule>(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
