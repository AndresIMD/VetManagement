using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Contracts.Exams;

namespace VetManagement.Api.Controllers.Exams;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ExamsPerformed.Read")]
public class ExamsPerformedController(ExamPerformedService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<List<ExamPerformedDto>> GetAll()
        => (await service.GetAllAsync()).Select(e => e.ToDto()).ToList();

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item.ToDto());
    }

    [HttpPost]
    [Authorize(Policy = "ExamsPerformed.Create")]
    public async Task<IActionResult> AddAsync([FromBody] ExamPerformedRequest request)
    {
        await service.AddAsync(request.ToDomain(id: 0), GetUserName());
        return Ok();
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ExamsPerformed.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] ExamPerformedRequest request)
    {
        await service.UpdateAsync(request.ToDomain(id), GetUserName());
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
