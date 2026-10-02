using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Audit;

/// <summary>
/// Record of a change made to an entity: who, when, what action and the serialized changes.
/// </summary>
public class AuditLog : Entity<int>
{
    public AuditLog() : base(0) { }

    public int EntityId { get; set; }

    public string EntityName { get; set; } = "";

    public DateTime Date { get; set; }

    public AuditActionType Action { get; set; }

    public string Changes { get; set; } = "";

    public string? User { get; set; }
}
