using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Exams;

namespace VetManagement.Infrastructure.Repositories;

public class ExamPerformedRepository(AppDbContext context) : Repository<ExamPerformed>(context), IExamPerformedRepository
{
    public async Task<IEnumerable<ExamPerformed>> GetFilteredAsync(
        int? patientId = null,
        string? responsible = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        IQueryable<ExamPerformed> query = _context.ExamsPerformed.AsNoTracking().Include(e => e.Items);

        if (patientId.HasValue)
        {
            query = query.Where(e => e.PatientId == patientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(responsible))
        {
            var term = responsible.Trim().ToLower();
            query = query.Where(e => e.Responsible.ToLower().Contains(term));
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.Date <= to.Value);
        }

        return await query.OrderByDescending(e => e.Date).ToListAsync();
    }

    public async Task<ExamPerformed?> GetByIdWithItemsAsync(int id)
    {
        return await _context.ExamsPerformed
            .Include(e => e.Items)
            .ThenInclude(i => i.ExternalLab)
            .FirstOrDefaultAsync(e => e.Id == id);
    }
}
