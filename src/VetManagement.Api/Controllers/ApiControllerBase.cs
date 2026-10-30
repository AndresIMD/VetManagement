using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Authorization;
using VetManagement.Application.Inventory;

namespace VetManagement.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string GetUserName() => User?.Identity?.Name ?? "System";

    /// <summary>
    /// The warehouses the user sees: by their roles, or all of them with the permission to manage warehouses. The one place
    /// that reads role names (a warehouse lists the roles that see it, as clinic data), never to grant a permission.
    /// </summary>
    protected StockScope GetStockScope() => new(
        User.HasClaim(Permissions.CLAIM_TYPE, Permissions.INVENTORY.WAREHOUSES),
        User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase));

    /// <summary>Largest page a list endpoint returns, so no request can ask for a whole table at once.</summary>
    public const int MAX_PAGE_SIZE = 200;

    /// <summary>The page asked for, within bounds: page from 0, size from 1 to <see cref="MAX_PAGE_SIZE"/>.</summary>
    protected static (int Page, int PageSize) Paging(int page, int pageSize) => (Math.Max(0, page), Math.Clamp(pageSize, 1, MAX_PAGE_SIZE));

    /// <summary>400 whose title is a message the page can show as-is.</summary>
    protected ObjectResult BadRequestMessage(string message)
        => Problem(title: message, statusCode: StatusCodes.Status400BadRequest);

    /// <summary>400 with Identity's errors (password rules, duplicate names...) as one readable message.</summary>
    protected ObjectResult IdentityProblem(IdentityResult result)
        => BadRequestMessage(string.Join(" ", result.Errors.Select(e => e.Description)));
}
