using VetManagement.Staff.UI.Models.Exams;

namespace VetManagement.Staff.UI.Services.Api;

public interface IExamApiService
{
    Task<List<Exam>?> GetAllExamsAsync();
    Task AddExamAsync(Exam exam);
    Task UpdateExamAsync(Exam exam);
    Task DeleteExamAsync(int id);
}
