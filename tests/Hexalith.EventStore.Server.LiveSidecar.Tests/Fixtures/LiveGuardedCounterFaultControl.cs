namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Counts actual domain effects and injects a lost response after one durable event batch.</summary>
public sealed class LiveGuardedCounterFaultControl
{
    private int _applications;
    private int _preparations;
    private long _failAfterSequence;

    /// <summary>Gets actual event handler invocations.</summary>
    public int Applications => Volatile.Read(ref _applications);

    /// <summary>Gets complete-prefix preparation invocations.</summary>
    public int Preparations => Volatile.Read(ref _preparations);

    /// <summary>Sets one sequence whose committed event response should be lost.</summary>
    public void LoseResponseAfter(long sequence) => Interlocked.Exchange(ref _failAfterSequence, sequence);

    /// <summary>Records the next application.</summary>
    internal void RecordApplication() => Interlocked.Increment(ref _applications);

    /// <summary>Records the next complete-prefix preparation.</summary>
    internal void RecordPreparation() => Interlocked.Increment(ref _preparations);

    /// <summary>Fails once after the selected event's receipt is durable.</summary>
    internal void AfterCommit(long sequence)
    {
        if (Interlocked.CompareExchange(ref _failAfterSequence, 0, sequence) == sequence)
            throw new IOException("Fixture lost the committed event response.");
    }
}
