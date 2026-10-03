using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Billing;

/// <summary>Opens a sale: from an appointment (service and online deposit included), a client, or just a name.</summary>
public sealed class CreateSaleRequest
{
    public int? ClientId { get; init; }
    public int? PetId { get; init; }
    public int? AppointmentId { get; init; }

    [MaxLength(200)]
    public string? CustomerName { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

/// <summary>Empty description/price on services and products take the catalog's name and price.</summary>
public sealed class AddSaleLineRequest
{
    public SaleLineKind Kind { get; init; }

    [MaxLength(200)]
    public string? Description { get; init; }

    [MaxLength(100)]
    public string? ServiceCode { get; init; }

    public int? ItemId { get; init; }

    [Range(1, 10_000)]
    public int Quantity { get; init; } = 1;

    [Range(0, 100_000_000)]
    public int? UnitPrice { get; init; }

    [Range(0, 100_000_000)]
    public int Discount { get; init; }
}

public sealed class AddSalePaymentRequest
{
    [Required, MaxLength(50)]
    public string Method { get; init; } = string.Empty;

    [Range(1, 100_000_000)]
    public int Amount { get; init; }

    [MaxLength(100)]
    public string? Reference { get; init; }
}

public sealed class VoidSaleRequest
{
    [Required, MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class CloseDayRequest
{
    public DateOnly Date { get; init; }

    [Range(0, 1_000_000_000)]
    public int CountedCash { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class SaleLineDto
{
    public int Id { get; set; }
    public SaleLineKind Kind { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ServiceCode { get; set; }
    public int? ItemId { get; set; }
    public int Quantity { get; set; }
    public int UnitPrice { get; set; }
    public int Discount { get; set; }
    public int Total { get; set; }
}

public sealed class SalePaymentDto
{
    public int Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string? Reference { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string? ReceivedBy { get; set; }
}

public sealed class SaleDto
{
    public int Id { get; set; }
    public SaleStatus Status { get; set; }
    public int? ClientId { get; set; }
    public int? PetId { get; set; }
    public int? AppointmentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateOnly BusinessDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public int Total { get; set; }
    public int PaidAmount { get; set; }
    public int Balance { get; set; }
    public List<SaleLineDto> Lines { get; set; } = [];
    public List<SalePaymentDto> Payments { get; set; } = [];
}

public sealed record MethodTotalDto(string Method, string Name, bool IsCash, int Amount, int Count);

public sealed class DaySummaryDto
{
    public DateOnly Date { get; set; }
    public List<MethodTotalDto> Totals { get; set; } = [];
    public int ExpectedCash { get; set; }
    public bool IsClosed { get; set; }
    public int? CountedCash { get; set; }
    public int? Difference { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? Notes { get; set; }
}

/// <summary>The clinic's billing settings document plus the version it was read at (send it back unchanged).</summary>
public sealed class BillingSettingsDocument
{
    public int Version { get; set; }
    public JsonElement Settings { get; set; }
}
