using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Domain.Exams;

namespace VetManagement.Infrastructure.Repositories;

public class LabMachineRepository(AppDbContext context) : Repository<LabMachine>(context), ILabMachineRepository
{
}
