namespace VetManagement.Application.Contracts.Services;

/// <summary>Sends a plain-text email. Implemented by the API over SMTP; tests record messages instead.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
