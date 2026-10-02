namespace VetManagement.Application.Payments;

/// <summary>
/// Where to send the client's browser to pay: a form POST to <see cref="Url"/> with <see cref="FormFields"/>
/// (WebPay requires a POST with token_ws; other providers may need no fields).
/// </summary>
public sealed record PaymentStart(string Token, string Url, IReadOnlyDictionary<string, string> FormFields);

public enum PaymentOutcome
{
    Approved,
    /// <summary>The issuer or the client rejected it; nothing was charged.</summary>
    Rejected,
    /// <summary>The provider couldn't be asked or answered unexpectedly; treat as not paid.</summary>
    Failed
}

public sealed record PaymentConfirmation(PaymentOutcome Outcome, int Amount, string? AuthorizationCode = null, string? Detail = null);

/// <summary>
/// A payment provider. Each clinic deployment picks one in configuration (Payments:Provider), so the booking
/// flow never depends on a specific provider. Credentials come from deployment config/secrets, never the database.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Provider name stored with the payment ("WebPayPlus", "Simulated", ...).</summary>
    string Name { get; }

    /// <param name="orderId">Our reference, unique per payment attempt (max 26 chars for WebPay).</param>
    /// <param name="amount">Whole pesos (CLP has no decimals).</param>
    /// <param name="returnUrl">Where the provider sends the browser back after paying.</param>
    Task<PaymentStart> StartAsync(string orderId, int amount, string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>Confirms (commits) the payment after the client comes back. Call once per token.</summary>
    Task<PaymentConfirmation> ConfirmAsync(string token, CancellationToken cancellationToken = default);

    /// <returns>Whether the provider accepted the refund.</returns>
    Task<bool> RefundAsync(string token, int amount, CancellationToken cancellationToken = default);
}
