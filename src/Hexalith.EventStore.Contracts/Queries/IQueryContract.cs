
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Defines mandatory query metadata as typed static members.
/// Implement this interface on query contract classes to get compile-time
/// safety for query routing metadata (FR57).
/// </summary>
public interface IQueryContract
{
    /// <summary>
    /// Gets the query type name used for actor ID routing (first segment).
    /// Must be kebab-case, no colons (reserved as actor ID separator).
    /// Example: "get-counter-status".
    /// </summary>
    static abstract string QueryType { get; }

    /// <summary>
    /// Gets the owning domain name (kebab-case).
    /// Example: "counter".
    /// </summary>
    static abstract string Domain { get; }

    /// <summary>
    /// Gets the projection type for ETag scope.
    /// Used to derive ETag actor ID: {ProjectionType}:{TenantId}.
    /// Often equals Domain, but can differ for cross-domain queries.
    /// Example: "counter".
    /// </summary>
    static abstract string ProjectionType { get; }
}
