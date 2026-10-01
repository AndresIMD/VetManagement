namespace VetManagement.Domain.Primitives;

/// <summary>
/// Base type for domain entities. Persistence and transport concerns must not be added here.
/// </summary>
public abstract class Entity<TId>(TId id) where TId : notnull
{
    public TId Id { get; set; } = id;
}
