using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Contracts.Exams;

namespace VetManagement.Api.Controllers.Exams;

[ApiController]
[Route("api/external-labs")]
[Authorize(Policy = "ExternalLabs.Read")]
public class ExternalLabsController(ExternalLabService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync()
        => Ok((await service.GetAllAsync()).Select(l => l.ToDto()).ToList());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(int id)
    {
        var lab = await service.GetByIdAsync(id);
        return lab is null ? NotFound() : Ok(lab.ToDto());
    }

    [HttpPost]
    [Authorize(Policy = "ExternalLabs.Create")]
    public async Task<IActionResult> AddAsync([FromBody] ExternalLabRequest request)
    {
        var created = await service.AddAsync(request.ToDomain(id: 0), GetUserName());
        return Ok(created.ToDto());
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ExternalLabs.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] ExternalLabRequest request)
    {
        await service.UpdateAsync(request.ToDomain(id), GetUserName());
        return Ok();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "ExternalLabs.Delete")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var result = await service.DeleteAsync(id, GetUserName());
        if (!result)
            return NotFound();
        return Ok();
    }
}
