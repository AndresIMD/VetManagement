using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Shared.Enums;

namespace VetManagement.Api.Controllers.Audit;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Audit.READ")]
public class AuditController(AuditService auditService) : ApiControllerBase
{
    [HttpGet("logs")]
    public async Task<IActionResult> GetAuditLogsFilteredAsync(
        [FromQuery] int? entityId = null,
        [FromQuery] string? entityName = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] AuditActionType? action = null,
        [FromQuery] string? user = null)
    {
        var logs = await auditService.GetLogsFilteredAsync(entityId, entityName, from, to, action, user);
        return Ok(logs);
    }

    [HttpGet("logs/paged")]
    public async Task<IActionResult> GetAuditLogsPagedAsync(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? entityName = null,
        [FromQuery] AuditActionType? action = null,
        [FromQuery] string? user = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? itemName = null)
    {
        var result = await auditService.GetLogsPagedAsync(page, pageSize, entityName, action, user, from, to, itemName);
        return Ok(result);
    }
}
