using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Configuration;

/// <summary>
/// A versioned JSON configuration document for the clinic (e.g. key "scheduling").
/// <see cref="Version"/> increases on every change and guards against concurrent edits.
/// </summary>
public class ClinicSetting : Entity<int>
{
    public ClinicSetting() : base(0) { }

    public string Key { get; set; } = string.Empty;
    public string Json { get; set; } = "{}";
    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
