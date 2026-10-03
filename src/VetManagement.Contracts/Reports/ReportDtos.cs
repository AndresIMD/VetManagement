using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Reports;

public sealed record ResourceOccupancyDto(string Code, string Name, int ScheduledMinutes, int BookedMinutes, int OccupancyPercent);

public sealed record NamedCountDto(string Code, string Name, int Count);

public sealed record AgendaReportDto(int Total, int Attended, int Upcoming, int Cancelled, int NoShow, int NoShowPercent, int Online,
    int Overbooked, List<ResourceOccupancyDto> Resources, List<NamedCountDto> Services);

public sealed record MoneyByKeyDto(string Key, string Name, int Amount);

public sealed record DailyAmountDto(DateOnly Date, int Amount);

public sealed record SoldItemDto(string Description, SaleLineKind Kind, int Quantity, int Amount);

public sealed record BillingReportDto(int Sales, int Voided, int Revenue, int Collected, int AverageTicket, int OpenBalance,
    List<MoneyByKeyDto> ByMethod, List<DailyAmountDto> Daily, List<SoldItemDto> TopItems);

public sealed record InventoryReportDto(int Items, int Units, long CostValue, long SaleValue, int LowStock, int OutOfStock);

public sealed record ClinicalReportDto(int Visits, int DosesApplied, int DosesDueSoon, int DosesOverdue);

/// <summary>Management figures for a period (clinic-local dates, inclusive).</summary>
public sealed record ReportSummaryDto(DateOnly From, DateOnly To, AgendaReportDto Agenda, BillingReportDto Billing,
    InventoryReportDto Inventory, ClinicalReportDto Clinical);
