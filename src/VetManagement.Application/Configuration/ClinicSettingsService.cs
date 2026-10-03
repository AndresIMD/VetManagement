using System.Text.Json;
using System.Text.Json.Serialization;
using VetManagement.Application.Common;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Services;
using VetManagement.Domain.Configuration;
using VetManagement.Domain.Enums;

namespace VetManagement.Application.Configuration;

public sealed record VersionedSettings<T>(T Settings, int Version);

public enum SettingsUpdateStatus
{
    Saved,
    Invalid,
    /// <summary>Someone saved a newer version since it was read.</summary>
    Conflict
}

public sealed record SettingsUpdateResult(SettingsUpdateStatus Status, int Version, IReadOnlyList<string> Errors);

/// <summary>
/// JSON format of the clinic's settings documents, shared by defaults, the database and the admin API.
/// Enums are written as names ("Vet", "ManualApproval") so documents stay readable for admins.
/// </summary>
public static class SettingsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"{typeof(T).Name} document is empty.");

    public static string Serialize<T>(T settings) => JsonSerializer.Serialize(settings, Options);
}

/// <summary>
/// One versioned settings document per clinic (row in ClinicSettings). The first read creates it from the
/// defaults; every save is validated, versioned (optimistic concurrency) and audited.
/// </summary>
public abstract class ClinicSettingsService<T>(IUnitOfWork unitOfWork, AuditService audit) where T : class
{
    protected abstract string SettingsKey { get; }
    protected abstract T CreateDefaults();
    protected abstract List<string> Validate(T settings);

    public async Task<VersionedSettings<T>> GetAsync()
    {
        var row = await unitOfWork.ClinicSettings.GetByKeyAsync(SettingsKey);
        if (row is not null)
            return new VersionedSettings<T>(SettingsJson.Deserialize<T>(row.Json), row.Version);

        var initial = CreateDefaults();
        row = new ClinicSetting { Key = SettingsKey, Json = SettingsJson.Serialize(initial), Version = 1, UpdatedAtUtc = DateTime.UtcNow, UpdatedBy = "defaults" };
        await unitOfWork.ClinicSettings.AddAsync(row);
        await unitOfWork.SaveChangesAsync();
        return new VersionedSettings<T>(initial, row.Version);
    }

    public async Task<SettingsUpdateResult> UpdateAsync(T settings, int expectedVersion, string userName)
    {
        var errors = Validate(settings);
        if (errors.Count > 0)
            return new SettingsUpdateResult(SettingsUpdateStatus.Invalid, expectedVersion, errors);

        var current = await GetAsync();
        var row = (await unitOfWork.ClinicSettings.GetByKeyAsync(SettingsKey))!;
        if (row.Version != expectedVersion)
            return new SettingsUpdateResult(SettingsUpdateStatus.Conflict, current.Version, []);

        row.Json = SettingsJson.Serialize(settings);
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

        await audit.LogAsync(typeof(T).Name, row.Id, AuditActionType.Edit, row.Json, userName);
        return new SettingsUpdateResult(SettingsUpdateStatus.Saved, row.Version, []);
    }
}
