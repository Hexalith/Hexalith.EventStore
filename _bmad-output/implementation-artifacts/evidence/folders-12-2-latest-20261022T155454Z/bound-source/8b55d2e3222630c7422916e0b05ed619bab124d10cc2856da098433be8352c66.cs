namespace Hexalith.EventStore.Client.Projections;

/// <summary>Read-only per-source fold evidence; this result does not establish whole-family freshness or lifecycle completion.</summary>
/// <param name="IsApplied">Whether the exact source delivery has a durable successful fold receipt in the observed generation.</param>
/// <param name="ReasonCode">A bounded failure classification.</param>
/// <param name="Epoch">The observed writer fence epoch.</param>
/// <param name="Generation">The observed selected generation.</param>
/// <param name="ReceiptIdentity">The opaque existing receipt identity, absent when unproven.</param>
/// <param name="ReceiptFingerprint">The exact existing source delivery digest, absent when unproven.</param>
public sealed record SharedProjectionAppliedDeliveryProof(bool IsApplied, string? ReasonCode, long Epoch,
    long Generation, string? ReceiptIdentity, string? ReceiptFingerprint);
