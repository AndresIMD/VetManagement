using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Services;
using VetManagement.Domain.Enums;

namespace VetManagement.Api.Controllers.Audit;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Audit.Read")]
public class AuditController(AuditService auditService) : ApiControllerBase
{
    [HttpGet("logs/paged")]
    public async Task<IActionResult> GetAuditLogsPagedAsync(
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? entityName = null,
        [FromQuery] AuditActionType? action = null,
        [FromQuery] string? user = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? entityId = null)
    {
        var result = await auditService.GetLogsPagedAsync(
            page: page,
            pageSize: pageSize,
            entityName: entityName,
            action: action,
            user: user,
            from: from,
            to: to,
            entityId: entityId);
        return Ok(result);
    }
}
