using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Contracts.Medical;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Medical;

namespace VetManagement.Api.Controllers.Medical;

[ApiController]
[Route("api/medical-visits")]
[Authorize(Policy = "Medical.Read")]
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
        return Ok(result.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var visit = await service.GetByIdAsync(id);
        return visit is null ? NotFound() : Ok(MapToDto(visit));
    }

    [HttpPost]
    [Authorize(Policy = "Medical.Create")]
    public async Task<IActionResult> AddAsync([FromBody] MedicalVisitRequest request)
    {
        await service.AddAsync(MapToDomain(request, id: 0), GetUserName());
        return Ok();
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Medical.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] MedicalVisitRequest request)
    {
        await service.UpdateAsync(MapToDomain(request, id), GetUserName());
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Medical.Delete")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var result = await service.DeleteAsync(id, GetUserName());
        if (!result)
            return NotFound();
        return Ok();
    }

    #region Mapping
    private static MedicalVisitDto MapToDto(MedicalVisit visit) => new()
    {
        Id = visit.Id,
        Date = visit.Date,
        PatientId = visit.PatientId,
        RecordNumber = visit.RecordNumber,
        PatientName = visit.PatientName,
        Responsible = visit.Responsible,
        Location = visit.Location,
        BudgetNumber = visit.BudgetNumber,
        PaymentStatus = visit.PaymentStatus,
        PaymentMethod = visit.PaymentMethod,
        TotalValue = visit.TotalValue,
        Procedures = visit.Procedures.Select(p => new VisitProcedureDto
        {
            Id = p.Id,
            ExamId = p.ExamId,
            Name = p.Name,
            Price = p.Price,
            Notes = p.Notes
        }).ToList()
    };

    // The visit id comes from the route; procedures keep their ids so an update modifies existing rows (0 = new).
    private static MedicalVisit MapToDomain(MedicalVisitRequest request, int id) => new()
    {
        Id = id,
        Date = request.Date,
        PatientId = request.PatientId,
        RecordNumber = request.RecordNumber,
        PatientName = request.PatientName,
        Responsible = request.Responsible,
        Location = request.Location,
        BudgetNumber = request.BudgetNumber,
        PaymentStatus = request.PaymentStatus,
        PaymentMethod = request.PaymentMethod,
        TotalValue = request.TotalValue,
        Procedures = request.Procedures.Select(p => new VisitProcedure
        {
            Id = p.Id,
            ExamId = p.ExamId,
            Name = p.Name,
            Price = p.Price,
            Notes = p.Notes
        }).ToList()
    };
    #endregion
}
