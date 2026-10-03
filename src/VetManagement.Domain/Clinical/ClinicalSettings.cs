using VetManagement.Domain.Enums;

namespace VetManagement.Domain.Clinical;

/// <summary>Per-clinic clinical policy: preventive protocols and owner reminders (see docs/architecture/CLINICAL.md).</summary>
public class ClinicalSettings
{
    /// <summary>Starting protocols; placeholders each clinic's vets adjust to their own schedule.</summary>
    public List<PreventiveProtocol> Protocols { get; set; } =
    [
        new() { Code = "rabies", Name = "Antirrábica", Kind = PreventiveKind.Vaccine, IntervalDays = 365 },
        new() { Code = "dog-multiple", Name = "Óctuple / Séxtuple", Kind = PreventiveKind.Vaccine, Species = Species.Dog, IntervalDays = 365 },
        new() { Code = "cat-triple", Name = "Triple felina", Kind = PreventiveKind.Vaccine, Species = Species.Cat, IntervalDays = 365 },
        new() { Code = "deworm-internal", Name = "Desparasitación interna", Kind = PreventiveKind.Deworming, IntervalDays = 90 },
        new() { Code = "deworm-external", Name = "Desparasitación externa", Kind = PreventiveKind.Deworming, IntervalDays = 30 }
    ];

    public PreventiveReminderSettings Reminders { get; set; } = new();

    /// <summary>Supplies used in visits and inventory products of applied doses leave stock (and go back when removed).</summary>
    public bool DeductStockOnUse { get; set; } = true;

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (Reminders.DaysBefore is < 0 or > 90) errors.Add("Reminders.DaysBefore must be between 0 and 90.");
        foreach (var p in Protocols)
        {
            if (string.IsNullOrWhiteSpace(p.Code) || string.IsNullOrWhiteSpace(p.Name))
                errors.Add("Every protocol needs a Code and a Name.");
            if (p.IntervalDays is < 0 or > 3650)
                errors.Add($"Protocol '{p.Code}': IntervalDays must be between 0 (no repeat) and 3650.");
        }
        foreach (var code in Protocols.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add($"Duplicate protocol code '{code}'.");
        return errors;
    }

    public PreventiveProtocol? FindProtocol(string? code)
        => Protocols.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
}

public class PreventiveProtocol
{
    /// <summary>Stable code stored on doses; rename <see cref="Name"/> freely, never the code.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PreventiveKind Kind { get; set; }
    /// <summary>Only offered for this species; null = any.</summary>
    public Species? Species { get; set; }
    /// <summary>Days until the next dose; 0 = single dose.</summary>
    public int IntervalDays { get; set; }
    public bool Enabled { get; set; } = true;
}

public class PreventiveReminderSettings
{
    /// <summary>Email the owner before a dose is due.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>How many days before the due date the reminder goes out (also the "due soon" window).</summary>
    public int DaysBefore { get; set; } = 7;
}
