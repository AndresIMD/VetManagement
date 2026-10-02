using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;
using VetManagement.Shared.Models.Medical;

namespace VetManagement.Api.Controllers.Medical;

[ApiController]
[Route("api/medical-visits")]
[Authorize(Policy = "Medical.READ")]
public class MedicalVisitsController(MedicalVisitService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? patientId = null,
        [FromQuery] string? responsible = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] string? search = null)
    {
        var result = await service.GetAllAsync(from, to, patientId, responsible, paymentStatus, search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var visit = await service.GetByIdAsync(id);
        return visit is null ? NotFound() : Ok(visit);
    }

    [HttpPost]
    [Authorize(Policy = "Medical.CREATE")]
    public async Task<IActionResult> AddAsync([FromBody] MedicalVisit visit)
    {
        await service.AddAsync(visit, GetUserName());
        return Ok();
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Medical.UPDATE")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] MedicalVisit visit)
    {
        if (id != visit.Id)
            return BadRequest();
        await service.UpdateAsync(visit, GetUserName());
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Medical.DELETE")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var result = await service.DeleteAsync(id, GetUserName());
        if (!result)
            return NotFound();
        return Ok();
    }
}
