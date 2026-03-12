
namespace Hexalith.EventStore.Contracts.Validation;

public record ValidateQueryRequest(
    string Tenant,
    string Domain,
    string QueryType,
    string? AggregateId = null);
