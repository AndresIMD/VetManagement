using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Services;
using VetManagement.Application.Configuration;

namespace VetManagement.Api.Controllers.Inventory;

[ApiController]
/// <summary>Schedule of the inventory email alerts: part of email sending, so it needs the same permission as the email endpoints.</summary>
[Route("api/inventory-alerts")]
public class InventoryAlertsController(InventoryEmailScheduleService schedules) : ApiControllerBase
{
    /// <summary>The current inventory email alert schedule.</summary>
    [HttpGet("schedule")]
    [Authorize(Policy = "System.SendEmail")]
    public async Task<ActionResult<InventoryEmailSchedule>> GetScheduleAsync()
        => (await schedules.GetAsync()).Settings;

    /// <summary>Saves the inventory email alert schedule.</summary>
    [HttpPost("schedule")]
    [Authorize(Policy = "System.SendEmail")]
    public async Task<IActionResult> SaveScheduleAsync([FromBody] InventoryEmailSchedule schedule)
    {
        var result = await schedules.SaveAsync(schedule, GetUserName());
        return result.Status == SettingsUpdateStatus.Saved ? Ok(new { ok = true }) : BadRequestMessage(string.Join(" ", result.Errors));
    }
}
