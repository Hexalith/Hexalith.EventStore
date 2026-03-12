
namespace Hexalith.EventStore.Contracts.Validation;

public record ValidateCommandRequest(
    string Tenant,
    string Domain,
    string CommandType,
    string? AggregateId = null);
