using VetManagement.Shared.Models.Exams;

namespace VetManagement.Application.Contracts.Persistence;

public interface IExamPerformedRepository : IRepository<ExamPerformed>
{
    Task<IEnumerable<ExamPerformed>> GetFilteredAsync(
        int? patientId = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null);

    Task<ExamPerformed?> GetByIdWithItemsAsync(int id);
}
