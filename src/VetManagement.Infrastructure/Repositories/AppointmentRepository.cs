using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Infrastructure.Repositories;

public class AppointmentRepository(AppDbContext context) : Repository<Appointment>(context), IAppointmentRepository
{
    public async Task<List<Appointment>> GetOverlappingAsync(DateTime fromUtc, DateTime toUtc, string? resourceCode = null)
        => await _context.Appointments
            .Where(a => a.StartUtc < toUtc && a.OccupiedUntilUtc > fromUtc)
            .Where(a => resourceCode == null || a.ResourceCode == resourceCode)
            .OrderBy(a => a.StartUtc)
            .ToListAsync();

    public async Task<List<Appointment>> GetByStatusFromAsync(AppointmentStatus status, DateTime fromUtc)
        => await _context.Appointments
            .Where(a => a.Status == status && a.StartUtc >= fromUtc)
            .OrderBy(a => a.StartUtc)
            .ToListAsync();
}
