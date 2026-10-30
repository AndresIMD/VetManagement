using VetManagement.Application.Contracts.Persistence;
using VetManagement.Application.Scheduling;
using VetManagement.Contracts.Inventory;
using VetManagement.Domain.Enums;

namespace VetManagement.Application.Inventory;

/// <summary>
/// Stock flow for a period, by the clinic's local dates: what went out and came in, the items that move most, and
/// those whose stock won't last long at that pace. Read-only; the movements are added up here, not in the browser.
/// </summary>
public class InventoryMetricsService(IUnitOfWork unitOfWork, SchedulingSettingsService schedulingSettings, TimeProvider clock)
{
    public const int MaxDays = 366;

    /// <summary>Most items drawn as their own line; more would not be readable.</summary>
    public const int MaxCompared = 8;

    /// <summary>Items whose stock lasts fewer days than this, at the period's pace, are listed as running out.</summary>
    public const int RunningOutDays = 14;

    private const int ListSize = 10;

    public static MetricsGrouping GroupingFor(DateOnly from, DateOnly to) => (to.DayNumber - from.DayNumber + 1) switch
    {
        1 => MetricsGrouping.Hour,
        <= 62 => MetricsGrouping.Day,
        <= 182 => MetricsGrouping.Week,
        _ => MetricsGrouping.Month
    };

    public async Task<InventoryMetricsDto> GetAsync(DateOnly from, DateOnly to, IReadOnlyCollection<int>? itemIds = null)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById((await schedulingSettings.GetAsync()).Settings.TimeZone);
        var now = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, zone);
        var today = DateOnly.FromDateTime(now);
        var days = to.DayNumber - from.DayNumber + 1;
        var previousFrom = from.AddDays(-days);
        var (fromUtc, toUtc) = AgendaRules.UtcRange(previousFrom, to, zone); // padded a day each side; filtered by local date below

        var chosen = itemIds is { Count: > 0 } ? itemIds.ToHashSet() : null;
        var allItems = (await unitOfWork.Items.GetAllAsync()).ToDictionary(i => i.Id);
        var items = chosen is null ? allItems : allItems.Values.Where(i => chosen.Contains(i.Id)).ToDictionary(i => i.Id);
        // Transfers between warehouses leave the clinic's stock as it was: they are neither use nor purchases.
        var allMovements = (await unitOfWork.InventoryMovements.GetQuantitiesAsync(fromUtc, toUtc))
            .Where(m => allItems.ContainsKey(m.ItemId) && !m.Type.IsTransfer())
            .Select(m => m with { Date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(m.Date, DateTimeKind.Utc), zone) })
            .ToList();
        var movements = allMovements.Where(m => items.ContainsKey(m.ItemId)).ToList();
        var inPeriod = movements.Where(m => DateOnly.FromDateTime(m.Date) >= from && DateOnly.FromDateTime(m.Date) <= to).ToList();
        var previousUnitsOut = movements
            .Where(m => IsOut(m.Type) && DateOnly.FromDateTime(m.Date) >= previousFrom && DateOnly.FromDateTime(m.Date) < from)
            .Sum(m => m.Quantity);

        var grouping = GroupingFor(from, to);
        var starts = BucketStarts(from, to, grouping, now);
        var unitsIn = new int[starts.Count];
        var unitsOut = new int[starts.Count];
        var series = (chosen ?? []).Where(items.ContainsKey).ToDictionary(id => id, _ => (Out: new int[starts.Count], In: new int[starts.Count]));
        foreach (var m in inPeriod)
        {
            var index = BucketIndex(m.Date, starts[0], grouping);
            if (index < 0 || index >= starts.Count)
                continue;
            var drawn = series.TryGetValue(m.ItemId, out var line);
            if (IsOut(m.Type))
            {
                unitsOut[index] += m.Quantity;
                if (drawn)
                    line.Out[index] += m.Quantity;
            }
            else if (IsIn(m.Type))
            {
                unitsIn[index] += m.Quantity;
                if (drawn)
                    line.In[index] += m.Quantity;
            }
        }

        // The pace counts only the days that have happened, so a period ending in the future doesn't dilute it.
        var elapsedDays = Math.Max(1, Math.Min(to.DayNumber, today.DayNumber) - from.DayNumber + 1);
        List<ItemFlowDto> Flows(IEnumerable<MovementQuantity> period) => period.GroupBy(m => m.ItemId).Select(g =>
        {
            var item = allItems[g.Key];
            var outUnits = g.Where(m => IsOut(m.Type)).Sum(m => m.Quantity);
            var inUnits = g.Where(m => IsIn(m.Type)).Sum(m => m.Quantity);
            double? cover = outUnits == 0 ? null : Math.Round(Math.Max(item.Stock, 0) / (outUnits / (double)elapsedDays), 1);
            return new ItemFlowDto(item.Id, item.Name, outUnits, inUnits, item.Stock, item.LowStockThreshold, (long)outUnits * item.SellPrice, cover);
        }).ToList();
        var flows = Flows(inPeriod);
        // The most used are the whole inventory's even with items chosen: the list is where more of them are picked.
        var allFlows = chosen is null ? flows
            : Flows(allMovements.Where(m => DateOnly.FromDateTime(m.Date) >= from && DateOnly.FromDateTime(m.Date) <= to));

        return new InventoryMetricsDto(from, to, grouping,
            UnitsOut: unitsOut.Sum(),
            UnitsIn: unitsIn.Sum(),
            PreviousUnitsOut: previousUnitsOut,
            Movements: inPeriod.Count,
            ItemsMoved: flows.Count,
            SaleValueOut: flows.Sum(f => f.SaleValueOut),
            CostValueOut: flows.Sum(f => (long)f.UnitsOut * items[f.ItemId].BuyPrice),
            LowStock: items.Values.Count(i => i.Stock > 0 && i.Stock <= i.LowStockThreshold),
            OutOfStock: items.Values.Count(i => i.Stock <= 0),
            Buckets: [.. starts.Select((s, i) => new MetricsBucketDto(s, unitsIn[i], unitsOut[i]))],
            TopItems: [.. allFlows.Where(f => f.UnitsOut > 0).OrderByDescending(f => f.UnitsOut).ThenBy(f => f.Name).Take(ListSize)],
            RunningOut: [.. flows.Where(f => f.DaysOfCover < RunningOutDays).OrderBy(f => f.DaysOfCover).ThenByDescending(f => f.UnitsOut).Take(ListSize)],
            Series: [.. series.Select(s => new ItemSeriesDto(s.Key, items[s.Key].Name, [.. s.Value.Out], [.. s.Value.In])).OrderBy(s => s.Name)]);
    }

    private static bool IsOut(InventoryMovementType type) => type is InventoryMovementType.Egress or InventoryMovementType.MassiveStockEgress;

    private static bool IsIn(InventoryMovementType type) => type is InventoryMovementType.Ingress or InventoryMovementType.MassiveStockIngress;

    /// <summary>Local start of each slice. A single day is split by hour, up to the current hour when it is today.</summary>
    private static List<DateTime> BucketStarts(DateOnly from, DateOnly to, MetricsGrouping grouping, DateTime now)
    {
        var starts = new List<DateTime>();
        switch (grouping)
        {
            case MetricsGrouping.Hour:
                var lastHour = from == DateOnly.FromDateTime(now) ? now.Hour : 23;
                for (var h = 0; h <= lastHour; h++)
                    starts.Add(from.ToDateTime(new TimeOnly(h, 0)));
                break;
            case MetricsGrouping.Day:
                for (var d = from; d <= to; d = d.AddDays(1))
                    starts.Add(d.ToDateTime(TimeOnly.MinValue));
                break;
            case MetricsGrouping.Week:
                var monday = from.AddDays(-(((int)from.DayOfWeek + 6) % 7));
                for (var d = monday; d <= to; d = d.AddDays(7))
                    starts.Add(d.ToDateTime(TimeOnly.MinValue));
                break;
            default:
                for (var d = new DateOnly(from.Year, from.Month, 1); d <= to; d = d.AddMonths(1))
                    starts.Add(d.ToDateTime(TimeOnly.MinValue));
                break;
        }
        return starts;
    }

    private static int BucketIndex(DateTime local, DateTime first, MetricsGrouping grouping) => grouping switch
    {
        MetricsGrouping.Hour => local.Hour,
        MetricsGrouping.Day => (local.Date - first).Days,
        MetricsGrouping.Week => (local.Date - first).Days / 7,
        _ => (local.Year - first.Year) * 12 + local.Month - first.Month
    };
}
