namespace VetManagement.Application.Contracts.Persistence;

public interface IUnitOfWork : IDisposable
{
    IItemRepository Items { get; }

    IClientRepository Clients { get; }

    IPetRepository Pets { get; }

    IExamRepository Exams { get; }

    IExamPerformedRepository ExamPerformed { get; }

    IInventoryMovementRepository InventoryMovements { get; }

    IAuditLogRepository AuditLogs { get; }

    IExternalLabRepository ExternalLabs { get; }

    IMedicalVisitRepository MedicalVisits { get; }
    IClinicSettingRepository ClinicSettings { get; }
    IAppointmentRepository Appointments { get; }
    ISaleRepository Sales { get; }
    IRepository<VetManagement.Domain.Clinical.PreventiveDose> PreventiveDoses { get; }
    IRepository<VetManagement.Domain.Billing.CashClose> CashCloses { get; }

    Task<int> SaveChangesAsync();

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction that holds an exclusive lock on <paramref name="lockKey"/>,
    /// so callers using the same key run one at a time (check-then-insert without races or deadlocks).
    /// Retried as a whole on transient failures; load everything the work needs inside it, since tracked
    /// entities are cleared per attempt.
    /// </summary>
    Task<T> ExecuteExclusiveAsync<T>(string lockKey, Func<Task<T>> work);
}
