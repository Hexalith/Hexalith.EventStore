
using Dapr.Actors.Runtime;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// In-memory implementation of <see cref="IActorStateManager"/> for unit testing without DAPR runtime.
/// Uses two dictionaries to simulate DAPR actor turn-based commit semantics:
/// pending state (uncommitted) and committed state.
/// </summary>
public sealed class InMemoryStateManager : IActorStateManager {
    private readonly Dictionary<string, object> _committedState = [];
    private readonly Dictionary<string, object> _pendingState = [];
    private readonly HashSet<string> _pendingRemovals = [];

    /// <summary>Gets the committed state for test assertions.</summary>
    public IReadOnlyDictionary<string, object> CommittedState => _committedState;

    /// <inheritdoc/>
    public Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        if (_pendingState.ContainsKey(stateName) && !_pendingRemovals.Contains(stateName)) {
            throw new InvalidOperationException($"An actor state with name '{stateName}' already exists.");
        }

        if (!_pendingState.ContainsKey(stateName) && !_pendingRemovals.Contains(stateName) && _committedState.ContainsKey(stateName)) {
            throw new InvalidOperationException($"An actor state with name '{stateName}' already exists.");
        }

        _pendingState[stateName] = value!;
        _ = _pendingRemovals.Remove(stateName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task AddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => AddStateAsync(stateName, value, cancellationToken);

    /// <inheritdoc/>
    public Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);
        ArgumentNullException.ThrowIfNull(updateValueFactory);

        if (TryGetPendingOrCommitted<T>(stateName, out T? existing)) {
            T updated = updateValueFactory(stateName, existing);
            _pendingState[stateName] = updated!;
            _ = _pendingRemovals.Remove(stateName);
            return Task.FromResult(updated);
        }

        _pendingState[stateName] = addValue!;
        _ = _pendingRemovals.Remove(stateName);
        return Task.FromResult(addValue);
    }

    /// <inheritdoc/>
    public Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, TimeSpan ttl, CancellationToken cancellationToken = default)
        => AddOrUpdateStateAsync(stateName, addValue, updateValueFactory, cancellationToken);

    /// <inheritdoc/>
    public Task ClearCacheAsync(CancellationToken cancellationToken = default) {
        _pendingState.Clear();
        _pendingRemovals.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        if (_pendingRemovals.Contains(stateName)) {
            return Task.FromResult(false);
        }

        bool exists = _pendingState.ContainsKey(stateName) || _committedState.ContainsKey(stateName);
        return Task.FromResult(exists);
    }

    /// <inheritdoc/>
    public Task<T> GetOrAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        if (TryGetPendingOrCommitted<T>(stateName, out T? existing)) {
            return Task.FromResult(existing);
        }

        _pendingState[stateName] = value!;
        _ = _pendingRemovals.Remove(stateName);
        return Task.FromResult(value);
    }

    /// <inheritdoc/>
    public Task<T> GetOrAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => GetOrAddStateAsync(stateName, value, cancellationToken);

    /// <inheritdoc/>
    public Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        if (_pendingRemovals.Contains(stateName)) {
            throw new KeyNotFoundException($"Actor state with name '{stateName}' was not found.");
        }

        if (_pendingState.TryGetValue(stateName, out object? pendingValue)) {
            return Task.FromResult((T)pendingValue);
        }

        if (_committedState.TryGetValue(stateName, out object? committedValue)) {
            return Task.FromResult((T)committedValue);
        }

        throw new KeyNotFoundException($"Actor state with name '{stateName}' was not found.");
    }

    /// <inheritdoc/>
    public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        bool existsInPending = _pendingState.ContainsKey(stateName) && !_pendingRemovals.Contains(stateName);
        bool existsInCommitted = _committedState.ContainsKey(stateName) && !_pendingRemovals.Contains(stateName);

        if (!existsInPending && !existsInCommitted) {
            throw new KeyNotFoundException($"Actor state with name '{stateName}' was not found.");
        }

        _ = _pendingState.Remove(stateName);
        _ = _pendingRemovals.Add(stateName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SaveStateAsync(CancellationToken cancellationToken = default) {
        foreach (string key in _pendingRemovals) {
            _ = _committedState.Remove(key);
        }

        foreach (KeyValuePair<string, object> kvp in _pendingState) {
            _committedState[kvp.Key] = kvp.Value;
        }

        _pendingState.Clear();
        _pendingRemovals.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        _pendingState[stateName] = value!;
        _ = _pendingRemovals.Remove(stateName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => SetStateAsync(stateName, value, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> TryAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        if (TryGetPendingOrCommitted<T>(stateName, out _)) {
            return Task.FromResult(false);
        }

        _pendingState[stateName] = value!;
        _ = _pendingRemovals.Remove(stateName);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> TryAddStateAsync<T>(string stateName, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => TryAddStateAsync(stateName, value, cancellationToken);

    /// <inheritdoc/>
    public Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        if (_pendingRemovals.Contains(stateName)) {
            return Task.FromResult(new ConditionalValue<T>(false, default!));
        }

        if (_pendingState.TryGetValue(stateName, out object? pendingValue)) {
            return Task.FromResult(new ConditionalValue<T>(true, (T)pendingValue));
        }

        if (_committedState.TryGetValue(stateName, out object? committedValue)) {
            return Task.FromResult(new ConditionalValue<T>(true, (T)committedValue));
        }

        return Task.FromResult(new ConditionalValue<T>(false, default!));
    }

    /// <inheritdoc/>
    public Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(stateName);

        bool existsInPending = _pendingState.ContainsKey(stateName) && !_pendingRemovals.Contains(stateName);
        bool existsInCommitted = _committedState.ContainsKey(stateName) && !_pendingRemovals.Contains(stateName);

        if (!existsInPending && !existsInCommitted) {
            return Task.FromResult(false);
        }

        _ = _pendingState.Remove(stateName);
        _ = _pendingRemovals.Add(stateName);
        return Task.FromResult(true);
    }

    private bool TryGetPendingOrCommitted<T>(string stateName, out T value) {
        if (_pendingRemovals.Contains(stateName)) {
            value = default!;
            return false;
        }

        if (_pendingState.TryGetValue(stateName, out object? pendingValue)) {
            value = (T)pendingValue;
            return true;
        }

        if (_committedState.TryGetValue(stateName, out object? committedValue)) {
            value = (T)committedValue;
            return true;
        }

        value = default!;
        return false;
    }
}
