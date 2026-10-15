using VetManagement.Domain.Enums;
using VetManagement.Domain.Clients;

namespace VetManagement.Application.Contracts.Persistence;

public interface IPetRepository : IRepository<Pet>
{
    Task<IEnumerable<Pet>> GetByOwnerIdAsync(int ownerId);

    Task<IEnumerable<Pet>> SearchAsync(string? searchTerm = null, Species? species = null, ReproductiveStatus? status = null);

    /// <summary>Pets by id, provisional ones included; not tracked.</summary>
    Task<List<Pet>> GetWithProvisionalAsync(IReadOnlyCollection<int> ids);

    /// <summary>Provisional pets with exactly this name (as stored, in capitals); tracked.</summary>
    Task<List<Pet>> FindProvisionalByNameAsync(string name);

    /// <summary>A provisional pet, tracked; null when it doesn't exist or was registered.</summary>
    Task<Pet?> GetProvisionalAsync(int id);
}
