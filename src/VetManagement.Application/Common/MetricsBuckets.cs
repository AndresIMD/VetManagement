using VetManagement.Contracts.Inventory;

namespace VetManagement.Application.Common;

/// <summary>
/// How a metrics period is split for its chart: by hour for a single day, then by day, week or month as the range grows.
/// Shared by the inventory and exam metrics, so both pages slice the same period the same way.
/// </summary>
public static class MetricsBuckets
{
    public static MetricsGrouping GroupingFor(DateOnly from, DateOnly to) => (to.DayNumber - from.DayNumber + 1) switch
    {
        1 => MetricsGrouping.Hour,
        <= 62 => MetricsGrouping.Day,
        <= 182 => MetricsGrouping.Week,
        _ => MetricsGrouping.Month
    };

    /// <summary>Local start of each slice. A single day is split by hour, up to the current hour when it is today.</summary>
    public static List<DateTime> Starts(DateOnly from, DateOnly to, MetricsGrouping grouping, DateTime now)
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

    /// <summary>The slice a local time falls in, counted from the first slice's start.</summary>
    public static int Index(DateTime local, DateTime first, MetricsGrouping grouping) => grouping switch
    {
        MetricsGrouping.Hour => local.Hour,
        MetricsGrouping.Day => (local.Date - first).Days,
        MetricsGrouping.Week => (local.Date - first).Days / 7,
        _ => (local.Year - first.Year) * 12 + local.Month - first.Month
    };
}
