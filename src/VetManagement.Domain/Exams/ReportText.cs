namespace VetManagement.Domain.Exams;

/// <summary>
/// The small markup of texts the clinic writes for its reports (e.g. the footer): each line prints as written, an empty
/// line leaves a gap and **text** prints in bold.
/// </summary>
public static class ReportText
{
    public const string BOLD = "**";

    /// <summary>The printed lines; empty lines inside the text stay, the ones before and after it don't.</summary>
    public static string[] Lines(string? text)
    {
        var trimmed = (text ?? string.Empty).Replace("\r", string.Empty).Trim();
        return trimmed.Length == 0 ? [] : [.. trimmed.Split('\n').Select(l => l.Trim())];
    }

    /// <summary>
    /// The title as printed: when its first word is in capitals the whole title is, e.g. "EXAMEN Gases en sangre" prints
    /// "EXAMEN GASES EN SANGRE" (exam names, often part of the title, may not be in capitals). Other titles print as written.
    /// </summary>
    public static string Title(string? title)
    {
        var text = (title ?? string.Empty).Trim();
        var letters = text.Split(' ', 2)[0].Where(char.IsLetter).ToList();
        return letters.Count >= 2 && letters.All(char.IsUpper) ? text.ToUpperInvariant() : text;
    }

    /// <summary>The pieces of one line, each bold or not. A "**" without its closing pair prints as written.</summary>
    public static List<ReportTextSpan> Spans(string line)
    {
        var parts = line.Split(BOLD);
        var spans = new List<ReportTextSpan>();
        for (var i = 0; i < parts.Length; i++)
        {
            var unclosed = i % 2 == 1 && i == parts.Length - 1;
            var text = unclosed ? BOLD + parts[i] : parts[i];
            if (text.Length > 0)
                spans.Add(new ReportTextSpan(text, i % 2 == 1 && !unclosed));
        }
        return spans;
    }
}

public sealed record ReportTextSpan(string Text, bool Bold);
