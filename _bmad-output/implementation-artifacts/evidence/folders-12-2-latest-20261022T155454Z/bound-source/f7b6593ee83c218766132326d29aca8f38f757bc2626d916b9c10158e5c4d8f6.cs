namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Bounded consecutive original references proposed for independent durable acknowledgement verification; caller statuses never prove acknowledgement.</summary>
/// <param name="Cut">Fresh complete reconciled index cut.</param><param name="ExpectedPrefix">Exact independently authenticated predecessor prefix, or zero after unavailable/changed proof.</param>
/// <param name="AcknowledgedEntries">At most one bounded page of consecutive originals; the authority independently authenticates their actual durable source acknowledgements.</param>
public sealed record SourcePublicationDispatchAdvance(SourcePublicationCheckpoint Cut, long ExpectedPrefix, IReadOnlyList<SourcePublicationIndexEntry> AcknowledgedEntries);
