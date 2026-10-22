using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>A readable fragment of one validated source range with raw immutable hashes.</summary>
internal sealed record ProjectionSourceGuardPage(EventEnvelope[] Stored, ProjectionEventDto[] Readable,
    IReadOnlyList<string> SourceEventDigests, long SourceBytes, long WireBytes)
{
    /// <summary>Counts the actual actor range once even when readability is streamed event by event.</summary>
    internal bool StartsRange { get; init; } = true;
}
