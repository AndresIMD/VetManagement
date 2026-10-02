using VetManagement.Domain.Enums;

namespace VetManagement.Domain.Scheduling;

/// <summary>
/// Per-clinic agenda configuration. Every business policy lives here so each clinic can set its own;
/// see docs/architecture/SCHEDULING.md.
/// </summary>
public class SchedulingSettings
{
    public bool Enabled { get; set; }
    public string TimeZone { get; set; } = "America/Santiago";
    /// <summary>Shown to clients in emails and on the booking site.</summary>
    public string ClinicName { get; set; } = string.Empty;
    public string? ClinicPhone { get; set; }
    public BrandingSettings Branding { get; set; } = new();
    public BookingPolicy Booking { get; set; } = new();
    public CancellationPolicy Cancellation { get; set; } = new();
    public PaymentPolicy Payment { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public List<ServiceDefinition> Services { get; set; } = [];
    public List<ResourceDefinition> Resources { get; set; } = [];
    public List<ScheduleException> Exceptions { get; set; } = [];

    /// <summary>Returns every problem found; an empty list means the settings can be saved.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _))
            errors.Add($"Unknown time zone '{TimeZone}'.");
        if (string.IsNullOrWhiteSpace(ClinicName)) errors.Add("ClinicName is required (clients see it in emails).");
        if (!System.Text.RegularExpressions.Regex.IsMatch(Branding.PrimaryColor ?? "", "^#[0-9A-Fa-f]{6}$"))
            errors.Add("Branding.PrimaryColor must be a hex color like #1565C0.");
        if (Branding.LogoUrl is { Length: > 0 } logo && !(Uri.TryCreate(logo, UriKind.Absolute, out var logoUri) && logoUri.Scheme == Uri.UriSchemeHttps))
            errors.Add("Branding.LogoUrl must be an absolute https URL.");
        if (Booking.MinNoticeMinutes < 0) errors.Add("Booking.MinNoticeMinutes must be 0 or more.");
        if (Booking.HorizonDays < 1) errors.Add("Booking.HorizonDays must be at least 1.");
        if (Booking.SlotStepMinutes < 5) errors.Add("Booking.SlotStepMinutes must be at least 5.");
        if (Cancellation.ClientDeadlineHours < 0) errors.Add("Cancellation.ClientDeadlineHours must be 0 or more.");
        if (Payment.PendingPaymentHoldMinutes < 1) errors.Add("Payment.PendingPaymentHoldMinutes must be at least 1.");
        if (Notifications.ReminderHoursBefore < 1) errors.Add("Notifications.ReminderHoursBefore must be at least 1.");

        AddDuplicates(errors, "service", Services.Select(s => s.Code));
        AddDuplicates(errors, "resource", Resources.Select(r => r.Code));
        var resourceCodes = Resources.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var s in Services)
        {
            if (string.IsNullOrWhiteSpace(s.Code) || string.IsNullOrWhiteSpace(s.Name))
                errors.Add("Every service needs a Code and a Name.");
            if (s.DurationMinutes < 5) errors.Add($"Service '{s.Code}': DurationMinutes must be at least 5.");
            if (s.BufferMinutes < 0) errors.Add($"Service '{s.Code}': BufferMinutes must be 0 or more.");
            if (s.Price < 0) errors.Add($"Service '{s.Code}': Price must be 0 or more.");
            if (s.DepositPercent is < 0 or > 100) errors.Add($"Service '{s.Code}': DepositPercent must be between 0 and 100.");
            if (s.ResourceCodes.Count == 0) errors.Add($"Service '{s.Code}': needs at least one resource.");
            foreach (var code in s.ResourceCodes.Where(c => !resourceCodes.Contains(c)))
                errors.Add($"Service '{s.Code}': unknown resource '{code}'.");
        }

        foreach (var r in Resources)
        {
            if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
                errors.Add("Every resource needs a Code and a Name.");
            if (r.ReleaseThresholdPercent is < 1 or > 100)
                errors.Add($"Resource '{r.Code}': ReleaseThresholdPercent must be between 1 and 100.");
            if (r.Weekly.GroupBy(d => d.Day).Any(g => g.Count() > 1))
                errors.Add($"Resource '{r.Code}': each day of the week may appear only once.");
            foreach (var day in r.Weekly)
                ValidateBlocks(errors, $"Resource '{r.Code}' {day.Day}", day.Blocks);
        }

        foreach (var e in Exceptions)
        {
            var label = $"Exception {e.From:yyyy-MM-dd}..{e.To:yyyy-MM-dd}";
            if (e.To < e.From) errors.Add($"{label}: To is before From.");
            if (e.ResourceCode is not null && !resourceCodes.Contains(e.ResourceCode))
                errors.Add($"{label}: unknown resource '{e.ResourceCode}'.");
            if (e.Kind == ScheduleExceptionKind.Absence && e.ResourceCode is null)
                errors.Add($"{label}: an Absence must name a resource (use Closed for the whole clinic).");
            if (e.Kind == ScheduleExceptionKind.CustomHours)
            {
                if (e.Blocks.Count == 0) errors.Add($"{label}: CustomHours needs at least one block.");
                ValidateBlocks(errors, label, e.Blocks);
            }
        }

        return errors;
    }

    private static void ValidateBlocks(List<string> errors, string label, List<TimeBlock> blocks)
    {
        foreach (var b in blocks)
        {
            if (b.End <= b.Start) errors.Add($"{label}: block {b.Start}-{b.End} must end after it starts.");
            if (b.MaxParallel < 1) errors.Add($"{label}: block {b.Start}-{b.End} needs MaxParallel of at least 1.");
        }
        var ordered = blocks.OrderBy(b => b.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].Start < ordered[i - 1].End)
                errors.Add($"{label}: blocks {ordered[i - 1].Start}-{ordered[i - 1].End} and {ordered[i].Start}-{ordered[i].End} overlap.");
    }

    private static void AddDuplicates(List<string> errors, string what, IEnumerable<string> codes)
    {
        foreach (var dup in codes.GroupBy(c => c, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Duplicate {what} code '{dup.Key}'.");
    }
}

/// <summary>Look of the clinic's public booking portal.</summary>
public class BrandingSettings
{
    /// <summary>Hex color, e.g. "#1565C0".</summary>
    public string PrimaryColor { get; set; } = "#1565C0";
    /// <summary>Absolute https URL of the clinic's logo (optional).</summary>
    public string? LogoUrl { get; set; }
}

public class BookingPolicy
{
    /// <summary>Public bookings must start at least this far in the future.</summary>
    public int MinNoticeMinutes { get; set; } = 60;
    /// <summary>Public bookings can be made at most this many days ahead.</summary>
    public int HorizonDays { get; set; } = 60;
    /// <summary>Distance between consecutive slot start times.</summary>
    public int SlotStepMinutes { get; set; } = 15;
}

public class CancellationPolicy
{
    /// <summary>Clients can cancel online up to this many hours before the appointment.</summary>
    public int ClientDeadlineHours { get; set; } = 24;
    public RefundMode RefundMode { get; set; } = RefundMode.ManualApproval;
}

public class PaymentPolicy
{
    /// <summary>A booking waiting for its WebPay deposit holds the slot this long, then is released.</summary>
    public int PendingPaymentHoldMinutes { get; set; } = 15;
}

/// <summary>Each client notification is switched on or off independently.</summary>
public class NotificationSettings
{
    public bool SendConfirmation { get; set; } = true;
    public bool SendReminder { get; set; } = true;
    public int ReminderHoursBefore { get; set; } = 24;
    public bool SendRescheduleNotice { get; set; } = true;
}

public class ServiceDefinition
{
    /// <summary>Stable identifier stored on appointments; never reuse a code for a different service.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Module switch: a disabled service can't be booked.</summary>
    public bool Enabled { get; set; }
    public bool AllowOnlineBooking { get; set; } = true;
    public int DurationMinutes { get; set; } = 30;
    /// <summary>Preparation time after each appointment; not bookable.</summary>
    public int BufferMinutes { get; set; }
    public int Price { get; set; }
    /// <summary>Share of the price paid upfront through WebPay (0 = no deposit).</summary>
    public int DepositPercent { get; set; }
    /// <summary>Resources (vets or rooms) able to perform the service.</summary>
    public List<string> ResourceCodes { get; set; } = [];
}

public class ResourceDefinition
{
    /// <summary>Stable identifier stored on appointments.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ResourceKind Kind { get; set; }
    public bool Enabled { get; set; } = true;
    public List<WeeklyAvailability> Weekly { get; set; } = [];
    /// <summary>Public sees one block at a time; the next opens when the previous ones are full enough.</summary>
    public bool ProgressiveRelease { get; set; }
    /// <summary>Booked share (in minutes) a block needs before the next one is released.</summary>
    public int ReleaseThresholdPercent { get; set; } = 100;
}

public class WeeklyAvailability
{
    public DayOfWeek Day { get; set; }
    public List<TimeBlock> Blocks { get; set; } = [];
}

public class TimeBlock
{
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    /// <summary>Appointments that may overlap inside this block (1 = one at a time).</summary>
    public int MaxParallel { get; set; } = 1;
    /// <summary>Shown to the public as extra capacity ("sobrecupo").</summary>
    public bool IsOverflow { get; set; }
}

public class ScheduleException
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    /// <summary>Null applies to the whole clinic.</summary>
    public string? ResourceCode { get; set; }
    public ScheduleExceptionKind Kind { get; set; }
    /// <summary>Hours that replace the weekly blocks when <see cref="Kind"/> is CustomHours.</summary>
    public List<TimeBlock> Blocks { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}
