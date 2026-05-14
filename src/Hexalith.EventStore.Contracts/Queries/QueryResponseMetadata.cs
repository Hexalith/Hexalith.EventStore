namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public metadata returned with gateway query responses.
/// </summary>
/// <param name="ETag">The normalized strong ETag token when available.</param>
/// <param name="IsNotModified">Whether the query response represents a not-modified result.</param>
/// <param name="IsStale">Whether the served projection is stale when the gateway can determine this authoritatively.</param>
/// <param name="IsDegraded">Whether the response was served through a degraded query path.</param>
/// <param name="ProjectionVersion">Projection version metadata when available.</param>
/// <param name="ServedAt">The time the gateway served the response.</param>
/// <param name="Paging">Paging metadata when paging policy applies.</param>
/// <param name="WarningCodes">Stable warning codes associated with the query result.</param>
public sealed record QueryResponseMetadata(
    string? ETag = null,
    bool? IsNotModified = null,
    bool? IsStale = null,
    bool? IsDegraded = null,
    string? ProjectionVersion = null,
    DateTimeOffset? ServedAt = null,
    QueryPagingMetadata? Paging = null,
    IReadOnlyList<string>? WarningCodes = null);
