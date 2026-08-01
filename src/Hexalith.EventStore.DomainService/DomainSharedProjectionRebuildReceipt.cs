namespace Hexalith.EventStore.DomainService;

/// <summary>Durable exact-retry receipt for one accepted authoritative inventory entry.</summary>
/// <param name="Ordinal">The zero-based inventory ordinal.</param>
/// <param name="HistoryFingerprint">The aggregate identity, erased state, and history fingerprint.</param>
internal sealed record DomainSharedProjectionRebuildReceipt(long Ordinal, string HistoryFingerprint);
