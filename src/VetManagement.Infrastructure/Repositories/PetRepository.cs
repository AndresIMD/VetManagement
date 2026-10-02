using Microsoft.EntityFrameworkCore;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Clients;

namespace VetManagement.Infrastructure.Repositories;

public class PetRepository(AppDbContext context) : Repository<Pet>(context), IPetRepository
{
    public async Task<IEnumerable<Pet>> GetByOwnerIdAsync(int ownerId)
    {
        return await _context.Pets
            .AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Pet>> SearchAsync(string? searchTerm = null, Species? species = null, ReproductiveStatus? status = null)
    {
        var query = _context.Pets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p =>
                p.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                (p.Breed != null && p.Breed.Contains(term, StringComparison.CurrentCultureIgnoreCase)));
        }

        if (species.HasValue)
        {
            query = query.Where(p => p.Species == species.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.ReproductiveStatus == status.Value);
        }

        return await query.ToListAsync();
    }
}
