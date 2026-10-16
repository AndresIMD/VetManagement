using System.Globalization;
using System.Resources;

namespace VetManagement.Domain.Localization;

/// <summary>
/// Texts the server shows to people (validation errors, client emails, default settings), in the clinic's language.
/// The key is the English text, so a text without a translation stays in English: <c>Texts.T("Unknown service '{0}'.", code)</c>.
/// Translations live in <c>Resources/Texts.{culture}.resx</c>. The API sets <see cref="Culture"/> from its configuration
/// (Localization:Culture) at start-up; until then (and in tests) texts are English.
/// </summary>
public static class Texts
{
    private static readonly ResourceManager Resources = new("VetManagement.Domain.Resources.Texts", typeof(Texts).Assembly);

    /// <summary>The clinic's language and formats. Invariant (English) unless configured.</summary>
    public static CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>Sets <see cref="Culture"/> from a name like "es-CL"; empty or unknown keeps English.</summary>
    public static void UseCulture(string? name)
    {
        CultureInfo? culture = null;
        if (!string.IsNullOrWhiteSpace(name))
        {
            try { culture = CultureInfo.GetCultureInfo(name.Trim()); }
            catch (CultureNotFoundException) { }
        }
        // ICU accepts any well-formed name ("xx-NOPE"); a real culture has an ISO 639-2 language.
        Culture = culture is { ThreeLetterISOLanguageName.Length: > 0 } && !culture.EnglishName.StartsWith("Unknown", StringComparison.Ordinal)
            ? culture
            : CultureInfo.InvariantCulture;
    }

    /// <summary>The text in <see cref="Culture"/>; <paramref name="args"/> fill its {0}, {1}...</summary>
    public static string T(string text, params object?[] args)
    {
        var translated = Resources.GetString(text, Culture) ?? text;
        return args.Length == 0 ? translated : string.Format(Culture, translated, args);
    }
}
