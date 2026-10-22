namespace Hexalith.EventStore.Client.Streams;

/// <summary>A reconstructed gap-free acknowledged prefix, never a separately stored delivery ledger.</summary>
/// <param name="AcknowledgedPrefix">Largest consecutively authenticated acknowledged immutable offset in this pass.</param>
/// <param name="IsComplete">Whether the entire observed finite index cut was acknowledged.</param>
/// <param name="FailureReason">An unresolved source/receiver/authority/bound reason, or null on completion.</param>
public sealed record SourcePublicationDispatchResult(long AcknowledgedPrefix, bool IsComplete, string? FailureReason);
