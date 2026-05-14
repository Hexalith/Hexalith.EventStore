namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public paging metadata returned with query responses when paging policy applies.
/// </summary>
/// <param name="PageSize">The effective page size.</param>
/// <param name="Offset">The effective offset when offset paging applies.</param>
/// <param name="NextCursor">The next cursor when cursor paging applies and a next page exists.</param>
/// <param name="TotalCount">The total count when the projection can provide it authoritatively.</param>
public sealed record QueryPagingMetadata(
    int PageSize,
    int? Offset = null,
    string? NextCursor = null,
    long? TotalCount = null);
