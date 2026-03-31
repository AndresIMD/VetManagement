using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Core;

namespace VetManagement.Application.Contracts.Persistence;

public interface IPetRepository : IRepository<Pet>
{
    Task<IEnumerable<Pet>> GetByOwnerIdAsync(int ownerId);

    Task<IEnumerable<Pet>> SearchAsync(string? searchTerm = null, Species? species = null, ReproductiveStatus? status = null);
}
