using VetManagement.Domain.Enums;
using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Configuration;

/// <summary>An image the clinic uploads for its documents (one per kind), e.g. the logo on exam reports.</summary>
public class ClinicImage : Entity<int>
{
    public ClinicImage() : base(0) { }

    public ClinicImageKind Kind { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
