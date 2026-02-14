namespace Hexalith.EventStore.Server.Events;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Manages aggregate state snapshots at configurable intervals.
/// Snapshot creation is advisory -- failures never block command processing (rule #12).
/// Does NOT call SaveStateAsync -- the caller commits atomically (D1).
/// </summary>
public class SnapshotManager(
    IOptions<SnapshotOptions> options,
    ILogger<SnapshotManager> logger) : ISnapshotManager
{
    /// <inheritdoc/>
    public Task<bool> ShouldCreateSnapshotAsync(string domain, long currentSequence, long lastSnapshotSequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        int interval = GetIntervalForDomain(domain);
        bool shouldCreate = (currentSequence - lastSnapshotSequence) >= interval;
        return Task.FromResult(shouldCreate);
    }

    /// <inheritdoc/>
    public async Task CreateSnapshotAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stateManager);

        try
        {
            var snapshot = new SnapshotRecord(
                SequenceNumber: sequenceNumber,
                State: state,
                CreatedAt: DateTimeOffset.UtcNow,
                Domain: identity.Domain,
                AggregateId: identity.AggregateId,
                TenantId: identity.TenantId);

            // Stage the snapshot write -- committed by caller's SaveStateAsync (D1)
            await stateManager
                .SetStateAsync(identity.SnapshotKey, snapshot)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Snapshot staged: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                sequenceNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Advisory: snapshot failure must not block command processing (rule #12)
            // Rule #5: Never log snapshot state content (payload data)
            logger.LogWarning(
                ex,
                "Snapshot creation failed (advisory, non-blocking): CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                sequenceNumber);
        }
    }

    /// <inheritdoc/>
    public async Task<SnapshotRecord?> LoadSnapshotAsync(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(stateManager);

        try
        {
            ConditionalValue<SnapshotRecord> result = await stateManager
                .TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey)
                .ConfigureAwait(false);

            if (!result.HasValue)
            {
                return null;
            }

            return result.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deserialization failure: delete corrupt snapshot and fall back to full replay
            // Rule #5: Never log snapshot state content
            logger.LogWarning(
                ex,
                "Snapshot deserialization failed, deleting corrupt snapshot and falling back to full replay: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId);

            try
            {
                await stateManager
                    .RemoveStateAsync(identity.SnapshotKey)
                    .ConfigureAwait(false);
            }
            catch (Exception removeEx) when (removeEx is not OperationCanceledException)
            {
                logger.LogWarning(
                    removeEx,
                    "Failed to delete corrupt snapshot: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                    correlationId,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId);
            }

            return null;
        }
    }

    private int GetIntervalForDomain(string domain)
    {
        SnapshotOptions opts = options.Value;

        if (opts.DomainIntervals.TryGetValue(domain, out int domainInterval))
        {
            return domainInterval;
        }

        return opts.DefaultInterval;
    }
}
