
using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// In-memory implementation of <see cref="ICommandArchiveStore"/> for testing.
/// Simulates TTL expiry using stored expiration timestamps.
/// </summary>
public sealed class InMemoryCommandArchiveStore : ICommandArchiveStore {
    private readonly ConcurrentDictionary<string, (ArchivedCommand Command, DateTimeOffset Expiry)> _store = new();

    /// <summary>Gets or sets the TTL in seconds for new entries.</summary>
    public int TtlSeconds { get; set; } = CommandStatusConstants.DefaultTtlSeconds;

    /// <inheritdoc/>
    public Task WriteCommandAsync(
        string tenantId,
        string correlationId,
        ArchivedCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(command);

        string key = CommandArchiveConstants.BuildKey(tenantId, correlationId);
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddSeconds(TtlSeconds);
        _store[key] = (command, expiry);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ArchivedCommand?> ReadCommandAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = CommandArchiveConstants.BuildKey(tenantId, correlationId);

        if (_store.TryGetValue(key, out (ArchivedCommand Command, DateTimeOffset Expiry) entry)) {
            if (entry.Expiry <= DateTimeOffset.UtcNow) {
                _ = _store.TryRemove(key, out _);
                return Task.FromResult<ArchivedCommand?>(null);
            }

            return Task.FromResult<ArchivedCommand?>(entry.Command);
        }

        return Task.FromResult<ArchivedCommand?>(null);
    }

    /// <summary>Gets all stored archives (including expired) for test assertions.</summary>
    public IReadOnlyDictionary<string, (ArchivedCommand Command, DateTimeOffset Expiry)> GetAllArchived()
        => _store;

    /// <summary>Gets the count of stored archive entries.</summary>
    public int GetArchiveCount() => _store.Count;

    /// <summary>Clears all stored entries.</summary>
    public void Clear() => _store.Clear();
}
