using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Services;

namespace VetManagement.Api.Controllers.Inventory;

[ApiController]
[Route("api/[controller]")]
public class InventoryAlertsController(IHostEnvironment env) : ApiControllerBase
{
    /// <summary>
    /// Retrieves the current inventory email alert schedule configuration.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current email schedule configuration.</returns>
    [HttpGet("schedule")]
    [Authorize(Policy = "Inventory.Read")]
    public async Task<ActionResult<InventoryEmailSchedule>> GetScheduleAsync(CancellationToken ct)
        => await InventoryEmailSchedule.LoadAsync(env, ct);

    /// <summary>
    /// Saves the inventory email alert schedule configuration.
    /// </summary>
    /// <param name="schedule">The schedule configuration to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ok result with success confirmation.</returns>
    [HttpPost("schedule")]
    [Authorize(Policy = "Inventory.Update")]
    public async Task<IActionResult> SaveScheduleAsync([FromBody] InventoryEmailSchedule schedule, CancellationToken ct)
    {
        await InventoryEmailSchedule.SaveAsync(env, schedule, ct);
        return Ok(new { ok = true });
    }
}
