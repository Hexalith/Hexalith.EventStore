namespace Hexalith.EventStore.Client.Queries;

/// <summary>
/// Encodes and validates protected, opaque pagination cursors for domain queries.
/// </summary>
/// <remarks>
/// <para>
/// This is the platform generalization of the per-domain cursor codecs domain modules previously
/// hand-wrote (e.g. <c>TenantQueryCursorCodec</c>). A cursor protects a logical pagination
/// <c>position</c> together with the <c>queryType</c> and <c>scope</c> it was issued for, so a cursor
/// minted for one query/scope can never be replayed against another. The position is opaque to clients
/// and tamper-evident: it is sealed with ASP.NET Core Data Protection.
/// </para>
/// <para>
/// Domains supply only the scope fields — compose a stable, collision-safe scope string with
/// <see cref="QueryCursorScope"/> and pass it to <see cref="Encode"/>/<see cref="TryDecode"/>.
/// Cross-domain cryptographic isolation comes from the codec's Data Protection purpose, configured at
/// registration time (see <c>AddEventStoreQueryCursorCodec</c>).
/// </para>
/// </remarks>
public interface IQueryCursorCodec {
    /// <summary>
    /// Creates an opaque cursor for the specified query, scope, and logical position.
    /// </summary>
    /// <param name="queryType">Query type that owns the cursor.</param>
    /// <param name="scope">Endpoint scope that owns the cursor (compose with <see cref="QueryCursorScope"/>).</param>
    /// <param name="position">Logical pagination position to protect.</param>
    /// <returns>A protected cursor string safe to return to clients.</returns>
    string Encode(string queryType, string scope, string position);

    /// <summary>
    /// Validates and decodes an optional cursor for the expected query and scope.
    /// </summary>
    /// <param name="cursor">Protected cursor submitted by the client.</param>
    /// <param name="queryType">Expected query type.</param>
    /// <param name="scope">Expected endpoint scope.</param>
    /// <param name="position">Decoded logical position when validation succeeds.</param>
    /// <param name="failureReason">
    /// Short, log-safe reason code when validation fails (e.g. <c>"malformed"</c>,
    /// <c>"wrong-query-type"</c>, <c>"wrong-scope"</c>, <c>"wrong-version"</c>,
    /// <c>"empty-position"</c>, <c>"too-large"</c>, <c>"tamper-or-key-rotation"</c>).
    /// <see langword="null"/> on success or when <paramref name="cursor"/> is empty.
    /// </param>
    /// <returns><see langword="true"/> when the cursor is empty or valid; otherwise <see langword="false"/>.</returns>
    bool TryDecode(string? cursor, string queryType, string scope, out string? position, out string? failureReason);
}
