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

    Task<int> SaveChangesAsync();
}
