using System.Text.Json;
using System.Text.Json.Serialization;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Scheduling;

/// <summary>
/// JSON format of <see cref="SchedulingSettings"/>, shared by the defaults file, the database and the admin API.
/// Enums are written as names ("Vet", "ManualApproval") so the document stays readable for admins.
/// </summary>
public static class SchedulingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SchedulingSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<SchedulingSettings>(json, Options)
        ?? throw new JsonException("Scheduling settings document is empty.");

    public static string Serialize(SchedulingSettings settings) => JsonSerializer.Serialize(settings, Options);
}
