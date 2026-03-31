using Microsoft.AspNetCore.Mvc;

namespace VetManagement.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string GetUserName() => User?.Identity?.Name ?? "System";
}
