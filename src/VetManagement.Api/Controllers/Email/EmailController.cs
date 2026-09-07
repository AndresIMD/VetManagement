using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Api.Services;

namespace VetManagement.Api.Controllers.Email;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "System.SendEmail")]
public class EmailController(EmailService emailService) : ApiControllerBase
{
    /// <summary>
    /// Represents the data required for a send email request.
    /// </summary>
    public record SendEmailRequest(string To, string? Cc, string Subject, string Body);

    [HttpPost("send")]
    public async Task<IActionResult> SendAsync([FromBody] SendEmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.To))
            return BadRequest("A 'To' recipient must be specified.");
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest("A subject must be specified.");
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("The email body cannot be empty.");

        var to = request.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cc = string.IsNullOrWhiteSpace(request.Cc) ? null : request.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await emailService.SendAsync(to, cc, request.Subject, request.Body, cancellationToken);

        return Ok(new { success = true, message = "Email sent successfully." });
    }
}
