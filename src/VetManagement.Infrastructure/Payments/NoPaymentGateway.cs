using VetManagement.Application.Payments;
using VetManagement.Domain.Localization;

namespace VetManagement.Infrastructure.Payments;

/// <summary>
/// For a clinic without the Scheduling module: nothing takes online payments there, so no provider is configured.
/// Starting or refunding a payment is a bug and fails loudly.
/// </summary>
public class NoPaymentGateway : IPaymentGateway
{
    public string Name => "None";

    public Task<PaymentStart> StartAsync(string orderId, int amount, string returnUrl, CancellationToken cancellationToken = default)
        => throw Off();

    public Task<PaymentConfirmation> ConfirmAsync(string token, CancellationToken cancellationToken = default)
        => throw Off();

    public Task<bool> RefundAsync(string token, int amount, CancellationToken cancellationToken = default)
        => throw Off();

    private static InvalidOperationException Off()
        => new(Texts.T("Online payments are off: the Scheduling module is disabled for this clinic."));
}
