namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Requests an existing per-event terminal batch receipt without invoking or repairing a handler.</summary>
/// <param name="Scope">The exact catalog-bound owner route.</param>
/// <param name="SequenceNumber">The admitted aggregate-local event sequence.</param>
/// <param name="MessageId">The original persisted event and per-event batch identity.</param>
/// <param name="OwnerIdentity">The selected owner identity.</param>
/// <param name="OwnerGeneration">The original selected generation.</param>
public sealed record ProjectionAppliedReceiptRequest(ProjectionOwnerProofRequest Scope, long SequenceNumber,
    string MessageId, string OwnerIdentity, string OwnerGeneration);
