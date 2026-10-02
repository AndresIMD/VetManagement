using System.Net.Mail;
using Microsoft.Extensions.Logging;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Payments;
using VetManagement.Application.Services;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

public sealed record OnlineBookingRequest(
    string ServiceCode,
    string ResourceCode,
    DateTime StartUtc,
    string OwnerFirstName,
    string OwnerLastName,
    string OwnerTaxId,
    string OwnerEmail,
    string OwnerPhone,
    string PetName,
    Species PetSpecies,
    string? Notes = null);

public enum OnlineBookingStatus
{
    Confirmed,
    /// <summary>The slot is held; send the client to <see cref="OnlineBookingResult.Payment"/> to pay the deposit.</summary>
    PaymentRequired,
    SlotUnavailable,
    Invalid
}

public sealed record OnlineBookingResult(OnlineBookingStatus Status, string? PublicToken = null, PaymentStart? Payment = null, string? Error = null);

public enum OnlineCancelStatus { Cancelled, TooLate, NotFound, NotCancellable }

public sealed record OnlineCancelResult(OnlineCancelStatus Status, DepositStatus? Deposit = null);

/// <summary>
/// Booking made by clients on the public booking portal: no account and no staff confirmation.
/// The client is matched by RUT (or created), the slot is checked with public rules, and services that
/// require a deposit are held until the payment provider confirms.
/// </summary>
public class OnlineBookingService(
    IUnitOfWork unitOfWork,
    SchedulingSettingsService settingsService,
    AgendaRules rules,
    IPaymentGateway payments,
    AppointmentNotifier notifier,
    AuditService audit,
    TimeProvider clock,
    ILogger<OnlineBookingService> logger)
{
    private const string Actor = "online-booking";

    /// <param name="paymentReturnUrl">API endpoint the provider sends the browser back to.</param>
    public async Task<OnlineBookingResult> BookAsync(OnlineBookingRequest request, string paymentReturnUrl)
    {
        var settings = (await settingsService.GetAsync()).Settings;
        if (!settings.Enabled)
            return Invalid("Online booking is not available.");
        var service = AgendaRules.FindBookableService(settings, request.ServiceCode, request.ResourceCode, out var error);
        if (service is null || !service.AllowOnlineBooking)
            return Invalid(error ?? $"Service '{request.ServiceCode}' can't be booked online.");
        if (!Rut.TryParse(request.OwnerTaxId, out var rut))
            return Invalid("The RUT is not valid.");
        if (!IsValidEmail(request.OwnerEmail))
            return Invalid("The email address is not valid.");
        var phoneDigits = new string(request.OwnerPhone.Where(char.IsAsciiDigit).ToArray());
        if (phoneDigits.Length is < 8 or > 15)
            return Invalid("The phone number is not valid.");
        if (string.IsNullOrWhiteSpace(request.OwnerFirstName) || string.IsNullOrWhiteSpace(request.OwnerLastName) || string.IsNullOrWhiteSpace(request.PetName))
            return Invalid("Name, last name and pet name are required.");

        var now = clock.GetUtcNow().UtcDateTime;
        var deposit = service.Price * service.DepositPercent / 100;
        Appointment? booked = null;

        var available = await unitOfWork.ExecuteExclusiveAsync(AgendaRules.BookingLockKey(request.ResourceCode), async () =>
        {
            if (!await rules.IsSlotFreeAsync(settings, service, request.ResourceCode, request.StartUtc, audience: BookingAudience.Public))
                return false;

            var (clientId, petId) = await UpsertClientAndPetAsync(request, rut, phoneDigits);
            booked = new Appointment
            {
                ServiceCode = service.Code,
                ResourceCode = request.ResourceCode,
                StartUtc = request.StartUtc,
                EndUtc = request.StartUtc.AddMinutes(service.DurationMinutes),
                OccupiedUntilUtc = request.StartUtc.AddMinutes(service.DurationMinutes + service.BufferMinutes),
                Source = AppointmentSource.Online,
                Status = deposit > 0 ? AppointmentStatus.PendingPayment : AppointmentStatus.Confirmed,
                DepositStatus = deposit > 0 ? DepositStatus.Pending : DepositStatus.NotRequired,
                PaymentHoldUntilUtc = deposit > 0 ? now.AddMinutes(settings.Payment.PendingPaymentHoldMinutes) : null,
                ClientId = clientId,
                PetId = petId,
                OwnerName = $"{request.OwnerFirstName.Trim()} {request.OwnerLastName.Trim()}",
                OwnerTaxId = rut.ToString(),
                OwnerEmail = request.OwnerEmail.Trim(),
                OwnerPhone = request.OwnerPhone.Trim(),
                PetName = request.PetName.Trim(),
                Notes = request.Notes,
                Price = service.Price,
                DepositAmount = deposit,
                PublicToken = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = now,
                CreatedBy = Actor
            };
            await unitOfWork.Appointments.AddAsync(booked);
            await unitOfWork.SaveChangesAsync();
            return true;
        });

        if (!available)
            return new OnlineBookingResult(OnlineBookingStatus.SlotUnavailable);

        await audit.LogAsync("Appointment", booked!.Id, AuditActionType.Add, $"Online booking ({rut})", Actor);

        if (deposit == 0)
        {
            await notifier.SendConfirmationAsync(booked, settings);
            return new OnlineBookingResult(OnlineBookingStatus.Confirmed, booked.PublicToken);
        }

        var payment = await payments.StartAsync($"VM{booked.Id}", deposit, paymentReturnUrl);
        booked.PaymentProvider = payments.Name;
        booked.PaymentToken = payment.Token;
        await unitOfWork.SaveChangesAsync();
        return new OnlineBookingResult(OnlineBookingStatus.PaymentRequired, booked.PublicToken, payment);
    }

    /// <summary>
    /// The client came back from the payment provider. Confirms the booking when paid; otherwise releases it.
    /// Safe to call twice for the same token.
    /// </summary>
    /// <param name="aborted">The client cancelled at the provider (WebPay sends TBK_TOKEN instead of token_ws).</param>
    /// <returns>The booking's public token, or null when the payment token is unknown.</returns>
    public async Task<string?> CompletePaymentAsync(string paymentToken, bool aborted)
    {
        var appointment = await unitOfWork.Appointments.GetByPaymentTokenAsync(paymentToken);
        if (appointment is null)
            return null;
        if (appointment.Status != AppointmentStatus.PendingPayment)
            return appointment.PublicToken; // already handled (page refresh, duplicate callback)

        var settings = (await settingsService.GetAsync()).Settings;
        var confirmation = aborted ? null : await payments.ConfirmAsync(paymentToken);
        if (confirmation is not { Outcome: PaymentOutcome.Approved } || confirmation.Amount != appointment.DepositAmount)
        {
            await CancelUnpaidAsync(appointment, aborted ? "Payment cancelled by the client" : $"Payment not approved ({confirmation?.Detail})");
            return appointment.PublicToken;
        }

        // Paid after the hold expired: the slot may have been taken meanwhile. Re-check under the lock.
        var stillFree = appointment.PaymentHoldUntilUtc >= clock.GetUtcNow().UtcDateTime
            || await unitOfWork.ExecuteExclusiveAsync(AgendaRules.BookingLockKey(appointment.ResourceCode), async () =>
            {
                var service = AgendaRules.FindBookableService(settings, appointment.ServiceCode, appointment.ResourceCode, out _);
                return service is not null && await rules.IsSlotFreeAsync(settings, service, appointment.ResourceCode, appointment.StartUtc, ignoreAppointmentId: appointment.Id);
            });

        // The exclusive section clears tracked entities: load the booking again before changing it.
        appointment = (await unitOfWork.Appointments.GetByPaymentTokenAsync(paymentToken))!;
        appointment.PaidAmount = confirmation.Amount;

        if (!stillFree)
        {
            var refunded = await payments.RefundAsync(paymentToken, confirmation.Amount);
            appointment.DepositStatus = refunded ? DepositStatus.Refunded : DepositStatus.RefundRequested;
            await CancelUnpaidAsync(appointment, "Paid after the hold expired and the slot was taken; deposit refunded");
            return appointment.PublicToken;
        }

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.DepositStatus = DepositStatus.Paid;
        appointment.PaidAmount = confirmation.Amount;
        appointment.PaymentHoldUntilUtc = null;
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync("Appointment", appointment.Id, AuditActionType.Edit, $"Deposit paid ({confirmation.Amount}, {payments.Name})", Actor);
        await notifier.SendConfirmationAsync(appointment, settings);
        return appointment.PublicToken;
    }

    public Task<Appointment?> GetBookingAsync(string publicToken) => unitOfWork.Appointments.GetByPublicTokenAsync(publicToken);

    /// <summary>Client cancels from the link in their email. The deposit follows the clinic's refund mode.</summary>
    public async Task<OnlineCancelResult> CancelAsync(string publicToken)
    {
        var appointment = await unitOfWork.Appointments.GetByPublicTokenAsync(publicToken);
        if (appointment is null)
            return new OnlineCancelResult(OnlineCancelStatus.NotFound);
        if (appointment.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.PendingPayment))
            return new OnlineCancelResult(OnlineCancelStatus.NotCancellable);

        var settings = (await settingsService.GetAsync()).Settings;
        var now = clock.GetUtcNow().UtcDateTime;
        if (now > appointment.StartUtc.AddHours(-settings.Cancellation.ClientDeadlineHours))
            return new OnlineCancelResult(OnlineCancelStatus.TooLate);

        if (appointment.DepositStatus == DepositStatus.Paid)
        {
            appointment.DepositStatus = settings.Cancellation.RefundMode switch
            {
                RefundMode.Automatic when await payments.RefundAsync(appointment.PaymentToken!, appointment.PaidAmount) => DepositStatus.Refunded,
                RefundMode.NoRefund => DepositStatus.Forfeited,
                _ => DepositStatus.RefundRequested // manual approval, or an automatic refund that failed
            };
        }
        else if (appointment.DepositStatus == DepositStatus.Pending)
        {
            appointment.DepositStatus = DepositStatus.NotRequired;
        }

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = now;
        appointment.CancelledBy = Actor;
        appointment.CancelReason = "Cancelled by the client";
        await unitOfWork.SaveChangesAsync();
        await audit.LogAsync("Appointment", appointment.Id, AuditActionType.Delete, $"Cancelled by the client; deposit {appointment.DepositStatus}", Actor);
        return new OnlineCancelResult(OnlineCancelStatus.Cancelled, appointment.DepositStatus);
    }

    /// <summary>Releases online bookings whose payment hold expired without a payment. Returns how many.</summary>
    public async Task<int> ExpireUnpaidHoldsAsync()
    {
        var expired = await unitOfWork.Appointments.GetExpiredPaymentHoldsAsync(clock.GetUtcNow().UtcDateTime);
        foreach (var appointment in expired)
            await CancelUnpaidAsync(appointment, "Payment not completed in time");
        return expired.Count;
    }

    private async Task CancelUnpaidAsync(Appointment appointment, string reason)
    {
        appointment.Status = AppointmentStatus.Cancelled;
        if (appointment.DepositStatus == DepositStatus.Pending)
            appointment.DepositStatus = DepositStatus.NotRequired;
        appointment.CancelledAtUtc = clock.GetUtcNow().UtcDateTime;
        appointment.CancelledBy = Actor;
        appointment.CancelReason = reason;
        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Online booking {AppointmentId} released: {Reason}", appointment.Id, reason);
    }

    /// <summary>Finds the client by RUT or creates it; finds their pet by name or creates it.</summary>
    private async Task<(int ClientId, int PetId)> UpsertClientAndPetAsync(OnlineBookingRequest request, Rut rut, string phoneDigits)
    {
        // ponytail: exact match on the normalized RUT; clients typed by staff in another format (e.g. with dots)
        // won't match and get a second record. Normalize TaxId on staff save if that shows up.
        var client = await unitOfWork.Clients.GetByTaxIdAsync(rut.ToString());
        if (client is null)
        {
            // The legacy PhoneNumber column is an int: keep the last 9 digits (a Chilean mobile without +56).
            var phone = int.Parse(phoneDigits[^Math.Min(9, phoneDigits.Length)..]);
            client = new Client(request.OwnerFirstName.Trim(), request.OwnerLastName.Trim(), rut.ToString(), string.Empty, phone, request.OwnerEmail.Trim());
            await unitOfWork.Clients.AddAsync(client);
            await unitOfWork.SaveChangesAsync();
        }

        var pet = client.Pets.FirstOrDefault(p => string.Equals(p.Name, request.PetName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (pet is null)
        {
            pet = new Pet { OwnerId = client.Id, Name = request.PetName.Trim(), Species = request.PetSpecies, Breed = "No informada" };
            await unitOfWork.Pets.AddAsync(pet);
            await unitOfWork.SaveChangesAsync();
        }

        return (client.Id, pet.Id);
    }

    private static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email)
           && MailAddress.TryCreate(email.Trim(), out var address)
           && address.Address == email.Trim()
           && address.Host.Contains('.');

    private static OnlineBookingResult Invalid(string error) => new(OnlineBookingStatus.Invalid, Error: error);
}
