using Microsoft.Extensions.Logging;
using VetManagement.Application.Contracts.Services;
using VetManagement.Domain.Localization;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

public enum AppointmentChange
{
    Rescheduled,
    Cancelled
}

/// <summary>
/// Client emails for appointments. Each kind is switched on/off in the clinic's notification settings.
/// Sending never fails the operation that triggered it: errors are logged and the booking stands.
/// Texts are in the clinic's language (<see cref="Texts"/>); times are shown in the clinic's time zone.
/// </summary>
public class AppointmentNotifier(IEmailSender email, ILogger<AppointmentNotifier> logger)
{
    /// <returns>Whether an email was sent.</returns>
    public Task<bool> SendConfirmationAsync(Appointment appointment, SchedulingSettings settings)
        => settings.Notifications.SendConfirmation
            ? SendAsync(appointment, settings, Texts.T("Your appointment is confirmed – {0}", settings.ClinicName),
                Texts.T("Your appointment is booked:") + Environment.NewLine + Details(appointment, settings))
            : Task.FromResult(false);

    public Task<bool> SendChangeAsync(Appointment appointment, SchedulingSettings settings, AppointmentChange change)
    {
        if (!settings.Notifications.SendRescheduleNotice)
            return Task.FromResult(false);

        return change == AppointmentChange.Rescheduled
            ? SendAsync(appointment, settings, Texts.T("Your appointment changed – {0}", settings.ClinicName),
                Texts.T("We had to move your appointment. The new date is:") + Environment.NewLine + Details(appointment, settings))
            : SendAsync(appointment, settings, Texts.T("Your appointment was cancelled – {0}", settings.ClinicName),
                Texts.T("We're sorry to tell you that your appointment was cancelled:") + Environment.NewLine + Details(appointment, settings) + Environment.NewLine +
                Texts.T("Contact us to book a new one."));
    }

    public Task<bool> SendReminderAsync(Appointment appointment, SchedulingSettings settings)
        => settings.Notifications.SendReminder
            ? SendAsync(appointment, settings, Texts.T("Appointment reminder – {0}", settings.ClinicName),
                Texts.T("This is a reminder of your upcoming appointment:") + Environment.NewLine + Details(appointment, settings))
            : Task.FromResult(false);

    private async Task<bool> SendAsync(Appointment appointment, SchedulingSettings settings, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(appointment.OwnerEmail))
            return false;

        var contact = string.IsNullOrWhiteSpace(settings.ClinicPhone) ? "" : Texts.T(" at {0}", settings.ClinicPhone);
        var body = Texts.T("Hello {0},", appointment.OwnerName) + Environment.NewLine + Environment.NewLine + message + Environment.NewLine + Environment.NewLine +
                   Texts.T("If you need to change or cancel your appointment, contact us{0}.", contact) + Environment.NewLine + Environment.NewLine + settings.ClinicName;
        try
        {
            await email.SendAsync(appointment.OwnerEmail, subject, body);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send '{Subject}' for appointment {AppointmentId}", subject, appointment.Id);
            return false;
        }
    }

    private static string Details(Appointment appointment, SchedulingSettings settings)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(appointment.StartUtc, zone);
        var service = settings.Services.FirstOrDefault(s => string.Equals(s.Code, appointment.ServiceCode, StringComparison.OrdinalIgnoreCase))?.Name ?? appointment.ServiceCode;
        var resource = settings.Resources.FirstOrDefault(r => string.Equals(r.Code, appointment.ResourceCode, StringComparison.OrdinalIgnoreCase))?.Name ?? appointment.ResourceCode;
        var pet = string.IsNullOrWhiteSpace(appointment.PetName) ? "" : Environment.NewLine + Texts.T("• Patient: {0}", appointment.PetName);

        return Texts.T("• Service: {0}", service) + Environment.NewLine +
               Texts.T("• Date: {0}", local.ToString("D", Texts.Culture)) + Environment.NewLine +
               Texts.T("• Time: {0}", local.ToString("HH:mm")) + Environment.NewLine +
               Texts.T("• With: {0}", resource) + pet;
    }
}
