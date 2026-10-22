namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Gates the first allocation in one append-race run and records allocation attempts as retry telemetry.
/// </summary>
public sealed class AppendDurabilityRaceSession : IDisposable
{
    private readonly AppendDurabilityRaceControl _owner;
    private readonly TaskCompletionSource _firstAllocationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _sync = new();
    private int _allocationAttempts;
    private int _armCalls;
    private int _armed;
    private int _disposed;
    private int _gateInterceptions;
    private string? _targetActorId;
    private string? _targetMessageId;
    private long _armedUtcTicks;
    private long _firstAllocationEnteredUtcTicks;
    private long _releasedUtcTicks;

    /// <summary>Initializes a race session owned by the fixture control.</summary>
    /// <param name="owner">The owning control.</param>
    /// <param name="sessionId">The evidence-safe session identifier.</param>
    internal AppendDurabilityRaceSession(AppendDurabilityRaceControl owner, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _owner = owner;
        SessionId = sessionId;
    }

    /// <summary>Gets the evidence-safe session identifier.</summary>
    public string SessionId { get; }

    /// <summary>Gets the number of allocations observed while this session was active.</summary>
    public int AllocationAttempts => Volatile.Read(ref _allocationAttempts);

    /// <summary>Gets the number of aggregate-handler arm calls for the intended command.</summary>
    public int ArmCalls => Volatile.Read(ref _armCalls);

    /// <summary>Gets the number of allocations actually held by the gate.</summary>
    public int GateInterceptions => Volatile.Read(ref _gateInterceptions);

    /// <summary>Gets when the aggregate-specific handler armed the allocation gate.</summary>
    public DateTimeOffset? ArmedAtUtc
        => ReadTimestamp(Volatile.Read(ref _armedUtcTicks));

    /// <summary>Gets the target aggregate actor id recorded by the arming handler.</summary>
    public string? TargetActorId
    {
        get
        {
            lock (_sync)
            {
                return _targetActorId;
            }
        }
    }

    /// <summary>Gets the target command message id recorded by the arming handler.</summary>
    public string? TargetMessageId
    {
        get
        {
            lock (_sync)
            {
                return _targetMessageId;
            }
        }
    }

    /// <summary>Gets when the first allocation reached the gate.</summary>
    public DateTimeOffset? FirstAllocationEnteredAtUtc
        => ReadTimestamp(Volatile.Read(ref _firstAllocationEnteredUtcTicks));

    /// <summary>Gets when the gate was released.</summary>
    public DateTimeOffset? ReleasedAtUtc
        => ReadTimestamp(Volatile.Read(ref _releasedUtcTicks));

    /// <summary>Waits until the actor reaches its first post-metadata-read allocation.</summary>
    /// <param name="cancellationToken">The bounded wait token.</param>
    /// <returns>A task that completes when the gate is occupied.</returns>
    public Task WaitForFirstAllocationAsync(CancellationToken cancellationToken)
        => _firstAllocationEntered.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// Arms the global allocator gate from the target aggregate handler after metadata rehydration.
    /// Repeated calls for the same target are recorded and remain idempotent.
    /// </summary>
    /// <param name="actorId">The intended aggregate actor id.</param>
    /// <param name="messageId">The intended command message id.</param>
    public void Arm(string actorId, string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        lock (_sync)
        {
            if (_targetActorId is not null
                && (!string.Equals(_targetActorId, actorId, StringComparison.Ordinal)
                    || !string.Equals(_targetMessageId, messageId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "An append-durability race session cannot be armed for more than one command.");
            }

            _targetActorId = actorId;
            _targetMessageId = messageId;
            _ = Interlocked.Increment(ref _armCalls);
            Interlocked.CompareExchange(
                ref _armedUtcTicks,
                DateTimeOffset.UtcNow.UtcTicks,
                comparand: 0);
            Volatile.Write(ref _armed, 1);
        }
    }

    /// <summary>Releases the gated allocation. Repeated calls are harmless.</summary>
    public void Release()
    {
        if (_release.TrySetResult())
        {
            Interlocked.CompareExchange(
                ref _releasedUtcTicks,
                DateTimeOffset.UtcNow.UtcTicks,
                comparand: 0);
        }
    }

    /// <summary>Releases the gate and unregisters this session.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Release();
        _owner.Complete(this);
    }

    /// <summary>Records an allocation and pauses the first one until the test releases it.</summary>
    /// <param name="cancellationToken">The actor allocation cancellation token.</param>
    /// <returns>A task representing the optional gate wait.</returns>
    internal async Task InterceptAllocationAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _armed) == 0)
        {
            return;
        }

        int attempt = Interlocked.Increment(ref _allocationAttempts);
        if (attempt != 1)
        {
            return;
        }

        _ = Interlocked.Increment(ref _gateInterceptions);
        Interlocked.CompareExchange(
            ref _firstAllocationEnteredUtcTicks,
            DateTimeOffset.UtcNow.UtcTicks,
            comparand: 0);
        _firstAllocationEntered.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset? ReadTimestamp(long utcTicks)
    {
        return utcTicks == 0 ? null : new DateTimeOffset(utcTicks, TimeSpan.Zero);
    }
}
