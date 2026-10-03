using VetManagement.Domain.Primitives;

namespace VetManagement.Domain.Clients;

/// <summary>
/// A time-limited link a client received by email to see their pets' file without an account.
/// Only a hash of the token is stored, so a database leak doesn't expose usable links.
/// </summary>
public class ClientAccessToken : Entity<int>
{
    public ClientAccessToken() : base(0) { }

    public int ClientId { get; set; }
    /// <summary>SHA-256 of the token, hex.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
