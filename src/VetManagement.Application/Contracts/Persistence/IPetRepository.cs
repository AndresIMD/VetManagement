using VetManagement.Domain.Enums;
using VetManagement.Domain.Clients;

namespace VetManagement.Application.Contracts.Persistence;

public interface IPetRepository : IRepository<Pet>
{
    Task<IEnumerable<Pet>> GetByOwnerIdAsync(int ownerId);

    Task<IEnumerable<Pet>> SearchAsync(string? searchTerm = null, Species? species = null, ReproductiveStatus? status = null);
}
