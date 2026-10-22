namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>One exact conditional index update; lost acknowledgement is reconciled through exact state read.</summary>
/// <param name="ExpectedRevision">The previous persisted index revision, zero for NoStream.</param>
/// <param name="State">The exact next revision and immutable reference state.</param>
public sealed record SourcePublicationIndexWrite(long ExpectedRevision, SourcePublicationIndexState State);
