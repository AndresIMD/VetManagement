using System.Text.Json;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

public sealed record BookingRequest(
    string ServiceCode,
    string ResourceCode,
    DateTime StartUtc,
    bool Overbook,
    string OwnerName,
    int? ClientId = null,
    int? PetId = null,
    string? OwnerTaxId = null,
    string? OwnerEmail = null,
    string? OwnerPhone = null,
    string? PetName = null,
    string? Notes = null);

public enum BookingStatus
{
    Done,
    /// <summary>The slot is taken, outside the resource's hours, or in the past.</summary>
    SlotUnavailable,
    /// <summary>The service or resource can't be booked (disabled, unknown, or not able to perform it).</summary>
    Invalid,
    NotFound
}

public sealed record BookingResult(BookingStatus Status, int? AppointmentId = null, string? Error = null);

/// <summary>
/// Staff-side agenda operations. Every booking and move re-checks availability while holding an exclusive
/// per-resource lock, so two people can never take the same last slot.
/// </summary>
public class AppointmentService(IUnitOfWork unitOfWork, SchedulingSettingsService settingsService, AuditService audit, AppointmentNotifier notifier, AgendaRules rules, TimeProvider clock)
{
    private const string EntityName = "Appointment";

    public async Task<IReadOnlyList<AvailableSlot>?> GetAvailabilityAsync(string serviceCode, DateOnly from, DateOnly to, BookingAudience audience)
    {
        var settings = (await settingsService.GetAsync()).Settings;
        if (settings.Services.All(s => !string.Equals(s.Code, serviceCode, StringComparison.OrdinalIgnoreCase)))
            return null;

        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
        var (fromUtc, toUtc) = AgendaRules.UtcRange(from, to, zone);
        var now = clock.GetUtcNow().UtcDateTime;
        var booked = (await unitOfWork.Appointments.GetOverlappingAsync(fromUtc, toUtc))
            .Where(a => a.OccupiesSlot(now))
            .Select(a => new BookedInterval(a.ResourceCode, a.StartUtc, a.OccupiedUntilUtc))
            .ToList();

        return AvailabilityCalculator.GetSlots(settings, serviceCode, from, to, booked, now, audience);
    }

    public async Task<List<Appointment>> ListAsync(DateTime fromUtc, DateTime toUtc, string? resourceCode, AppointmentStatus? status)
        => (await unitOfWork.Appointments.GetOverlappingAsync(fromUtc, toUtc, resourceCode))
            .Where(a => status is null || a.Status == status)
            .ToList();

    /// <summary>Staff booking. With <see cref="BookingRequest.Overbook"/> the capacity check is skipped (sobrecupo).</summary>
    public async Task<BookingResult> BookAsync(BookingRequest request, string userName)
    {
        var settings = (await settingsService.GetAsync()).Settings;
        var service = AgendaRules.FindBookableService(settings, request.ServiceCode, request.ResourceCode, out var error);
        if (service is null)
            return new BookingResult(BookingStatus.Invalid, Error: error);

        Appointment? booked = null;
        var result = await unitOfWork.ExecuteExclusiveAsync(AgendaRules.BookingLockKey(request.ResourceCode), async () =>
        {
            if (!await rules.IsSlotFreeAsync(settings, service, request.ResourceCode, request.StartUtc, request.Overbook, ignoreAppointmentId: null))
                return new BookingResult(BookingStatus.SlotUnavailable);

            var appointment = new Appointment
            {
                ServiceCode = service.Code,
                ResourceCode = request.ResourceCode,
                StartUtc = request.StartUtc,
                EndUtc = request.StartUtc.AddMinutes(service.DurationMinutes),
                OccupiedUntilUtc = request.StartUtc.AddMinutes(service.DurationMinutes + service.BufferMinutes),
                Status = AppointmentStatus.Confirmed,
                Source = AppointmentSource.Staff,
                IsOverbooked = request.Overbook,
                ClientId = request.ClientId,
                PetId = request.PetId,
                OwnerName = request.OwnerName,
                OwnerTaxId = request.OwnerTaxId,
                OwnerEmail = request.OwnerEmail,
                OwnerPhone = request.OwnerPhone,
                PetName = request.PetName,
                Notes = request.Notes,
                Price = service.Price,
                DepositAmount = service.Price * service.DepositPercent / 100,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
                CreatedBy = userName
            };
            await unitOfWork.Appointments.AddAsync(appointment);
            await unitOfWork.SaveChangesAsync();
            booked = appointment;
            return new BookingResult(BookingStatus.Done, appointment.Id);
        });

        if (result.Status == BookingStatus.Done)
        {
                await audit.LogAsync(EntityName, result.AppointmentId!.Value, AuditActionType.Add, JsonSerializer.Serialize(request), userName);
            await notifier.SendConfirmationAsync(booked!, settings);
        }
        return result;
    }

    public async Task<BookingResult> RescheduleAsync(int id, string resourceCode, DateTime startUtc, bool overbook, string userName)
    {
        var settings = (await settingsService.GetAsync()).Settings;

        Appointment? moved = null;
        var result = await unitOfWork.ExecuteExclusiveAsync(AgendaRules.BookingLockKey(resourceCode), async () =>
        {
            var appointment = await unitOfWork.Appointments.GetByIdAsync(id);
            if (appointment is null)
                return new BookingResult(BookingStatus.NotFound);
            if (appointment.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.NeedsReschedule))
                return new BookingResult(BookingStatus.Invalid, Error: $"A {appointment.Status} appointment can't be moved.");

            var service = AgendaRules.FindBookableService(settings, appointment.ServiceCode, resourceCode, out var error);
            if (service is null)
                return new BookingResult(BookingStatus.Invalid, Error: error);
            if (!await rules.IsSlotFreeAsync(settings, service, resourceCode, startUtc, overbook, ignoreAppointmentId: id))
                return new BookingResult(BookingStatus.SlotUnavailable);

            appointment.ResourceCode = resourceCode;
            appointment.StartUtc = startUtc;
            appointment.EndUtc = startUtc.AddMinutes(service.DurationMinutes);
            appointment.OccupiedUntilUtc = startUtc.AddMinutes(service.DurationMinutes + service.BufferMinutes);
            appointment.Status = AppointmentStatus.Confirmed;
            appointment.IsOverbooked = overbook;
            await unitOfWork.SaveChangesAsync();
            moved = appointment;
            return new BookingResult(BookingStatus.Done, id);
        });

        if (result.Status == BookingStatus.Done)
        {
                await audit.LogAsync(EntityName, id, AuditActionType.Edit, $"Rescheduled to {resourceCode} at {startUtc:O} (overbook: {overbook})", userName);
            await notifier.SendChangeAsync(moved!, settings, AppointmentChange.Rescheduled);
        }
        return result;
    }

    public async Task<BookingResult> CancelAsync(int id, string? reason, string userName)
    {
        var appointment = await unitOfWork.Appointments.GetByIdAsync(id);
        if (appointment is null)
            return new BookingResult(BookingStatus.NotFound);
        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed or AppointmentStatus.NoShow)
            return new BookingResult(BookingStatus.Invalid, Error: $"A {appointment.Status} appointment can't be cancelled.");

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = clock.GetUtcNow().UtcDateTime;
        appointment.CancelledBy = userName;
        appointment.CancelReason = reason;
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync(EntityName, id, AuditActionType.Delete, $"Cancelled: {reason}", userName);
        await notifier.SendChangeAsync(appointment, (await settingsService.GetAsync()).Settings, AppointmentChange.Cancelled);
        return new BookingResult(BookingStatus.Done, id);
    }

    /// <summary>
    /// After the settings change, flags upcoming confirmed appointments whose day is now Closed or whose
    /// resource is Absent, so staff can move them. Returns how many were flagged.
    /// </summary>
    public async Task<int> FlagAffectedByExceptionsAsync(string userName)
    {
        var settings = (await settingsService.GetAsync()).Settings;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
        var upcoming = await unitOfWork.Appointments.GetByStatusFromAsync(AppointmentStatus.Confirmed, clock.GetUtcNow().UtcDateTime);

        var affected = upcoming.Where(a =>
        {
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, zone));
            return settings.Exceptions.Any(e =>
                e.Kind is ScheduleExceptionKind.Closed or ScheduleExceptionKind.Absence
                && date >= e.From && date <= e.To
                && (e.ResourceCode is null || string.Equals(e.ResourceCode, a.ResourceCode, StringComparison.OrdinalIgnoreCase)));
        }).ToList();

        foreach (var appointment in affected)
            appointment.Status = AppointmentStatus.NeedsReschedule;
        if (affected.Count == 0)
            return 0;

        await unitOfWork.SaveChangesAsync();
        foreach (var appointment in affected)
            await audit.LogAsync(EntityName, appointment.Id, AuditActionType.Edit, "Needs reschedule: resource unavailable that day", userName);
        return affected.Count;
    }

    /// <summary>
    /// Emails the reminder for confirmed appointments starting within the configured window, once each.
    /// A failed send is retried on the next run. Returns how many were sent.
    /// </summary>
    public async Task<int> SendDueRemindersAsync()
    {
        var settings = (await settingsService.GetAsync()).Settings;
        if (!settings.Notifications.SendReminder)
            return 0;

        var now = clock.GetUtcNow().UtcDateTime;
        var due = (await unitOfWork.Appointments.GetByStatusFromAsync(
                AppointmentStatus.Confirmed, now, now.AddHours(settings.Notifications.ReminderHoursBefore)))
            .Where(a => a.ReminderSentAtUtc is null && !string.IsNullOrWhiteSpace(a.OwnerEmail))
            .ToList();

        var sent = 0;
        foreach (var appointment in due)
        {
            if (!await notifier.SendReminderAsync(appointment, settings))
                continue;
            appointment.ReminderSentAtUtc = now;
            sent++;
        }
        if (sent > 0)
            await unitOfWork.SaveChangesAsync();
        return sent;
    }

}
