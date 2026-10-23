using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Exams;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Exams;

/// <summary>The brands and models of lab machines the exam catalog picks from.</summary>
[Route("api/lab-machines")]
[Authorize(Policy = "Exams.Read")]
public class LabMachinesController(LabMachineService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LabMachineDto>>> GetAllAsync()
        => Ok((await service.ListAsync()).Select(x => new LabMachineDto(x.Machine.Id, x.Machine.Brand, x.Machine.Model, x.ExamCount)).ToList());

    [HttpPost]
    [Authorize(Policy = "Exams.Create")]
    public async Task<IActionResult> AddAsync([FromBody] LabMachineRequest request)
        => ToActionResult(await service.AddAsync(request.Brand, request.Model, GetUserName()));

    [HttpPut("{id}")]
    [Authorize(Policy = "Exams.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] LabMachineRequest request)
        => ToActionResult(await service.UpdateAsync(id, request.Brand, request.Model, GetUserName()));

    [HttpDelete("{id}")]
    [Authorize(Policy = "Exams.Delete")]
    public async Task<IActionResult> DeleteAsync(int id)
        => ToActionResult(await service.DeleteAsync(id, GetUserName()));

    private IActionResult ToActionResult(LabMachineResult result) => result.Status switch
    {
        LabMachineStatus.Done => Ok(new { id = result.Id, examsUpdated = result.ExamsUpdated }),
        LabMachineStatus.NotFound => NotFound(),
        _ => BadRequestMessage(result.Error ?? Texts.T("Invalid request."))
    };
}
