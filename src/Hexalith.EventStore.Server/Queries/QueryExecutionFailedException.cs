namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Thrown when a projection query fails with a known HTTP-facing outcome.
/// </summary>
public sealed class QueryExecutionFailedException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExecutionFailedException"/> class.
    /// </summary>
    /// <param name="correlationId">The query correlation identifier.</param>
    /// <param name="tenant">The tenant identifier associated with the failed query.</param>
    /// <param name="domain">The domain associated with the failed query.</param>
    /// <param name="aggregateId">The aggregate identifier associated with the failed query.</param>
    /// <param name="queryType">The query type associated with the failure.</param>
    /// <param name="statusCode">The HTTP status code to expose for the failure.</param>
    /// <param name="detail">The sanitized failure detail.</param>
    /// <param name="reasonCode">The stable query reason code to expose in ProblemDetails.</param>
    public QueryExecutionFailedException(
        string correlationId,
        string tenant,
        string domain,
        string aggregateId,
        string queryType,
        int statusCode,
        string detail,
        string? reasonCode = null)
        : base(detail) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);

        CorrelationId = correlationId;
        Tenant = tenant;
        Domain = domain;
        AggregateId = aggregateId;
        QueryType = queryType;
        StatusCode = statusCode;
        Detail = detail;
        ReasonCode = reasonCode;
    }

    /// <summary>
    /// Gets the query correlation identifier.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Gets the tenant identifier associated with the failed query.
    /// </summary>
    public string Tenant { get; }

    /// <summary>
    /// Gets the domain associated with the failed query.
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// Gets the aggregate identifier associated with the failed query.
    /// </summary>
    public string AggregateId { get; }

    /// <summary>
    /// Gets the query type associated with the failure.
    /// </summary>
    public string QueryType { get; }

    /// <summary>
    /// Gets the HTTP status code to expose for the failure.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the sanitized failure detail.
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Gets the stable query reason code to expose in ProblemDetails.
    /// </summary>
    public string? ReasonCode { get; }
}
