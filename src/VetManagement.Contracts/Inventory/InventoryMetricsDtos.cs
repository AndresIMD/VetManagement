namespace VetManagement.Contracts.Inventory;

/// <summary>How the period is split on the chart; the API picks it from the length of the range.</summary>
public enum MetricsGrouping
{
    Hour,
    Day,
    Week,
    Month
}

/// <summary>Units that came in and went out in one slice of the period. <see cref="Start"/> is clinic-local time.</summary>
public sealed record MetricsBucketDto(DateTime Start, int UnitsIn, int UnitsOut);

/// <summary>One item's flow in the period, with its stock today.</summary>
/// <param name="DaysOfCover">Days the current stock lasts at the period's pace; null when nothing went out.</param>
public sealed record ItemFlowDto(int ItemId, string Name, int UnitsOut, int UnitsIn, int Stock, int LowStockThreshold, long SaleValueOut,
    double? DaysOfCover);

/// <summary>Units of one item that went out, per bucket.</summary>
public sealed record ItemSeriesDto(int ItemId, string Name, List<int> UnitsOut);

/// <summary>
/// Stock flow between two clinic-local dates (inclusive), for all items or the chosen ones.
/// Values use today's prices; "previous" is the period of the same length right before.
/// </summary>
public sealed record InventoryMetricsDto(
    DateOnly From,
    DateOnly To,
    MetricsGrouping Grouping,
    int UnitsOut,
    int UnitsIn,
    int PreviousUnitsOut,
    int Movements,
    int ItemsMoved,
    long SaleValueOut,
    long CostValueOut,
    int LowStock,
    int OutOfStock,
    List<MetricsBucketDto> Buckets,
    List<ItemFlowDto> TopItems,
    List<ItemFlowDto> RunningOut,
    List<ItemSeriesDto> Series);
