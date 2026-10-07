using System.Collections;

using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Detects source access before complete-array count admission.</summary>
internal sealed class ReplayAdmissionProbeCollection(int count, Action? onCount = null) : IReadOnlyList<ReplayEventEnvelope>
{
    internal int CountReads { get; private set; }
    internal int IndexReads { get; private set; }

    public int Count
    {
        get { CountReads++; onCount?.Invoke(); return count; }
    }

    public ReplayEventEnvelope this[int index]
    {
        get { IndexReads++; throw new InvalidOperationException("Source index must not be read."); }
    }

    public IEnumerator<ReplayEventEnvelope> GetEnumerator()
        => throw new InvalidOperationException("Source must not be enumerated.");

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
