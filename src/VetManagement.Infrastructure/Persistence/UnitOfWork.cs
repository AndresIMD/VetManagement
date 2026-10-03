using System.Data;
using Microsoft.EntityFrameworkCore;
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
    private IAppointmentRepository? _appointments;
    private ISaleRepository? _sales;
    private IRepository<VetManagement.Domain.Billing.CashClose>? _cashCloses;

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

    public IAppointmentRepository Appointments => _appointments ??= new AppointmentRepository(context);

    public ISaleRepository Sales => _sales ??= new SaleRepository(context);

    public IRepository<VetManagement.Domain.Billing.CashClose> CashCloses => _cashCloses ??= new Repository<VetManagement.Domain.Billing.CashClose>(context);

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

    public Task<T> ExecuteExclusiveAsync<T>(string lockKey, Func<Task<T>> work)
    {
        // The SQL Server provider uses a retrying execution strategy, which rejects user-initiated
        // transactions unless the whole unit runs through the strategy.
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            if (!context.Database.IsRelational())
                return await work(); // in-memory test provider: no transactions or locks

            await using var transaction = await context.Database.BeginTransactionAsync();
            // An application lock queues concurrent callers with the same key. Serializable isolation
            // alone deadlocked under contention (verified: parallel bookings ended as 500s).
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = {lockKey}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                IF @result < 0 THROW 50001, 'Timed out waiting for the booking lock.', 1;
                """);
            var result = await work();
            await transaction.CommitAsync();
            return result;
        });
    }

    public void Dispose()
        => context.Dispose();
}
