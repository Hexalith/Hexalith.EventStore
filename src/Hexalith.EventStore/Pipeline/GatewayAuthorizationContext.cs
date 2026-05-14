namespace Hexalith.EventStore.Pipeline;

internal sealed record GatewayAuthorizationContext(
    string Tenant,
    string Domain,
    string MessageType,
    string MessageCategory,
    string? AggregateId,
    string SubjectId);
