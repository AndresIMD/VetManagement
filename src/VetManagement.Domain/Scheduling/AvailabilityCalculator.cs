using VetManagement.Domain.Enums;

namespace VetManagement.Domain.Scheduling;

public enum BookingAudience
{
    /// <summary>Online clients: progressive release, minimum notice and horizon apply.</summary>
    Public,
    /// <summary>Clinic staff: sees every block of the day.</summary>
    Staff
}

/// <summary>Time a resource is already taken (an active appointment including its buffer).</summary>
public record BookedInterval(string ResourceCode, DateTime StartUtc, DateTime EndUtc);

public record AvailableSlot(string ResourceCode, DateTime StartUtc, DateTime EndUtc, bool IsOverflow);

/// <summary>
/// Computes bookable slots for a service from the clinic's settings and existing bookings. Pure: no I/O,
/// the current time is passed in. See docs/architecture/SCHEDULING.md.
/// </summary>
public static class AvailabilityCalculator
{
    public static IReadOnlyList<AvailableSlot> GetSlots(
        SchedulingSettings settings,
        string serviceCode,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<BookedInterval> booked,
        DateTime nowUtc,
        BookingAudience audience)
    {
        var isPublic = audience == BookingAudience.Public;
        var service = settings.Services.FirstOrDefault(s => string.Equals(s.Code, serviceCode, StringComparison.OrdinalIgnoreCase));
        if (service is null || !service.Enabled)
            return [];
        if (isPublic && (!settings.Enabled || !service.AllowOnlineBooking))
            return [];

        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
        var earliestStartUtc = isPublic ? nowUtc.AddMinutes(settings.Booking.MinNoticeMinutes) : nowUtc;
        var lastPublicDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone)).AddDays(settings.Booking.HorizonDays);
        if (isPublic && to > lastPublicDate)
            to = lastPublicDate;

        var resources = settings.Resources
            .Where(r => r.Enabled && service.ResourceCodes.Contains(r.Code, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var slots = new List<AvailableSlot>();
        foreach (var resource in resources)
        {
            var resourceBookings = booked
                .Where(b => string.Equals(b.ResourceCode, resource.Code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var blocksReleased = true; // progressive release: false once an earlier block still has room
                foreach (var block in BlocksFor(settings, resource, date).OrderBy(b => b.Start))
                {
                    var blockSlots = SlotsInBlock(settings, service, resource.Code, block, date, zone, resourceBookings, earliestStartUtc);

                    if (isPublic && resource.ProgressiveRelease)
                    {
                        if (!blocksReleased)
                            break;
                        // The next block opens once this one is full enough, or nothing in it can be booked anymore.
                        blocksReleased = blockSlots.Count == 0
                            || OccupancyPercent(block, date, zone, resourceBookings) >= resource.ReleaseThresholdPercent;
                    }

                    slots.AddRange(blockSlots);
                }
            }
        }

        return slots.OrderBy(s => s.StartUtc).ThenBy(s => s.ResourceCode).ToList();
    }

    /// <summary>The day's blocks: weekly hours, replaced by CustomHours and removed by Closed/Absence.</summary>
    public static List<TimeBlock> BlocksFor(SchedulingSettings settings, ResourceDefinition resource, DateOnly date)
    {
        var exceptions = settings.Exceptions
            .Where(e => date >= e.From && date <= e.To
                && (e.ResourceCode is null || string.Equals(e.ResourceCode, resource.Code, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (exceptions.Any(e => e.Kind is ScheduleExceptionKind.Closed or ScheduleExceptionKind.Absence))
            return [];

        // A resource-specific CustomHours wins over a clinic-wide one.
        var custom = exceptions
            .Where(e => e.Kind == ScheduleExceptionKind.CustomHours)
            .OrderBy(e => e.ResourceCode is null)
            .FirstOrDefault();
        if (custom is not null)
            return custom.Blocks;

        return resource.Weekly.FirstOrDefault(w => w.Day == date.DayOfWeek)?.Blocks ?? [];
    }

    private static List<AvailableSlot> SlotsInBlock(
        SchedulingSettings settings, ServiceDefinition service, string resourceCode, TimeBlock block,
        DateOnly date, TimeZoneInfo zone, List<BookedInterval> bookings, DateTime earliestStartUtc)
    {
        var result = new List<AvailableSlot>();
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        var occupied = duration + TimeSpan.FromMinutes(service.BufferMinutes);
        var step = TimeSpan.FromMinutes(settings.Booking.SlotStepMinutes);

        for (var start = block.Start.ToTimeSpan(); start + duration <= block.End.ToTimeSpan(); start += step)
        {
            var local = date.ToDateTime(TimeOnly.FromTimeSpan(start));
            if (zone.IsInvalidTime(local))
                continue; // skipped by a DST change
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
            if (startUtc < earliestStartUtc)
                continue;
            if (MaxConcurrent(bookings, startUtc, startUtc + occupied) >= block.MaxParallel)
                continue;

            result.Add(new AvailableSlot(resourceCode, startUtc, startUtc + duration, block.IsOverflow));
        }

        return result;
    }

    /// <summary>Highest number of bookings overlapping at any instant of [startUtc, endUtc).</summary>
    private static int MaxConcurrent(List<BookedInterval> bookings, DateTime startUtc, DateTime endUtc)
    {
        var overlapping = bookings.Where(b => b.StartUtc < endUtc && b.EndUtc > startUtc).ToList();
        if (overlapping.Count == 0)
            return 0;

        // Concurrency only changes where an interval starts, so checking those instants is enough.
        return overlapping
            .Select(b => b.StartUtc > startUtc ? b.StartUtc : startUtc)
            .Max(instant => overlapping.Count(o => o.StartUtc <= instant && o.EndUtc > instant));
    }

    /// <summary>Booked minutes inside the block as a share of its total capacity.</summary>
    private static double OccupancyPercent(TimeBlock block, DateOnly date, TimeZoneInfo zone, List<BookedInterval> bookings)
    {
        var blockStartUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(block.Start), zone);
        var blockEndUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(block.End), zone);
        var capacityMinutes = (blockEndUtc - blockStartUtc).TotalMinutes * block.MaxParallel;

        var bookedMinutes = bookings.Sum(b =>
        {
            var start = b.StartUtc > blockStartUtc ? b.StartUtc : blockStartUtc;
            var end = b.EndUtc < blockEndUtc ? b.EndUtc : blockEndUtc;
            return end > start ? (end - start).TotalMinutes : 0;
        });

        return capacityMinutes <= 0 ? 100 : bookedMinutes / capacityMinutes * 100;
    }
}
