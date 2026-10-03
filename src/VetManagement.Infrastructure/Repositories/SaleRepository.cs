using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Enums;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Infrastructure.Repositories;

public class SaleRepository(AppDbContext context) : Repository<Sale>(context), ISaleRepository
{
    public async Task<Sale?> GetWithDetailsAsync(int id)
        => await _context.Sales.Include(s => s.Lines).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<Sale>> ListAsync(DateOnly from, DateOnly to, SaleStatus? status)
        => await _context.Sales.AsNoTracking()
            .Include(s => s.Lines).Include(s => s.Payments)
            .Where(s => s.BusinessDate >= from && s.BusinessDate <= to && (status == null || s.Status == status))
            .OrderByDescending(s => s.Id)
            .ToListAsync();

    public async Task<Sale?> GetActiveByAppointmentAsync(int appointmentId)
        => await _context.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.AppointmentId == appointmentId && s.Status != SaleStatus.Voided);

    public async Task<List<SalePayment>> GetPaymentsAsync(DateOnly from, DateOnly to)
        => await _context.Sales.AsNoTracking()
            .Where(s => s.Status != SaleStatus.Voided)
            .SelectMany(s => s.Payments)
            .Where(p => p.BusinessDate >= from && p.BusinessDate <= to)
            .ToListAsync();
}
