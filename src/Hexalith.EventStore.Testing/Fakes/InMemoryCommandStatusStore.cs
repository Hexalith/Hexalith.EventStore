
using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// In-memory implementation of <see cref="ICommandStatusStore"/> for testing.
/// Simulates TTL expiry using stored expiration timestamps.
/// </summary>
public sealed class InMemoryCommandStatusStore : ICommandStatusStore {
    private readonly ConcurrentDictionary<string, (CommandStatusRecord Record, DateTimeOffset Expiry)> _store = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<CommandStatusRecord>> _history = new();

    /// <summary>Gets or sets the TTL in seconds for new entries.</summary>
    public int TtlSeconds { get; set; } = CommandStatusConstants.DefaultTtlSeconds;

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
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddSeconds(TtlSeconds);
        _store[key] = (status, expiry);
        ConcurrentQueue<CommandStatusRecord> queue = _history.GetOrAdd(key, _ => new ConcurrentQueue<CommandStatusRecord>());
        queue.Enqueue(status);
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

        if (_store.TryGetValue(key, out (CommandStatusRecord Record, DateTimeOffset Expiry) entry)) {
            if (entry.Expiry <= DateTimeOffset.UtcNow) {
                _ = _store.TryRemove(key, out _);
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

    /// <summary>
    /// Gets the write history for a status key in append order.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="correlationId">Correlation identifier.</param>
    /// <returns>Status history in write order, or empty if none.</returns>
    public IReadOnlyList<CommandStatusRecord> GetStatusHistory(string tenantId, string correlationId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = CommandStatusConstants.BuildKey(tenantId, correlationId);
        return _history.TryGetValue(key, out ConcurrentQueue<CommandStatusRecord>? queue)
            ? [.. queue]
            : [];
    }

    /// <summary>Clears all stored entries.</summary>
    public void Clear() {
        _store.Clear();
        _history.Clear();
    }
}
