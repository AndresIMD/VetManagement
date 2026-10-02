namespace VetManagement.Domain.Enums;

public enum ResourceKind
{
    Vet,
    Room
}

/// <summary>What happens to a paid deposit when a client cancels before the deadline.</summary>
public enum RefundMode
{
    Automatic,
    NoRefund,
    ManualApproval
}

public enum ScheduleExceptionKind
{
    /// <summary>No bookings (holiday, clinic closed, resource on leave).</summary>
    Closed,
    /// <summary>A resource is absent; its confirmed appointments need rescheduling.</summary>
    Absence,
    /// <summary>Replaces the weekly blocks with <c>Blocks</c> for the given dates.</summary>
    CustomHours
}
