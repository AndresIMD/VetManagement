using VetManagement.Domain.Exams;

namespace VetManagement.Application.Contracts.Persistence;

public interface IExamReportRepository : IRepository<ExamReport>
{
    /// <summary>The latest reports (of a pet, or of the clinic), newest reception first; not tracked.</summary>
    Task<List<ExamReport>> GetLatestAsync(int? petId, int count);

    /// <summary>
    /// Patients typed into reports (with their provisional pet) whose name or owner contains <paramref name="term"/>, as
    /// typed in each one's latest report; by name, at most <paramref name="count"/>.
    /// </summary>
    Task<List<ReportPatient>> FindTypedPatientsAsync(string term, int count);
}
