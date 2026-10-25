namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>The journal-owned conditional state anchor.</summary>
/// <param name="Revision">The committed primary revision.</param><param name="Digest">The exact canonical primary state digest.</param>
public sealed record SourcePublicationAuthorityAnchor(long Revision, string Digest);
