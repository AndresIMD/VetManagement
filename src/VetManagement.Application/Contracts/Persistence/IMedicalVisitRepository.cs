using VetManagement.Domain.Enums;
using VetManagement.Domain.Medical;

namespace VetManagement.Application.Contracts.Persistence;

public interface IMedicalVisitRepository : IRepository<MedicalVisit>
{
    Task<IEnumerable<MedicalVisit>> GetFilteredAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? patientId = null,
        string? responsible = null,
        PaymentStatus? paymentStatus = null,
        string? search = null);

    /// <summary>
    /// Gets a medical visit with all procedures included.
    /// </summary>
    /// <param name="id">The visit ID.</param>
    /// <returns>The visit with procedures, or null if not found.</returns>
    Task<MedicalVisit?> GetByIdWithProceduresAsync(int id);
}
