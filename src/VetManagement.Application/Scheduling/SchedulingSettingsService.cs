using VetManagement.Application.Configuration;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

/// <summary>The placeholder settings document a clinic starts from (scheduling.defaults.json).</summary>
public sealed record SchedulingDefaults(string Json);

/// <summary>The clinic's agenda configuration (key "scheduling").</summary>
public class SchedulingSettingsService(IUnitOfWork unitOfWork, AuditService audit, SchedulingDefaults defaults)
    : ClinicSettingsService<SchedulingSettings>(unitOfWork, audit)
{
    protected override string SettingsKey => "scheduling";
    protected override SchedulingSettings CreateDefaults() => SchedulingJson.Deserialize(defaults.Json);
    protected override List<string> Validate(SchedulingSettings settings) => settings.Validate();
}
