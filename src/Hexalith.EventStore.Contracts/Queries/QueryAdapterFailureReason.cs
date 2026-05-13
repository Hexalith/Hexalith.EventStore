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
}
