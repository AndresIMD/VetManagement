using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace VetManagement.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string GetUserName() => User?.Identity?.Name ?? "System";

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
