using VetManagement.Domain.Clients;

namespace VetManagement.Application.Contracts.Persistence;

public interface IClientRepository : IRepository<Client>
{
    Task<Client?> GetByTaxIdAsync(string taxId);

    Task<IEnumerable<Client>> SearchAsync(string? searchTerm = null);

    /// <summary>Clients by id, provisional ones included; tracked, so a provisional owner can be updated.</summary>
    Task<List<Client>> GetWithProvisionalAsync(IReadOnlyCollection<int> ids);
}
