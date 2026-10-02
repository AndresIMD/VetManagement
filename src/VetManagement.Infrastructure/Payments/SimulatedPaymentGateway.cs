using System.Collections.Concurrent;
using VetManagement.Application.Payments;

namespace VetManagement.Infrastructure.Payments;

/// <summary>
/// Development/demo provider: no money moves. The client sees a local page with Approve/Reject buttons
/// (SimulatedPaymentController) and is sent back to the return URL like WebPay would.
/// Never allowed in Production (the API refuses to start).
/// </summary>
public class SimulatedPaymentGateway(SimulatedPaymentStore store) : IPaymentGateway
{
    public const string PagePath = "/api/public/payments/simulated/";

    public string Name => "Simulated";

    public Task<PaymentStart> StartAsync(string orderId, int amount, string returnUrl, CancellationToken cancellationToken = default)
    {
        var token = Guid.NewGuid().ToString("N");
        store.Payments[token] = new SimulatedPayment(amount, returnUrl);
        // The return URL points at this API, so the simulated page lives on the same host.
        var page = new Uri(new Uri(returnUrl), PagePath + token).ToString();
        return Task.FromResult(new PaymentStart(token, page, new Dictionary<string, string>()));
    }

    public Task<PaymentConfirmation> ConfirmAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(store.Payments.TryGetValue(token, out var payment) && payment.Approved == true
            ? new PaymentConfirmation(PaymentOutcome.Approved, payment.Amount, "SIMULATED")
            : new PaymentConfirmation(PaymentOutcome.Rejected, payment?.Amount ?? 0));

    public Task<bool> RefundAsync(string token, int amount, CancellationToken cancellationToken = default)
        => Task.FromResult(store.Payments.ContainsKey(token));
}

public sealed class SimulatedPayment(int amount, string returnUrl)
{
    public int Amount { get; } = amount;
    public string ReturnUrl { get; } = returnUrl;
    public bool? Approved { get; set; }
}

/// <summary>In-memory pending simulated payments (singleton; lost on restart, which is fine for demos).</summary>
public sealed class SimulatedPaymentStore
{
    public ConcurrentDictionary<string, SimulatedPayment> Payments { get; } = new();
}
