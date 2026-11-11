using System.Reflection;

namespace VetManagement.Staff.UI.Services.App;

/// <summary>
/// The version of the staff app this browser runs: the day it was built and its commit, as an update package is named
/// (VetManagement-actualizacion-2026-11-10-cea3d43.zip). Shown under the user's name in the user menu; a PC showing an
/// older one than the server's package hasn't reloaded since the update.
/// </summary>
public static class AppVersion
{
    /// <summary>"2026-11-10 · cea3d43", or empty when the build knows neither (then nothing is shown).</summary>
    public static string Text { get; } = Describe(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(AppVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildDate")?.Value);

    /// <param name="informationalVersion">"1.0.0+&lt;commit&gt;": the SDK adds the commit when it builds from a git checkout.</param>
    /// <param name="buildDate">The day of the build (VetManagement.Staff.UI.csproj).</param>
    public static string Describe(string? informationalVersion, string? buildDate)
    {
        var plus = informationalVersion?.IndexOf('+') ?? -1;
        var commit = plus >= 0 ? informationalVersion![(plus + 1)..] : string.Empty;
        if (commit.Length > 7)
            commit = commit[..7];
        return string.Join(" · ", new[] { buildDate, commit }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
