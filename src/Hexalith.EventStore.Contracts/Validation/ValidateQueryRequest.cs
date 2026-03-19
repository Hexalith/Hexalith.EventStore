
namespace Hexalith.EventStore.Contracts.Validation;

/// <summary>
/// Represents a request to validate whether a query is authorized for submission.
/// </summary>
/// <param name="Tenant">The tenant identifier for multi-tenant isolation.</param>
/// <param name="Domain">The domain name the query targets.</param>
/// <param name="QueryType">The type discriminator of the query.</param>
/// <param name="AggregateId">Optional aggregate identifier for instance-level authorization.</param>
public record ValidateQueryRequest(
    string Tenant,
    string Domain,
    string QueryType,
    string? AggregateId = null);
