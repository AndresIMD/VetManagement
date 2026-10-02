using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Audit;

public sealed class AuditLogDto
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public string EntityName { get; set; } = "";
    public DateTime Date { get; set; }
    public AuditActionType Action { get; set; }
    public string Changes { get; set; } = "";
    public string? User { get; set; }
}
