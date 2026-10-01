using System.Text;
using Microsoft.EntityFrameworkCore;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Inventory;

namespace VetManagement.Api.Services;

/// <summary>
/// Background service that periodically checks inventory levels and sends alert emails.
/// </summary>
public class InventoryAlertsHostedService(
    ILogger<InventoryAlertsHostedService> logger,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment env)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Inventory Alerts Service starting");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await CheckAndSendAsync(stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Inventory Alerts Service");
            }
        }
        logger.LogInformation("Inventory Alerts Service stopping");
    }

    private async Task CheckAndSendAsync(CancellationToken ct)
    {
        var schedule = await InventoryEmailSchedule.LoadAsync(env, ct);
        if (!schedule.Enabled)
            return;

        var now = DateTimeOffset.Now;
        if ((int)now.DayOfWeek != schedule.DayOfWeek || now.Hour != schedule.Hour || now.Minute != schedule.Minute)
            return;

        if (schedule.LastSentUtc.HasValue && (now.UtcDateTime - schedule.LastSentUtc.Value).TotalMinutes < 1)
            return;

        logger.LogInformation("Scheduled inventory alert check starting");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

        var items = await db.Items.AsNoTracking().ToListAsync(ct);
        var (lowStockItems, outOfStockItems) = BuildAlerts(items);

        var body = BuildEmailBody(
            schedule.IncludeLowStock ? lowStockItems : [],
            schedule.IncludeOutOfStock ? outOfStockItems : []);

        if (string.IsNullOrWhiteSpace(body))
        {
            logger.LogInformation("No inventory alerts");
            return;
        }

        var to = schedule.To.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cc = string.IsNullOrWhiteSpace(schedule.Cc) ? null : schedule.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        logger.LogInformation("Sending inventory alert to {To}", string.Join(", ", to));
        await emailService.SendAsync(to, cc, schedule.Subject, body, ct);

        schedule.LastSentUtc = DateTime.UtcNow;
        await InventoryEmailSchedule.SaveAsync(env, schedule, ct);
        logger.LogInformation("Inventory alert sent successfully");
    }

    private static (List<ItemAlert> LowStock, List<ItemAlert> OutOfStock) BuildAlerts(List<Item> items)
    {
        var lowStock = new List<ItemAlert>();
        var outOfStock = new List<ItemAlert>();

        var drugs = items.OfType<Drug>().Where(d => !string.IsNullOrWhiteSpace(d.Compound)).ToList();
        var nonDrugs = items.Where(i => i is not Drug).ToList();

        var drugGroups = drugs
            .GroupBy(d => d.Compound!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Compound = g.Key,
                TotalStock = g.Sum(d => d.Stock),
                MinThreshold = g.Select(d => d.LowStockThreshold).Where(t => t > 0).DefaultIfEmpty(0).Min()
            });

        lowStock.AddRange(drugGroups
            .Where(x => x.MinThreshold > 0 && x.TotalStock > 0 && x.TotalStock <= x.MinThreshold)
            .Select(x => new ItemAlert(x.Compound, x.TotalStock)));

        outOfStock.AddRange(drugGroups
            .Where(x => x.TotalStock == 0)
            .Select(x => new ItemAlert(x.Compound, 0)));

        lowStock.AddRange(nonDrugs
            .Where(i => i.LowStockThreshold > 0 && i.Stock > 0 && i.Stock <= i.LowStockThreshold)
            .Select(i => new ItemAlert(i.Name, i.Stock)));

        outOfStock.AddRange(nonDrugs
            .Where(i => i.Stock == 0)
            .Select(i => new ItemAlert(i.Name, 0)));

        return (lowStock.OrderBy(x => x.Stock).ToList(), outOfStock.OrderBy(x => x.Name).ToList());
    }

    private static string BuildEmailBody(List<ItemAlert> lowStockItems, List<ItemAlert> outOfStockItems)
    {
        if (lowStockItems.Count == 0 && outOfStockItems.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"Report Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine("System: Inventory Alerts");
        sb.AppendLine();

        if (lowStockItems.Any())
        {
            sb.AppendLine("--- Low Stock Items ---");
            foreach (var item in lowStockItems)
                sb.AppendLine($"- {item.Name} (Stock: {item.Stock})");
            sb.AppendLine();
        }

        if (outOfStockItems.Any())
        {
            sb.AppendLine("--- Out of Stock Items ---");
            foreach (var item in outOfStockItems)
                sb.AppendLine($"- {item.Name}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private record ItemAlert(string Name, int Stock);
}
