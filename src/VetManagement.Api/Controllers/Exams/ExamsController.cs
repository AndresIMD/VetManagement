using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Api.Controllers.Exams;

[Route("api/[controller]")]
[Authorize(Policy = "Exams.Read")]
public class ExamsController(ExamService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Exam>>> GetAllAsync()
    {
        var exams = await service.GetAllAsync();
        return Ok(exams);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        var exam = await service.GetByIdAsync(id);
        if (exam == null)
            return NotFound();
        return Ok(exam);
    }

    [HttpPost]
    [Authorize(Policy = "Exams.Create")]
    public async Task<IActionResult> AddAsync([FromBody] Exam exam)
    {
        await service.AddAsync(exam, GetUserName());
        return Ok();
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Exams.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] Exam exam)
    {
        var oldData = await service.GetByIdAsNoTrackingAsync(id);
        if (oldData == null)
            return NotFound();

        exam.Id = id;
        await service.UpdateAsync(exam, oldData, GetUserName());
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Exams.Delete")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var result = await service.DeleteAsync(id, GetUserName());
        if (!result)
            return NotFound();
        return Ok();
    }
}
