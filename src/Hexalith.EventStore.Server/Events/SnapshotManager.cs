
using Dapr.Actors.Runtime;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Snapshots;
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
    IEventPayloadProtectionService payloadProtectionService,
    ISnapshotPolicyResolver? snapshotPolicyResolver = null) : ISnapshotManager {
    private const int MaximumFoldedStateBytes = 1024 * 1024;
    private const int MaximumProtectedEnvelopeBytes = 4 * 1024 * 1024;

    /// <inheritdoc/>
    public async Task<bool> ShouldCreateSnapshotAsync(
        string tenantId,
        string domain,
        string aggregateType,
        long currentSequence,
        long lastSnapshotSequence,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

        int interval = await GetIntervalAsync(tenantId, domain, aggregateType, cancellationToken).ConfigureAwait(false);
        bool shouldCreate = (currentSequence - lastSnapshotSequence) >= interval;
        return shouldCreate;
    }

    /// <inheritdoc/>
    public Task<bool> ShouldCreateSnapshotAsync(string tenantId, string domain, long currentSequence, long lastSnapshotSequence)
        => ShouldCreateSnapshotAsync(tenantId, domain, domain, currentSequence, lastSnapshotSequence);

    /// <inheritdoc/>
    public Task CreateSnapshotAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false)
        => CreateSnapshotAsync(identity, sequenceNumber, state, stateManager, JsonSerializerOptions.Web,
            correlationId, cancellationToken, throwOnFailure);

    /// <inheritdoc/>
    public Task CreateSnapshotAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        JsonSerializerOptions serializerOptions,
        string? correlationId = null,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false)
        => CreateSnapshotCoreAsync(identity, sequenceNumber, state, stateManager, serializerOptions,
            null, correlationId, cancellationToken, throwOnFailure);

    /// <inheritdoc/>
    public Task CreateSnapshotAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        JsonSerializerOptions serializerOptions,
        FoldedSnapshotCaptureLimits limits,
        string? correlationId = null,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false)
        => CreateSnapshotCoreAsync(identity, sequenceNumber, state, stateManager, serializerOptions,
            limits, correlationId, cancellationToken, throwOnFailure);

    /// <summary>Stages an actor-owned automatic replacement with an allowlisted retained-key reason.</summary>
    internal Task<bool> CreateAutomaticReplacementAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        JsonSerializerOptions serializerOptions,
        FoldedSnapshotCaptureLimits limits,
        string replacementReasonClass,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (replacementReasonClass is not ("SnapshotEnvelopeVersionUnknown" or
            "SnapshotProviderOpaque" or "SnapshotUnreadableProtected"))
        {
            throw new ArgumentOutOfRangeException(nameof(replacementReasonClass));
        }
        return CreateSnapshotCoreAsync(identity, sequenceNumber, state, stateManager, serializerOptions,
            limits, correlationId, cancellationToken, throwOnFailure: false);
    }

    private async Task<bool> CreateSnapshotCoreAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        JsonSerializerOptions serializerOptions,
        FoldedSnapshotCaptureLimits? limits,
        string? correlationId,
        CancellationToken cancellationToken,
        bool throwOnFailure) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        cancellationToken.ThrowIfCancellationRequested();
        int maximumStateBytes = limits?.MaximumCanonicalStateMeasurementBytes ?? MaximumFoldedStateBytes;
        int maximumEnvelopeBytes = limits?.MaximumCanonicalEnvelopeBytes ?? MaximumProtectedEnvelopeBytes;
        int maximumDepth = limits?.MaximumDepth ?? 64;

        try {
            if (!SnapshotEnvelopeInspector.IsSupportedWriteSerializer(serializerOptions)) {
                throw new InvalidOperationException("SnapshotSerializerUnsupported");
            }
            if (sequenceNumber <= 0) {
                throw new InvalidOperationException("SnapshotSequenceInvalid");
            }
            // Reserve the complete state serializer buffer before invoking a converter.
            // The actor admits optional evidence at or below this same ceiling.
            using var stateOutput = new BoundedSnapshotByteStream(maximumStateBytes);
            JsonSerializer.Serialize(stateOutput, state, state.GetType(), serializerOptions);
            byte[] stateBytes = stateOutput.ToArray();
            using JsonDocument stateDocument = JsonDocument.Parse(stateBytes, new JsonDocumentOptions { MaxDepth = maximumDepth });
            JsonElement folded = stateDocument.RootElement.Clone();
            if (!SnapshotEnvelopeInspector.IsFoldedObject(folded)) {
                throw new InvalidOperationException(SnapshotEnvelopeInspector.IsDomainServiceCurrentState(folded)
                    ? "SnapshotStateShapeCollision" : "SnapshotStateInvalid");
            }

            SnapshotProtectionResult protectionResult = await payloadProtectionService
                .ProtectSnapshotAsync(identity, folded, cancellationToken)
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
                ProtectionMetadata: protectionResult.Metadata) { SnapshotEnvelopeVersion = 1 };

            if (protectionResult.Metadata.State == PayloadProtectionState.Unprotected) {
                using var protectedStateOutput = new BoundedSnapshotByteStream(maximumStateBytes);
                JsonSerializer.Serialize(protectedStateOutput, protectionResult.State,
                    protectionResult.State.GetType(), serializerOptions);
                byte[] protectedStateBytes = protectedStateOutput.ToArray();
                if (!stateBytes.AsSpan().SequenceEqual(protectedStateBytes)) {
                    throw new InvalidOperationException("SnapshotStateChangedByNoOpProtection");
                }
                using var envelopeOutput = new BoundedSnapshotByteStream(
                    Math.Min(checked(stateBytes.Length + 4096), maximumEnvelopeBytes));
                try { JsonSerializer.Serialize(envelopeOutput, snapshot, serializerOptions); }
                catch (InvalidOperationException exception) when (exception.Message == "SnapshotCaptureLimitExceeded") {
                    throw new InvalidOperationException("SnapshotEnvelopeBoundExceeded", exception);
                }
                byte[] envelopeBytes = envelopeOutput.ToArray();
                if (envelopeBytes.Length > (long)stateBytes.Length + 4096) {
                    throw new InvalidOperationException("SnapshotEnvelopeBoundExceeded");
                }
            }
            else {
                using var protectedEnvelopeOutput = new BoundedSnapshotByteStream(maximumEnvelopeBytes);
                JsonSerializer.Serialize(protectedEnvelopeOutput, snapshot, serializerOptions);
            }

            // Stage the snapshot write -- committed by caller's SaveStateAsync (D1)
            cancellationToken.ThrowIfCancellationRequested();
            await stateManager
                .SetStateAsync(identity.SnapshotKey, snapshot, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Snapshot staged: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                sequenceNumber);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            if (throwOnFailure) {
                throw;
            }

            // Advisory: snapshot failure must not block command processing (rule #12)
            // Rule #5: Never log snapshot state content (payload data)
            logger.LogWarning(
                "Snapshot creation failed (advisory, non-blocking): CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}, FailureClass={FailureClass}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                sequenceNumber,
                ex is InvalidOperationException && ex.Message.StartsWith("Snapshot", StringComparison.Ordinal)
                    ? ex.Message : ex.GetType().Name);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<SnapshotRecord?> LoadSnapshotAsync(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        SnapshotLoadResult inspected = await LoadSnapshotForCommandAsync(
            identity, stateManager, correlationId, cancellationToken, allowCorruptPlaintextRemoval: true).ConfigureAwait(false);
        return inspected.Snapshot;
    }

    /// <summary>Preserves the command-time bypass class from the same raw load used for replay.</summary>
    internal async Task<SnapshotLoadResult> LoadSnapshotForCommandAsync(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default,
        bool allowCorruptPlaintextRemoval = true)
    {
        SnapshotLoadResult inspected = await ReadSnapshotCoreAsync(
            identity, stateManager, correlationId, cancellationToken, allowCorruptPlaintextRemoval).ConfigureAwait(false);
        return inspected.Outcome == SnapshotLoadOutcome.Readable
            && inspected.Snapshot is { State: JsonElement state }
            && SnapshotEnvelopeInspector.IsFoldedObject(state)
            ? inspected : inspected.Outcome == SnapshotLoadOutcome.Readable
                ? SnapshotLoadResult.Corrupt("SnapshotStateShapeCollision") : inspected;
    }

    /// <inheritdoc/>
    public Task<SnapshotLoadResult> InspectSnapshotForManualOverwriteAsync(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        => ReadSnapshotCoreAsync(identity, stateManager, correlationId, cancellationToken, allowCorruptPlaintextRemoval: false);

    private async Task<SnapshotLoadResult> ReadSnapshotCoreAsync(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        string? correlationId,
        CancellationToken cancellationToken,
        bool allowCorruptPlaintextRemoval)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(stateManager);
        cancellationToken.ThrowIfCancellationRequested();
        ConditionalValue<JsonElement> rawResult;
        try
        {
            rawResult = await stateManager.TryGetStateAsync<JsonElement>(identity.SnapshotKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            logger.LogWarning("Snapshot read unavailable: CorrelationId={CorrelationId}, FailureClass=SnapshotReadUnavailable", correlationId);
            return SnapshotLoadResult.InfrastructureFailure("SnapshotReadUnavailable");
        }
        if (!rawResult.HasValue) { return SnapshotLoadResult.Absent(); }
        JsonElement raw = rawResult.Value;
        if (!SnapshotEnvelopeInspector.TryReadVersion(raw, out _))
        {
            return SnapshotLoadResult.ProviderOpaque("SnapshotEnvelopeVersionUnknown");
        }
        if (!SnapshotEnvelopeInspector.TryReadSequence(raw, out _))
        {
            return SnapshotLoadResult.Corrupt("SnapshotSequenceInvalid");
        }
        if (!SnapshotEnvelopeInspector.TryGetUniqueProperty(raw, "state", out _))
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateInvalid");
        }

        SnapshotRecord snapshot;
        try
        {
            snapshot = raw.Deserialize<SnapshotRecord>(JsonSerializerOptions.Web)
                ?? throw new JsonException("Snapshot record is null.");
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            if (allowCorruptPlaintextRemoval && IsProvenUnprotected(raw))
            {
                try { await stateManager.RemoveStateAsync(identity.SnapshotKey, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch { logger.LogWarning("Corrupt snapshot removal unavailable: FailureClass=SnapshotRemovalUnavailable"); }
            }
            return SnapshotLoadResult.Corrupt("CorruptSnapshot");
        }
        if (!HasAddressedIdentity(snapshot, identity))
        {
            Log.MismatchedSnapshotRetained(logger, identity.TenantId, identity.Domain, identity.AggregateId);
            return SnapshotLoadResult.Corrupt("AddressMismatch");
        }

        EventStorePayloadProtectionMetadata metadata = NormalizeSnapshotMetadata(snapshot.ProtectionMetadata);
        if (metadata.State == PayloadProtectionState.ProviderOpaque)
        {
            return SnapshotLoadResult.ProviderOpaque("SnapshotProtectionOpaque");
        }
        if (metadata.State == PayloadProtectionState.Unprotected
            && (snapshot.State is null || snapshot.State is JsonElement { ValueKind: JsonValueKind.Null }))
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateInvalid");
        }
        SnapshotUnprotectionOutcome outcome;
        try
        {
            outcome = await payloadProtectionService.TryUnprotectSnapshotAsync(
                identity, snapshot.State, metadata, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return SnapshotLoadResult.InfrastructureFailure("SnapshotUnprotectUnavailable");
        }
        if (!outcome.IsReadable || outcome.State is null)
        {
            return SnapshotLoadResult.UnreadableProtected("SnapshotUnreadableProtected");
        }
        JsonElement state;
        try
        {
            state = outcome.State is JsonElement element
                ? element : JsonSerializer.SerializeToElement(outcome.State, JsonSerializerOptions.Web);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateInvalid");
        }
        if (state.ValueKind != JsonValueKind.Object)
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateInvalid");
        }
        return SnapshotLoadResult.Readable(snapshot with { State = state, ProtectionMetadata = outcome.Metadata });
    }

    private static bool IsProvenUnprotected(JsonElement raw)
    {
        if (!SnapshotEnvelopeInspector.TryGetUniqueProperty(raw, "protectionMetadata", out JsonElement metadata))
        {
            // An absent protection record is the legacy plaintext mode.
            return !raw.EnumerateObject().Any(static property =>
                string.Equals(property.Name, "protectionMetadata", StringComparison.OrdinalIgnoreCase));
        }
        if (metadata.ValueKind == JsonValueKind.Null) { return true; }
        try
        {
            return metadata.Deserialize<EventStorePayloadProtectionMetadata>(JsonSerializerOptions.Web)?.State
                == PayloadProtectionState.Unprotected;
        }
        catch { return false; }
    }

    /// <summary>Reads a checked logical range through the shared production reader.</summary>
    internal Task<DaprProductionLogicalReplay> ReadReplayRangeAsync(
        DaprProductionLogicalEventReader reader,
        AggregateIdentity identity,
        string aggregateType,
        long startSequence,
        int count,
        CancellationToken cancellationToken,
        long expectedActorHead,
        long? expectedRetainedFloor = null,
        bool includeDomainView = true,
        LegacyEventArrayBudget? arrayBudget = null) {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(options.Value);
        cancellationToken.ThrowIfCancellationRequested();
        return reader.ReadRangeAsync(
            identity,
            aggregateType,
            startSequence,
            count,
            cancellationToken,
            expectedActorHead,
            expectedRetainedFloor,
            includeDomainView,
            arrayBudget);
    }

    private async Task<int> GetIntervalAsync(string tenantId, string domain, string aggregateType, CancellationToken cancellationToken) {
        if (snapshotPolicyResolver is not null) {
            int? persistedInterval = await snapshotPolicyResolver
                .GetIntervalAsync(tenantId, domain, aggregateType, cancellationToken)
                .ConfigureAwait(false);
            if (persistedInterval is not null) {
                return persistedInterval.Value;
            }
        }

        SnapshotOptions opts = options.Value;

        // Static fallback resolution: tenant-domain > domain > default
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

    private static bool HasAddressedIdentity(SnapshotRecord? snapshot, AggregateIdentity identity)
        => snapshot is not null
            && string.Equals(snapshot.TenantId, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(snapshot.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(snapshot.AggregateId, identity.AggregateId, StringComparison.Ordinal);

    private static partial class Log {
        [LoggerMessage(
            EventId = 7101,
            Level = LogLevel.Warning,
            Message = "Mismatched snapshot retained; replay requires event history: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=SnapshotAddressMismatch")]
        public static partial void MismatchedSnapshotRetained(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId);

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
