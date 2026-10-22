using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Records full-prefix preparations and loses one already-durable response at each selected boundary.</summary>
public sealed class LiveReplayCounterFaultControl
{
    private readonly HashSet<DomainProjectionRebuildBatchAction> _lose = [];
    private int _preparations;

    internal int Preparations => Volatile.Read(ref _preparations);

    internal void RecordPreparation() => Interlocked.Increment(ref _preparations);

    internal void LoseOnce(DomainProjectionRebuildBatchAction action) { lock (_lose) _lose.Add(action); }

    internal bool Consume(DomainProjectionRebuildBatchAction action) { lock (_lose) return _lose.Remove(action); }
}
