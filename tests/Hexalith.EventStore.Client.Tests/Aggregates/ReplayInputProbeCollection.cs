using System.Collections;

using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Reports a source count without allocating an oversized fixture array.</summary>
internal sealed class ReplayInputProbeCollection(int reportedCount, ReplayEventEnvelope? item = null) : IReadOnlyList<ReplayEventEnvelope>
{
    /// <summary>Gets the number of source count observations.</summary>
    internal int CountReads { get; private set; }

    /// <summary>Gets the number of event reference reads.</summary>
    internal int IndexReads { get; private set; }

    /// <inheritdoc/>
    public int Count { get { CountReads++; return reportedCount; } }

    /// <inheritdoc/>
    public ReplayEventEnvelope this[int index]
    {
        get { IndexReads++; return item ?? throw new InvalidOperationException("Oversized source was read."); }
    }

    /// <inheritdoc/>
    public IEnumerator<ReplayEventEnvelope> GetEnumerator() => throw new InvalidOperationException("Source was enumerated.");

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
