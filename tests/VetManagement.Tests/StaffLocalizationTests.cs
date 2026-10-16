using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using VetManagement.Staff.UI.Resources;
using VetManagement.Staff.UI.Services.App;

namespace VetManagement.Tests;

// Changes the process-wide culture: runs alone and puts it back.
[Collection(nameof(StaffLocalizationTests))]
[CollectionDefinition(nameof(StaffLocalizationTests), DisableParallelization = true)]
public class StaffLocalizationTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture, _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
    }

    private static IServiceProvider Build(string? culture)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddStaffLocalization();
        StaffLocalization.ApplyCulture(culture);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Spanish_TranslatesTextsAndMudBlazor_AndFormatsDatesTheChileanWay()
    {
        var provider = Build("es-CL");

        provider.GetRequiredService<IStringLocalizer<Strings>>()["Log in"].Value.Should().Be("Ingresar");
        provider.GetRequiredService<MudLocalizer>()["MudDataGridPager_RowsPerPage"].Value.Should().Be("Filas por página:");
        new DateTime(2026, 10, 15).ToString("d", CultureInfo.CurrentCulture).Should().Be("15-10-2026");
    }

    [Fact]
    public void ChileanSpanish_OverridesOnlyItsOwnTerms()
    {
        var chile = Build("es-CL").GetRequiredService<IStringLocalizer<Strings>>();
        chile["Tax ID"].Value.Should().Be("RUT");
        chile["Log in"].Value.Should().Be("Ingresar");

        Build("es-MX").GetRequiredService<IStringLocalizer<Strings>>()["Tax ID"].Value.Should().Be("Identificación fiscal");
    }

    [Fact]
    public void AMissingTranslation_ShowsTheEnglishKey()
        => Build("es-CL").GetRequiredService<IStringLocalizer<Strings>>()["Some text nobody translated"].Value
            .Should().Be("Some text nobody translated");

    [Theory]
    [InlineData(null)]
    [InlineData("xx-NOPE")]
    public void NoOrUnknownCulture_IsEnglish(string? culture)
    {
        var provider = Build(culture);

        CultureInfo.CurrentUICulture.Name.Should().Be(StaffLocalization.DefaultCulture);
        provider.GetRequiredService<IStringLocalizer<Strings>>()["Log in"].Value.Should().Be("Log in");
    }

    [Fact]
    public void SpanishResources_HaveNoDuplicateOrEmptyEntries()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "VetManagement.Staff.UI", "Resources", "Strings.es.resx");
        var entries = XDocument.Load(file).Root!.Elements("data")
            .Select(d => (Key: (string)d.Attribute("name")!, Value: (string?)d.Element("value"))).ToList();

        // Keys that differ only in case collide when the resources are compiled: one of them is dropped.
        entries.GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).Should().BeEmpty();
        entries.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).Should().BeEmpty();
    }
}
