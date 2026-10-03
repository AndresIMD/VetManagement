using System.ComponentModel.DataAnnotations;
using VetManagement.Domain.Enums;

namespace VetManagement.Contracts.ClientPortal;

public sealed class PortalLinkRequest
{
    [Required, MaxLength(20)]
    public string Rut { get; init; } = string.Empty;
}

public sealed record PortalDoseDto(string ProductName, PreventiveKind Kind, DateOnly AppliedOn, DateOnly? NextDueOn, PreventiveStatus Status);

/// <summary>A past visit as the owner sees it; diagnosis and treatment only when the clinic shows them.</summary>
public sealed record PortalVisitDto(DateTime Date, string? Reason, string? Diagnosis, string? Treatment, decimal? WeightKg);

public sealed record PortalPetDto(string Name, Species Species, string Breed, DateTime Birthdate, float Weight,
    List<PortalDoseDto> Doses, List<PortalVisitDto> Visits);

/// <summary><see cref="PublicToken"/> links to the booking page (view/cancel) when the appointment was booked online.</summary>
public sealed record PortalAppointmentDto(DateTime StartUtc, string ServiceName, string ResourceName, string? PetName,
    AppointmentStatus Status, string? PublicToken);

public sealed record ClientPortalDto(string ClientName, DateTime ExpiresAtUtc, List<PortalPetDto> Pets, List<PortalAppointmentDto> Upcoming);
