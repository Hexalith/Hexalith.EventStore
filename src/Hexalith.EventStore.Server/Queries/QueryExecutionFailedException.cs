namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Thrown when a projection query fails with a known HTTP-facing outcome.
/// </summary>
public sealed class QueryExecutionFailedException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExecutionFailedException"/> class.
    /// </summary>
    public QueryExecutionFailedException(
        string correlationId,
        string tenant,
        string domain,
        string aggregateId,
        string queryType,
        int statusCode,
        string detail)
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
    }

    public string CorrelationId { get; }

    public string Tenant { get; }

    public string Domain { get; }

    public string AggregateId { get; }

    public string QueryType { get; }

    public int StatusCode { get; }

    public string Detail { get; }
}
