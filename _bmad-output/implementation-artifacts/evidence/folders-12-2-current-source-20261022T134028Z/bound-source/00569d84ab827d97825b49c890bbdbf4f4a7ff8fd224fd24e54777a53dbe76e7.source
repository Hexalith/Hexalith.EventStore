using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>An ordered page with its exact finite-cut reconciliation checkpoint.</summary>
/// <param name="Checkpoint">The persisted source-qualified cut.</param>
/// <param name="Entries">The immutable ordered page.</param>
/// <param name="NextOffset">The exclusive cursor for the next page.</param>
/// <param name="HasMore">Whether more entries exist through this same captured checkpoint.</param>
public sealed record SourcePublicationPage(SourcePublicationCheckpoint Checkpoint, IReadOnlyList<SourcePublicationIndexEntry> Entries, long NextOffset, bool HasMore);
