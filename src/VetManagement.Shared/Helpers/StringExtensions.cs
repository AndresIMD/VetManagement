using System.Globalization;
using System.Text;

namespace VetManagement.Shared.Helpers;

/// <summary>
/// Extension methods for string manipulation and normalization.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Removes diacritical marks (accents) from a string.
    /// </summary>
    /// <param name="text">Text to process.</param>
    /// <returns>Text without diacritical marks.</returns>
    public static string RemoveDiacritics(this string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Compares two strings ignoring accents and case.
    /// </summary>
    public static bool EqualsIgnoringAccents(this string? text1, string? text2)
    {
        if (text1 == null && text2 == null)
            return true;
        if (text1 == null || text2 == null)
            return false;

        var normalized1 = text1.RemoveDiacritics();
        var normalized2 = text2.RemoveDiacritics();

        return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if source contains value, ignoring accents and case.
    /// </summary>
    public static bool ContainsIgnoringAccents(this string? source, string? value)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
            return false;

        var normalizedSource = source.RemoveDiacritics();
        var normalizedValue = value.RemoveDiacritics();

        return normalizedSource.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if source starts with value, ignoring accents and case.
    /// </summary>
    public static bool StartsWithIgnoringAccents(this string? source, string? value)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
            return false;

        var normalizedSource = source.RemoveDiacritics();
        var normalizedValue = value.RemoveDiacritics();

        return normalizedSource.StartsWith(normalizedValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the index of value in source, ignoring accents and case.
    /// </summary>
    public static int IndexOfIgnoringAccents(this string? source, string? value)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
            return -1;

        var normalizedSource = source.RemoveDiacritics();
        var normalizedValue = value.RemoveDiacritics();

        return normalizedSource.IndexOf(normalizedValue, StringComparison.OrdinalIgnoreCase);
    }
}
