using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Api.Controllers.Exams;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ExternalLabs.Read")]
public class ExternalLabsController(ExternalLabService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync()
        => Ok(await service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(int id)
    {
        var lab = await service.GetByIdAsync(id);
        return lab is null ? NotFound() : Ok(lab);
    }

    [HttpPost]
    [Authorize(Policy = "ExternalLabs.Create")]
    public async Task<IActionResult> AddAsync([FromBody] ExternalLab lab)
    {
        var created = await service.AddAsync(lab, GetUserName());
        return Ok(created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ExternalLabs.Update")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] ExternalLab lab)
    {
        if (id != lab.Id)
            return BadRequest();
        await service.UpdateAsync(lab, GetUserName());
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
