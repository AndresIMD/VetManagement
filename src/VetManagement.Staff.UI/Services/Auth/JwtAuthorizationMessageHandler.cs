using System.Net.Http.Headers;
using VetManagement.Staff.UI.Services.Api;

namespace VetManagement.Staff.UI.Services.Auth;

/// <summary>
/// A delegating handler that automatically attaches the JWT bearer token to outgoing
/// HTTP requests. This handler should be added to the HttpClient used for API calls.
/// </summary>
public class JwtAuthorizationMessageHandler(AccountApiService accountService) : DelegatingHandler
{
    /// <summary>
    /// Overrides the SendAsync method to intercept the request, retrieve the token,
    /// and add it to the Authorization header before sending it.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await accountService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
