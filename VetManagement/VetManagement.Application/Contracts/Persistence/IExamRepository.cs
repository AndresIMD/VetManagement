using VetManagement.Shared.Models.Exams;

namespace VetManagement.Application.Contracts.Persistence;

public interface IExamRepository : IRepository<Exam>
{
    Task<IEnumerable<Exam>> SearchAsync(string? searchTerm = null);
}
