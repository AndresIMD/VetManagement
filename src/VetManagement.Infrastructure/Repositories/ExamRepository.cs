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
            var term = searchTerm.Trim();
            query = query.Where(e =>
                e.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                (e.Brand != null && e.Brand.Contains(term, StringComparison.CurrentCultureIgnoreCase)) ||
                (e.Machine != null && e.Machine.Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        }

        return await query.ToListAsync();
    }
}
