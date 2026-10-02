namespace VetManagement.Application.Common;

/// <summary>
/// A page of query results returned by application services. Controllers map it to the transport PagedResponse.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
