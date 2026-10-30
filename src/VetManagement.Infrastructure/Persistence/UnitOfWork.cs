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
    private ILabMachineRepository? _labMachines;
    private IMedicalVisitRepository? _medicalVisits;
    private IClinicSettingRepository? _clinicSettings;
    private IAppointmentRepository? _appointments;
    private ISaleRepository? _sales;
    private IRepository<VetManagement.Domain.Clinical.PreventiveDose>? _preventiveDoses;
    private IRepository<VetManagement.Domain.Clinical.VisitSupply>? _visitSupplies;
    private IRepository<VetManagement.Domain.Clients.ClientAccessToken>? _clientAccessTokens;
    private IRepository<VetManagement.Domain.Billing.CashClose>? _cashCloses;
    private IExamReportRepository? _examReports;
    private IRepository<VetManagement.Domain.Configuration.ClinicImage>? _clinicImages;

    public IItemRepository Items => _items ??= new ItemRepository(context);

    public IClientRepository Clients => _clients ??= new ClientRepository(context);

    public IPetRepository Pets => _pets ??= new PetRepository(context);

    public IExamRepository Exams => _exams ??= new ExamRepository(context);

    public IExamPerformedRepository ExamPerformed => _examPerformed ??= new ExamPerformedRepository(context);

    public IInventoryMovementRepository InventoryMovements => _inventoryMovements ??= new InventoryMovementRepository(context);

    private IRepository<VetManagement.Domain.Inventory.Warehouse>? _warehouses;
    public IRepository<VetManagement.Domain.Inventory.Warehouse> Warehouses => _warehouses ??= new Repository<VetManagement.Domain.Inventory.Warehouse>(context);

    private IWarehouseStockRepository? _warehouseStocks;
    public IWarehouseStockRepository WarehouseStocks => _warehouseStocks ??= new WarehouseStockRepository(context);

    public IAuditLogRepository AuditLogs => _auditLogs ??= new AuditLogRepository(context);

    public IExternalLabRepository ExternalLabs => _externalLabs ??= new ExternalLabRepository(context);

    public ILabMachineRepository LabMachines => _labMachines ??= new LabMachineRepository(context);

    public IMedicalVisitRepository MedicalVisits => _medicalVisits ??= new MedicalVisitRepository(context);

    public IClinicSettingRepository ClinicSettings => _clinicSettings ??= new ClinicSettingRepository(context);

    public IAppointmentRepository Appointments => _appointments ??= new AppointmentRepository(context);

    public ISaleRepository Sales => _sales ??= new SaleRepository(context);

    public IRepository<VetManagement.Domain.Clinical.PreventiveDose> PreventiveDoses => _preventiveDoses ??= new Repository<VetManagement.Domain.Clinical.PreventiveDose>(context);

    public IRepository<VetManagement.Domain.Clinical.VisitSupply> VisitSupplies => _visitSupplies ??= new Repository<VetManagement.Domain.Clinical.VisitSupply>(context);

    public IRepository<VetManagement.Domain.Clients.ClientAccessToken> ClientAccessTokens => _clientAccessTokens ??= new Repository<VetManagement.Domain.Clients.ClientAccessToken>(context);

    public IRepository<VetManagement.Domain.Billing.CashClose> CashCloses => _cashCloses ??= new Repository<VetManagement.Domain.Billing.CashClose>(context);

    public IExamReportRepository ExamReports => _examReports ??= new ExamReportRepository(context);

    public IRepository<VetManagement.Domain.Configuration.ClinicImage> ClinicImages => _clinicImages ??= new Repository<VetManagement.Domain.Configuration.ClinicImage>(context);

    private IConsumptionSheetRepository? _consumptionSheets;
    public IConsumptionSheetRepository ConsumptionSheets => _consumptionSheets ??= new ConsumptionSheetRepository(context);

    private IRepository<VetManagement.Domain.Consumption.ConsumptionPricing>? _consumptionPricings;
    public IRepository<VetManagement.Domain.Consumption.ConsumptionPricing> ConsumptionPricings => _consumptionPricings ??= new Repository<VetManagement.Domain.Consumption.ConsumptionPricing>(context);

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
