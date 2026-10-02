using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Medical;

namespace VetManagement.Infrastructure.Repositories;

public class MedicalVisitRepository(AppDbContext context) : Repository<MedicalVisit>(context), IMedicalVisitRepository
{
    public async Task<IEnumerable<MedicalVisit>> GetFilteredAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? patientId = null,
        string? responsible = null,
        PaymentStatus? paymentStatus = null,
        string? search = null)
    {
        IQueryable<MedicalVisit> query = _context.MedicalVisits
            .AsNoTracking()
            .Include(v => v.Procedures);

        if (from.HasValue)
        {
            query = query.Where(v => v.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(v => v.Date <= to.Value);
        }

        if (patientId.HasValue)
        {
            query = query.Where(v => v.PatientId == patientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(responsible))
        {
            var term = responsible.Trim().ToLower();
            query = query.Where(v => v.Responsible.ToLower().Contains(term));
        }

        if (paymentStatus.HasValue && paymentStatus.Value != PaymentStatus.Pending)
        {
            query = query.Where(v => v.PaymentStatus == paymentStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(v =>
                v.Responsible.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                (v.Location != null && v.Location.Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        }

        return await query.OrderByDescending(v => v.Date).ToListAsync();
    }

    public async Task<MedicalVisit?> GetByIdWithProceduresAsync(int id)
    {
        return await _context.MedicalVisits
             .Include(v => v.Procedures)
             .FirstOrDefaultAsync(v => v.Id == id);
    }
}
