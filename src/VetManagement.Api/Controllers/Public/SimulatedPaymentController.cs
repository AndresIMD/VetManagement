using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetManagement.Infrastructure.Payments;

namespace VetManagement.Api.Controllers.Public;

/// <summary>
/// The "bank page" of <see cref="SimulatedPaymentGateway"/>. Only reachable when that provider is configured
/// (its store is registered only then), so it can't exist in Production.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
[AllowAnonymous]
[Route("api/public/payments/simulated/{token}")]
public class SimulatedPaymentController(IServiceProvider services) : ControllerBase
{
    [HttpGet, HttpPost]
    public IActionResult Page(string token)
    {
        if (Find(token) is not { } payment)
            return NotFound();

        var html = $$"""
            <!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Pago simulado</title>
            <style>body{font-family:system-ui,sans-serif;max-width:420px;margin:48px auto;padding:0 16px;text-align:center}
            button{font-size:1rem;padding:10px 20px;margin:6px;border-radius:6px;border:0;cursor:pointer}
            .ok{background:#2e7d32;color:#fff}.no{background:#c62828;color:#fff}</style></head><body>
            <h2>Pago simulado</h2><p>Ambiente de desarrollo: no se cobra dinero real.</p>
            <p style="font-size:1.4rem"><strong>${{payment.Amount:N0}}</strong> CLP</p>
            <form method="post" action="{{WebUtility.HtmlEncode(token)}}/approve" style="display:inline"><button class="ok" type="submit">Aprobar pago</button></form>
            <form method="post" action="{{WebUtility.HtmlEncode(token)}}/reject" style="display:inline"><button class="no" type="submit">Rechazar</button></form>
            </body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost("approve")]
    public IActionResult Approve(string token) => Decide(token, approved: true);

    [HttpPost("reject")]
    public IActionResult Reject(string token) => Decide(token, approved: false);

    private IActionResult Decide(string token, bool approved)
    {
        if (Find(token) is not { } payment)
            return NotFound();
        payment.Approved = approved;
        // Like WebPay: back to the return URL carrying token_ws.
        var separator = payment.ReturnUrl.Contains('?') ? '&' : '?';
        return Redirect($"{payment.ReturnUrl}{separator}token_ws={Uri.EscapeDataString(token)}");
    }

    private SimulatedPayment? Find(string token)
        => services.GetService<SimulatedPaymentStore>() is { } store && store.Payments.TryGetValue(token, out var payment) ? payment : null;
}
