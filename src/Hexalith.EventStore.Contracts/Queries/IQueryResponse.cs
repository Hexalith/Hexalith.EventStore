
namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Defines the response contract for query projections with mandatory projection type metadata.
/// Implement this interface on query response types to enforce compile-time safety
/// for projection type identification (FR62).
/// </summary>
/// <typeparam name="T">The projection data type. Covariant to allow
/// <c>IQueryResponse&lt;DerivedType&gt;</c> to be assigned to <c>IQueryResponse&lt;BaseType&gt;</c>.</typeparam>
public interface IQueryResponse<out T> {
    /// <summary>
    /// Gets the projection data returned by the query.
    /// </summary>
    T Data { get; }

    /// <summary>
    /// Gets the projection type name used for ETag scope.
    /// Use short kebab-case names (e.g., <c>counter</c>, <c>order-list</c>) — they are
    /// base64url-encoded in self-routing ETags, so longer names produce longer HTTP header values (FR64).
    /// Must not contain <c>:</c> (colon — actor ID separator) or exceed 100 characters.
    /// </summary>
    string ProjectionType { get; }
}
