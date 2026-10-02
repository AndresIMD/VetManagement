using System.Globalization;
using Microsoft.Extensions.Logging;
using VetManagement.Application.Contracts.Services;
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
/// Texts are Spanish because clients read them; times are shown in the clinic's time zone.
/// </summary>
public class AppointmentNotifier(IEmailSender email, ILogger<AppointmentNotifier> logger)
{
    private static readonly CultureInfo Chile = CultureInfo.GetCultureInfo("es-CL");

    /// <returns>Whether an email was sent.</returns>
    public Task<bool> SendConfirmationAsync(Appointment appointment, SchedulingSettings settings)
        => settings.Notifications.SendConfirmation
            ? SendAsync(appointment, settings, $"Confirmación de tu hora – {settings.ClinicName}",
                $"Tu hora quedó agendada:{Environment.NewLine}{Details(appointment, settings)}")
            : Task.FromResult(false);

    public Task<bool> SendChangeAsync(Appointment appointment, SchedulingSettings settings, AppointmentChange change)
    {
        if (!settings.Notifications.SendRescheduleNotice)
            return Task.FromResult(false);

        return change == AppointmentChange.Rescheduled
            ? SendAsync(appointment, settings, $"Tu hora cambió – {settings.ClinicName}",
                $"Tuvimos que mover tu hora. La nueva fecha es:{Environment.NewLine}{Details(appointment, settings)}")
            : SendAsync(appointment, settings, $"Tu hora fue cancelada – {settings.ClinicName}",
                $"Lamentamos informarte que tu hora fue cancelada:{Environment.NewLine}{Details(appointment, settings)}{Environment.NewLine}" +
                "Contáctanos para agendar una nueva.");
    }

    public Task<bool> SendReminderAsync(Appointment appointment, SchedulingSettings settings)
        => settings.Notifications.SendReminder
            ? SendAsync(appointment, settings, $"Recordatorio de tu hora – {settings.ClinicName}",
                $"Te recordamos tu próxima hora:{Environment.NewLine}{Details(appointment, settings)}")
            : Task.FromResult(false);

    private async Task<bool> SendAsync(Appointment appointment, SchedulingSettings settings, string subject, string message)
    {
        if (string.IsNullOrWhiteSpace(appointment.OwnerEmail))
            return false;

        var contact = string.IsNullOrWhiteSpace(settings.ClinicPhone) ? "" : $" al {settings.ClinicPhone}";
        var body = $"Hola {appointment.OwnerName},{Environment.NewLine}{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}" +
                   $"Si necesitas cambiar o cancelar tu hora, contáctanos{contact}.{Environment.NewLine}{Environment.NewLine}{settings.ClinicName}";
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
        var pet = string.IsNullOrWhiteSpace(appointment.PetName) ? "" : $"{Environment.NewLine}• Paciente: {appointment.PetName}";

        return $"• Servicio: {service}{Environment.NewLine}" +
               $"• Fecha: {local.ToString("dddd d 'de' MMMM 'de' yyyy", Chile)}{Environment.NewLine}" +
               $"• Hora: {local:HH:mm}{Environment.NewLine}" +
               $"• Atiende: {resource}{pet}";
    }
}
