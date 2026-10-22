using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Exams;

namespace VetManagement.Infrastructure.Repositories;

public class ExamRepository(AppDbContext context) : Repository<Exam>(context), IExamRepository
{
    public async Task<IEnumerable<Exam>> SearchAsync(string? searchTerm = null)
    {
        var query = _context.Exams.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(term) ||
                (e.Brand != null && e.Brand.ToLower().Contains(term)) ||
                (e.Machine != null && e.Machine.ToLower().Contains(term)));
        }

        return await query.ToListAsync();
    }
}
