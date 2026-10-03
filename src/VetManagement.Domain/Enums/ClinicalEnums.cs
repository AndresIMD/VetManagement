namespace VetManagement.Domain.Enums;

public enum PreventiveKind
{
    Vaccine,
    Deworming,
    Other
}

public enum PreventiveStatus
{
    UpToDate,
    /// <summary>Next dose within the clinic's reminder window.</summary>
    DueSoon,
    Overdue,
    /// <summary>A later dose of the same series was applied.</summary>
    Superseded,
    /// <summary>No next dose scheduled.</summary>
    NoRepeat
}
