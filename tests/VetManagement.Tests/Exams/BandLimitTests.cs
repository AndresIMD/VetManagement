using FluentAssertions;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;

namespace VetManagement.Tests.Exams;

/// <summary>
/// A band's limit is included (≥ / ≤) or left out (&gt; / &lt;), so where two bands meet a value right on the limit falls
/// in one of them, and the reference says which: "≤ 5 Normal / ≥ 5 Alto" printed 5 as both.
/// </summary>
public class BandLimitTests
{
    private static ReportParameter Parameter(params InterpretationBand[] bands) => new() { Code = "x", Name = "X", Bands = [.. bands] };

    /// <summary>The upper band takes the 5: "&lt; 5 Normal / ≥ 5 Alto".</summary>
    private static ReportParameter FiveIsHigh() => Parameter(
        new InterpretationBand { Max = 5, MaxExclusive = true, Label = "Normal" },
        new InterpretationBand { Min = 5, Label = "Alto", Abnormal = true });

    /// <summary>The lower band keeps the 5: "≤ 5 Normal / &gt; 5 Alto".</summary>
    private static ReportParameter FiveIsNormal() => Parameter(
        new InterpretationBand { Max = 5, Label = "Normal" },
        new InterpretationBand { Min = 5, MinExclusive = true, Label = "Alto", Abnormal = true });

    [Fact]
    public void TheReference_ShowsWhichBandTakesTheSharedLimit()
    {
        FiveIsHigh().ReferenceFor(Species.Dog).Should().Be("< 5 Normal / ≥ 5 Alto");
        FiveIsNormal().ReferenceFor(Species.Dog).Should().Be("≤ 5 Normal / > 5 Alto");
    }

    [Fact]
    public void ABandBetweenTwoLimitsLeftOut_PrintsBothSigns()
    {
        var parameter = Parameter(
            new InterpretationBand { Max = 200, Label = "Normal" },
            new InterpretationBand { Min = 200, MinExclusive = true, Max = 400, MaxExclusive = true, Label = "Zona gris", Abnormal = true },
            new InterpretationBand { Min = 400, Label = "Consistente con pancreatitis", Abnormal = true });

        parameter.ReferenceFor(Species.Dog).Should().Be("≤ 200 Normal / > 200 - < 400 Zona gris / ≥ 400 Consistente con pancreatitis");
        parameter.Evaluate("399.95", Species.Dog).Band!.Label.Should().Be("Zona gris", "no gap is left between 399.9 and 400");
        parameter.Evaluate("400", Species.Dog).Band!.Label.Should().Be("Consistente con pancreatitis");
    }

    [Theory]
    [InlineData("5", "Alto", true)]
    [InlineData("5,0", "Alto", true)]
    [InlineData("4.99", "Normal", false)]
    [InlineData("<5", "Normal", false)] // surely below 5
    [InlineData(">5", "Alto", true)]
    [InlineData("≤5", null, false)] // 5 (Alto) or less (Normal): not settled, and not outside them all
    public void ALimitLeftOut_GoesToTheOtherBand(string value, string? band, bool abnormal)
    {
        var evaluation = FiveIsHigh().Evaluate(value, Species.Dog);

        (evaluation.Band?.Label).Should().Be(band);
        evaluation.Abnormal.Should().Be(abnormal);
    }

    [Theory]
    [InlineData("5", "Normal")]
    [InlineData("5.01", "Alto")]
    [InlineData(">5", "Alto")]
    [InlineData("≥5", null)] // 5 (Normal) or more (Alto)
    public void AnIncludedLimit_KeepsTheValueInItsBand(string value, string? band)
        => (FiveIsNormal().Evaluate(value, Species.Dog).Band?.Label).Should().Be(band);

    [Fact]
    public void TheInterpretation_ReadsTheBandThatTakesTheLimit()
    {
        var template = new ExamReportTemplate { ExamId = 1, Title = "X", Parameters = [FiveIsHigh()] };

        template.Interpret(template.ResultsFor(new Dictionary<string, string> { ["x"] = "5" }, Species.Dog)).Should().Be("Alto");
    }

    [Theory]
    [InlineData("5", true)]
    [InlineData("10", true)]
    [InlineData("≤5", true)]
    [InlineData(">=10", true)]
    [InlineData("7", false)]
    [InlineData("<6", false)] // may be 5.5, inside
    [InlineData(">9", false)]
    public void WithoutARange_AValueOnALimitLeftOut_IsOutsideTheBands(string value, bool flagged)
    {
        var parameter = Parameter(new InterpretationBand { Min = 5, MinExclusive = true, Max = 10, MaxExclusive = true, Label = "Normal" });

        parameter.Evaluate(value, Species.Dog).Abnormal.Should().Be(flagged);
    }

    [Fact]
    public void ASignOnAnEmptyLimit_IsIgnored()
    {
        // The editor keeps the sign when its number is cleared.
        var parameter = Parameter(new InterpretationBand { MinExclusive = true, Max = 5, Label = "Normal" });

        parameter.ReferenceFor(Species.Dog).Should().Be("≤ 5 Normal");
        parameter.Evaluate("0", Species.Dog).Band.Should().NotBeNull();
        Template(parameter).Validate().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ABandOnASingleNumber_MustIncludeIt(bool minExclusive, bool maxExclusive, bool valid)
    {
        var template = Template(Parameter(new InterpretationBand { Min = 5, MinExclusive = minExclusive, Max = 5, MaxExclusive = maxExclusive, Label = "5" }));

        template.Validate().Should().HaveCount(valid ? 0 : 1);
    }

    private static ExamReportTemplate Template(ReportParameter parameter) => new() { ExamId = 1, Title = "X", Parameters = [parameter] };
}
