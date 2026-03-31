using VetManagement.Shared.Models.Exams;

namespace VetManagement.Shared.Services.Api;

public interface IExamApiService
{
    Task<List<Exam>?> GetAllExamsAsync();
    Task AddExamAsync(Exam exam);
    Task UpdateExamAsync(Exam exam);
    Task DeleteExamAsync(int id);
}
