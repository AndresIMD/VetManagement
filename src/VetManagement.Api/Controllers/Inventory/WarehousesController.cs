using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Authorization;
using VetManagement.Application.Inventory;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Inventory;

/// <summary>
/// The clinic's warehouses. Everyone in inventory lists the ones they see; whoever manages warehouses (Admin) creates
/// them, chooses the roles that see each one and archives them.
/// </summary>
[ApiController]
[Route("api/inventory/warehouses")]
[Authorize(Policy = "Inventory.Read")]
public class WarehousesController(WarehouseService service) : ApiControllerBase
{
    /// <summary>Roles a warehouse can list. Admin isn't one: it sees every warehouse.</summary>
    private static readonly string[] AssignableRoles = [.. Permissions.ROLES.ALL.Where(r => r != Permissions.ROLES.ADMIN)];

    [HttpGet]
    public async Task<ActionResult<List<WarehouseDto>>> GetAsync()
    {
        var scope = GetStockScope();
        return Ok((await service.ListAsync(scope)).Select(w => ToDto(w, withRoles: scope.SeesAll)).ToList());
    }

    [HttpGet("roles")]
    [Authorize(Policy = "Inventory.Warehouses")]
    public ActionResult<List<string>> GetRoles() => Ok(AssignableRoles.ToList());

    [HttpPost]
    [Authorize(Policy = "Inventory.Warehouses")]
    public async Task<IActionResult> CreateAsync([FromBody] WarehouseRequest request)
    {
        if (UnknownRoles(request) is { } error)
            return BadRequestMessage(error);
        return ToResult(await service.CreateAsync(request.Name, request.Roles, GetUserName()));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Inventory.Warehouses")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] WarehouseRequest request)
    {
        if (UnknownRoles(request) is { } error)
            return BadRequestMessage(error);
        return ToResult(await service.UpdateAsync(id, request.Name, request.Roles, request.IsArchived, GetUserName()));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Inventory.Warehouses")]
    public async Task<IActionResult> DeleteAsync(int id) => ToResult(await service.DeleteAsync(id, GetUserName()));

    private static string? UnknownRoles(WarehouseRequest request)
    {
        var unknown = request.Roles.Where(r => !AssignableRoles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase)).ToList();
        return unknown.Count == 0 ? null : Texts.T("Unknown role '{0}'.", string.Join(", ", unknown));
    }

    private IActionResult ToResult(WarehouseResult result) => result.Status switch
    {
        WarehouseStatus.Done => Ok(new { id = result.Id }),
        WarehouseStatus.NotFound => NotFound(),
        _ => BadRequestMessage(result.Error!)
    };

    private static WarehouseDto ToDto(Warehouse warehouse, bool withRoles) => new()
    {
        Id = warehouse.Id,
        Name = warehouse.Name,
        IsDailyUse = warehouse.IsDailyUse,
        IsArchived = warehouse.IsArchived,
        Roles = withRoles ? [.. warehouse.Roles] : []
    };
}
