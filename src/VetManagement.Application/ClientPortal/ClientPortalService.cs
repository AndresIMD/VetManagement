using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using VetManagement.Application.Clinical;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Contracts.Services;
using VetManagement.Application.Scheduling;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Medical;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.ClientPortal;

public enum LinkRequestStatus
{
    /// <summary>Sent if the RUT belongs to a client with an email; the answer is the same either way.</summary>
    Accepted,
    InvalidRut,
    Disabled
}

public sealed record PortalPet(Pet Pet, List<DoseView> Doses, List<MedicalVisit> Visits);

public sealed record PortalView(string ClientName, DateTime ExpiresAtUtc, bool ShowVisitDetails, List<PortalPet> Pets,
    List<Appointment> Upcoming, SchedulingSettings Agenda);

/// <summary>
/// The client's view of their pets' file, without an account: they enter their RUT and get a time-limited link by
/// email. Whether a RUT exists is never revealed, links are stored hashed, and one link is sent per client every
/// few minutes at most.
/// </summary>
public class ClientPortalService(
    IUnitOfWork unitOfWork,
    ClinicalSettingsService clinicalSettings,
    SchedulingSettingsService schedulingSettings,
    ClinicalService clinical,
    IEmailSender email,
    TimeProvider clock,
    ILogger<ClientPortalService> logger)
{
    private static readonly TimeSpan ResendInterval = TimeSpan.FromMinutes(2);

    /// <param name="linkBase">Portal page the token is appended to, e.g. https://reservas.clinica.cl/mis-mascotas/</param>
    public async Task<LinkRequestStatus> RequestLinkAsync(string? rutInput, string linkBase)
    {
        var settings = (await clinicalSettings.GetAsync()).Settings.ClientPortal;
        if (!settings.Enabled)
            return LinkRequestStatus.Disabled;
        if (!Rut.TryParse(rutInput, out var rut))
            return LinkRequestStatus.InvalidRut;

        // ponytail: exact match on the normalized RUT; staff-typed RUTs in another format aren't found (same as online booking).
        var client = await unitOfWork.Clients.GetByTaxIdAsync(rut.ToString());
        if (client is null || string.IsNullOrWhiteSpace(client.Email))
            return LinkRequestStatus.Accepted;

        var now = clock.GetUtcNow().UtcDateTime;
        var recent = await unitOfWork.ClientAccessTokens.FindAsync(t => t.ClientId == client.Id && t.CreatedAtUtc > now - ResendInterval);
        if (recent.Any())
            return LinkRequestStatus.Accepted; // a link just went out; don't flood the inbox

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expires = now.AddMinutes(settings.LinkValidMinutes);
        await unitOfWork.ClientAccessTokens.AddAsync(new ClientAccessToken { ClientId = client.Id, TokenHash = Hash(token), CreatedAtUtc = now, ExpiresAtUtc = expires });
        await unitOfWork.SaveChangesAsync();

        var agenda = (await schedulingSettings.GetAsync()).Settings;
        var body =
            $"Hola {client.Name},{Environment.NewLine}{Environment.NewLine}" +
            $"Para ver la ficha de tus mascotas (vacunas, atenciones y próximas horas) abre este enlace:{Environment.NewLine}" +
            $"{linkBase.TrimEnd('/')}/{token}{Environment.NewLine}{Environment.NewLine}" +
            $"El enlace funciona durante {settings.LinkValidMinutes} minutos. Si no lo pediste, ignora este correo.{Environment.NewLine}{Environment.NewLine}" +
            agenda.ClinicName;
        try
        {
            await email.SendAsync(client.Email, $"Tu acceso a la ficha de tus mascotas – {agenda.ClinicName}", body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send the client portal link to client {ClientId}", client.Id);
        }
        return LinkRequestStatus.Accepted;
    }

    /// <summary>The client's pets, preventive care, visits and upcoming appointments; null if the link is unknown or expired.</summary>
    public async Task<PortalView?> GetAsync(string token)
    {
        var settings = (await clinicalSettings.GetAsync()).Settings.ClientPortal;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(token))
            return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = Hash(token);
        var access = (await unitOfWork.ClientAccessTokens.FindAsync(t => t.TokenHash == hash && t.ExpiresAtUtc > now)).FirstOrDefault();
        if (access is null)
            return null;

        var client = await unitOfWork.Clients.GetByIdAsNoTrackingAsync(access.ClientId);
        if (client is null)
            return null;

        var pets = new List<PortalPet>();
        foreach (var pet in await unitOfWork.Pets.GetByOwnerIdAsync(client.Id))
        {
            var history = await clinical.GetHistoryAsync(pet.Id);
            if (history is not null)
                pets.Add(new PortalPet(pet, history.Doses.Where(d => d.Status != PreventiveStatus.Superseded).ToList(), history.Visits));
        }

        var upcoming = (await unitOfWork.Appointments.FindAsync(a => a.ClientId == client.Id && a.EndUtc > now
                && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.NeedsReschedule || a.Status == AppointmentStatus.PendingPayment)))
            .OrderBy(a => a.StartUtc).ToList();

        return new PortalView($"{client.Name} {client.LastName}".Trim(), access.ExpiresAtUtc, settings.ShowVisitDetails, pets, upcoming,
            (await schedulingSettings.GetAsync()).Settings);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
