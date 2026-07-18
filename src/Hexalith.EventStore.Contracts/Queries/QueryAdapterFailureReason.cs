namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Coarse projection adapter failure categories shared by EventStore and downstream query actors.
/// </summary>
/// <remarks>
/// These values identify adapter-edge failures without exposing actor exception details,
/// payload bytes, or protected data. Story 22.4 owns final HTTP ProblemDetails type URI
/// and reason-code taxonomy.
/// </remarks>
public static class QueryAdapterFailureReason {
    /// <summary>
    /// The actor returned success without payload bytes.
    /// </summary>
    public const string MissingPayload = "missing-payload";

    /// <summary>
    /// The query envelope was malformed or incompatible.
    /// </summary>
    public const string InvalidEnvelope = "invalid-envelope";

    /// <summary>
    /// The supplied pagination cursor was malformed, expired, or scoped to a different query.
    /// Maps to an HTTP 400 so clients can distinguish a bad cursor from an internal failure.
    /// </summary>
    public const string InvalidCursor = "invalid-cursor";

    /// <summary>
    /// The actor response shape was null or otherwise incompatible.
    /// </summary>
    public const string ActorResponseMismatch = "actor-response-mismatch";

    /// <summary>
    /// The actor does not support the query type.
    /// </summary>
    public const string UnsupportedQueryType = "unsupported-query-type";

    /// <summary>
    /// The actor payload could not be serialized or deserialized as the public adapter wire shape.
    /// </summary>
    public const string SerializationFailure = "serialization-failure";

    /// <summary>
    /// Actor invocation failed before a valid adapter response was returned.
    /// </summary>
    public const string ActorException = "actor-exception";

    /// <summary>
    /// The query type is unknown to the projection adapter.
    /// </summary>
    public const string UnknownQueryType = "unknown-query-type";

    /// <summary>
    /// The actor runtime reported missing actor registration or address information.
    /// </summary>
    public const string ActorNotFoundInfrastructure = "actor-not-found-infrastructure";

    /// <summary>
    /// The actor returned a 403 Forbidden response; the caller is not permitted to execute this query.
    /// </summary>
    public const string Forbidden = "Forbidden";

    /// <summary>
    /// Internal-only diagnostic marker recorded when the opt-in safe-denial adapter unified a
    /// <see cref="Forbidden"/> result into the shared not-found shape for a route that has opted
    /// into the safe-denial boundary. Never surfaced on the wire response — the caller-visible
    /// query router result for a safe-denied query carries the exact same shape as a genuine
    /// not-found result (no error message). This constant exists solely so server-side logs can
    /// still distinguish a policy-denied outcome from a genuinely nonexistent resource.
    /// </summary>
    public const string SafeDenialForbidden = "safe-denial-forbidden";

    /// <summary>
    /// Internal-only diagnostic marker recorded when the opt-in safe-denial adapter unified the
    /// second existing not-found shape -- a projection actor's "no projection state available"
    /// failure message (see <see cref="MissingProjectionState"/>) -- into the shared not-found
    /// shape for a route that has opted into the safe-denial boundary. Never surfaced on the wire
    /// response, for the same reason as <see cref="SafeDenialForbidden"/>.
    /// </summary>
    public const string SafeDenialMissingProjectionState = "safe-denial-missing-projection-state";

    /// <summary>
    /// The second existing not-found shape: a projection actor reports success=false with this
    /// exact error message (rather than the router result's hard not-found flag set to
    /// <see langword="true"/>) when no projection state has ever been built for the target
    /// aggregate. Recognized by the opt-in safe-denial adapter so both genuine not-found shapes
    /// are unified identically to a safe-denied Forbidden result.
    /// </summary>
    public const string MissingProjectionState = "No projection state available for this aggregate";
}
