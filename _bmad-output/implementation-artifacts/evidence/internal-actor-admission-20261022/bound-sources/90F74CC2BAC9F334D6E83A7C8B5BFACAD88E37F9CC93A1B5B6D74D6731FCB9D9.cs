namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Controls the deterministic index-handler failure used by live v2 retry evidence.</summary>
public sealed class LiveNamedProjectionFaultControl {
    private int _detailInvocationCount;
    private int _indexInvocationCount;

    /// <summary>Gets or sets whether the index handler returns a retryable outcome.</summary>
    public bool FailIndex { get; set; }

    /// <summary>Gets the detail handler invocation count.</summary>
    public int DetailInvocationCount => Volatile.Read(ref _detailInvocationCount);

    /// <summary>Gets the index handler invocation count.</summary>
    public int IndexInvocationCount => Volatile.Read(ref _indexInvocationCount);

    /// <summary>Records a detail handler invocation.</summary>
    public void RecordDetailInvocation() => _ = Interlocked.Increment(ref _detailInvocationCount);

    /// <summary>Records an index handler invocation.</summary>
    public void RecordIndexInvocation() => _ = Interlocked.Increment(ref _indexInvocationCount);

    /// <summary>Clears deterministic failure and invocation evidence.</summary>
    public void Reset() {
        FailIndex = false;
        _ = Interlocked.Exchange(ref _detailInvocationCount, 0);
        _ = Interlocked.Exchange(ref _indexInvocationCount, 0);
    }
}
