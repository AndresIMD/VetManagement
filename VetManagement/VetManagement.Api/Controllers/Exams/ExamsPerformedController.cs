using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Api.Controllers.Exams;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ExamsPerformed.Read")]
public class ExamsPerformedController(ExamPerformedService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<List<ExamPerformed>> GetAll()
        => await service.GetAllAsync();

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "ExamsPerformed.Create")]
    public async Task<IActionResult> AddAsync([FromBody] ExamPerformed exam)
    {
        await service.AddAsync(exam, GetUserName());
        return Ok();
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ExamsPerformed.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] ExamPerformed exam)
    {
        if (id != exam.Id)
            return BadRequest();
        await service.UpdateAsync(exam, GetUserName());
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ExamsPerformed.Delete")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var result = await service.DeleteAsync(id, GetUserName());
        if (!result)
            return NotFound();
        return Ok();
    }
}
