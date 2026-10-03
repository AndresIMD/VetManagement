using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VetManagement.Application.ClientPortal;
using VetManagement.Contracts.ClientPortal;

namespace VetManagement.Api.Controllers.Public;

/// <summary>Client portal: pets' file through an emailed link, no account. Rate limited per client IP.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/portal")]
[EnableRateLimiting("public-read")]
public class PublicClientPortalController(ClientPortalService service, BookingPortalOptions portal) : ControllerBase
{
    /// <summary>Emails an access link if the RUT belongs to a client. 202 either way, so RUTs can't be probed.</summary>
    [HttpPost("link")]
    [EnableRateLimiting("public-write")]
    public async Task<IActionResult> RequestLinkAsync([FromBody] PortalLinkRequest request)
    {
        var status = await service.RequestLinkAsync(request.Rut, $"{portal.BaseUrl.TrimEnd('/')}/mis-mascotas");
        return status switch
        {
            LinkRequestStatus.Accepted => Accepted(),
            LinkRequestStatus.InvalidRut => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["rut"] = ["The RUT is not valid."] })),
            _ => NotFound()
        };
    }

    /// <summary>The client's pets, vaccines, visits and upcoming appointments. 404 if the link is unknown or expired.</summary>
    [HttpGet("{token}")]
    public async Task<ActionResult<ClientPortalDto>> GetAsync(string token)
    {
        var view = await service.GetAsync(token);
        if (view is null)
            return NotFound();

        string ServiceName(string code) => view.Agenda.Services.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase))?.Name ?? code;
        string ResourceName(string code) => view.Agenda.Resources.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase))?.Name ?? code;

        return Ok(new ClientPortalDto(view.ClientName, view.ExpiresAtUtc,
            view.Pets.Select(p => new PortalPetDto(p.Pet.Name, p.Pet.Species, p.Pet.Breed, p.Pet.Birthdate, p.Pet.Weight,
                p.Doses.Select(d => new PortalDoseDto(d.Dose.ProductName, d.Dose.Kind, d.Dose.AppliedOn, d.Dose.NextDueOn, d.Status)).ToList(),
                p.Visits.Select(v => new PortalVisitDto(v.Date, v.Reason,
                    view.ShowVisitDetails ? v.Diagnosis : null, view.ShowVisitDetails ? v.Treatment : null, v.WeightKg)).ToList())).ToList(),
            view.Upcoming.Select(a => new PortalAppointmentDto(a.StartUtc, ServiceName(a.ServiceCode), ResourceName(a.ResourceCode), a.PetName, a.Status, a.PublicToken)).ToList()));
    }
}
