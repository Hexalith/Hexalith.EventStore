namespace Hexalith.EventStore.Testing.Fakes;

using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

/// <summary>
/// In-memory implementation of <see cref="ICommandStatusStore"/> for testing.
/// Simulates TTL expiry using stored expiration timestamps.
/// </summary>
public sealed class InMemoryCommandStatusStore : ICommandStatusStore {
    private readonly ConcurrentDictionary<string, (CommandStatusRecord Record, DateTimeOffset Expiry)> _store = new();
    private int _ttlSeconds = CommandStatusConstants.DefaultTtlSeconds;

    /// <summary>Gets or sets the TTL in seconds for new entries.</summary>
    public int TtlSeconds {
        get => _ttlSeconds;
        set => _ttlSeconds = value;
    }

    /// <inheritdoc/>
    public Task WriteStatusAsync(
        string tenantId,
        string correlationId,
        CommandStatusRecord status,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(status);

        string key = CommandStatusConstants.BuildKey(tenantId, correlationId);
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddSeconds(_ttlSeconds);
        _store[key] = (status, expiry);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<CommandStatusRecord?> ReadStatusAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = CommandStatusConstants.BuildKey(tenantId, correlationId);

        if (_store.TryGetValue(key, out var entry)) {
            if (entry.Expiry <= DateTimeOffset.UtcNow) {
                _store.TryRemove(key, out _);
                return Task.FromResult<CommandStatusRecord?>(null);
            }

            return Task.FromResult<CommandStatusRecord?>(entry.Record);
        }

        return Task.FromResult<CommandStatusRecord?>(null);
    }

    /// <summary>Gets a snapshot of all stored statuses (including expired) for test assertions.</summary>
    public IReadOnlyDictionary<string, (CommandStatusRecord Record, DateTimeOffset Expiry)> GetAllStatuses()
        => new Dictionary<string, (CommandStatusRecord Record, DateTimeOffset Expiry)>(_store);

    /// <summary>Gets the count of stored status entries.</summary>
    public int GetStatusCount() => _store.Count;

    /// <summary>Clears all stored entries.</summary>
    public void Clear() => _store.Clear();
}
