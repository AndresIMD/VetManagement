using FluentAssertions;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;

namespace VetManagement.Tests.Exams;

/// <summary>What the reference column prints for a parameter.</summary>
public class ReferenceColumnTests
{
    private static ReportParameter Cpl(bool onePerLine) => new()
    {
        Code = "cpl", Name = "cPL", Unit = "ng/mL", BandsOnePerLine = onePerLine,
        Bands =
        [
            new InterpretationBand { Max = 200, Label = "Normal" },
            new InterpretationBand { Min = 200, Max = 399.9m, Label = "Zona gris", Abnormal = true },
            new InterpretationBand { Min = 400, Label = "Consistente con pancreatitis", Abnormal = true }
        ]
    };

    [Fact]
    public void Bands_PrintInARow_SplitBySlashes()
        => Cpl(onePerLine: false).ReferenceFor(Species.Dog)
            .Should().Be("≤ 200 Normal / 200 - 399.9 Zona gris / ≥ 400 Consistente con pancreatitis");

    [Fact]
    public void Bands_CanPrintOneUnderTheOther()
    {
        var parameter = Cpl(onePerLine: true);

        parameter.ReferenceFor(Species.Dog).Should().Be("≤ 200 Normal\n200 - 399.9 Zona gris\n≥ 400 Consistente con pancreatitis");
        parameter.ToResult("250", Species.Dog).Reference.Should().Contain("\n", "the report keeps the lines");
    }

    [Fact]
    public void ANormalRange_StillWins_OverTheBands()
    {
        var parameter = Cpl(onePerLine: true);
        parameter.Ranges = [new ReferenceRange { Min = 0, Max = 200 }];

        parameter.ReferenceFor(Species.Dog).Should().Be("0 - 200");
    }
}
