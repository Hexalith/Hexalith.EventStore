using System.Runtime.Serialization;

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
[DataContract]
[KnownType(typeof(string[]))]
public sealed record QueryResponseMetadata(
    [property: DataMember] string? ETag = null,
    [property: DataMember] bool? IsNotModified = null,
    [property: DataMember] bool? IsStale = null,
    [property: DataMember] bool? IsDegraded = null,
    [property: DataMember] string? ProjectionVersion = null,
    [property: DataMember] DateTimeOffset? ServedAt = null,
    [property: DataMember] QueryPagingMetadata? Paging = null,
    IReadOnlyList<string>? WarningCodes = null) {
    private string[]? _warningCodes = WarningCodes?.ToArray();

    /// <summary>
    /// Gets stable warning codes associated with the query result.
    /// </summary>
    [DataMember]
    public IReadOnlyList<string>? WarningCodes {
        get => _warningCodes;
        init => _warningCodes = value?.ToArray();
    }

    /// <summary>
    /// Gets the authoritative route that produced the response.
    /// </summary>
    [DataMember]
    public QueryResponseProvenance Provenance { get; init; } = QueryResponseProvenance.Unknown;

    /// <summary>
    /// Gets authoritative projection lifecycle evidence for projection-backed responses.
    /// </summary>
    [DataMember]
    public ProjectionLifecycleState Lifecycle { get; init; } = ProjectionLifecycleState.Unknown;
}
