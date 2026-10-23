using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Inventory;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Inventory;

[Route("api/inventory/metrics")]
[Authorize(Policy = "Inventory.Metrics")]
public class InventoryMetricsController(InventoryMetricsService service) : ApiControllerBase
{
    /// <summary>Stock flow between two clinic-local dates (inclusive), for all items or the chosen ones.</summary>
    [HttpGet]
    public async Task<ActionResult<InventoryMetricsDto>> GetAsync([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] List<int>? itemIds = null)
    {
        if (to < from || to.DayNumber - from.DayNumber >= InventoryMetricsService.MaxDays)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["to"] = [Texts.T("Use a range of at most {0} days, ending on or after the start.", InventoryMetricsService.MaxDays)]
            }));
        if (itemIds is { Count: > InventoryMetricsService.MaxCompared })
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["itemIds"] = [Texts.T("Compare at most {0} items at a time.", InventoryMetricsService.MaxCompared)]
            }));

        return Ok(await service.GetAsync(from, to, itemIds));
    }
}
