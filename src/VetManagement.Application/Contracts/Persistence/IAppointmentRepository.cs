using VetManagement.Domain.Enums;
using VetManagement.Domain.Scheduling;

namespace VetManagement.Application.Contracts.Persistence;

public interface IAppointmentRepository : IRepository<Appointment>
{
    /// <summary>Appointments (any status) whose busy time [StartUtc, OccupiedUntilUtc) overlaps the range.</summary>
    Task<List<Appointment>> GetOverlappingAsync(DateTime fromUtc, DateTime toUtc, string? resourceCode = null);

    Task<List<Appointment>> GetByStatusFromAsync(AppointmentStatus status, DateTime fromUtc);
}
