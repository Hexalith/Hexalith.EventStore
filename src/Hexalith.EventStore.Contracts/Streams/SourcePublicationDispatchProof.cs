namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Independently retained immutable exact receipt chain, separate from the primary index checkpoint.</summary>
/// <param name="Progress">The stable original prefix proof.</param><param name="ExpectedPrefix">The original consecutive predecessor.</param>
/// <param name="PredecessorReceiptId">The retained predecessor proof, or null for prefix zero.</param>
/// <param name="Receipts">Actual independently matched source/receiver original acknowledgements for this bounded advance.</param>
public sealed record SourcePublicationDispatchProof(SourcePublicationDispatchProgress Progress, long ExpectedPrefix,
    string? PredecessorReceiptId, IReadOnlyList<SourcePublicationAcknowledgement> Receipts);
