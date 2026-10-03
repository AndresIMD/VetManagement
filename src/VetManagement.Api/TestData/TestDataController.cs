using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Controllers;
using VetManagement.Contracts.TestData;

namespace VetManagement.Api.TestData;

/// <summary>
/// Sample data for development and testing. Answers 404 in Production, always, and elsewhere only in Development
/// or Testing, or when <c>TestData:Enabled</c> is true (e.g. a demo server). Admin only.
/// </summary>
[Route("api/dev/test-data")]
[Authorize(Policy = "System.TestData")]
public class TestDataController(TestDataService service, IHostEnvironment environment, IConfiguration configuration) : ApiControllerBase
{
    private bool Available => !environment.IsProduction()
        && (environment.IsDevelopment() || environment.IsEnvironment("Testing") || configuration.GetValue<bool>("TestData:Enabled"));

    [HttpGet]
    public async Task<ActionResult<List<TestDataStatusDto>>> GetAsync()
    {
        if (!Available)
            return NotFound();
        return Ok((await service.GetStatusAsync())
            .Select(s => new TestDataStatusDto(s.Generator.Key, s.Generator.Name, s.Generator.Description, [.. s.Generator.DependsOn], s.Existing, [.. s.Missing]))
            .ToList());
    }

    /// <summary>Creates <paramref name="count"/> records of one kind. 409 names what must be created first.</summary>
    [HttpPost("{key}")]
    public async Task<IActionResult> CreateAsync(string key, [FromQuery] int count = 100)
    {
        if (!Available)
            return NotFound();
        var result = await service.CreateAsync(key, count);
        if (result.Error is null)
            return Ok(new TestDataCreatedDto(key, result.Created));
        return result.Error.Contains("first")
            ? Conflict(new ProblemDetails { Title = result.Error, Status = StatusCodes.Status409Conflict })
            : ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["testData"] = [result.Error] }));
    }
}
