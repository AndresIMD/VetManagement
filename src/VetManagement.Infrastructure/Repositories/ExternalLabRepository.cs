using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Shared.Models.Exams;

namespace VetManagement.Infrastructure.Repositories;

public class ExternalLabRepository(AppDbContext context) : Repository<ExternalLab>(context), IExternalLabRepository
{
}
