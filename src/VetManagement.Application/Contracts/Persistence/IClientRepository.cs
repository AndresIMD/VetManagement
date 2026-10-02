using VetManagement.Domain.Clients;

namespace VetManagement.Application.Contracts.Persistence;

public interface IClientRepository : IRepository<Client>
{
    Task<Client?> GetByTaxIdAsync(string taxId);

    Task<IEnumerable<Client>> SearchAsync(string? searchTerm = null);
}
