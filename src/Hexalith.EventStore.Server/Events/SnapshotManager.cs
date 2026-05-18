
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Events;
/// <summary>
/// Manages aggregate state snapshots at configurable intervals.
/// Snapshot creation is advisory -- failures never block command processing (rule #12).
/// Does NOT call SaveStateAsync -- the caller commits atomically (D1).
/// </summary>
public partial class SnapshotManager(
    IOptions<SnapshotOptions> options,
    ILogger<SnapshotManager> logger,
    IEventPayloadProtectionService payloadProtectionService) : ISnapshotManager {
    /// <inheritdoc/>
    public Task<bool> ShouldCreateSnapshotAsync(string tenantId, string domain, long currentSequence, long lastSnapshotSequence) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        int interval = GetInterval(tenantId, domain);
        bool shouldCreate = (currentSequence - lastSnapshotSequence) >= interval;
        return Task.FromResult(shouldCreate);
    }

    /// <inheritdoc/>
    public async Task CreateSnapshotAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stateManager);

        try {
            SnapshotProtectionResult protectionResult = await payloadProtectionService
                .ProtectSnapshotAsync(identity, state, cancellationToken)
                .ConfigureAwait(false);
            if (!EventStorePayloadProtectionMetadataCarrier.TryValidate(protectionResult.Metadata, out _)) {
                throw new InvalidOperationException("Snapshot protection metadata is invalid.");
            }

            var snapshot = new SnapshotRecord(
                SequenceNumber: sequenceNumber,
                State: protectionResult.State,
                CreatedAt: DateTimeOffset.UtcNow,
                Domain: identity.Domain,
                AggregateId: identity.AggregateId,
                TenantId: identity.TenantId,
                ProtectionMetadata: protectionResult.Metadata);

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
        catch (Exception ex) when (ex is not OperationCanceledException) {
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
        string? correlationId = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(stateManager);

        try {
            ConditionalValue<SnapshotRecord> result = await stateManager
                .TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey)
                .ConfigureAwait(false);

            if (!result.HasValue) {
                return null;
            }

            SnapshotRecord snapshot = result.Value;

            // Legacy snapshots persisted before Story 22.7a have no ProtectionMetadata. Map to the
            // explicit legacy compatibility record so callers never see a null sentinel.
            EventStorePayloadProtectionMetadata effectiveMetadata = NormalizeSnapshotMetadata(snapshot.ProtectionMetadata);

            // Story 22.7b: Provider-opaque protected snapshots must NOT be deleted (would erase
            // audit-relevant state). Return null so the actor falls back to full replay; if the
            // event tail is also unreadable, the pre-domain readability boundary will fail closed.
            // Story 22.7c: route every fail-closed snapshot-load decision through the canonical
            // ProtectedDataReadabilityDecisionFactory so the publisher, actor, snapshot manager,
            // and stream reader emit decisions with identical shape.
            if (effectiveMetadata.State == PayloadProtectionState.ProviderOpaque) {
                ProtectedDataReadabilityDecision opaqueDecision = ProtectedDataReadabilityDecisionFactory.FromMetadata(
                    effectiveMetadata,
                    ProtectedDataDecisionStage.SnapshotLoad,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    snapshot.SequenceNumber,
                    correlationId);
                Log.UnreadableProtectedSnapshot(
                    logger,
                    correlationId ?? string.Empty,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    snapshot.SequenceNumber,
                    opaqueDecision.ReasonCode);
                return null;
            }

            // Story 22.7b: use the typed snapshot unprotect entry point. Unreadable outcomes (e.g.
            // missing key, provider unavailable) DO NOT trigger snapshot deletion — protected
            // unreadable data is not the same as corrupt unprotected data. The actor will replay
            // the event tail; if that tail is also unreadable, the pre-domain boundary fails closed.
            SnapshotUnprotectionOutcome outcome;
            try {
                outcome = await payloadProtectionService
                    .TryUnprotectSnapshotAsync(identity, snapshot.State, effectiveMetadata, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
                outcome = SnapshotUnprotectionOutcome.Unreadable(
                    UnreadableProtectedDataReason.ProviderUnavailable,
                    effectiveMetadata);
            }

            ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecisionFactory.FromOutcome(
                outcome,
                ProtectedDataDecisionStage.SnapshotLoad,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                snapshot.SequenceNumber,
                correlationId);
            if (!decision.IsReadable) {
                Log.UnreadableProtectedSnapshot(
                    logger,
                    correlationId ?? string.Empty,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    snapshot.SequenceNumber,
                    decision.ReasonCode);
                return null;
            }

            return snapshot with { State = outcome.State!, ProtectionMetadata = outcome.Metadata };
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // Deserialization failure: delete corrupt snapshot and fall back to full replay
            // Rule #5: Never log snapshot state content
            // Story 22.7b: this RemoveStateAsync path is ONLY for non-protected corrupt
            // deserialization (schema drift on plaintext snapshots). Protected unreadable cases
            // are routed above via TryUnprotectSnapshotAsync and DO NOT delete the snapshot.
            logger.LogWarning(
                ex,
                "Snapshot deserialization failed, deleting corrupt snapshot and falling back to full replay: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId);

            try {
                await stateManager
                    .RemoveStateAsync(identity.SnapshotKey)
                    .ConfigureAwait(false);
            }
            catch (Exception removeEx) when (removeEx is not OperationCanceledException) {
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

    private int GetInterval(string tenantId, string domain) {
        SnapshotOptions opts = options.Value;

        // Three-tier resolution: tenant-domain > domain > default
        string tenantDomainKey = $"{tenantId}:{domain}".ToLowerInvariant();
        if (opts.TenantDomainIntervals.TryGetValue(tenantDomainKey, out int tenantDomainInterval)) {
            return tenantDomainInterval;
        }

        if (opts.DomainIntervals.TryGetValue(domain, out int domainInterval)) {
            return domainInterval;
        }

        return opts.DefaultInterval;
    }

    private static EventStorePayloadProtectionMetadata NormalizeSnapshotMetadata(EventStorePayloadProtectionMetadata? metadata) {
        if (metadata is null) {
            return EventStorePayloadProtectionMetadataCarrier.Legacy();
        }

        if (EventStorePayloadProtectionMetadataCarrier.TryValidate(metadata, out _)) {
            return metadata;
        }

        return metadata.MetadataVersion > EventStorePayloadProtectionMetadata.CurrentMetadataVersion
            ? EventStorePayloadProtectionMetadata.ProviderOpaque("unknownVersion")
            : EventStorePayloadProtectionMetadata.ProviderOpaque("forbidden");
    }

    private static partial class Log {
        // Story 22.7b: unreadable protected snapshot. Carries safe envelope metadata + reason code
        // only — no snapshot state, no payload bytes, no key alias, no provider exception text.
        [LoggerMessage(
            EventId = 7100,
            Level = LogLevel.Warning,
            Message = "Unreadable protected snapshot retained (no deletion): CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}, ReasonCode={ReasonCode}, Stage=SnapshotUnreadable")]
        public static partial void UnreadableProtectedSnapshot(
            ILogger logger,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            long sequenceNumber,
            string reasonCode);
    }
}
