using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Scheduling;
using VetManagement.Contracts.Scheduling;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Api.Controllers.Scheduling;

[Route("api/scheduling/settings")]
[Authorize(Policy = "Scheduling.Read")]
public class SchedulingSettingsController(SchedulingSettingsService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SchedulingSettingsDocument>> GetAsync()
    {
        var current = await service.GetAsync();
        return Ok(new SchedulingSettingsDocument
        {
            Version = current.Version,
            Settings = JsonSerializer.SerializeToElement(current.Settings, SchedulingJson.Options)
        });
    }

    /// <summary>Saves the whole document. 400 lists every validation error; 409 means it changed since it was read.</summary>
    [HttpPut]
    [Authorize(Policy = "Scheduling.Manage")]
    public async Task<IActionResult> UpdateAsync([FromBody] SchedulingSettingsDocument document)
    {
        SchedulingSettings? settings;
        try
        {
            settings = document.Settings.Deserialize<SchedulingSettings>(SchedulingJson.Options);
        }
        catch (JsonException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = [ex.Message] }));
        }
        if (settings is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = ["Settings are required."] }));

        var result = await service.UpdateAsync(settings, document.Version, GetUserName());
        return result.Status switch
        {
            SettingsUpdateStatus.Saved => Ok(new { version = result.Version }),
            SettingsUpdateStatus.Conflict => Conflict(new ProblemDetails
            {
                Title = "The settings were changed by someone else.",
                Detail = $"Reload the latest version ({result.Version}) and apply your changes again.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["settings"] = [.. result.Errors] }))
        };
    }
}
