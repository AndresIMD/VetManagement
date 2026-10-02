using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

/// <summary>
/// Booking rules shared by staff and online booking: what can be booked and whether a start time is free.
/// Callers hold the per-resource lock (<see cref="BookingLockKey"/>) while checking and inserting.
/// </summary>
public class AgendaRules(IUnitOfWork unitOfWork, TimeProvider clock)
{
    public static ServiceDefinition? FindBookableService(SchedulingSettings settings, string serviceCode, string resourceCode, out string? error)
    {
        error = null;
        var service = settings.Services.FirstOrDefault(s => string.Equals(s.Code, serviceCode, StringComparison.OrdinalIgnoreCase));
        if (service is null || !service.Enabled)
            error = $"Service '{serviceCode}' is not available.";
        else if (!service.ResourceCodes.Contains(resourceCode, StringComparer.OrdinalIgnoreCase)
                 || settings.Resources.All(r => !r.Enabled || !string.Equals(r.Code, resourceCode, StringComparison.OrdinalIgnoreCase)))
            error = $"Resource '{resourceCode}' can't perform '{serviceCode}'.";
        return error is null ? service : null;
    }

    /// <summary>
    /// The start must be one of the slots the calculator offers to <paramref name="audience"/> (staff may overbook).
    /// For the public this also applies progressive release, minimum notice and horizon.
    /// </summary>
    public async Task<bool> IsSlotFreeAsync(SchedulingSettings settings, ServiceDefinition service, string resourceCode, DateTime startUtc,
        bool overbook = false, int? ignoreAppointmentId = null, BookingAudience audience = BookingAudience.Staff)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (overbook)
            return audience == BookingAudience.Staff && startUtc >= now;

        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startUtc, zone));
        var (fromUtc, toUtc) = UtcRange(date, date, zone);
        var booked = (await unitOfWork.Appointments.GetOverlappingAsync(fromUtc, toUtc, resourceCode))
            .Where(a => a.Id != ignoreAppointmentId && a.OccupiesSlot(now))
            .Select(a => new BookedInterval(a.ResourceCode, a.StartUtc, a.OccupiedUntilUtc))
            .ToList();

        return AvailabilityCalculator.GetSlots(settings, service.Code, date, date, booked, now, audience)
            .Any(s => string.Equals(s.ResourceCode, resourceCode, StringComparison.OrdinalIgnoreCase) && s.StartUtc == startUtc);
    }

    /// <summary>Bookings for the same resource are serialized; different resources proceed in parallel.</summary>
    public static string BookingLockKey(string resourceCode) => $"booking:{resourceCode.ToLowerInvariant()}";

    /// <summary>UTC bounds of whole local days, padded one day so bookings crossing midnight are included.</summary>
    public static (DateTime FromUtc, DateTime ToUtc) UtcRange(DateOnly from, DateOnly to, TimeZoneInfo zone)
        => (TimeZoneInfo.ConvertTimeToUtc(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), zone),
            TimeZoneInfo.ConvertTimeToUtc(to.AddDays(2).ToDateTime(TimeOnly.MinValue), zone));
}
