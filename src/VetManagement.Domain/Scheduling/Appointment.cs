using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Scheduling;

/// <summary>
/// A booked slot. Service and resource are referenced by their stable codes from the scheduling settings;
/// price and deposit are copied at booking time so later price changes don't alter existing bookings.
/// </summary>
public class Appointment : Entity<int>
{
    public Appointment() : base(0) { }

    public string ServiceCode { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    /// <summary>End plus the service's buffer: the resource is busy until then.</summary>
    public DateTime OccupiedUntilUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;
    public AppointmentSource Source { get; set; } = AppointmentSource.Staff;
    /// <summary>Booked by staff beyond the available capacity.</summary>
    public bool IsOverbooked { get; set; }

    /// <summary>Existing client record, when known (online bookings are matched by tax id).</summary>
    public int? ClientId { get; set; }
    public int? PetId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerTaxId { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerPhone { get; set; }
    public string? PetName { get; set; }
    public string? Notes { get; set; }

    public int Price { get; set; }
    public int DepositAmount { get; set; }
    /// <summary>While waiting for the WebPay deposit, the slot is held until this time.</summary>
    public DateTime? PaymentHoldUntilUtc { get; set; }

    /// <summary>Set once the reminder email went out, so it is never sent twice.</summary>
    public DateTime? ReminderSentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>Whether this appointment still takes its slot at <paramref name="nowUtc"/>.</summary>
    public bool OccupiesSlot(DateTime nowUtc) => Status switch
    {
        AppointmentStatus.Confirmed or AppointmentStatus.NeedsReschedule => true,
        AppointmentStatus.PendingPayment => PaymentHoldUntilUtc > nowUtc,
        _ => false
    };
}
