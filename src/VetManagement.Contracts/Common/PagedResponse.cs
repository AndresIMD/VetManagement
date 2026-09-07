namespace VetManagement.Contracts.Common;

/// <summary>
/// Stable transport contract for a server-paginated collection.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
