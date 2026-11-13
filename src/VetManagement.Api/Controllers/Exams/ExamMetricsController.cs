using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Application.Exams;
using VetManagement.Contracts.Exams;
using VetManagement.Domain.Localization;

namespace VetManagement.Api.Controllers.Exams;

/// <summary>Its own route: under api/exam-reports, "metrics" would sit beside the reports' ids.</summary>
[Route("api/exam-metrics")]
[Authorize(Policy = "Exams.Metrics")]
public class ExamMetricsController(ExamMetricsService service) : ApiControllerBase
{
    /// <summary>Lab reports between two dates (inclusive), by the date the sample was received.</summary>
    [HttpGet]
    public async Task<ActionResult<ExamMetricsDto>> GetAsync([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber >= ExamMetricsService.MaxDays)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["to"] = [Texts.T("Use a range of at most {0} days, ending on or after the start.", ExamMetricsService.MaxDays)]
            }));

        return Ok(await service.GetAsync(from, to));
    }
}
