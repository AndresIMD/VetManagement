using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace VetManagement.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string GetUserName() => User?.Identity?.Name ?? "System";

    /// <summary>400 whose title is a message the page can show as-is.</summary>
    protected ObjectResult BadRequestMessage(string message)
        => Problem(title: message, statusCode: StatusCodes.Status400BadRequest);

    /// <summary>400 with Identity's errors (password rules, duplicate names...) as one readable message.</summary>
    protected ObjectResult IdentityProblem(IdentityResult result)
        => BadRequestMessage(string.Join(" ", result.Errors.Select(e => e.Description)));
}
