using FluentAssertions;
using VetManagement.Application.Scheduling;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Tests.Scheduling;

public class SchedulingSettingsTests
{
    private static string DefaultsPath => Path.Combine(AppContext.BaseDirectory, "Scheduling", "scheduling.defaults.json");

    private static SchedulingSettings Defaults() => SchedulingJson.Deserialize(File.ReadAllText(DefaultsPath));

    [Fact]
    public void DefaultsFile_LoadsAndIsValid_WithAgendaDisabled()
    {
        var settings = Defaults();

        settings.Validate().Should().BeEmpty();
        settings.Enabled.Should().BeFalse("a clinic activates its agenda after entering real values");
        settings.Services.Should().OnlyContain(s => !s.Enabled);
        settings.Services.Should().Contain(s => s.Code == "specialist" && s.DepositPercent == 50);
    }

    [Fact]
    public void DefaultsFile_SpecialistUsesProgressiveRelease_WithOverflowBlock()
    {
        var specialist = Defaults().Resources.Single(r => r.Code == "specialist-1");

        specialist.ProgressiveRelease.Should().BeTrue();
        var blocks = specialist.Weekly.Single().Blocks;
        blocks[0].Should().BeEquivalentTo(new { Start = new TimeOnly(10, 0), End = new TimeOnly(13, 0), IsOverflow = false });
        blocks[1].Should().BeEquivalentTo(new { Start = new TimeOnly(13, 30), End = new TimeOnly(17, 0), IsOverflow = true });
    }

    [Fact]
    public void Json_RoundTrips_WithReadableEnums()
    {
        var json = SchedulingJson.Serialize(Defaults());

        json.Should().Contain("\"ManualApproval\"").And.Contain("\"Vet\"").And.Contain("\"Wednesday\"");
        SchedulingJson.Deserialize(json).Should().BeEquivalentTo(Defaults());
    }

    [Fact]
    public void Validate_ReportsEveryInconsistency()
    {
        var settings = Defaults();
        settings.TimeZone = "Mars/Olympus";
        settings.Services[0].DepositPercent = 150;
        settings.Services[1].ResourceCodes = ["ghost"];
        settings.Services.Add(new ServiceDefinition { Code = "CONSULTATION", Name = "dup", ResourceCodes = ["vet-general"] });
        settings.Resources[0].Weekly[0].Blocks.Add(new TimeBlock { Start = new TimeOnly(12, 0), End = new TimeOnly(15, 0) });
        settings.Exceptions.Add(new ScheduleException
        {
            From = new DateOnly(2026, 3, 2), To = new DateOnly(2026, 3, 1), Kind = ScheduleExceptionKind.Absence
        });

        var errors = settings.Validate();

        errors.Should().Contain(e => e.Contains("Mars/Olympus"));
        errors.Should().Contain(e => e.Contains("DepositPercent"));
        errors.Should().Contain(e => e.Contains("unknown resource 'ghost'"));
        errors.Should().Contain(e => e.Contains("Duplicate service code"));
        errors.Should().Contain(e => e.Contains("overlap"));
        errors.Should().Contain(e => e.Contains("To is before From"));
        errors.Should().Contain(e => e.Contains("Absence must name a resource"));
    }
}
