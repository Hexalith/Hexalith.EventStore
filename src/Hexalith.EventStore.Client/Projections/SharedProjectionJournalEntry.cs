namespace Hexalith.EventStore.Client.Projections;

internal sealed record SharedProjectionJournalEntry(
    string SourceStream,
    long EnvelopePosition,
    string Digest,
    byte[] CanonicalPayload,
    byte[] Checkpoint,
    SharedProjectionMutation[]? Prepared,
    SharedProjectionControlIndexIntent? PreparedControlIndexIntent = null,
    SharedProjectionFailureStatus? Failure = null,
    bool Parked = false,
    SharedProjectionChunkReference? PayloadChunks = null,
    bool PayloadReady = true);
