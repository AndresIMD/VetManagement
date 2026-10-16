using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using VetManagement.Staff.UI.Resources;

namespace VetManagement.Staff.UI.Services.App;

/// <summary>
/// One language per clinic deployment: the culture comes from the app's config.json ("Culture", e.g. "es-CL").
/// It sets the texts (Strings.{language}.resx) and the date/number formats. Absent or unknown = English.
/// </summary>
public static class StaffLocalization
{
    public const string DefaultCulture = "en-US";

    public static IServiceCollection AddStaffLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.AddTransient<MudLocalizer, StaffMudLocalizer>();
        return services;
    }

    /// <summary>
    /// Sets the app's culture. In WebAssembly call it after building the host and before running it: the host then
    /// downloads that culture's translations (satellite assemblies), which it only does for a culture changed after
    /// it was built.
    /// </summary>
    public static void ApplyCulture(string? culture)
    {
        var info = Resolve(culture);
        CultureInfo.DefaultThreadCurrentCulture = info;
        CultureInfo.DefaultThreadCurrentUICulture = info;
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
    }

    private static CultureInfo Resolve(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return CultureInfo.GetCultureInfo(DefaultCulture);
        try
        {
            // ICU accepts any well-formed name ("xx-NOPE"), with no ISO 639-2 language; predefinedOnly isn't usable
            // in WebAssembly, where it rejects every culture.
            var info = CultureInfo.GetCultureInfo(culture.Trim());
            var unknown = string.IsNullOrEmpty(info.ThreeLetterISOLanguageName) || info.EnglishName.StartsWith("Unknown", StringComparison.Ordinal);
            return unknown ? CultureInfo.GetCultureInfo(DefaultCulture) : info;
        }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo(DefaultCulture); }
    }
}

/// <summary>MudBlazor's built-in texts (pager, pickers, grids) from the same resource files, by MudBlazor key.</summary>
public sealed class StaffMudLocalizer(IStringLocalizer<Strings> strings) : MudLocalizer
{
    public override LocalizedString this[string key]
    {
        get
        {
            var text = strings[key];
            // Not translated: MudBlazor falls back to its own English text.
            return text.ResourceNotFound ? new LocalizedString(key, key, resourceNotFound: true) : text;
        }
    }
}
