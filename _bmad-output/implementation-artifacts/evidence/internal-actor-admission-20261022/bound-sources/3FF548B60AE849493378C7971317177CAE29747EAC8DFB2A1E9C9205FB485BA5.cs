namespace Hexalith.EventStore.Server.Projections;

/// <summary>Returns a bounded operator-facing reconciliation outcome.</summary>
/// <param name="Status">The reconciliation status.</param>
/// <param name="ReasonCode">The support-safe reason.</param>
/// <param name="PreservedSequence">The sequence preserved by the operation.</param>
internal sealed record ProjectionDeliveryReconciliationResult(
    ProjectionDeliveryReconciliationStatus Status,
    string ReasonCode,
    long PreservedSequence);
