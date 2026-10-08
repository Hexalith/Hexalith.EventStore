using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Owns one verified current payload separately from the logical proof carrier.</summary>
/// <param name="Sequence">The exact signed source sequence.</param>
/// <param name="CanonicalType">The admitted current event contract identity.</param>
/// <param name="Version">The admitted current payload version.</param>
/// <param name="Format">The admitted current readable format.</param>
/// <param name="Payload">The privately owned effective payload.</param>
internal sealed record PrivateLogicalReplayEvent(long Sequence, string CanonicalType, int Version, string Format,
    ImmutablePayload Payload) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => Payload.Dispose();
}
