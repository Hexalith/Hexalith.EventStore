using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Commands;

/// <summary>
/// DAPR state store implementation of command activity tracking and querying.
/// Persists a single global admin command index so activity survives restarts while
/// preserving the original in-memory filtering semantics.
/// </summary>
public sealed class DaprCommandActivityTracker(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> options,
    ILogger<DaprCommandActivityTracker> logger) : ICommandActivityTracker
    , ICommandActivityReader {
    private const int MaxEntries = 1000;
    private const int MaxEtagRetries = 3;
    private const string ActivityIndexKey = "admin:command-activity:all";
    private readonly string _stateStoreName = options.Value.StateStoreName;

    /// <inheritdoc/>
    public async Task TrackAsync(
        string tenantId,
        string domain,
        string aggregateId,
        string correlationId,
        string commandType,
        CommandStatus status,
        DateTimeOffset timestamp,
        int? eventCount,
        string? failureReason,
        CancellationToken ct = default) {
        try {
            ArgumentNullException.ThrowIfNull(tenantId);
            string normalizedTenantId = NormalizeStoredTenantId(tenantId);
            var summary = new CommandSummary(
                normalizedTenantId, domain, aggregateId, correlationId,
                commandType, status, timestamp, eventCount, failureReason);

            bool saved = await TryUpsertActivityIndexAsync(summary, ct).ConfigureAwait(false);
            if (!saved) {
                logger.LogWarning(
                    "Failed to track command activity after {MaxRetries} optimistic-concurrency attempts: CorrelationId={CorrelationId}",
                    MaxEtagRetries,
                    correlationId);
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(
                ex,
                "Failed to track command activity: CorrelationId={CorrelationId}, ExceptionType={ExceptionType}",
                correlationId,
                ex.GetType().Name);
        }
    }

    /// <inheritdoc/>
    public async Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(
        string? tenantId,
        string? status,
        string? commandType,
        int count = 1000,
        CancellationToken ct = default) {
        try {
            string? normalizedTenantId = NormalizeOptionalFilter(tenantId);
            string? normalizedStatus = NormalizeOptionalFilter(status);
            string? normalizedCommandType = NormalizeOptionalFilter(commandType);
            count = Math.Clamp(count, 1, MaxEntries);

            List<CommandSummary>? commands = await daprClient
                .GetStateAsync<List<CommandSummary>>(_stateStoreName, ActivityIndexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (commands is null || commands.Count == 0) {
                return new PagedResult<CommandSummary>([], 0, null);
            }

            IEnumerable<CommandSummary> filtered = commands;

            if (normalizedTenantId is not null) {
                filtered = filtered.Where(c => c.TenantId.Equals(normalizedTenantId, StringComparison.OrdinalIgnoreCase));
            }

            if (normalizedStatus is not null) {
                filtered = CommandStatusFilterHelper.ApplyStatusFilter(filtered, normalizedStatus);
            }

            if (normalizedCommandType is not null) {
                filtered = filtered.Where(c => c.CommandType.Contains(normalizedCommandType, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            IReadOnlyList<CommandSummary> page = filteredList
                .OrderByDescending(c => c.Timestamp)
                .Take(count)
                .ToList();

            return new PagedResult<CommandSummary>(page, filteredList.Count, null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (JsonException ex) {
            logger.LogError(
                ex,
                "Failed to deserialize command activity from state store: Key={Key}, ExceptionType={ExceptionType}",
                ActivityIndexKey,
                ex.GetType().Name);
            return new PagedResult<CommandSummary>([], 0, null);
        }
        catch (Exception ex) {
            logger.LogError(
                ex,
                "Failed to read command activity from state store: Key={Key}, ExceptionType={ExceptionType}",
                ActivityIndexKey,
                ex.GetType().Name);
            return new PagedResult<CommandSummary>([], 0, null);
        }
    }

    private static string? NormalizeOptionalFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeStoredTenantId(string tenantId)
        => tenantId.Trim();

    private static bool MatchesIdentity(CommandSummary existing, CommandSummary summary)
        => existing.CorrelationId.Equals(summary.CorrelationId, StringComparison.Ordinal)
            && existing.AggregateId.Equals(summary.AggregateId, StringComparison.Ordinal)
            && existing.Domain.Equals(summary.Domain, StringComparison.OrdinalIgnoreCase)
            && existing.TenantId.Equals(summary.TenantId, StringComparison.OrdinalIgnoreCase);

    private static List<CommandSummary> UpsertAndTrim(List<CommandSummary> commands, CommandSummary summary) {
        int index = commands.FindIndex(c => MatchesIdentity(c, summary));
        if (index >= 0) {
            commands[index] = summary;
        }
        else {
            commands.Add(summary);
        }

        return commands
            .OrderByDescending(c => c.Timestamp)
            .Take(MaxEntries)
            .ToList();
    }

    private async Task<bool> TryUpsertActivityIndexAsync(CommandSummary summary, CancellationToken ct) {
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            try {
                (List<CommandSummary>? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<List<CommandSummary>>(_stateStoreName, ActivityIndexKey, cancellationToken: ct)
                    .ConfigureAwait(false);

                List<CommandSummary> updated = UpsertAndTrim(existing ?? [], summary);
                bool saved = await daprClient
                    .TrySaveStateAsync(_stateStoreName, ActivityIndexKey, updated, etag, cancellationToken: ct)
                    .ConfigureAwait(false);

                if (saved) {
                    return true;
                }

                logger.LogDebug(
                    "ETag mismatch while updating command activity index '{IndexKey}', retry {Attempt}.",
                    ActivityIndexKey,
                    attempt + 1);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while updating command activity index '{IndexKey}'.",
                    attempt + 1,
                    ActivityIndexKey);
            }
        }

        return false;
    }
}
