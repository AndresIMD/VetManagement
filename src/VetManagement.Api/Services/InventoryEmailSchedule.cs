using System.Text.Json;

namespace VetManagement.Api.Services;

public class InventoryEmailSchedule
{
    public bool Enabled { get; set; }
    public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday, 6=Saturday
    public int Hour { get; set; }
    public int Minute { get; set; }
    public string To { get; set; } = string.Empty; // Comma-separated emails
    public string? Cc { get; set; } // Comma-separated emails
    public string Subject { get; set; } = "Inventory Alerts";
    public bool IncludeLowStock { get; set; } = true;
    public bool IncludeOutOfStock { get; set; } = true;
    public DateTime? LastSentUtc { get; set; }

    private static string GetFilePath(IHostEnvironment env)
        => Path.Combine(env.ContentRootPath, "App_Data", "inventory_email_schedule.json");

    public static async Task<InventoryEmailSchedule> LoadAsync(IHostEnvironment env, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(env);
        try
        {
            if (!File.Exists(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var defaultSchedule = new InventoryEmailSchedule();
                await SaveAsync(env, defaultSchedule, cancellationToken);
                return defaultSchedule;
            }

            await using var fileStream = File.OpenRead(path);
            var schedule = await JsonSerializer.DeserializeAsync<InventoryEmailSchedule>(fileStream, cancellationToken: cancellationToken);
            return schedule ?? new InventoryEmailSchedule();
        }
        catch (Exception)
        {
            return new InventoryEmailSchedule();
        }
    }

    public static async Task SaveAsync(IHostEnvironment env, InventoryEmailSchedule schedule, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(env);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var fileStream = File.Create(path);
        await JsonSerializer.SerializeAsync(fileStream, schedule, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }
}
