using System.Text.Json;
using VetManagement.Application.Configuration;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

/// <summary>JSON format of <see cref="SchedulingSettings"/> (see <see cref="SettingsJson"/>).</summary>
public static class SchedulingJson
{
    public static JsonSerializerOptions Options => SettingsJson.Options;

    public static SchedulingSettings Deserialize(string json) => SettingsJson.Deserialize<SchedulingSettings>(json);

    public static string Serialize(SchedulingSettings settings) => SettingsJson.Serialize(settings);
}
