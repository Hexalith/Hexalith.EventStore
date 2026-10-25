namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>An immutable original authority-journal record.</summary>
/// <param name="Key">The exact recovery identity.</param><param name="Body">The canonical retained original bytes.</param>
public sealed record SourcePublicationAuthorityRecord(string Key, byte[] Body);
