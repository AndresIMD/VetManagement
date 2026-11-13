using VetManagement.Application.Common;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Contracts.Exams;
using VetManagement.Contracts.Inventory;

namespace VetManagement.Application.Exams;

/// <summary>
/// Lab reports for a period, by the date each sample was received: how many, of which exams, who wrote them, how many
/// show a value out of range, and the kits they took from inventory. Read-only; the reports are counted in the database's
/// projection, never loaded whole.
/// </summary>
public class ExamMetricsService(IUnitOfWork unitOfWork)
{
    public const int MaxDays = 366;

    private const int ListSize = 10;

    public async Task<ExamMetricsDto> GetAsync(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        var previousFrom = from.AddDays(-days);
        var facts = await unitOfWork.ExamReports.GetFactsAsync(previousFrom, to);
        var reports = facts.Where(f => f.ReceivedOn >= from).ToList();

        // A report has a date, not an hour: a single day is one bar.
        var grouping = MetricsBuckets.GroupingFor(from, to) is MetricsGrouping.Hour ? MetricsGrouping.Day : MetricsBuckets.GroupingFor(from, to);
        var starts = MetricsBuckets.Starts(from, to, grouping, DateTime.MinValue);
        var counts = new int[starts.Count];
        foreach (var report in reports)
        {
            var index = MetricsBuckets.Index(report.ReceivedOn.ToDateTime(TimeOnly.MinValue), starts[0], grouping);
            if (index >= 0 && index < counts.Length)
                counts[index]++;
        }

        var kits = reports.Where(r => r.StockDeducted && r.StockQuantity > 0).ToList();
        return new ExamMetricsDto(from, to, grouping,
            Reports: reports.Count,
            PreviousReports: facts.Count - reports.Count,
            WithAbnormal: reports.Count(r => r.AnyAbnormal),
            KitUnits: kits.Sum(r => r.StockQuantity),
            Buckets: [.. starts.Select((start, i) => new ExamMetricsBucketDto(start, counts[i]))],
            ByExam: [.. reports.GroupBy(r => r.ExamId)
                .Select(g => new ExamCountDto(g.Key, g.OrderBy(r => r.ReceivedOn).ThenBy(r => r.Id).Last().Title, g.Count(), g.Count(r => r.AnyAbnormal)))
                .OrderByDescending(e => e.Reports).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(ListSize)],
            ByReporter: [.. reports.GroupBy(r => r.CreatedBy, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ReporterCountDto(g.First().CreatedBy, g.Count()))
                .OrderByDescending(r => r.Reports).ThenBy(r => r.User, StringComparer.CurrentCultureIgnoreCase)],
            Kits: [.. kits.GroupBy(r => r.StockItemName ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new KitUseDto(g.First().StockItemName ?? string.Empty, g.Count(), g.Sum(r => r.StockQuantity)))
                .OrderByDescending(k => k.Units).ThenBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(ListSize)]);
    }
}
