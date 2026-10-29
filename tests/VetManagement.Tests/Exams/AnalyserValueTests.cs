using FluentAssertions;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;

namespace VetManagement.Tests.Exams;

/// <summary>
/// Values an analyser gives without an exact number: a bound ("&lt; 1.0", "&gt;1000") counts only where it settles the
/// question, and a value it couldn't measure ("No medible") is never flagged nor used by the rules.
/// </summary>
public class AnalyserValueTests
{
    [Theory]
    [InlineData("< 1.0", 1.0, ValueBound.Below)]
    [InlineData(">1000", 1000, ValueBound.Above)]
    [InlineData("≤ 5", 5, ValueBound.AtMost)]
    [InlineData("<=5", 5, ValueBound.AtMost)]
    [InlineData(">= 5", 5, ValueBound.AtLeast)]
    [InlineData("86,6", 86.6, ValueBound.Exact)]
    [InlineData("-2", -2, ValueBound.Exact)]
    public void Parse_KeepsTheSideOfABound(string text, double value, ValueBound bound)
        => ReportNumber.Parse(text).Should().Be(new ReportNumber((decimal)value, bound));

    [Theory]
    [InlineData("No medible")]
    [InlineData("---")]
    [InlineData("...")]
    [InlineData("/")]
    [InlineData("<>5")]
    [InlineData("")]
    public void Parse_IsNull_ForWhatIsNotANumber(string text) => ReportNumber.Parse(text).Should().BeNull();

    private static ReportParameter Cortisol() => new()
    {
        Code = "cortisol", Name = "Cortisol", Unit = "µg/dL", Ranges = [new ReferenceRange { Min = 1.0m, Max = 5.0m }]
    };

    [Theory]
    [InlineData("<1.0", ResultDirection.Low)] // below 1.0, so below the range that starts there
    [InlineData("<1.5", ResultDirection.None)] // may still be within it: not flagged
    [InlineData(">5.0", ResultDirection.High)]
    [InlineData(">4.0", ResultDirection.None)]
    [InlineData("0.9", ResultDirection.Low)]
    [InlineData("5.0", ResultDirection.None)]
    public void Range_FlagsABound_OnlyWhenItIsSurelyOutside(string value, ResultDirection expected)
    {
        var evaluation = Cortisol().Evaluate(value, Species.Dog);

        evaluation.Direction.Should().Be(expected);
        evaluation.Abnormal.Should().Be(expected != ResultDirection.None);
    }

    [Theory]
    [InlineData("No medible")]
    [InlineData("---")]
    public void NotMeasured_IsNeverFlagged(string value)
        => Cortisol().Evaluate(value, Species.Dog).Should().Be(new BandResult(null, false));

    private static ReportParameter Cpl() => new()
    {
        Code = "cpl", Name = "cPL", Unit = "ng/mL",
        Bands =
        [
            new InterpretationBand { Max = 200, Label = "Normal" },
            new InterpretationBand { Min = 200, Max = 399.9m, Label = "Zona gris", Abnormal = true },
            new InterpretationBand { Min = 400, Label = "Consistente con pancreatitis", Abnormal = true }
        ]
    };

    [Theory]
    [InlineData(">1000", "Consistente con pancreatitis", true)]
    [InlineData("<50", "Normal", false)]
    [InlineData("<300", null, false)] // normal or grey zone: no band, and not outside them all
    public void Bands_TakeABound_OnlyWhenItFallsWhollyInOne(string value, string? band, bool abnormal)
    {
        var evaluation = Cpl().Evaluate(value, Species.Dog);

        (evaluation.Band?.Label).Should().Be(band);
        evaluation.Abnormal.Should().Be(abnormal);
    }

    [Fact]
    public void Bands_WithoutARange_FlagABoundThatIsSurelyOutsideThemAll()
    {
        var parameter = new ReportParameter { Code = "x", Name = "X", Bands = [new InterpretationBand { Min = 5, Max = 10, Label = "Normal" }] };

        parameter.Evaluate("<5", Species.Dog).Abnormal.Should().BeTrue();
        parameter.Evaluate("<6", Species.Dog).Abnormal.Should().BeFalse();
    }

    private static ExamReportTemplate Suppression(params InterpretationRule[] rules) => new()
    {
        ExamId = 1,
        Title = "SUPRESIÓN CON DEXAMETASONA",
        Parameters =
        [
            new ReportParameter { Code = "basal", Name = "Cortisol basal" },
            new ReportParameter { Code = "4h", Name = "Cortisol 4 h" },
            new ReportParameter { Code = "8h", Name = "Cortisol 8 h" }
        ],
        Rules = [.. rules]
    };

    private static InterpretationRule Rule(string label, params RuleCondition[] conditions) => new() { Label = label, Conditions = [.. conditions] };

    private static string? Interpret(ExamReportTemplate template, params (string Code, string Value)[] values)
        => template.Interpret(template.ResultsFor(values.ToDictionary(v => v.Code, v => v.Value), Species.Dog));

    [Fact]
    public void Rules_DontTakeABoundForItsNumber()
    {
        // "< 1.0" used to count as 1.0, so "8 h ≥ 1.0" held and the report printed the wrong interpretation.
        var template = Suppression(
            Rule("Sin supresión", new RuleCondition { Code = "8h", Operator = RuleOperator.GreaterOrEqual, Value = 1.0m }),
            Rule("Supresión normal", new RuleCondition { Code = "8h", Operator = RuleOperator.LessThan, Value = 1.0m }));

        Interpret(template, ("8h", "<1.0")).Should().Be("Supresión normal");
        Interpret(template, ("8h", ">1.0")).Should().Be("Sin supresión");
        Interpret(template, ("8h", "1.0")).Should().Be("Sin supresión");
    }

    [Theory]
    [InlineData("8", "Compatible con origen hipofisario")] // 8 < half of "more than 20"
    [InlineData("12", null)] // half of "more than 20" may be more or less than 12
    public void Rules_ComparingWithAPercentage_KeepTheBasisBound(string fourHours, string? expected)
    {
        var template = Suppression(Rule("Compatible con origen hipofisario",
            new RuleCondition { Code = "4h", Operator = RuleOperator.LessThan, Value = 50, PercentOf = "basal" }));

        Interpret(template, ("basal", ">20"), ("4h", fourHours)).Should().Be(expected);
    }

    [Fact]
    public void Rules_IgnoreAValueNotMeasured()
    {
        var template = Suppression(Rule("Sin supresión", new RuleCondition { Code = "8h", Operator = RuleOperator.GreaterOrEqual, Value = 1.4m }));

        Interpret(template, ("8h", "No medible")).Should().BeNull();
    }

    [Theory]
    [InlineData("1.2", "2.0", "Compatible con origen hipofisario")] // 4 h < 1.4
    [InlineData("2.0", "2.0", "Compatible con origen hipofisario")] // 4 h < 50 % of basal
    [InlineData("<1.0", "2.0", "Compatible con origen hipofisario")] // a bound that settles 4 h < 1.4
    [InlineData("3.0", "2.0", null)] // neither alternative
    [InlineData("1.2", "1.2", null)] // 8 h not above 1.4
    public void Rules_AConditionHoldsByAnyOfItsAlternatives(string fourHours, string eightHours, string? expected)
    {
        // The clinic's reading: 4 h (< 1.4 or < 50 % of basal) and 8 h (> 1.4 and < 50 % of basal).
        var template = Suppression(Rule("Compatible con origen hipofisario",
            new RuleCondition
            {
                Code = "4h", Operator = RuleOperator.LessThan, Value = 1.4m,
                Or = [new RuleCondition { Code = "4h", Operator = RuleOperator.LessThan, Value = 50, PercentOf = "basal" }]
            },
            new RuleCondition { Code = "8h", Operator = RuleOperator.GreaterThan, Value = 1.4m },
            new RuleCondition { Code = "8h", Operator = RuleOperator.LessThan, Value = 50, PercentOf = "basal" }));

        template.Validate().Should().BeEmpty();
        Interpret(template, ("basal", "5.0"), ("4h", fourHours), ("8h", eightHours)).Should().Be(expected);
    }

    [Fact]
    public void Rules_AnAlternativeUsingAnUnknownParameter_IsRejected()
    {
        var template = Suppression(Rule("Sin supresión",
            new RuleCondition { Code = "8h", Operator = RuleOperator.GreaterOrEqual, Value = 1.4m, Or = [new RuleCondition { Code = "12h", Value = 1 }] }));

        template.Validate().Should().ContainSingle().Which.Should().Contain("rule 1");
    }

    [Fact]
    public void Times_ANegativeFactor_TurnsTheBoundAround()
        => new ReportNumber(4, ValueBound.Below).Times(-0.5m).Should().Be(new ReportNumber(-2, ValueBound.Above));

    [Theory]
    [InlineData(7, true)] // low-dose dexamethasone suppression tables have 7 columns
    [InlineData(CriteriaTable.MaxColumns, true)]
    [InlineData(CriteriaTable.MaxColumns + 1, false)]
    public void CriteriaTable_TakesUpToItsMaxColumns(int columns, bool valid)
    {
        var template = Suppression();
        template.Sections.CriteriaTable = true;
        template.Criteria = new CriteriaTable
        {
            Columns = [.. Enumerable.Range(1, columns).Select(i => $"Column {i}")],
            Rows = [new CriteriaRow { Cells = [.. Enumerable.Repeat("-", columns)] }]
        };

        template.Validate().Should().HaveCount(valid ? 0 : 1);
    }
}
