using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VetManagement.Application.Payments;

namespace VetManagement.Infrastructure.Payments;

/// <summary>Deployment settings for Transbank WebPay Plus (section Payments:WebPayPlus).</summary>
public sealed class WebPayPlusOptions
{
    /// <summary>"Integration" (Transbank's sandbox) or "Production".</summary>
    public string Environment { get; set; } = "Integration";
    public string CommerceCode { get; set; } = string.Empty;
    /// <summary>Secret: set with user-secrets / environment variables, never in appsettings.json.</summary>
    public string ApiKeySecret { get; set; } = string.Empty;

    /// <summary>Transbank's public test credentials for the integration environment.</summary>
    public const string IntegrationCommerceCode = "597055555532";
    public const string IntegrationApiKeySecret = "579B532A7440BB0C9079DED94D31EA1615BACEB56610332264630D42D0A36B1C";

    public bool IsProduction => string.Equals(Environment, "Production", StringComparison.OrdinalIgnoreCase);
    public Uri BaseAddress => new(IsProduction ? "https://webpay3g.transbank.cl" : "https://webpay3gint.transbank.cl");
}

/// <summary>Transbank WebPay Plus REST API v1.2 (create, commit, refund).</summary>
public class WebPayPlusGateway(HttpClient http, WebPayPlusOptions options, ILogger<WebPayPlusGateway> logger) : IPaymentGateway
{
    private const string Transactions = "/rswebpaytransaction/api/webpay/v1.2/transactions";

    public string Name => "WebPayPlus";

    public async Task<PaymentStart> StartAsync(string orderId, int amount, string returnUrl, CancellationToken cancellationToken = default)
    {
        using var request = Authorized(HttpMethod.Post, Transactions);
        request.Content = JsonContent.Create(new CreateRequest(orderId, orderId, amount, returnUrl));
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "create", cancellationToken);

        var created = (await response.Content.ReadFromJsonAsync<CreateResponse>(cancellationToken))!;
        return new PaymentStart(created.Token, created.Url, new Dictionary<string, string> { ["token_ws"] = created.Token });
    }

    public async Task<PaymentConfirmation> ConfirmAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = Authorized(HttpMethod.Put, $"{Transactions}/{Uri.EscapeDataString(token)}");
            using var response = await http.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, "commit", cancellationToken);

            var commit = (await response.Content.ReadFromJsonAsync<CommitResponse>(cancellationToken))!;
            var approved = commit.Status == "AUTHORIZED" && commit.ResponseCode == 0;
            return new PaymentConfirmation(approved ? PaymentOutcome.Approved : PaymentOutcome.Rejected,
                commit.Amount, commit.AuthorizationCode, $"status={commit.Status} response_code={commit.ResponseCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogError(ex, "WebPay commit failed for token {Token}", token);
            return new PaymentConfirmation(PaymentOutcome.Failed, 0, Detail: ex.Message);
        }
    }

    public async Task<bool> RefundAsync(string token, int amount, CancellationToken cancellationToken = default)
    {
        using var request = Authorized(HttpMethod.Post, $"{Transactions}/{Uri.EscapeDataString(token)}/refunds");
        request.Content = JsonContent.Create(new { amount });
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return true;
        logger.LogError("WebPay refund of {Amount} for token {Token} failed: {Status} {Body}",
            amount, token, (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        return false;
    }

    private HttpRequestMessage Authorized(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(options.BaseAddress, path));
        request.Headers.Add("Tbk-Api-Key-Id", options.CommerceCode);
        request.Headers.Add("Tbk-Api-Key-Secret", options.ApiKeySecret);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"WebPay {operation} failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}",
                null, response.StatusCode);
    }

    private sealed record CreateRequest(
        [property: JsonPropertyName("buy_order")] string BuyOrder,
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("return_url")] string ReturnUrl);

    private sealed record CreateResponse(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("url")] string Url);

    private sealed record CommitResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("response_code")] int? ResponseCode,
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("authorization_code")] string? AuthorizationCode);
}
