namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Stable public reason codes for non-auth query ProblemDetails responses.
/// </summary>
public static class QueryProblemReasonCodes {
    /// <summary>
    /// Indicates that the request contains malformed or unknown query policy input.
    /// </summary>
    public const string MalformedRequest = "query_malformed_request";

    /// <summary>
    /// Indicates that the requested paging policy is invalid or unsupported.
    /// </summary>
    public const string InvalidPage = "query_invalid_page";

    /// <summary>
    /// Indicates that the request used a filter policy that is not supported.
    /// </summary>
    public const string UnsupportedFilter = "query_unsupported_filter";

    /// <summary>
    /// Indicates that the request used a search policy that is not supported.
    /// </summary>
    public const string UnsupportedSearch = "query_unsupported_search";

    /// <summary>
    /// Indicates that the request used an order policy that is not supported.
    /// </summary>
    public const string UnsupportedOrder = "query_unsupported_order";

    /// <summary>
    /// Indicates that the requested projection could not be found.
    /// </summary>
    public const string ProjectionMissing = "query_projection_missing";

    /// <summary>
    /// Indicates that the requested projection could not satisfy the freshness policy.
    /// </summary>
    public const string ProjectionStale = "query_projection_stale";

    /// <summary>
    /// Indicates that search was served in a degraded mode.
    /// </summary>
    public const string DegradedSearch = "query_degraded_search";

    /// <summary>
    /// Indicates that a projection returned a malformed response.
    /// </summary>
    public const string MalformedProjectionResponse = "query_malformed_projection_response";

    /// <summary>
    /// Indicates that the projection did not respond before the timeout.
    /// </summary>
    public const string ProjectionTimeout = "query_projection_timeout";

    /// <summary>
    /// Indicates that the query type or projection behavior is not implemented.
    /// </summary>
    public const string NotImplemented = "query_not_implemented";

    /// <summary>
    /// Indicates that the query failed with an internal server error.
    /// </summary>
    public const string InternalError = "query_internal_error";
}
