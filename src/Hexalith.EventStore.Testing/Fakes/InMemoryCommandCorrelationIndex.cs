using System.Collections.Concurrent;

using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>
/// Deterministic in-memory command correlation index with injectable conflict behavior.
/// </summary>
public sealed class InMemoryCommandCorrelationIndex(TimeProvider? timeProvider = null) : ICommandCorrelationIndex
{
    private readonly ConcurrentDictionary<string, CommandCorrelationIndexRecord> _records = new();
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Gets or sets the live-entry capacity per tenant and correlation identifier.</summary>
    public int Capacity { get; set; } = 128;

    /// <summary>Gets or sets the TTL applied to new entries.</summary>
    public int TtlSeconds { get; set; } = CommandStatusConstants.DefaultTtlSeconds;

    /// <summary>Gets or sets the ETag conflict retry count.</summary>
    public int MaxConcurrencyRetries { get; set; } = 3;

    /// <summary>Gets or sets the number of deterministic write conflicts remaining.</summary>
    public int ConflictsRemaining { get; set; }

    /// <inheritdoc/>
    public Task<CommandCorrelationIndexAddOutcome> AddAsync(
        string tenantId,
        string correlationId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        string key = CommandCorrelationIndexConstants.BuildKey(tenantId, correlationId);
        lock (_sync)
        {
            for (int attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
            {
                if (ConflictsRemaining > 0)
                {
                    ConflictsRemaining--;
                    continue;
                }

                DateTimeOffset now = _timeProvider.GetUtcNow();
                _ = _records.TryGetValue(key, out CommandCorrelationIndexRecord? stored);
                List<CommandCorrelationIndexEntry> entries = stored?.Entries
                    .Where(entry => entry.ExpiresAt > now)
                    .ToList() ?? [];
                if (entries.Any(entry => string.Equals(entry.MessageId, messageId, StringComparison.Ordinal)))
                {
                    bool duplicateOverflowed = IsOverflowActive(stored, now);
                    _records[key] = new CommandCorrelationIndexRecord(
                        entries,
                        duplicateOverflowed,
                        duplicateOverflowed ? stored?.OverflowExpiresAt : null);
                    return Task.FromResult(CommandCorrelationIndexAddOutcome.Duplicate);
                }

                bool overflowed = IsOverflowActive(stored, now);
                DateTimeOffset? overflowExpiresAt = overflowed ? stored?.OverflowExpiresAt : null;
                CommandCorrelationIndexAddOutcome outcome;
                if (entries.Count >= Capacity)
                {
                    overflowed = true;
                    overflowExpiresAt = now.AddSeconds(TtlSeconds);
                    outcome = CommandCorrelationIndexAddOutcome.Overflow;
                }
                else
                {
                    entries.Add(new CommandCorrelationIndexEntry(messageId, now.AddSeconds(TtlSeconds)));
                    outcome = CommandCorrelationIndexAddOutcome.Added;
                }

                _records[key] = new CommandCorrelationIndexRecord(entries, overflowed, overflowExpiresAt);
                return Task.FromResult(outcome);
            }
        }

        return Task.FromResult(CommandCorrelationIndexAddOutcome.RetryExhausted);
    }

    /// <inheritdoc/>
    public Task<CommandCorrelationResolution> ResolveAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        string key = CommandCorrelationIndexConstants.BuildKey(tenantId, correlationId);
        lock (_sync)
        {
            if (!_records.TryGetValue(key, out CommandCorrelationIndexRecord? stored))
            {
                return Task.FromResult(new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.NotFound));
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            List<CommandCorrelationIndexEntry> entries = stored.Entries
                .Where(entry => entry.ExpiresAt > now)
                .ToList();
            bool overflowed = IsOverflowActive(stored, now);
            _records[key] = new CommandCorrelationIndexRecord(
                entries,
                overflowed,
                overflowed ? stored.OverflowExpiresAt : null);
            if (overflowed || entries.Count > 1)
            {
                return Task.FromResult(new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.Ambiguous));
            }

            return Task.FromResult(entries.Count == 1
                ? new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.Resolved, entries[0].MessageId)
                : new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.NotFound));
        }
    }

    /// <summary>Gets a snapshot of all index records for assertions.</summary>
    public IReadOnlyDictionary<string, CommandCorrelationIndexRecord> GetAllRecords()
        => new Dictionary<string, CommandCorrelationIndexRecord>(_records);

    private static bool IsOverflowActive(CommandCorrelationIndexRecord? record, DateTimeOffset now)
        => record?.Overflowed == true
            && (record.OverflowExpiresAt is null || record.OverflowExpiresAt > now);
}
