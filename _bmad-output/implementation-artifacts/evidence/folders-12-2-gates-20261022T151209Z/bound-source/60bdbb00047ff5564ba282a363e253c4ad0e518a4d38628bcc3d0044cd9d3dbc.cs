
namespace Hexalith.EventStore.Contracts.Validation;

/// <summary>
/// Represents a request to validate whether a command is authorized for submission.
/// </summary>
/// <param name="Tenant">The tenant identifier for multi-tenant isolation.</param>
/// <param name="Domain">The domain name the command targets.</param>
/// <param name="CommandType">The type discriminator of the command.</param>
/// <param name="AggregateId">Optional aggregate identifier for instance-level authorization.</param>
public record ValidateCommandRequest(
    string Tenant,
    string Domain,
    string CommandType,
    string? AggregateId = null);
