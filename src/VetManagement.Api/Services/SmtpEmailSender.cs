using VetManagement.Application.Contracts.Services;

namespace VetManagement.Api.Services;

/// <summary>Application's email port over the existing SMTP <see cref="EmailService"/>.</summary>
public class SmtpEmailSender(EmailService smtp) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        => smtp.SendAsync([to], null, subject, body, cancellationToken);
}
