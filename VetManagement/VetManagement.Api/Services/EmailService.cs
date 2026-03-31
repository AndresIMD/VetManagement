using System.Net;
using System.Net.Mail;

namespace VetManagement.Api.Services;

/// <summary>
/// Provides a service to send emails using SMTP settings from the application's configuration
/// </summary>
public class EmailService(IConfiguration configuration, ILogger<EmailService> logger)
{
    /// <summary>
    /// Checks if a configuration value is a placeholder ("<default-value>")
    /// </summary>
    private static bool IsPlaceholder(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().StartsWith('<') && value.Trim().EndsWith('>');

    /// <summary>
    /// Sends an email asynchronously
    /// </summary>
    /// <param name="to">An array of primary recipients.</param>
    /// <param name="cc">An optional array of CC recipients.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="body">The email body.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown if no 'to' recipients are provided.</exception>
    /// <exception cref="InvalidOperationException">Thrown if SMTP settings are missing or contain placeholders.</exception>
    public async Task SendAsync(string[] to, string[]? cc, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (to == null || to.Length == 0)
            throw new ArgumentException("At least one recipient must be specified.", nameof(to));

        var host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("Config 'Smtp:Host' is not defined.");
        var port = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
        var user = configuration["Smtp:User"];
        var pass = configuration["Smtp:Pass"];
        var from = configuration["Smtp:From"] ?? user ?? throw new InvalidOperationException("Config 'Smtp:From' or 'Smtp:User' is not defined.");
        var enableSsl = bool.TryParse(configuration["Smtp:EnableSsl"], out var ssl) && ssl;

        if (IsPlaceholder(host) || IsPlaceholder(user) || IsPlaceholder(pass) || IsPlaceholder(from))
            throw new InvalidOperationException("SMTP configuration contains placeholders. Configure 'Smtp:Host', 'Smtp:User', 'Smtp:Pass', and 'Smtp:From' using 'dotnet user-secrets set'.");

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = string.IsNullOrWhiteSpace(user) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(user, pass)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(from),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        foreach (var address in to.Where(a => !string.IsNullOrWhiteSpace(a)))
            message.To.Add(address.Trim());

        if (cc != null)
        {
            foreach (var address in cc.Where(a => !string.IsNullOrWhiteSpace(a)))
                message.CC.Add(address.Trim());
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending email to {To}", string.Join(", ", to));
            throw;
        }
    }
}
