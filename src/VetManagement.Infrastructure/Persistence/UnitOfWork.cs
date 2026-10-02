using VetManagement.Application.Common;
using VetManagement.Application.Contracts.Persistence;
using VetManagement.Infrastructure.Data;
using VetManagement.Infrastructure.Repositories;

namespace VetManagement.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private IItemRepository? _items;
    private IClientRepository? _clients;
    private IPetRepository? _pets;
    private IExamRepository? _exams;
    private IExamPerformedRepository? _examPerformed;
    private IInventoryMovementRepository? _inventoryMovements;
    private IAuditLogRepository? _auditLogs;
    private IExternalLabRepository? _externalLabs;
    private IMedicalVisitRepository? _medicalVisits;
    private IClinicSettingRepository? _clinicSettings;

    public IItemRepository Items => _items ??= new ItemRepository(context);

    public IClientRepository Clients => _clients ??= new ClientRepository(context);

    public IPetRepository Pets => _pets ??= new PetRepository(context);

    public IExamRepository Exams => _exams ??= new ExamRepository(context);

    public IExamPerformedRepository ExamPerformed => _examPerformed ??= new ExamPerformedRepository(context);

    public IInventoryMovementRepository InventoryMovements => _inventoryMovements ??= new InventoryMovementRepository(context);

    public IAuditLogRepository AuditLogs => _auditLogs ??= new AuditLogRepository(context);

    public IExternalLabRepository ExternalLabs => _externalLabs ??= new ExternalLabRepository(context);

    public IMedicalVisitRepository MedicalVisits => _medicalVisits ??= new MedicalVisitRepository(context);

    public IClinicSettingRepository ClinicSettings => _clinicSettings ??= new ClinicSettingRepository(context);

    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public void Dispose()
        => context.Dispose();
}
