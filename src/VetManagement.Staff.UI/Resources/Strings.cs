using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using VetManagement.Domain.Enums;

namespace VetManagement.Staff.UI.Resources;

/// <summary>
/// Texts of the staff UI. The key is the English text, so a text without a translation shows in English:
/// <c>@L["Save"]</c> in markup, <c>L["Saved {0}", name]</c> for placeholders. Translations live in
/// <c>Strings.{culture}.resx</c> (e.g. <c>Strings.es.resx</c>); MudBlazor's own texts use their MudBlazor key there.
/// Code without an injected localizer (helpers, services) uses <see cref="T"/>.
/// </summary>
public sealed class Strings
{
    private static readonly ResourceManager Resources = new(typeof(Strings).FullName!, typeof(Strings).Assembly);

    /// <summary>The text in the current UI culture; <paramref name="args"/> fill its {0}, {1}...</summary>
    public static string T(string text, params object?[] args)
    {
        var translated = Resources.GetString(text, CultureInfo.CurrentUICulture) ?? text;
        return args.Length == 0 ? translated : string.Format(CultureInfo.CurrentCulture, translated, args);
    }
}

public static class EnumTextExtensions
{
    /// <summary>
    /// The enum's display string (<see cref="DisplayStringAttribute"/>, or the member name as words: PendingPayment →
    /// "Pending payment") in the current UI culture.
    /// </summary>
    public static string ToText<TEnum>(this TEnum value) where TEnum : Enum
    {
        var name = value.ToString();
        var display = value.GetDisplayString();
        if (display == name)
            display = Regex.Replace(name, "(?<=[a-z])([A-Z])", m => " " + m.Value.ToLowerInvariant());
        return Strings.T(display);
    }
}
