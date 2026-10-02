using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.Scheduling;

/// <summary>What the public booking portal needs to render a clinic's booking page.</summary>
public sealed class PublicBookingInfoDto
{
    public bool Enabled { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public string? ClinicPhone { get; set; }
    public int CancellationDeadlineHours { get; set; }
    public RefundMode RefundMode { get; set; }
    /// <summary>Origins the portal may send the client back to (the clinic's own websites).</summary>
    public List<string> AllowedReturnOrigins { get; set; } = [];
    public List<PublicServiceDto> Services { get; set; } = [];
}

public sealed class PublicServiceDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int Price { get; set; }
    public int DepositAmount { get; set; }
    public List<PublicResourceDto> Resources { get; set; } = [];
}

public sealed record PublicResourceDto(string Code, string Name);

public sealed class OnlineBookingRequestDto
{
    [Required, MaxLength(100)] public string ServiceCode { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string ResourceCode { get; init; } = string.Empty;
    public DateTime StartUtc { get; init; }
    [Required, MaxLength(100)] public string OwnerFirstName { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string OwnerLastName { get; init; } = string.Empty;
    [Required, MaxLength(20)] public string OwnerTaxId { get; init; } = string.Empty;
    [Required, MaxLength(256), EmailAddress] public string OwnerEmail { get; init; } = string.Empty;
    [Required, MaxLength(30)] public string OwnerPhone { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string PetName { get; init; } = string.Empty;
    public Species PetSpecies { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
}

/// <summary>
/// Result of an online booking. With <see cref="PaymentUrl"/> the portal must send the browser there with a
/// form POST of <see cref="PaymentFields"/>; otherwise the booking is already confirmed.
/// </summary>
public sealed class OnlineBookingResponseDto
{
    public string PublicToken { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public string? PaymentUrl { get; set; }
    public Dictionary<string, string> PaymentFields { get; set; } = [];
}

/// <summary>A booking as its owner sees it through the secret link.</summary>
public sealed class PublicBookingDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public AppointmentStatus Status { get; set; }
    public DepositStatus DepositStatus { get; set; }
    public int DepositAmount { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? PetName { get; set; }
    public bool CanCancel { get; set; }
    /// <summary>Latest moment the client can still cancel online.</summary>
    public DateTime CancelDeadlineUtc { get; set; }
}
