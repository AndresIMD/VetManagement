using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Exams;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Infrastructure.Repositories;

public class ExamReportRepository(AppDbContext context) : Repository<ExamReport>(context), IExamReportRepository
{
    public async Task<List<ExamReport>> GetLatestAsync(int? petId, int count)
        => await _context.ExamReports.AsNoTracking()
            .Where(r => petId == null || r.PetId == petId)
            .OrderByDescending(r => r.ReceivedOn).ThenByDescending(r => r.Id)
            .Take(count)
            .ToListAsync();

    public async Task<List<ReportPatient>> FindTypedPatientsAsync(string term, int count)
    {
        // Two small queries instead of reading every typed report: the pets whose typed data matches, then the latest
        // report of each, read without the results (only the patient columns).
        var lowered = term.Trim().ToLower();
        var petIds = await _context.ExamReports.AsNoTracking()
            .Where(r => r.Patient != null && r.PetId != null
                        && (r.Patient.Name.ToLower().Contains(lowered)
                            || ((r.Patient.OwnerName ?? "") + " " + (r.Patient.OwnerLastName ?? "")).ToLower().Contains(lowered)))
            .Select(r => r.PetId!.Value)
            .Distinct()
            .OrderByDescending(id => id)
            .Take(50)
            .ToListAsync();
        if (petIds.Count == 0)
            return [];

        var typed = await _context.ExamReports.AsNoTracking()
            .Where(r => r.Patient != null && r.PetId != null && petIds.Contains(r.PetId.Value))
            .Select(r => new { PetId = r.PetId!.Value, r.ReceivedOn, r.Id, Patient = r.Patient! })
            .ToListAsync();
        return typed
            .GroupBy(r => r.PetId)
            .Select(g => g.OrderByDescending(r => r.ReceivedOn).ThenByDescending(r => r.Id).First().Patient)
            .Where(p => p.Name.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase)
                        || $"{p.OwnerName} {p.OwnerLastName}".Contains(term.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .Take(count)
            .ToList();
    }
}
