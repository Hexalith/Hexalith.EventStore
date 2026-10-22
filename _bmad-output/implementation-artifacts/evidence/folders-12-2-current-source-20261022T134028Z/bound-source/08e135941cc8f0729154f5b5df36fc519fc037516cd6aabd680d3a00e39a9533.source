
namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Thrown when a projection actor cannot be found for a query request.
/// Propagated to the controller exception handler to produce a 404 response.
/// </summary>
public class QueryNotFoundException : Exception {
    public QueryNotFoundException(string tenant, string domain, string aggregateId, string queryType)
        : base($"No projection found for {tenant}:{domain}:{aggregateId} (query type: {queryType})") {
        Tenant = tenant;
        Domain = domain;
        AggregateId = aggregateId;
        QueryType = queryType;
    }

    public QueryNotFoundException()
        : base() {
    }

    public QueryNotFoundException(string message)
        : base(message) {
    }

    public QueryNotFoundException(string message, Exception innerException)
        : base(message, innerException) {
    }

    public string Tenant { get; } = string.Empty;

    public string Domain { get; } = string.Empty;

    public string AggregateId { get; } = string.Empty;

    public string QueryType { get; } = string.Empty;
}
