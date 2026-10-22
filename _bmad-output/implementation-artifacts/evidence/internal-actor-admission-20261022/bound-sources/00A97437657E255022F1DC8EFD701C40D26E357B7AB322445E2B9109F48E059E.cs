namespace Hexalith.EventStore.Client.Projections;

/// <summary>Structured evidence returned by marker-gated rebuild staging operations.</summary>
/// <param name="Status">The proven durable staging state.</param>
/// <param name="Fingerprint">The canonical manifest fingerprint.</param>
/// <param name="Reason">An optional bounded recovery or conflict reason.</param>
public sealed record ReadModelBatchStagingResult(
    ReadModelBatchStagingStatus Status,
    string Fingerprint,
    string? Reason = null);
