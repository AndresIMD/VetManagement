using VetManagement.Domain.Exams;

namespace VetManagement.Application.Contracts.Persistence;

/// <summary>What the exam metrics count of one report, without its results or patient.</summary>
/// <param name="AnyAbnormal">At least one value out of its range.</param>
/// <param name="StockDeducted">The report took its kit from inventory.</param>
public sealed record ExamReportFacts(int Id, DateOnly ReceivedOn, int ExamId, string Title, string CreatedBy, bool AnyAbnormal,
    string? StockItemName, int StockQuantity, bool StockDeducted);

public interface IExamReportRepository : IRepository<ExamReport>
{
    /// <summary>The reports whose sample was received between the two dates (inclusive), as <see cref="ExamReportFacts"/>.</summary>
    Task<List<ExamReportFacts>> GetFactsAsync(DateOnly from, DateOnly to);

    /// <summary>The latest reports (of a pet, or of the clinic), newest reception first; not tracked.</summary>
    Task<List<ExamReport>> GetLatestAsync(int? petId, int count);

    /// <summary>
    /// Patients typed into reports (with their provisional pet) whose name or owner contains <paramref name="term"/>, as
    /// typed in each one's latest report; by name, at most <paramref name="count"/>.
    /// </summary>
    Task<List<ReportPatient>> FindTypedPatientsAsync(string term, int count);
}
