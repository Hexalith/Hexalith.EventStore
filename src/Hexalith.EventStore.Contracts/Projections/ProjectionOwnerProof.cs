namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Read-only proof of selected output and its existing immutable completed receipt.</summary>
/// <param name="Selection">The persisted owner, progress, generation and freshness binding.</param>
/// <param name="ReceiptIdentity">The opaque existing receipt identity.</param>
/// <param name="ReceiptFingerprint">The immutable manifest fingerprint.</param>
public sealed record ProjectionOwnerProof(ProjectionOwnerSelection Selection, string ReceiptIdentity, string ReceiptFingerprint);
