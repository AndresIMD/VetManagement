namespace VetManagement.Application.Common;

/// <summary>
/// A save was rejected because the data changed after it was read (optimistic concurrency).
/// Thrown by the unit of work so Application code doesn't depend on EF Core.
/// </summary>
public class ConcurrencyConflictException(Exception inner) : Exception("The data was modified by someone else.", inner);
