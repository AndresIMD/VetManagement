using System.Net.Http.Json;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Staff.UI.Services.Api;

public class AlertsApiService(HttpClient http)
{
    /// <summary>
    /// Defines the data structure for an email sending request.
    /// </summary>
    public record SendEmailRequest(string To, string? Cc, string Subject, string Body);

    public async Task SendInventoryEmailAsync(string to, string? cc, string subject, string body, CancellationToken ct = default)
    {
        var payload = new SendEmailRequest(to, cc, subject, body);
        var response = await http.PostAsJsonAsync(ApiRouteConstants.SEND_EMAIL, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Error sending email: {(int)response.StatusCode} - {response.ReasonPhrase}. {errorBody}");
        }
    }
}
