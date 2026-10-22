using Dapr.Actors.Runtime;

using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Adds deterministic tracing, before-delegate and after-delegate failures, and concurrent-winner
/// injection around the pending/committed semantics of <see cref="InMemoryStateManager"/>.
/// </summary>
internal sealed class FaultInjectingActorStateManager : IActorStateManager
{
    private readonly Dictionary<(string Operation, int CallNumber), Func<FaultInjectingActorStateManager, Task>>
        _beforeActions = [];
    private readonly Dictionary<(string Operation, int CallNumber), Exception> _afterFaults = [];
    private readonly Dictionary<(string Operation, int CallNumber), Exception> _beforeFaults = [];
    private readonly Dictionary<string, int> _callCounts = new(StringComparer.Ordinal);
    private readonly InMemoryStateManager _inner = new();

    /// <summary>Gets the ordered state-manager operation trace.</summary>
    internal List<string> Trace { get; } = [];

    /// <summary>Gets immutable snapshots captured after successful commits and winner injections.</summary>
    internal List<IReadOnlyDictionary<string, object>> CommittedSnapshots { get; } = [];

    /// <summary>Gets the currently committed state without exposing pending operations.</summary>
    internal IReadOnlyDictionary<string, object> CommittedState => _inner.CommittedState;

    /// <summary>Returns a fresh committed-state view, modeling a newly loaded observer.</summary>
    internal IReadOnlyDictionary<string, object> CreateCommittedView()
        => new Dictionary<string, object>(_inner.CommittedState, StringComparer.Ordinal);

    /// <summary>Schedules an exception before one exact operation occurrence delegates.</summary>
    internal void FaultOnCall(
        string operation,
        int callNumber,
        Exception exception,
        Func<FaultInjectingActorStateManager, Task>? beforeThrow = null)
    {
        ValidateFault(operation, callNumber, exception);
        _beforeFaults[(operation, callNumber)] = exception;
        if (beforeThrow is not null)
        {
            _beforeActions[(operation, callNumber)] = beforeThrow;
        }
    }

    /// <summary>
    /// Schedules an exception after one exact operation occurrence delegates. For
    /// <c>SaveState</c>, this models a commit that succeeded durably before the caller saw failure.
    /// </summary>
    internal void FaultAfterCall(string operation, int callNumber, Exception exception)
    {
        ValidateFault(operation, callNumber, exception);
        _afterFaults[(operation, callNumber)] = exception;
    }

    /// <summary>Schedules a deterministic action immediately before one operation delegates.</summary>
    internal void ActBeforeCall(
        string operation,
        int callNumber,
        Func<FaultInjectingActorStateManager, Task> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(callNumber);
        ArgumentNullException.ThrowIfNull(action);
        _beforeActions[(operation, callNumber)] = action;
    }

    /// <summary>Replaces pending attempt state with state committed by a concurrent winner.</summary>
    internal async Task InjectConcurrentWinnerAsync(IReadOnlyDictionary<string, object> winnerState)
    {
        ArgumentNullException.ThrowIfNull(winnerState);
        await _inner.ClearCacheAsync().ConfigureAwait(false);
        foreach ((string key, object value) in winnerState)
        {
            await _inner.SetStateAsync(key, value).ConfigureAwait(false);
        }

        await _inner.SaveStateAsync().ConfigureAwait(false);
        Trace.Add("ConcurrentWinner");
        CaptureCommittedSnapshot();
    }

    /// <summary>Seeds committed state without adding setup calls to the operation trace.</summary>
    internal async Task SeedCommittedStateAsync(IReadOnlyDictionary<string, object> state)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _inner.ClearCacheAsync().ConfigureAwait(false);
        foreach ((string key, object value) in state)
        {
            await _inner.SetStateAsync(key, value).ConfigureAwait(false);
        }

        await _inner.SaveStateAsync().ConfigureAwait(false);
        Trace.Clear();
        CommittedSnapshots.Clear();
        _callCounts.Clear();
    }

    /// <inheritdoc/>
    public Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        => ExecuteAsync($"AddState:{stateName}", () => _inner.AddStateAsync(stateName, value, cancellationToken));

    /// <inheritdoc/>
    public Task AddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => ExecuteAsync($"AddState:{stateName}", () => _inner.AddStateAsync(stateName, value, ttl, cancellationToken));

    /// <inheritdoc/>
    public Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, CancellationToken cancellationToken = default)
        => ExecuteAsync($"AddOrUpdateState:{stateName}", () => _inner.AddOrUpdateStateAsync(stateName, addValue, updateValueFactory, cancellationToken));

    /// <inheritdoc/>
    public Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, TimeSpan ttl, CancellationToken cancellationToken = default)
        => ExecuteAsync($"AddOrUpdateState:{stateName}", () => _inner.AddOrUpdateStateAsync(stateName, addValue, updateValueFactory, ttl, cancellationToken));

    /// <inheritdoc/>
    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync("ClearCache", () => _inner.ClearCacheAsync(cancellationToken));

    /// <inheritdoc/>
    public Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"ContainsState:{stateName}", () => _inner.ContainsStateAsync(stateName, cancellationToken));

    /// <inheritdoc/>
    public Task<T> GetOrAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        => ExecuteAsync($"GetOrAddState:{stateName}", () => _inner.GetOrAddStateAsync(stateName, value, cancellationToken));

    /// <inheritdoc/>
    public Task<T> GetOrAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => ExecuteAsync($"GetOrAddState:{stateName}", () => _inner.GetOrAddStateAsync(stateName, value, ttl, cancellationToken));

    /// <inheritdoc/>
    public Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"GetState:{stateName}", () => _inner.GetStateAsync<T>(stateName, cancellationToken));

    /// <inheritdoc/>
    public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"RemoveState:{stateName}", () => _inner.RemoveStateAsync(stateName, cancellationToken));

    /// <inheritdoc/>
    public async Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        int callNumber = await BeforeOperationAsync("SaveState").ConfigureAwait(false);
        await _inner.SaveStateAsync(cancellationToken).ConfigureAwait(false);
        CaptureCommittedSnapshot();
        await AfterOperationAsync("SaveState", callNumber).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        => ExecuteAsync($"SetState:{stateName}", () => _inner.SetStateAsync(stateName, value, cancellationToken));

    /// <inheritdoc/>
    public Task SetStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => ExecuteAsync($"SetState:{stateName}", () => _inner.SetStateAsync(stateName, value, ttl, cancellationToken));

    /// <inheritdoc/>
    public Task<bool> TryAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        => ExecuteAsync($"TryAddState:{stateName}", () => _inner.TryAddStateAsync(stateName, value, cancellationToken));

    /// <inheritdoc/>
    public Task<bool> TryAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => ExecuteAsync($"TryAddState:{stateName}", () => _inner.TryAddStateAsync(stateName, value, ttl, cancellationToken));

    /// <inheritdoc/>
    public Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"TryGetState:{stateName}", () => _inner.TryGetStateAsync<T>(stateName, cancellationToken));

    /// <inheritdoc/>
    public Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
        => ExecuteAsync($"TryRemoveState:{stateName}", () => _inner.TryRemoveStateAsync(stateName, cancellationToken));

    /// <inheritdoc/>
    public Task UnloadStateAsync(string stateName, UnloadStateOptions? options = null, CancellationToken cancellationToken = default)
        => ExecuteAsync($"UnloadState:{stateName}", () => _inner.UnloadStateAsync(stateName, options, cancellationToken));

    private static void ValidateFault(string operation, int callNumber, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(callNumber);
        ArgumentNullException.ThrowIfNull(exception);
    }

    private async Task ExecuteAsync(string operation, Func<Task> action)
    {
        int callNumber = await BeforeOperationAsync(operation).ConfigureAwait(false);
        await action().ConfigureAwait(false);
        await AfterOperationAsync(operation, callNumber).ConfigureAwait(false);
    }

    private async Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action)
    {
        int callNumber = await BeforeOperationAsync(operation).ConfigureAwait(false);
        T result = await action().ConfigureAwait(false);
        await AfterOperationAsync(operation, callNumber).ConfigureAwait(false);
        return result;
    }

    private async Task<int> BeforeOperationAsync(string operation)
    {
        Trace.Add(operation);
        _callCounts.TryGetValue(operation, out int currentCount);
        int callNumber = currentCount + 1;
        _callCounts[operation] = callNumber;
        var key = (operation, callNumber);
        if (_beforeActions.TryGetValue(key, out Func<FaultInjectingActorStateManager, Task>? action))
        {
            await action(this).ConfigureAwait(false);
        }

        if (_beforeFaults.TryGetValue(key, out Exception? exception))
        {
            throw exception;
        }

        return callNumber;
    }

    private Task AfterOperationAsync(string operation, int callNumber)
    {
        Trace.Add($"{operation}:Delegated");
        return _afterFaults.TryGetValue((operation, callNumber), out Exception? exception)
            ? Task.FromException(exception)
            : Task.CompletedTask;
    }

    private void CaptureCommittedSnapshot()
        => CommittedSnapshots.Add(new Dictionary<string, object>(_inner.CommittedState, StringComparer.Ordinal));
}
