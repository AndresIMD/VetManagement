using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VetManagement.Application.Scheduling;
using VetManagement.Contracts.Scheduling;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Api.Controllers.Public;

/// <summary>Deployment settings of the clinic's public booking portal (section BookingPortal).</summary>
public sealed class BookingPortalOptions
{
    /// <summary>Where the portal is hosted, e.g. https://reservas.spvetclinic.cl (the API redirects there after paying).</summary>
    public string BaseUrl { get; set; } = "https://localhost:7300";
    /// <summary>Public URL of this API, used as the payment return URL. Defaults to the incoming request's host.</summary>
    public string? ApiBaseUrl { get; set; }
    /// <summary>Clinic websites the portal may send clients back to.</summary>
    public List<string> AllowedReturnOrigins { get; set; } = [];
}

/// <summary>Anonymous endpoints for the public booking portal. Rate limited per client IP.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/booking")]
[EnableRateLimiting("public-read")]
public class PublicBookingController(
    SchedulingSettingsService settingsService,
    VetManagement.Application.Clinical.ClinicalSettingsService clinicalSettings,
    AppointmentService appointments,
    OnlineBookingService online,
    BookingPortalOptions portal,
    TimeProvider clock) : ControllerBase
{
    [HttpGet("info")]
    public async Task<ActionResult<PublicBookingInfoDto>> GetInfoAsync()
    {
        var settings = (await settingsService.GetAsync()).Settings;
        return new PublicBookingInfoDto
        {
            Enabled = settings.Enabled,
            ClinicName = settings.ClinicName,
            TimeZone = settings.TimeZone,
            ClinicPhone = settings.ClinicPhone,
            ClientPortalEnabled = (await clinicalSettings.GetAsync()).Settings.ClientPortal.Enabled,
            PrimaryColor = settings.Branding.PrimaryColor,
            LogoUrl = settings.Branding.LogoUrl,
            CancellationDeadlineHours = settings.Cancellation.ClientDeadlineHours,
            RefundMode = settings.Cancellation.RefundMode,
            AllowedReturnOrigins = portal.AllowedReturnOrigins,
            Services = !settings.Enabled ? [] : settings.Services
                .Where(s => s.Enabled && s.AllowOnlineBooking)
                .Select(s => new PublicServiceDto
                {
                    Code = s.Code,
                    Name = s.Name,
                    DurationMinutes = s.DurationMinutes,
                    Price = s.Price,
                    DepositAmount = s.Price * s.DepositPercent / 100,
                    Resources = settings.Resources
                        .Where(r => r.Enabled && s.ResourceCodes.Contains(r.Code, StringComparer.OrdinalIgnoreCase))
                        .Select(r => new PublicResourceDto(r.Code, r.Name)).ToList()
                }).ToList()
        };
    }

    /// <summary>Bookable slots as clients see them (progressive release, minimum notice, horizon).</summary>
    [HttpGet("availability")]
    public async Task<ActionResult<List<AvailableSlotDto>>> GetAvailabilityAsync([FromQuery(Name = "service")] string serviceCode, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 31)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["to"] = ["Use a range of at most 31 days."] }));
        var slots = await appointments.GetAvailabilityAsync(serviceCode, from, to, BookingAudience.Public);
        return slots is null ? NotFound() : slots.Select(s => new AvailableSlotDto(s.ResourceCode, s.StartUtc, s.EndUtc, s.IsOverflow)).ToList();
    }

    [HttpPost]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<OnlineBookingResponseDto>> BookAsync([FromBody] OnlineBookingRequestDto request)
    {
        var result = await online.BookAsync(new OnlineBookingRequest(
            request.ServiceCode, request.ResourceCode, request.StartUtc, request.OwnerFirstName, request.OwnerLastName,
            request.OwnerTaxId, request.OwnerEmail, request.OwnerPhone, request.PetName, request.PetSpecies, request.Notes),
            paymentReturnUrl: $"{ApiBaseUrl}/api/public/booking/payment-return");

        return result.Status switch
        {
            OnlineBookingStatus.Confirmed or OnlineBookingStatus.PaymentRequired => new OnlineBookingResponseDto
            {
                PublicToken = result.PublicToken!,
                Confirmed = result.Status == OnlineBookingStatus.Confirmed,
                PaymentUrl = result.Payment?.Url,
                PaymentFields = result.Payment?.FormFields.ToDictionary() ?? []
            },
            OnlineBookingStatus.SlotUnavailable => Conflict(new ProblemDetails
            {
                Title = "That time was just taken.",
                Detail = "Please pick another time.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["booking"] = [result.Error!] }))
        };
    }

    /// <summary>
    /// The payment provider sends the client's browser here (WebPay: token_ws when processed, TBK_TOKEN when the
    /// client aborted; by POST or GET). Confirms or releases the booking, then returns the client to the portal.
    /// </summary>
    [HttpGet("payment-return"), HttpPost("payment-return")]
    [DisableRateLimiting]
    public async Task<IActionResult> PaymentReturnAsync()
    {
        var token = ReadField("token_ws");
        var aborted = ReadField("TBK_TOKEN");
        var publicToken = token is not null ? await online.CompletePaymentAsync(token, aborted: false)
            : aborted is not null ? await online.CompletePaymentAsync(aborted, aborted: true)
            : null;

        return Redirect(publicToken is null
            ? $"{portal.BaseUrl.TrimEnd('/')}/booking/error"
            : $"{portal.BaseUrl.TrimEnd('/')}/booking/{publicToken}");
    }

    [HttpGet("{publicToken}")]
    public async Task<ActionResult<PublicBookingDto>> GetBookingAsync(string publicToken)
    {
        var appointment = await online.GetBookingAsync(publicToken);
        if (appointment is null)
            return NotFound();

        var settings = (await settingsService.GetAsync()).Settings;
        var deadline = appointment.StartUtc.AddHours(-settings.Cancellation.ClientDeadlineHours);
        return new PublicBookingDto
        {
            ServiceName = settings.Services.FirstOrDefault(s => s.Code == appointment.ServiceCode)?.Name ?? appointment.ServiceCode,
            ResourceName = settings.Resources.FirstOrDefault(r => r.Code == appointment.ResourceCode)?.Name ?? appointment.ResourceCode,
            StartUtc = appointment.StartUtc,
            EndUtc = appointment.EndUtc,
            Status = appointment.Status,
            DepositStatus = appointment.DepositStatus,
            DepositAmount = appointment.DepositAmount,
            OwnerName = appointment.OwnerName,
            PetName = appointment.PetName,
            CancelDeadlineUtc = deadline,
            CanCancel = appointment.Status is AppointmentStatus.Confirmed or AppointmentStatus.PendingPayment
                        && clock.GetUtcNow().UtcDateTime <= deadline
        };
    }

    [HttpPost("{publicToken}/cancel")]
    [EnableRateLimiting("public-write")]
    public async Task<IActionResult> CancelAsync(string publicToken)
    {
        var result = await online.CancelAsync(publicToken);
        return result.Status switch
        {
            OnlineCancelStatus.Cancelled => Ok(new { deposit = result.Deposit }),
            OnlineCancelStatus.NotFound => NotFound(),
            OnlineCancelStatus.TooLate => Conflict(new ProblemDetails
            {
                Title = "It's too late to cancel online.",
                Detail = "Please contact the clinic.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Conflict(new ProblemDetails { Title = "This booking can no longer be cancelled.", Status = StatusCodes.Status409Conflict })
        };
    }

    private string ApiBaseUrl => (portal.ApiBaseUrl ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

    private string? ReadField(string name)
    {
        if (Request.HasFormContentType && Request.Form.TryGetValue(name, out var form) && !string.IsNullOrEmpty(form))
            return form.ToString();
        return Request.Query.TryGetValue(name, out var query) && !string.IsNullOrEmpty(query) ? query.ToString() : null;
    }
}
