namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Independent erasure observation. Partial transport stays unknown, and an effective or unknown pin blocks destruction until that same hold is released.
/// </summary>
/// <param name="ManifestDigest">Digest of the retained binding, from <see cref="InteractionProtectionBindingProvider.ManifestDigest"/>.</param>
/// <param name="OrderDigest">Digest of the admitted target order and original receipt order, from <see cref="InteractionProtectionBindingProvider.ReceiptOrderDigest"/>.</param>
/// <param name="AdmittedTargets">Admitted target ids in their original order. The adapter does not sort them.</param>
/// <param name="OriginalReceipts">Original ordered receipts, one per admitted target.</param>
/// <param name="TransportKnown">Whether the transport result is known. Unknown stays denied.</param>
/// <param name="Partial">Whether the transport result covered only part of the manifest.</param>
/// <param name="EffectiveHoldId">Effective pin that still blocks destruction, or null when none is held.</param>
/// <param name="UnknownPin">Whether a pin of unknown disposition blocks destruction.</param>
/// <param name="UnpinnedHoldId">Hold id released by unpin. A different hold does not release <paramref name="EffectiveHoldId"/>.</param>
public sealed record InteractionProtectionErasureWitness(
    string ManifestDigest,
    string OrderDigest,
    IReadOnlyList<string> AdmittedTargets,
    IReadOnlyList<string> OriginalReceipts,
    bool TransportKnown,
    bool Partial,
    string? EffectiveHoldId,
    bool UnknownPin,
    string? UnpinnedHoldId);
