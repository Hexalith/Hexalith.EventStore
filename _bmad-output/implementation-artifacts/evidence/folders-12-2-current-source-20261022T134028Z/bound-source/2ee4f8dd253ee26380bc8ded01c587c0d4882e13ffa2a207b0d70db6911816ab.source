namespace Hexalith.EventStore.Queries;

/// <summary>
/// Tells the query pipeline whether a domain serves a given query type via an <c>IDomainQueryHandler</c>
/// (the domain-service <c>/query</c> endpoint), as advertised in the operational-index metadata. Used to
/// decide capability-declared routing: handler-based queries go to the domain's <c>/query</c> endpoint;
/// everything else uses the platform projection-actor path.
/// </summary>
public interface IDomainQueryHandlerRegistry {
    /// <summary>
    /// Determines whether the specified domain serves the specified query type via a domain query handler.
    /// </summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <param name="queryType">The query type discriminator.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><c>true</c> if the domain advertises a handler for the query type; otherwise <c>false</c>.</returns>
    Task<bool> SupportsQueryAsync(string domain, string queryType, CancellationToken cancellationToken = default);
}
