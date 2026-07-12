using Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// DAPR state-store implementation of the bounded command correlation index.
/// </summary>
public sealed class DaprCommandCorrelationIndex(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> statusOptions,
    IOptions<CommandCorrelationIndexOptions> indexOptions,
    ILogger<DaprCommandCorrelationIndex> logger,
    TimeProvider? timeProvider = null) : ICommandCorrelationIndex
{
    private static readonly StateOptions s_stateOptions = new()
    {
        Concurrency = ConcurrencyMode.FirstWrite,
    };

    private TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<CommandCorrelationIndexAddOutcome> AddAsync(
        string tenantId,
        string correlationId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        CommandCorrelationIndexOptions configured = indexOptions.Value;
        if (configured.Capacity <= 0 || configured.MaxConcurrencyRetries < 0)
        {
            throw new InvalidOperationException("Command correlation index options are invalid.");
        }

        string key = CommandCorrelationIndexConstants.BuildKey(tenantId, correlationId);
        for (int attempt = 0; attempt <= configured.MaxConcurrencyRetries; attempt++)
        {
            (CommandCorrelationIndexRecord? stored, string etag) = await daprClient
                .GetStateAndETagAsync<CommandCorrelationIndexRecord>(
                    statusOptions.Value.StateStoreName,
                    key,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            DateTimeOffset now = TimeProvider.GetUtcNow();
            List<CommandCorrelationIndexEntry> entries = stored?.Entries
                .Where(entry => entry.ExpiresAt > now)
                .ToList() ?? [];
            bool overflowed = IsOverflowActive(stored, now);
            DateTimeOffset? overflowExpiresAt = overflowed ? stored?.OverflowExpiresAt : null;
            bool pruned = stored is not null
                && (entries.Count != stored.Entries.Count || overflowed != stored.Overflowed);

            if (entries.Any(entry => string.Equals(entry.MessageId, messageId, StringComparison.Ordinal)))
            {
                if (!pruned)
                {
                    return CommandCorrelationIndexAddOutcome.Duplicate;
                }

                var prunedRecord = new CommandCorrelationIndexRecord(entries, overflowed, overflowExpiresAt);
                if (await TrySaveAsync(key, prunedRecord, etag, cancellationToken).ConfigureAwait(false))
                {
                    return CommandCorrelationIndexAddOutcome.Duplicate;
                }

                continue;
            }

            CommandCorrelationIndexAddOutcome outcome;
            if (entries.Count >= configured.Capacity)
            {
                overflowed = true;
                overflowExpiresAt = now.AddSeconds(statusOptions.Value.TtlSeconds);
                outcome = CommandCorrelationIndexAddOutcome.Overflow;
            }
            else
            {
                entries.Add(new CommandCorrelationIndexEntry(
                    messageId,
                    now.AddSeconds(statusOptions.Value.TtlSeconds)));
                outcome = CommandCorrelationIndexAddOutcome.Added;
            }

            var updated = new CommandCorrelationIndexRecord(entries, overflowed, overflowExpiresAt);
            if (await TrySaveAsync(key, updated, etag, cancellationToken).ConfigureAwait(false))
            {
                return outcome;
            }
        }

        logger.LogWarning(
            "Correlation index update exhausted retries: TenantId={TenantId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
            tenantId,
            correlationId,
            messageId);
        return CommandCorrelationIndexAddOutcome.RetryExhausted;
    }

    /// <inheritdoc/>
    public async Task<CommandCorrelationResolution> ResolveAsync(
        string tenantId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        string key = CommandCorrelationIndexConstants.BuildKey(tenantId, correlationId);
        CommandCorrelationIndexOptions configured = indexOptions.Value;
        for (int attempt = 0; attempt <= configured.MaxConcurrencyRetries; attempt++)
        {
            (CommandCorrelationIndexRecord? stored, string etag) = await daprClient
                .GetStateAndETagAsync<CommandCorrelationIndexRecord>(
                    statusOptions.Value.StateStoreName,
                    key,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (stored is null)
            {
                return new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.NotFound);
            }

            DateTimeOffset now = TimeProvider.GetUtcNow();
            List<CommandCorrelationIndexEntry> entries = stored.Entries
                .Where(entry => entry.ExpiresAt > now)
                .ToList();
            bool overflowed = IsOverflowActive(stored, now);
            DateTimeOffset? overflowExpiresAt = overflowed ? stored.OverflowExpiresAt : null;
            if (entries.Count != stored.Entries.Count || overflowed != stored.Overflowed)
            {
                var pruned = new CommandCorrelationIndexRecord(entries, overflowed, overflowExpiresAt);
                if (!await TrySaveAsync(key, pruned, etag, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }
            }

            if (overflowed || entries.Count > 1)
            {
                return new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.Ambiguous);
            }

            return entries.Count == 1
                ? new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.Resolved, entries[0].MessageId)
                : new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.NotFound);
        }

        return new CommandCorrelationResolution(CommandCorrelationResolutionOutcome.Ambiguous);
    }

    private static bool IsOverflowActive(CommandCorrelationIndexRecord? record, DateTimeOffset now)
        => record?.Overflowed == true
            && (record.OverflowExpiresAt is null || record.OverflowExpiresAt > now);

    private Task<bool> TrySaveAsync(
        string key,
        CommandCorrelationIndexRecord record,
        string? etag,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            ["ttlInSeconds"] = statusOptions.Value.TtlSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
        };
        return daprClient.TrySaveStateAsync(
            statusOptions.Value.StateStoreName,
            key,
            record,
            etag ?? string.Empty,
            s_stateOptions,
            metadata,
            cancellationToken);
    }
}
