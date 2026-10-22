using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>One durably ordered discovery entry; retries reuse its offset.</summary>
/// <param name="Offset">The monotonic index offset, independent of global-position reservations.</param>
/// <param name="Publication">The immutable source-qualified descriptor.</param>
public sealed record SourcePublicationIndexEntry(long Offset, SourcePublicationDescriptor Publication);
