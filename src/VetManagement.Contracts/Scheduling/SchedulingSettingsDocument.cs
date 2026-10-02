using System.Text.Json;

namespace VetManagement.Contracts.Scheduling;

/// <summary>
/// The clinic's agenda configuration as exchanged with the admin UI: the same JSON document as
/// scheduling.defaults.json, plus the version it was read at (send it back unchanged when saving).
/// </summary>
public sealed class SchedulingSettingsDocument
{
    public int Version { get; set; }
    public JsonElement Settings { get; set; }
}
