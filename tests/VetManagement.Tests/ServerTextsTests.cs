using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Localization;

namespace VetManagement.Tests;

// Changes the server's process-wide language: runs alone (non-parallel collection) and puts English back.
[Collection(nameof(StaffLocalizationTests))]
public class ServerTextsTests : IDisposable
{
    public void Dispose() => Texts.Culture = CultureInfo.InvariantCulture;

    [Fact]
    public void Spanish_TranslatesMessagesAndDefaults()
    {
        Texts.UseCulture("es-CL");

        Texts.T("Unknown service '{0}'.", "x").Should().Be("Servicio desconocido 'x'.");
        new BillingSettings().PaymentMethods.Should().Contain(m => m.Code == "Cash" && m.Name == "Efectivo");
        new Sale { CustomerName = "Ana" }.Void("", "admin", DateTime.UtcNow).Should().Be("Se necesita un motivo para anular una venta.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("xx-NOPE")]
    public void NoOrUnknownCulture_IsEnglish(string? culture)
    {
        Texts.UseCulture(culture);

        Texts.Culture.Should().Be(CultureInfo.InvariantCulture);
        Texts.T("Unknown service '{0}'.", "x").Should().Be("Unknown service 'x'.");
    }

    [Fact]
    public void SpanishResources_HaveNoDuplicateOrEmptyEntries()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "VetManagement.Domain", "Resources", "Texts.es.resx");
        var entries = XDocument.Load(file).Root!.Elements("data")
            .Select(d => (Key: (string)d.Attribute("name")!, Value: (string?)d.Element("value"))).ToList();

        // Keys that differ only in case collide when the resources are compiled: one of them is dropped.
        entries.GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).Should().BeEmpty();
        entries.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).Should().BeEmpty();
    }
}
