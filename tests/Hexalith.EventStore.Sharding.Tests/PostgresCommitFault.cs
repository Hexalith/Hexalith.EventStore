namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Counts actual matching protocol observations and holds one explicit fault gate.</summary>
/// <param name="marker">The exact unique PostgreSQL NOTICE message.</param>
/// <param name="boundary">The selected COMMIT boundary.</param>
internal sealed class PostgresCommitFault(string marker, PostgresCommitBoundary boundary)
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _break = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _notices;
    private int _commits;
    private int _forwarded;
    private int _completed;
    private int _gates;

    /// <summary>Gets the unique exact-key marker.</summary>
    internal string Marker => marker;

    /// <summary>Gets the selected boundary.</summary>
    internal PostgresCommitBoundary Boundary => boundary;

    /// <summary>Gets the count of matching provider write notices, including retries.</summary>
    internal int Notices => Volatile.Read(ref _notices);

    /// <summary>Gets the count of matching frontend COMMIT messages.</summary>
    internal int Commits => Volatile.Read(ref _commits);

    /// <summary>Gets matching COMMIT messages forwarded to the provider.</summary>
    internal int ForwardedCommits => Volatile.Read(ref _forwarded);

    /// <summary>Gets matching backend COMMIT completions, including withheld acknowledgements.</summary>
    internal int CompletedCommits => Volatile.Read(ref _completed);

    /// <summary>Gets actual gates reached.</summary>
    internal int Gates => Volatile.Read(ref _gates);

    /// <summary>Gets whether the explicit gate has actually signaled readiness.</summary>
    internal bool Reached => _reached.Task.IsCompletedSuccessfully;

    /// <summary>Observes the explicit boundary; cancellation never manufactures readiness.</summary>
    internal Task WaitAsync(CancellationToken token) => _reached.Task.WaitAsync(token);

    /// <summary>Explicitly breaks the selected connection at its observed boundary.</summary>
    internal void BreakConnection()
    {
        if (!_reached.Task.IsCompletedSuccessfully) throw new InvalidOperationException("No provider boundary has been observed.");
        _break.TrySetResult();
    }

    /// <summary>Records every exact provider notice.</summary>
    internal void ObserveNotice() => Interlocked.Increment(ref _notices);

    /// <summary>Records every matching COMMIT attempt.</summary>
    internal void ObserveCommit() => Interlocked.Increment(ref _commits);

    /// <summary>Records every matching COMMIT forwarded to PostgreSQL.</summary>
    internal void ObserveForwardedCommit() => Interlocked.Increment(ref _forwarded);

    /// <summary>Records every matching PostgreSQL COMMIT completion.</summary>
    internal void ObserveCompletedCommit() => Interlocked.Increment(ref _completed);

    /// <summary>Holds the first boundary until an explicit break, or teardown cancellation.</summary>
    internal async Task GateAsync(CancellationToken token)
    {
        if (Interlocked.Increment(ref _gates) != 1) return; // Retain unexpected retries in the counts.
        _reached.TrySetResult();
        await _break.Task.WaitAsync(token);
        throw new PostgresFaultDisconnectException();
    }
}
