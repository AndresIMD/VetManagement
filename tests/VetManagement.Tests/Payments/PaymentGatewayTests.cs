using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using VetManagement.Api.Payments;
using VetManagement.Application.Payments;
using VetManagement.Infrastructure.Payments;

namespace VetManagement.Tests.Payments;

public class WebPayPlusGatewayTests
{
    private sealed class FakeTransbank(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly WebPayPlusOptions Sandbox = new()
    {
        CommerceCode = WebPayPlusOptions.IntegrationCommerceCode, ApiKeySecret = WebPayPlusOptions.IntegrationApiKeySecret
    };

    private static (WebPayPlusGateway Gateway, FakeTransbank Transbank) Create(HttpStatusCode status, string body)
    {
        var fake = new FakeTransbank(status, body);
        return (new WebPayPlusGateway(new HttpClient(fake), Sandbox, NullLogger<WebPayPlusGateway>.Instance), fake);
    }

    [Fact]
    public async Task Start_CreatesTheTransaction_AndAsksForAPostWithTokenWs()
    {
        // Response shape verified against Transbank's integration sandbox.
        var (gateway, transbank) = Create(HttpStatusCode.OK, """{"token":"01ab","url":"https://webpay3gint.transbank.cl/webpayserver/initTransaction"}""");

        var start = await gateway.StartAsync("order-1", 22500, "https://api.clinic.cl/api/public/booking/payment-return");

        transbank.Request!.Method.Should().Be(HttpMethod.Post);
        transbank.Request.RequestUri!.ToString().Should().Be("https://webpay3gint.transbank.cl/rswebpaytransaction/api/webpay/v1.2/transactions");
        transbank.Request.Headers.GetValues("Tbk-Api-Key-Id").Should().Equal("597055555532");
        transbank.Request.Headers.Contains("Tbk-Api-Key-Secret").Should().BeTrue();
        var body = JsonNode.Parse(transbank.RequestBody!)!;
        body["buy_order"]!.GetValue<string>().Should().Be("order-1");
        body["amount"]!.GetValue<int>().Should().Be(22500);
        body["return_url"]!.GetValue<string>().Should().EndWith("/payment-return");

        start.Url.Should().Be("https://webpay3gint.transbank.cl/webpayserver/initTransaction");
        start.FormFields.Should().Equal(new Dictionary<string, string> { ["token_ws"] = "01ab" });
    }

    [Theory]
    [InlineData("""{"status":"AUTHORIZED","response_code":0,"amount":22500,"authorization_code":"1213"}""", PaymentOutcome.Approved)]
    [InlineData("""{"status":"FAILED","response_code":-1,"amount":22500}""", PaymentOutcome.Rejected)]
    public async Task Confirm_MapsTheCommitResult(string commitBody, PaymentOutcome expected)
    {
        var (gateway, transbank) = Create(HttpStatusCode.OK, commitBody);

        var result = await gateway.ConfirmAsync("01ab");

        transbank.Request!.Method.Should().Be(HttpMethod.Put);
        transbank.Request.RequestUri!.AbsolutePath.Should().EndWith("/transactions/01ab");
        result.Outcome.Should().Be(expected);
    }

    [Fact]
    public async Task Confirm_OfAnUnpaidTransaction_IsNotAnApproval()
    {
        // What the sandbox answers when committing a token that was never paid.
        var (gateway, _) = Create((HttpStatusCode)422, """{"error_message":"Invalid status '0' for transaction while authorizing."}""");

        (await gateway.ConfirmAsync("01ab")).Outcome.Should().Be(PaymentOutcome.Failed);
    }
}

public class PaymentsSetupTests
{
    private static IServiceProvider Build(string environment, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value)).Build();
        var env = new HostingEnvironment { EnvironmentName = environment };
        var services = new ServiceCollection().AddLogging();
        services.AddPaymentGateway(configuration, env);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Development_WithoutConfiguration_UsesTheSimulatedProvider()
        => Build(Environments.Development).GetRequiredService<IPaymentGateway>().Name.Should().Be("Simulated");

    [Fact]
    public void WebPayIntegration_WithoutCredentials_UsesTransbanksPublicSandboxCredentials()
    {
        var provider = Build(Environments.Development, ("Payments:Provider", "WebPayPlus"));

        provider.GetRequiredService<IPaymentGateway>().Name.Should().Be("WebPayPlus");
        provider.GetRequiredService<WebPayPlusOptions>().CommerceCode.Should().Be(WebPayPlusOptions.IntegrationCommerceCode);
    }

    [Theory]
    [InlineData(null, null)]                         // no provider configured
    [InlineData("Simulated", null)]                  // clients could book without paying
    [InlineData("WebPayPlus", "Integration")]        // the sandbox charges nothing
    [InlineData("WebPayPlus", "Production")]         // production without credentials
    public void Production_RejectsUnsafeConfiguration_AtStartup(string? provider, string? webPayEnvironment)
    {
        var settings = new List<(string, string)>();
        if (provider is not null) settings.Add(("Payments:Provider", provider));
        if (webPayEnvironment is not null) settings.Add(("Payments:WebPayPlus:Environment", webPayEnvironment));

        var build = () => Build(Environments.Production, [.. settings]);

        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Production_WithRealCredentials_UsesWebPay()
        => Build(Environments.Production,
                ("Payments:Provider", "WebPayPlus"), ("Payments:WebPayPlus:Environment", "Production"),
                ("Payments:WebPayPlus:CommerceCode", "597000000001"), ("Payments:WebPayPlus:ApiKeySecret", "secret"))
            .GetRequiredService<IPaymentGateway>().Name.Should().Be("WebPayPlus");
}
