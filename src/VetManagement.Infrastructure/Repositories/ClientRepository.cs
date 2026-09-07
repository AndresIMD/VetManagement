using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Infrastructure.Repositories;

public class ClientRepository(AppDbContext context) : Repository<Client>(context), IClientRepository
{
    public override async Task<List<Client>> GetAllAsync()
    {
        return await _context.Clients
            .AsNoTracking()
            .Include(c => c.Pets)
            .ToListAsync();
    }

    public override async Task<Client?> GetByIdAsync(int id)
    {
        return await _context.Clients
            .Include(c => c.Pets)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Client?> GetByTaxIdAsync(string taxId)
    {
        return await _context.Clients
            .AsNoTracking()
            .Include(c => c.Pets)
            .FirstOrDefaultAsync(c => c.TaxId == taxId);
    }

    public async Task<IEnumerable<Client>> SearchAsync(string? searchTerm = null)
    {
        IQueryable<Client> query = _context.Clients
            .AsNoTracking()
            .Include(c => c.Pets);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(c =>
                c.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                c.LastName.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                c.TaxId.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                (c.Email != null && c.Email.Contains(term, StringComparison.CurrentCultureIgnoreCase)) ||
                (c.PhoneNumber.ToString().Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        }

        return await query.ToListAsync();
    }
}
