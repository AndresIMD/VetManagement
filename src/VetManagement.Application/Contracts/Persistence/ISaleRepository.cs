using VetManagement.Domain.Billing;
using VetManagement.Domain.Enums;

namespace VetManagement.Application.Contracts.Persistence;

public interface ISaleRepository : IRepository<Sale>
{
    /// <summary>The sale with its lines and payments, tracked for changes.</summary>
    Task<Sale?> GetWithDetailsAsync(int id);

    Task<List<Sale>> ListAsync(DateOnly from, DateOnly to, SaleStatus? status);

    /// <summary>The appointment's sale that is not voided, if any.</summary>
    Task<Sale?> GetActiveByAppointmentAsync(int appointmentId);

    /// <summary>Payments counted on that business date, excluding voided sales.</summary>
    Task<List<SalePayment>> GetDayPaymentsAsync(DateOnly date);
}
