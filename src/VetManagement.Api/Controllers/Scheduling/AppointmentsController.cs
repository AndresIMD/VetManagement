using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Scheduling;
using VetManagement.Contracts.Scheduling;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Api.Controllers.Scheduling;

[Route("api/scheduling")]
[Authorize(Policy = "Scheduling.Read")]
public class AppointmentsController(AppointmentService service) : ApiControllerBase
{
    /// <summary>Free slots for a service as staff sees them (every block, no notice/horizon limits).</summary>
    [HttpGet("availability")]
    public async Task<ActionResult<List<AvailableSlotDto>>> GetAvailabilityAsync([FromQuery(Name = "service")] string serviceCode, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 62)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["to"] = ["Use a range of at most 62 days."] }));

        var slots = await service.GetAvailabilityAsync(serviceCode, from, to, BookingAudience.Staff);
        return slots is null
            ? NotFound()
            : Ok(slots.Select(s => new AvailableSlotDto(s.ResourceCode, s.StartUtc, s.EndUtc, s.IsOverflow)).ToList());
    }

    [HttpGet("appointments")]
    public async Task<ActionResult<List<AppointmentDto>>> ListAsync(
        [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, [FromQuery] string? resource = null, [FromQuery] AppointmentStatus? status = null)
        => Ok((await service.ListAsync(fromUtc, toUtc, resource, status)).Select(MapToDto).ToList());

    [HttpPost("appointments")]
    [Authorize(Policy = "Scheduling.Book")]
    public async Task<IActionResult> BookAsync([FromBody] CreateAppointmentRequest request)
    {
        var result = await service.BookAsync(new BookingRequest(
            request.ServiceCode, request.ResourceCode, request.StartUtc, request.Overbook, request.OwnerName,
            request.ClientId, request.PetId, request.OwnerTaxId, request.OwnerEmail, request.OwnerPhone, request.PetName, request.Notes),
            GetUserName());
        return ToActionResult(result);
    }

    [HttpPost("appointments/{id:int}/reschedule")]
    [Authorize(Policy = "Scheduling.Book")]
    public async Task<IActionResult> RescheduleAsync(int id, [FromBody] RescheduleAppointmentRequest request)
        => ToActionResult(await service.RescheduleAsync(id, request.ResourceCode, request.StartUtc, request.Overbook, GetUserName()));

    [HttpPost("appointments/{id:int}/cancel")]
    [Authorize(Policy = "Scheduling.Book")]
    public async Task<IActionResult> CancelAsync(int id, [FromBody] CancelAppointmentRequest request)
        => ToActionResult(await service.CancelAsync(id, request.Reason, GetUserName()));

    private IActionResult ToActionResult(BookingResult result) => result.Status switch
    {
        BookingStatus.Done => Ok(new { id = result.AppointmentId }),
        BookingStatus.NotFound => NotFound(),
        BookingStatus.SlotUnavailable => Conflict(new ProblemDetails
        {
            Title = "That time is no longer available.",
            Detail = "Pick another slot, or book it as overbooking (sobrecupo).",
            Status = StatusCodes.Status409Conflict
        }),
        _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["appointment"] = [result.Error ?? "Invalid request."] }))
    };

    private static AppointmentDto MapToDto(Appointment a) => new()
    {
        Id = a.Id,
        ServiceCode = a.ServiceCode,
        ResourceCode = a.ResourceCode,
        StartUtc = a.StartUtc,
        EndUtc = a.EndUtc,
        Status = a.Status,
        Source = a.Source,
        IsOverbooked = a.IsOverbooked,
        ClientId = a.ClientId,
        PetId = a.PetId,
        OwnerName = a.OwnerName,
        OwnerTaxId = a.OwnerTaxId,
        OwnerEmail = a.OwnerEmail,
        OwnerPhone = a.OwnerPhone,
        PetName = a.PetName,
        Notes = a.Notes,
        Price = a.Price,
        DepositAmount = a.DepositAmount,
        CancelReason = a.CancelReason
    };
}
