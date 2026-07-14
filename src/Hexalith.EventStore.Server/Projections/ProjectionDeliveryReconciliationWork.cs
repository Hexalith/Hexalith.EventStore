namespace Hexalith.EventStore.Server.Projections;

/// <summary>Payload-free operator work evidence for a fail-closed projection delivery scope.</summary>
/// <param name="TenantId">The tenant scope.</param>
/// <param name="Domain">The aggregate domain.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="ProjectionName">The named projection route.</param>
/// <param name="ReasonCode">The bounded support-safe reason.</param>
/// <param name="ObservedSequence">The triggering aggregate-local sequence.</param>
/// <param name="DeliveryStateVersion">The observed delivery row schema version.</param>
/// <param name="OperatorId">The attributable operator, when reconciliation has run.</param>
/// <param name="RecordedAt">The UTC evidence time.</param>
internal sealed record ProjectionDeliveryReconciliationWork(
    string TenantId,
    string Domain,
    string AggregateId,
    string ProjectionName,
    string ReasonCode,
    long ObservedSequence,
    int DeliveryStateVersion,
    string? OperatorId,
    DateTimeOffset RecordedAt);
