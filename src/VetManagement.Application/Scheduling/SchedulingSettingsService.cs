using VetManagement.Application.Common;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Configuration;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

/// <summary>The placeholder settings document a clinic starts from (scheduling.defaults.json).</summary>
public sealed record SchedulingDefaults(string Json);

public sealed record VersionedSettings(SchedulingSettings Settings, int Version);

public enum SettingsUpdateStatus
{
    Saved,
    Invalid,
    /// <summary>Someone saved a newer version since it was read.</summary>
    Conflict
}

public sealed record SettingsUpdateResult(SettingsUpdateStatus Status, int Version, IReadOnlyList<string> Errors);

/// <summary>
/// Reads and saves the clinic's agenda configuration. The first read creates it from the defaults;
/// every save is validated, versioned (optimistic concurrency) and audited.
/// </summary>
public class SchedulingSettingsService(IUnitOfWork unitOfWork, AuditService audit, SchedulingDefaults defaults)
{
    public const string Key = "scheduling";

    public async Task<VersionedSettings> GetAsync()
    {
        var row = await unitOfWork.ClinicSettings.GetByKeyAsync(Key);
        if (row is not null)
            return new VersionedSettings(SchedulingJson.Deserialize(row.Json), row.Version);

        var initial = SchedulingJson.Deserialize(defaults.Json);
        row = new ClinicSetting { Key = Key, Json = SchedulingJson.Serialize(initial), Version = 1, UpdatedAtUtc = DateTime.UtcNow, UpdatedBy = "defaults" };
        await unitOfWork.ClinicSettings.AddAsync(row);
        await unitOfWork.SaveChangesAsync();
        return new VersionedSettings(initial, row.Version);
    }

    public async Task<SettingsUpdateResult> UpdateAsync(SchedulingSettings settings, int expectedVersion, string userName)
    {
        var errors = settings.Validate();
        if (errors.Count > 0)
            return new SettingsUpdateResult(SettingsUpdateStatus.Invalid, expectedVersion, errors);

        var current = await GetAsync();
        var row = (await unitOfWork.ClinicSettings.GetByKeyAsync(Key))!;
        if (row.Version != expectedVersion)
            return new SettingsUpdateResult(SettingsUpdateStatus.Conflict, current.Version, []);

        row.Json = SchedulingJson.Serialize(settings);
        row.Version++;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = userName;
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyConflictException)
        {
            // Another save landed between our read and write (the Version concurrency token caught it).
            return new SettingsUpdateResult(SettingsUpdateStatus.Conflict, expectedVersion, []);
        }

        await audit.LogAsync("SchedulingSettings", row.Id, AuditActionType.Edit, row.Json, userName);
        return new SettingsUpdateResult(SettingsUpdateStatus.Saved, row.Version, []);
    }
}
