using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Localization;

namespace VetManagement.Domain.Exams;

/// <summary>
/// Per-clinic exam report configuration (see docs/modules/EXAM_REPORTS.md): how every report looks (paper,
/// colours, header, footer; logo and background are uploaded images) and, per catalog exam, what its report
/// contains and how results are interpreted.
/// </summary>
public class ExamReportSettings
{
    public PaperSize Paper { get; set; } = PaperSize.Letter;

    /// <summary>Space kept free around the content, so a background frame can sit there.</summary>
    public ReportMargins MarginsMm { get; set; } = new();

    /// <summary>Bar behind the exam title and table headers (CSS colour).</summary>
    public string AccentColor { get; set; } = "#12A9C1";

    /// <summary>Bar behind the patient data heading (CSS colour).</summary>
    public string HeaderColor { get; set; } = "#2D2E30";

    /// <summary>Band behind the section titles (CSS colour); empty = the patient bar's colour, as before it could be chosen.</summary>
    public string? SectionColor { get; set; }

    /// <summary>The section titles' colour as printed.</summary>
    [JsonIgnore]
    public string SectionBarColor => string.IsNullOrWhiteSpace(SectionColor) ? HeaderColor : SectionColor;

    /// <summary>The section titles' text: white like the other bars, dark on a light colour so it stays readable.</summary>
    [JsonIgnore]
    public string SectionTextColor => TextColorOn(SectionBarColor);

    /// <summary>
    /// White text, or the report's dark text when the background is light (perceived brightness, YIQ, of 150 or more out of
    /// 255): brand colours of medium tone keep white text, pale and bright ones (light grey, yellow, orange) get dark text.
    /// </summary>
    public static string TextColorOn(string background)
    {
        if (!IsColor(background))
            return "#FFFFFF";
        int Channel(int start) => Convert.ToInt32(background.Substring(start, 2), 16);
        var brightness = (Channel(1) * 299 + Channel(3) * 587 + Channel(5) * 114) / 1000.0;
        return brightness >= 150 ? "#222222" : "#FFFFFF";
    }

    /// <summary>Lines next to the logo, e.g. "LABORATORIO" and the address.</summary>
    public List<string> HeaderLines { get; set; } = [];

    /// <summary>
    /// Clinic details printed at the bottom (name, tax id, address, phone), written as a paragraph: each line prints as
    /// written, an empty line leaves a gap and **text** prints in bold (see <see cref="ReportText"/>).
    /// </summary>
    public string Footer { get; set; } = string.Empty;

    public const int MaxFooterLines = 12;
    public const int MaxFooterLength = 1000;

    /// <summary>
    /// Earlier documents kept the footer as a list of lines, the first one in bold: read into <see cref="Footer"/>, never written.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? FooterLines
    {
        get => null;
        set
        {
            if (value is { Count: > 0 } && string.IsNullOrWhiteSpace(Footer))
                Footer = string.Join("\n", value.Select((line, i) => i == 0 ? $"{ReportText.BOLD}{line}{ReportText.BOLD}" : line));
        }
    }

    public string Disclaimer { get; set; } = Texts.T("The result of this exam is not a diagnosis; please consult your referring vet.");

    /// <summary>Font of the whole report; one of <see cref="Fonts"/>.</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>Fonts every Windows PC has.</summary>
    public static readonly string[] SystemFonts =
        ["Segoe UI", "Arial", "Calibri", "Verdana", "Tahoma", "Trebuchet MS", "Century Gothic", "Georgia", "Cambria", "Times New Roman"];

    /// <summary>
    /// Open-licence fonts (SIL OFL) shipped with the staff app and embedded in the printed report, so they look the same on
    /// every computer: Inter and Figtree (close to Aptos), Jost (close to Futura).
    /// </summary>
    public static readonly string[] BundledFonts = ["Inter", "Figtree", "Jost"];

    public static readonly string[] Fonts = [.. SystemFonts, .. BundledFonts];

    public const double DefaultFontPt = 10.5;
    public const double MinFontPt = 8;
    public const double MaxFontPt = 14;

    /// <summary>Text size of the pet and owner data, in points.</summary>
    public double PatientFontPt { get; set; } = DefaultFontPt;

    /// <summary>Text size of the results (table, interpretation, criteria), in points; the smaller texts in it keep their proportion.</summary>
    public double ResultsFontPt { get; set; } = DefaultFontPt;

    /// <summary>Later pages repeat the patient, owner and reception date in a slim box; off, their results start under the header.</summary>
    public bool PatientDataOnLaterPages { get; set; } = true;

    /// <summary>How the result columns' texts sit (centered unless chosen); the parameter names always align left.</summary>
    public ColumnAlign ColumnAlign { get; set; } = ColumnAlign.Center;

    /// <summary>1 = background image as uploaded; lower values fade it into a watermark.</summary>
    public double BackgroundOpacity { get; set; } = 1;

    /// <summary>The background fills the whole sheet, or only the results box (behind the table).</summary>
    public BackgroundArea BackgroundArea { get; set; } = BackgroundArea.Page;

    /// <summary>Keep the background's proportions; false stretches it over the whole area.</summary>
    public bool BackgroundKeepRatio { get; set; }

    /// <summary>With the proportions kept: the side the background fills.</summary>
    public ImageFit BackgroundFit { get; set; } = ImageFit.Width;

    /// <summary>With the proportions kept: top/centre/bottom when filling the width, left/centre/right when filling the height.</summary>
    public ImageAlign BackgroundAlign { get; set; } = ImageAlign.Start;

    /// <summary>Logo height in mm; with the proportions kept its width follows.</summary>
    public int LogoHeightMm { get; set; } = 30;

    /// <summary>Logo width in mm, used only when its proportions aren't kept.</summary>
    public int LogoWidthMm { get; set; } = 80;

    public bool LogoKeepRatio { get; set; } = true;

    public List<ExamReportTemplate> Templates { get; set; } = [];

    public ExamReportTemplate? FindTemplate(int examId) => Templates.FirstOrDefault(t => t.ExamId == examId);

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (MarginsMm.Top is < 0 or > 60 || MarginsMm.Right is < 0 or > 60 || MarginsMm.Bottom is < 0 or > 60 || MarginsMm.Left is < 0 or > 60)
            errors.Add(Texts.T("Margins must be between 0 and 60 mm."));
        if (BackgroundOpacity is < 0.05 or > 1)
            errors.Add(Texts.T("BackgroundOpacity must be between 0.05 and 1."));
        if (LogoHeightMm is < 10 or > 60 || LogoWidthMm is < 10 or > 150)
            errors.Add(Texts.T("The logo must be 10–60 mm high and 10–150 mm wide."));
        if (!IsColor(AccentColor) || !IsColor(HeaderColor) || (!string.IsNullOrWhiteSpace(SectionColor) && !IsColor(SectionColor)))
            errors.Add(Texts.T("Colours must be written as #RRGGBB."));
        if (!Fonts.Contains(FontFamily))
            errors.Add(Texts.T("FontFamily must be one of: {0}.", string.Join(", ", Fonts)));
        if (PatientFontPt is < MinFontPt or > MaxFontPt || ResultsFontPt is < MinFontPt or > MaxFontPt)
            errors.Add(Texts.T("Text sizes must be between {0} and {1} pt.", MinFontPt, MaxFontPt));
        if (HeaderLines.Count > 6 || ReportText.Lines(Footer).Length > MaxFooterLines)
            errors.Add(Texts.T("Use at most 6 header lines and {0} footer lines.", MaxFooterLines));
        if (Footer.Length > MaxFooterLength)
            errors.Add(Texts.T("The footer can be at most {0} characters long.", MaxFooterLength));

        foreach (var examId in Templates.GroupBy(t => t.ExamId).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add(Texts.T("Exam {0} has more than one report template.", examId));
        foreach (var template in Templates)
            errors.AddRange(template.Validate());
        return errors;
    }

    private static bool IsColor(string value) => Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$");
}

public class ReportMargins
{
    public int Top { get; set; } = 12;
    public int Right { get; set; } = 15;
    public int Bottom { get; set; } = 12;
    public int Left { get; set; } = 15;
}

/// <summary>The report of one catalog exam: its title, which blocks it prints and the measured parameters.</summary>
public class ExamReportTemplate
{
    public int ExamId { get; set; }

    /// <summary>Printed on the result bar, e.g. "PRUEBA cPL".</summary>
    public string Title { get; set; } = string.Empty;

    public ExamReportSections Sections { get; set; } = new();

    public List<ReportParameter> Parameters { get; set; } = [];

    /// <summary>Interpretations that depend on several values (e.g. cortisol at 0, 4 and 8 h); checked in order.</summary>
    public List<InterpretationRule> Rules { get; set; } = [];

    /// <summary>The manufacturer's criteria table, printed under the results when <see cref="ExamReportSections.CriteriaTable"/> is on.</summary>
    public CriteriaTable Criteria { get; set; } = new();

    public ReportParameter? FindParameter(string code)
        => Parameters.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The printed lines for the values given, in the template's order. A section title prints above the first value of
    /// its own (the parameters after it, up to the next title), and not at all when none of them has a value. A page
    /// break is kept only between values (never first or last; several in a row count once), before a title that waits
    /// for its first value.
    /// </summary>
    public List<ExamReportResult> ResultsFor(IReadOnlyDictionary<string, string> values, Species species)
    {
        var results = new List<ExamReportResult>();
        ReportParameter? section = null, pageBreak = null;
        foreach (var parameter in Parameters)
        {
            if (parameter.IsSection)
                section = parameter;
            else if (parameter.IsPageBreak)
                pageBreak ??= parameter;
            else if (values.TryGetValue(parameter.Code, out var value))
            {
                if (pageBreak is not null && results.Count > 0)
                    results.Add(new ExamReportResult { Code = pageBreak.Code, Kind = ReportParameterKind.PageBreak });
                if (section is not null)
                    results.Add(new ExamReportResult { Code = section.Code, Name = section.Name, Kind = ReportParameterKind.Section });
                (section, pageBreak) = (null, null);
                results.Add(parameter.ToResult(value, species));
            }
        }
        return results;
    }

    /// <summary>The automatic interpretation: the first rule whose conditions all hold, otherwise the band labels.</summary>
    public string? Interpret(IReadOnlyList<ExamReportResult> results)
    {
        var numbers = results.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => ReportParameter.ParseNumber(g.First().Value), StringComparer.OrdinalIgnoreCase);
        return Rules.FirstOrDefault(r => r.Matches(numbers))?.Label ?? ExamReport.InterpretationFrom(results);
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        var name = string.IsNullOrWhiteSpace(Title) ? $"Exam {ExamId}" : Title;
        if (ExamId <= 0) errors.Add(Texts.T("Template '{0}': ExamId is required.", name));
        if (string.IsNullOrWhiteSpace(Title)) errors.Add(Texts.T("Template for exam {0}: Title is required.", ExamId));
        if (!Parameters.Any(p => p.TakesValue)) errors.Add(Texts.T("Template '{0}': add at least one parameter.", name));
        foreach (var code in Parameters.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add(Texts.T("Template '{0}': duplicate parameter code '{1}'.", name, code));
        foreach (var p in Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Code) || (string.IsNullOrWhiteSpace(p.Name) && !p.IsPageBreak))
                errors.Add(Texts.T("Template '{0}': every parameter needs a Code and a Name.", name));
            foreach (var band in p.Bands)
            {
                if (band.Min is { } min && band.Max is { } max && min > max)
                    errors.Add(Texts.T("Template '{0}', parameter '{1}': a band's Min is greater than its Max.", name, p.Code));
                if (band.Min is null && band.Max is null)
                    errors.Add(Texts.T("Template '{0}', parameter '{1}': a band needs a Min, a Max or both.", name, p.Code));
            }
            if (p.Ranges.Any(r => r.Min is { } min && r.Max is { } max && min > max))
                errors.Add(Texts.T("Template '{0}', parameter '{1}': a normal range's Min is greater than its Max.", name, p.Code));
            if (p.Reference == ReferenceDisplay.Text && string.IsNullOrWhiteSpace(p.ReferenceText))
                errors.Add(Texts.T("Template '{0}', parameter '{1}': write the reference text, or show the range instead.", name, p.Code));
            if (p.Kind == ReportParameterKind.Text && p.Bands.Count > 0)
                errors.Add(Texts.T("Template '{0}', parameter '{1}': text parameters have no bands.", name, p.Code));
            if (p.AbnormalOptions.Any(a => !p.Options.Contains(a, StringComparer.OrdinalIgnoreCase)))
                errors.Add(Texts.T("Template '{0}', parameter '{1}': abnormal results must be among its options.", name, p.Code));
        }

        bool IsNumber(string? code) => FindParameter(code ?? string.Empty) is { Kind: ReportParameterKind.Number };
        foreach (var (rule, index) in Rules.Select((r, i) => (r, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(rule.Label))
                errors.Add(Texts.T("Template '{0}', rule {1}: write what it means.", name, index));
            if (rule.Conditions.Count == 0)
                errors.Add(Texts.T("Template '{0}', rule {1}: add at least one condition.", name, index));
            if (rule.Conditions.Any(c => !IsNumber(c.Code) || (!string.IsNullOrWhiteSpace(c.PercentOf) && !IsNumber(c.PercentOf))))
                errors.Add(Texts.T("Template '{0}', rule {1}: conditions must use number parameters of this template.", name, index));
        }

        if (Sections.CriteriaTable && (Criteria.Columns.Count == 0 || Criteria.Rows.Count == 0))
            errors.Add(Texts.T("Template '{0}': the criteria table needs at least one column and one row.", name));
        if (Criteria.Columns.Count > CriteriaTable.MaxColumns || Criteria.Rows.Count > CriteriaTable.MaxRows)
            errors.Add(Texts.T("Template '{0}': the criteria table has at most {1} columns and {2} rows.", name, CriteriaTable.MaxColumns, CriteriaTable.MaxRows));
        if (Criteria.Rows.Any(r => r.Cells.Count != Criteria.Columns.Count))
            errors.Add(Texts.T("Template '{0}': every row of the criteria table needs one cell per column.", name));
        return errors;
    }
}

/// <summary>Blocks a report prints. The exam title and the results table always print.</summary>
public class ExamReportSections
{
    /// <summary>Patient, species, age, sex, owner, referring vet and reception date.</summary>
    public bool PatientData { get; set; } = true;
    /// <summary>Brand and machine of the catalog exam, e.g. "Vcheck - BIONOTE".</summary>
    public bool Method { get; set; } = true;
    public bool Interpretation { get; set; } = true;
    /// <summary>Name of the staff member who issued the report.</summary>
    public bool ReportedBy { get; set; }
    /// <summary>↑ / ↓ next to values above or below the normal range; without them such values are only printed in red.</summary>
    public bool DirectionArrows { get; set; } = true;
    /// <summary>A column with the values of the pet's previous report of the same exam, headed by its date.</summary>
    public bool PreviousResult { get; set; }
    /// <summary>The form asks for the lot of the test kit or reagent (e.g. blood typing); hidden for other exams.</summary>
    public bool LotNumber { get; set; }
    /// <summary>The template's criteria table (e.g. the manufacturer's cortisol table) under the results.</summary>
    public bool CriteriaTable { get; set; }
}

/// <summary>
/// A table copied from the exam's instructions, e.g. 4 h / 8 h cortisol → interpretation. It's printed as written, for the
/// reader; the automatic interpretation comes from <see cref="InterpretationRule"/>s.
/// </summary>
public class CriteriaTable
{
    public const int MaxColumns = 6;
    public const int MaxRows = 20;

    /// <summary>Optional heading, e.g. "Prueba de supresión con dexametasona a dosis baja".</summary>
    public string? Title { get; set; }
    public List<string> Columns { get; set; } = [];
    public List<CriteriaRow> Rows { get; set; } = [];
}

public class CriteriaRow
{
    /// <summary>One text per column; a line break inside a cell prints as a new line.</summary>
    public List<string> Cells { get; set; } = [];
}

/// <summary>One measured value of an exam, e.g. cPL in ng/mL.</summary>
public class ReportParameter
{
    /// <summary>Stable key of the stored values, made from the first name; renaming the parameter keeps it.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ReportParameterKind Kind { get; set; } = ReportParameterKind.Number;

    /// <summary>A section title: only its name is used, printed above the parameters that follow it.</summary>
    [JsonIgnore]
    public bool IsSection => Kind == ReportParameterKind.Section;

    /// <summary>A page break: prints nothing, and the lines after it start a new page.</summary>
    [JsonIgnore]
    public bool IsPageBreak => Kind == ReportParameterKind.PageBreak;

    /// <summary>A number or text that holds a result (not a section title or page break).</summary>
    [JsonIgnore]
    public bool TakesValue => Kind.TakesValue();

    public string? Unit { get; set; }
    /// <summary>What the reference column shows: the normal range (default), a written text, or nothing.</summary>
    public ReferenceDisplay Reference { get; set; } = ReferenceDisplay.Range;
    /// <summary>Printed in the reference column when <see cref="Reference"/> is Text.</summary>
    public string? ReferenceText { get; set; }
    /// <summary>Normal range, per species or for all; values outside it are marked low or high.</summary>
    public List<ReferenceRange> Ranges { get; set; } = [];
    /// <summary>Draw the value on a bar against the normal range (needs a range with both limits).</summary>
    public bool ShowBar { get; set; }
    /// <summary>Text parameters: values offered in a list (e.g. blood types); empty = free text.</summary>
    public List<string> Options { get; set; } = [];
    /// <summary>Text parameters: options that are not a normal result (e.g. "Positivo"); they print in red.</summary>
    public List<string> AbnormalOptions { get; set; } = [];
    /// <summary>Optional meanings by range (e.g. "Zona gris"); the first band that contains the value wins.</summary>
    public List<InterpretationBand> Bands { get; set; } = [];

    /// <summary>The normal range for the species: its own one wins over one for all species; empty rows are ignored.</summary>
    public ReferenceRange? RangeFor(Species species)
    {
        var ranges = Ranges.Where(r => r.Min is not null || r.Max is not null).ToList();
        return ranges.FirstOrDefault(r => r.Species == species) ?? ranges.FirstOrDefault(r => r.Species is null);
    }

    /// <summary>
    /// How a value reads for the species: below or above the normal range, the band it falls in, and whether it is
    /// flagged (outside the range, in an abnormal band, or outside every band when there is no range).
    /// A text value is flagged when it is one of the <see cref="AbnormalOptions"/>; unparsable numbers never are.
    /// </summary>
    public BandResult Evaluate(string? value, Species species)
    {
        if (Kind == ReportParameterKind.Text)
            return new BandResult(null, value is not null && AbnormalOptions.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase));
        if (ParseNumber(value) is not { } number)
            return new BandResult(null, false);

        var range = RangeFor(species);
        var direction = range switch
        {
            { Min: { } min } when number < min => ResultDirection.Low,
            { Max: { } max } when number > max => ResultDirection.High,
            _ => ResultDirection.None
        };
        var bands = BandsFor(species);
        var band = bands.FirstOrDefault(b => (b.Min is null || number >= b.Min) && (b.Max is null || number <= b.Max));
        var outsideEveryBand = range is null && bands.Count > 0 && band is null;
        return new BandResult(band, direction != ResultDirection.None || band?.Abnormal == true || outsideEveryBand, direction);
    }

    public List<InterpretationBand> BandsFor(Species species)
        => Bands.Where(b => b.Species is null || b.Species == species).ToList();

    /// <summary>The printed line for a value: name, unit, reference, band, flag and the bar's range.</summary>
    public ExamReportResult ToResult(string value, Species species)
    {
        var evaluation = Evaluate(value, species);
        var barRange = ShowBar && RangeFor(species) is { Min: not null, Max: not null } range ? range : null;
        return new ExamReportResult
        {
            Code = Code,
            Name = Name,
            Kind = Kind,
            Unit = Unit,
            Value = value,
            Reference = ReferenceFor(species),
            Band = string.IsNullOrWhiteSpace(evaluation.Band?.Label) ? null : evaluation.Band.Label,
            Abnormal = evaluation.Abnormal,
            Direction = evaluation.Direction,
            BarMin = barRange?.Min,
            BarMax = barRange?.Max
        };
    }

    /// <summary>
    /// The reference column for the species: the written text; or the normal range ("3.5 - 5.8"), and without one the
    /// labelled bands ("≤ 200 Normal / 200 - 400 Zona gris / ...").
    /// </summary>
    public string ReferenceFor(Species species) => Reference switch
    {
        ReferenceDisplay.Text => ReferenceText ?? string.Empty,
        ReferenceDisplay.None => string.Empty,
        _ => RangeFor(species)?.RangeText()
             ?? string.Join(" / ", BandsFor(species).Where(b => !string.IsNullOrWhiteSpace(b.Label)).Select(b => b.RangeText()))
    };

    /// <summary>
    /// Reads "86.6", "86,6" or an analyser's bound such as "&lt; 1.0" / "&gt;99" (evaluated as the bound).
    /// </summary>
    public static decimal? ParseNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim().TrimStart('<', '>', '≤', '≥', '=').Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    }
}

/// <summary>A range of a numeric parameter with its meaning, e.g. 200–400 ng/mL "Zona gris". Bounds are inclusive.</summary>
public class InterpretationBand
{
    /// <summary>Only for this species; null = any.</summary>
    public Species? Species { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    /// <summary>Printed as the interpretation, e.g. "Consistente con pancreatitis". May be empty for a plain normal range.</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>A value in this band is flagged on the report.</summary>
    public bool Abnormal { get; set; }

    public string RangeText()
    {
        var range = (Min, Max) switch
        {
            ({ } min, { } max) => $"{min.ToString(CultureInfo.InvariantCulture)} - {max.ToString(CultureInfo.InvariantCulture)}",
            ({ } min, null) => $"≥ {min.ToString(CultureInfo.InvariantCulture)}",
            (null, { } max) => $"≤ {max.ToString(CultureInfo.InvariantCulture)}",
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(Label) ? range : $"{range} {Label}";
    }
}

/// <summary>Normal range of a parameter; both limits included, either may be open.</summary>
public class ReferenceRange
{
    /// <summary>Only for this species; null = any.</summary>
    public Species? Species { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }

    public string RangeText() => (Min, Max) switch
    {
        ({ } min, { } max) => $"{min.ToString(CultureInfo.InvariantCulture)} - {max.ToString(CultureInfo.InvariantCulture)}",
        ({ } min, null) => $"≥ {min.ToString(CultureInfo.InvariantCulture)}",
        (null, { } max) => $"≤ {max.ToString(CultureInfo.InvariantCulture)}",
        _ => string.Empty
    };
}

/// <summary>
/// An interpretation that holds when all its conditions do, e.g. "8 h ≥ 1.4 and 4 h &lt; 50 % of basal" →
/// "Compatible con hiperadrenocorticismo hipofisario".
/// </summary>
public class InterpretationRule
{
    public List<RuleCondition> Conditions { get; set; } = [];
    /// <summary>Printed as the interpretation.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>All conditions hold; a missing or non-numeric value makes its condition false.</summary>
    public bool Matches(IReadOnlyDictionary<string, decimal?> values)
        => Conditions.Count > 0 && Conditions.All(c => c.Holds(values));
}

/// <summary>A parameter compared with a number, or with a percentage of another parameter.</summary>
public class RuleCondition
{
    public string Code { get; set; } = string.Empty;
    public RuleOperator Operator { get; set; } = RuleOperator.LessThan;
    public decimal Value { get; set; }
    /// <summary>When set, the threshold is <see cref="Value"/> % of this parameter's value (e.g. 50 % of basal).</summary>
    public string? PercentOf { get; set; }

    public bool Holds(IReadOnlyDictionary<string, decimal?> values)
    {
        if (values.GetValueOrDefault(Code) is not { } value)
            return false;
        var threshold = Value;
        if (!string.IsNullOrWhiteSpace(PercentOf))
        {
            if (values.GetValueOrDefault(PercentOf) is not { } basis)
                return false;
            threshold = basis * Value / 100;
        }
        return Operator switch
        {
            RuleOperator.LessThan => value < threshold,
            RuleOperator.LessOrEqual => value <= threshold,
            RuleOperator.GreaterThan => value > threshold,
            _ => value >= threshold
        };
    }
}

public sealed record BandResult(InterpretationBand? Band, bool Abnormal, ResultDirection Direction = ResultDirection.None);
