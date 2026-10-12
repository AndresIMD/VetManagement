using System.Globalization;
using System.Text.RegularExpressions;
using VetManagement.Domain.Enums;

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

    /// <summary>Lines next to the logo, e.g. "LABORATORIO" and the address.</summary>
    public List<string> HeaderLines { get; set; } = [];

    /// <summary>Clinic details printed at the bottom (name, tax id, address, phone).</summary>
    public List<string> FooterLines { get; set; } = [];

    public string Disclaimer { get; set; } = "El resultado de este examen no constituye diagnóstico, por favor consulte con su médico tratante.";

    /// <summary>Font of the whole report; one of <see cref="Fonts"/>, which every Windows PC has.</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    public static readonly string[] Fonts =
        ["Segoe UI", "Arial", "Calibri", "Verdana", "Tahoma", "Trebuchet MS", "Century Gothic", "Georgia", "Cambria", "Times New Roman"];

    /// <summary>1 = background image as uploaded; lower values fade it into a watermark.</summary>
    public double BackgroundOpacity { get; set; } = 1;

    public List<ExamReportTemplate> Templates { get; set; } = [];

    public ExamReportTemplate? FindTemplate(int examId) => Templates.FirstOrDefault(t => t.ExamId == examId);

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (MarginsMm.Top is < 0 or > 60 || MarginsMm.Right is < 0 or > 60 || MarginsMm.Bottom is < 0 or > 60 || MarginsMm.Left is < 0 or > 60)
            errors.Add("Margins must be between 0 and 60 mm.");
        if (BackgroundOpacity is < 0.05 or > 1)
            errors.Add("BackgroundOpacity must be between 0.05 and 1.");
        if (!IsColor(AccentColor) || !IsColor(HeaderColor))
            errors.Add("Colours must be written as #RRGGBB.");
        if (!Fonts.Contains(FontFamily))
            errors.Add($"FontFamily must be one of: {string.Join(", ", Fonts)}.");
        if (HeaderLines.Count > 6 || FooterLines.Count > 8)
            errors.Add("Use at most 6 header lines and 8 footer lines.");

        foreach (var examId in Templates.GroupBy(t => t.ExamId).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add($"Exam {examId} has more than one report template.");
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

    public ReportParameter? FindParameter(string code)
        => Parameters.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));

    public List<string> Validate()
    {
        var errors = new List<string>();
        var name = string.IsNullOrWhiteSpace(Title) ? $"Exam {ExamId}" : Title;
        if (ExamId <= 0) errors.Add($"Template '{name}': ExamId is required.");
        if (string.IsNullOrWhiteSpace(Title)) errors.Add($"Template for exam {ExamId}: Title is required.");
        if (Parameters.Count == 0) errors.Add($"Template '{name}': add at least one parameter.");
        foreach (var code in Parameters.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key))
            errors.Add($"Template '{name}': duplicate parameter code '{code}'.");
        foreach (var p in Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Code) || string.IsNullOrWhiteSpace(p.Name))
                errors.Add($"Template '{name}': every parameter needs a Code and a Name.");
            foreach (var band in p.Bands)
            {
                if (band.Min is { } min && band.Max is { } max && min > max)
                    errors.Add($"Template '{name}', parameter '{p.Code}': a band's Min is greater than its Max.");
                if (band.Min is null && band.Max is null)
                    errors.Add($"Template '{name}', parameter '{p.Code}': a band needs a Min, a Max or both.");
            }
            if (p.Ranges.Any(r => r.Min is { } min && r.Max is { } max && min > max))
                errors.Add($"Template '{name}', parameter '{p.Code}': a normal range's Min is greater than its Max.");
            if (p.Reference == ReferenceDisplay.Text && string.IsNullOrWhiteSpace(p.ReferenceText))
                errors.Add($"Template '{name}', parameter '{p.Code}': write the reference text, or show the range instead.");
            if (p.Kind == ReportParameterKind.Text && p.Bands.Count > 0)
                errors.Add($"Template '{name}', parameter '{p.Code}': text parameters have no bands.");
        }
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
}

/// <summary>One measured value of an exam, e.g. cPL in ng/mL.</summary>
public class ReportParameter
{
    /// <summary>Stable key of the stored values, made from the first name; renaming the parameter keeps it.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ReportParameterKind Kind { get; set; } = ReportParameterKind.Number;
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
    /// Text and unparsable values are never flagged.
    /// </summary>
    public BandResult Evaluate(string? value, Species species)
    {
        if (Kind != ReportParameterKind.Number || ParseNumber(value) is not { } number)
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

public sealed record BandResult(InterpretationBand? Band, bool Abnormal, ResultDirection Direction = ResultDirection.None);
