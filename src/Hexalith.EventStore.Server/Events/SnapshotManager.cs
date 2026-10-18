
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Handlers;
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
    private const int MaximumProtectedEnvelopeBytes = MaximumFoldedStateBytes + 4096;
    private const string FoldedSnapshotTypeId = "hx-snapshot-v1:folded-state";

    /// <summary>Stages an exact occurrence while returning its call-scoped completion to the actor's atomic commit.</summary>
    internal Task<bool> StageSnapshotAsync(AggregateIdentity identity, long sequenceNumber, object state,
        IActorStateManager stateManager, JsonSerializerOptions serializerOptions, FoldedSnapshotCaptureLimits limits,
        string? correlationId, CancellationToken cancellationToken, bool throwOnFailure,
        ICollection<PayloadProtectionCompletionContext> completions)
        => CreateSnapshotCoreAsync(identity, sequenceNumber, state, stateManager, serializerOptions, limits,
            correlationId, cancellationToken, throwOnFailure, completions);

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
    internal async Task<bool> CreateAutomaticReplacementAsync(
        AggregateIdentity identity,
        long sequenceNumber,
        object state,
        IActorStateManager stateManager,
        JsonSerializerOptions serializerOptions,
        FoldedSnapshotCaptureLimits limits,
        string replacementReasonClass,
        string? correlationId,
        CancellationToken cancellationToken,
        ICollection<PayloadProtectionCompletionContext>? completions = null)
    {
        if (replacementReasonClass is not ("SnapshotEnvelopeVersionUnknown" or
            "SnapshotProviderOpaque" or "SnapshotUnreadableProtected" or
            "AddressMismatch" or "SnapshotSequenceInvalid" or
            "SnapshotStateInvalid" or "SnapshotStateShapeCollision" or "CorruptSnapshot" or "SnapshotReadUnavailable" or
            "SnapshotUnprotectUnavailable"))
        {
            throw new ArgumentOutOfRangeException(nameof(replacementReasonClass));
        }
        bool staged = await CreateSnapshotCoreAsync(identity, sequenceNumber, state, stateManager, serializerOptions,
            limits, correlationId, cancellationToken, throwOnFailure: false, completions).ConfigureAwait(false);
        if (staged)
        {
            Log.ReplacementStaged(logger, correlationId, identity.TenantId, identity.Domain,
                identity.AggregateId, sequenceNumber, replacementReasonClass);
        }
        return staged;
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
        bool throwOnFailure,
        ICollection<PayloadProtectionCompletionContext>? completions = null) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stateManager);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        cancellationToken.ThrowIfCancellationRequested();
        int maximumStateBytes = limits?.MaximumCanonicalStateMeasurementBytes ?? MaximumFoldedStateBytes;
        int maximumEnvelopeBytes = limits?.MaximumCanonicalEnvelopeBytes ?? MaximumProtectedEnvelopeBytes;
        int maximumDepth = limits?.MaximumDepth ?? 64;
        long scratchBytes = limits?.MaximumSerializerScratchBytes ?? 32L * 1024 * 1024;
        long maximumStateInputBytes = limits?.MaximumActorParsedStateBytes ?? MaximumFoldedStateBytes;
        long maximumEnvelopeInputBytes = SnapshotEnvelopeInspector.EnvelopeInputBytes(checked((int)maximumStateInputBytes));

        try {
            if (!BoundedSnapshotJsonBuffer.CanSerialize(maximumStateBytes, serializerOptions, scratchBytes, maximumStateInputBytes)
                || !BoundedSnapshotJsonBuffer.CanSerialize(maximumEnvelopeBytes, serializerOptions, scratchBytes, maximumEnvelopeInputBytes)) {
                throw new InvalidOperationException("SnapshotCaptureLimitExceeded");
            }
            if (!SnapshotEnvelopeInspector.IsSupportedWriteSerializer(serializerOptions, scratchBytes)) {
                throw new InvalidOperationException("SnapshotSerializerUnsupported");
            }
            if (sequenceNumber <= 0) {
                throw new InvalidOperationException("SnapshotSequenceInvalid");
            }
            // Reserve the complete state serializer buffer before invoking a converter.
            // The actor admits optional evidence at or below this same ceiling.
            using var stateOutput = new BoundedSnapshotByteStream(maximumStateBytes, serializerOptions, scratchBytes, maximumStateInputBytes);
            BoundedSnapshotJsonBuffer.Serialize(stateOutput, state, state.GetType(), serializerOptions,
                maximumStateBytes, scratchBytes, maximumStateInputBytes);
            byte[] stateBytes = stateOutput.ToArray();
            using JsonDocument stateDocument = JsonDocument.Parse(stateBytes, new JsonDocumentOptions { MaxDepth = maximumDepth });
            JsonElement folded = stateDocument.RootElement.Clone();
            if (!SnapshotEnvelopeInspector.IsFoldedObject(folded)) {
                throw new InvalidOperationException(SnapshotEnvelopeInspector.IsDomainServiceCurrentState(folded)
                    ? "SnapshotStateShapeCollision" : "SnapshotStateInvalid");
            }

            SnapshotProtectionWriteResult writeResult = payloadProtectionService is Hexalith.EventStore.PayloadProtection.InteractionPayloadProtectionService
                ? await payloadProtectionService
                .ProtectSnapshotAsync(identity, folded, SnapshotStateJsonContext.Default.JsonElement,
                    new PayloadProtectionOccurrenceContext(checked((ulong)sequenceNumber),
                        PayloadProtectionPayloadKind.Snapshot, FoldedSnapshotTypeId), cancellationToken)
                .ConfigureAwait(false)
                : new(await payloadProtectionService.ProtectSnapshotAsync(identity, folded, cancellationToken).ConfigureAwait(false), null);
            SnapshotProtectionResult protectionResult = writeResult.ProtectionResult;
            if (writeResult.CompletionContext is not null && completions is null)
            { throw new InvalidOperationException("SnapshotCompletionOwnerRequired"); }
            if (protectionResult is null || protectionResult.State is null) {
                throw new InvalidOperationException("SnapshotStateInvalid");
            }
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
                using var protectedStateOutput = new BoundedSnapshotByteStream(maximumStateBytes, serializerOptions, scratchBytes, maximumStateInputBytes);
                BoundedSnapshotJsonBuffer.Serialize(protectedStateOutput, protectionResult.State,
                    protectionResult.State.GetType(), serializerOptions, maximumStateBytes, scratchBytes, maximumStateInputBytes);
                byte[] protectedStateBytes = protectedStateOutput.ToArray();
                if (!stateBytes.AsSpan().SequenceEqual(protectedStateBytes)) {
                    throw new InvalidOperationException("SnapshotStateChangedByNoOpProtection");
                }
                using var envelopeOutput = new BoundedSnapshotByteStream(
                    Math.Min(checked(stateBytes.Length + 4096), maximumEnvelopeBytes), serializerOptions, scratchBytes, maximumEnvelopeInputBytes);
                try {
                    BoundedSnapshotJsonBuffer.Serialize(envelopeOutput, snapshot, typeof(SnapshotRecord),
                        serializerOptions, envelopeOutput.CapacityLimit, scratchBytes, maximumEnvelopeInputBytes);
                }
                catch (InvalidOperationException exception) when (exception.Message == "SnapshotCaptureLimitExceeded") {
                    throw new InvalidOperationException("SnapshotEnvelopeBoundExceeded", exception);
                }
                byte[] envelopeBytes = envelopeOutput.ToArray();
                if (envelopeBytes.Length > (long)stateBytes.Length + 4096) {
                    throw new InvalidOperationException("SnapshotEnvelopeBoundExceeded");
                }
            }
            else {
                using var protectedEnvelopeOutput = new BoundedSnapshotByteStream(maximumEnvelopeBytes, serializerOptions, scratchBytes, maximumEnvelopeInputBytes);
                BoundedSnapshotJsonBuffer.Serialize(protectedEnvelopeOutput, snapshot, typeof(SnapshotRecord),
                    serializerOptions, maximumEnvelopeBytes, scratchBytes, maximumEnvelopeInputBytes);
            }

            // Stage the snapshot write -- committed by caller's SaveStateAsync (D1)
            cancellationToken.ThrowIfCancellationRequested();
            await stateManager
                .SetStateAsync(identity.SnapshotKey, snapshot, cancellationToken)
                .ConfigureAwait(false);
            if (writeResult.CompletionContext is not null) { completions!.Add(writeResult.CompletionContext); }

            logger.LogInformation(
                "Snapshot staged: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                sequenceNumber);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
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
                SnapshotFailureClass(ex));
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
            identity, stateManager, correlationId, cancellationToken, allowCorruptPlaintextRemoval: false).ConfigureAwait(false);
        return inspected.Snapshot;
    }

    /// <summary>Preserves the command-time bypass class from the same raw load used for replay.</summary>
    internal async Task<SnapshotLoadResult> LoadSnapshotForCommandAsync(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        string? correlationId = null,
        CancellationToken cancellationToken = default,
        bool allowCorruptPlaintextRemoval = true,
        long? authoritativeHead = null)
    {
        SnapshotLoadResult inspected = await ReadSnapshotCoreAsync(
            identity, stateManager, correlationId, cancellationToken, allowCorruptPlaintextRemoval,
            authoritativeHead).ConfigureAwait(false);
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
        bool allowCorruptPlaintextRemoval,
        long? authoritativeHead = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(stateManager);
        cancellationToken.ThrowIfCancellationRequested();
        ConditionalValue<object> rawResult;
        try
        {
            // Dapr retains the object staged by SetStateAsync in its activation cache.
            // Requesting JsonElement after staging SnapshotRecord fails the cache cast.
            rawResult = await stateManager.TryGetStateAsync<object>(identity.SnapshotKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            Log.ReadUnavailable(logger, correlationId, identity.TenantId, identity.Domain, identity.AggregateId);
            return SnapshotLoadResult.InfrastructureFailure("SnapshotReadUnavailable");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!rawResult.HasValue) { return SnapshotLoadResult.Absent(); }
        if (rawResult.Value is SnapshotRecord { SnapshotEnvelopeVersion: not (0 or 1) })
        {
            return SnapshotLoadResult.ProviderOpaque("SnapshotEnvelopeVersionUnknown");
        }
        JsonElement raw;
        try
        {
            raw = rawResult.Value is JsonElement element
                ? element : JsonSerializer.SerializeToElement(rawResult.Value, JsonSerializerOptions.Web);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return SnapshotLoadResult.InfrastructureFailure("SnapshotReadUnavailable"); }
        catch
        {
            return SnapshotLoadResult.ProviderOpaque("SnapshotRecordUnavailable");
        }
        if (!SnapshotEnvelopeInspector.TryReadVersion(raw, out _))
        {
            return SnapshotLoadResult.ProviderOpaque("SnapshotEnvelopeVersionUnknown");
        }
        if (!SnapshotEnvelopeInspector.TryReadSequence(raw, out long sequence)
            || (authoritativeHead is long head && sequence > head))
        {
            return SnapshotLoadResult.Corrupt("SnapshotSequenceInvalid");
        }
        if (!HasRawAddressedIdentity(raw, identity))
        {
            Log.MismatchedSnapshotRetained(logger, identity.TenantId, identity.Domain, identity.AggregateId);
            return SnapshotLoadResult.Corrupt("AddressMismatch");
        }
        int protectionMetadataFields = 0;
        JsonElement rawProtectionMetadata = default;
        foreach (JsonProperty property in raw.EnumerateObject())
        {
            if (!string.Equals(property.Name, "protectionMetadata", StringComparison.OrdinalIgnoreCase)) { continue; }
            rawProtectionMetadata = property.Value;
            if (++protectionMetadataFields > 1) { return SnapshotLoadResult.ProviderOpaque("SnapshotProtectionOpaque"); }
        }
        if (protectionMetadataFields == 1 && SnapshotEnvelopeInspector.HasDuplicatePropertyNames(rawProtectionMetadata))
        {
            return SnapshotLoadResult.ProviderOpaque("SnapshotProtectionOpaque");
        }
        if (!SnapshotEnvelopeInspector.TryGetUniqueProperty(raw, "state", out JsonElement rawState))
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateInvalid");
        }
        bool provenUnprotected = IsProvenUnprotected(raw);
        if (SnapshotEnvelopeInspector.HasDuplicatePropertyNames(rawState))
        {
            return provenUnprotected
                ? SnapshotLoadResult.Corrupt("SnapshotStateInvalid")
                : SnapshotLoadResult.ProviderOpaque("SnapshotProtectionOpaque");
        }
        if (provenUnprotected && rawState.ValueKind != JsonValueKind.Object)
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateInvalid");
        }
        if (provenUnprotected && SnapshotEnvelopeInspector.IsDomainServiceCurrentState(rawState))
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateShapeCollision") with { CoveredSequence = sequence };
        }

        SnapshotRecord snapshot;
        try
        {
            snapshot = raw.Deserialize<SnapshotRecord>(JsonSerializerOptions.Web)
                ?? throw new JsonException("Snapshot record is null.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return SnapshotLoadResult.InfrastructureFailure("SnapshotReadUnavailable"); }
        catch
        {
            if (allowCorruptPlaintextRemoval && authoritativeHead is > 0 && provenUnprotected)
            {
                try { await stateManager.RemoveStateAsync(identity.SnapshotKey, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { Log.RemovalUnavailable(logger, correlationId, identity.TenantId, identity.Domain, identity.AggregateId); }
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
            LogUnreadableProtectedSnapshot(identity, sequence, correlationId,
                ProtectedDataReadabilityDecisionFactory.FromMetadata(metadata,
                    ProtectedDataDecisionStage.SnapshotLoad, identity.TenantId, identity.Domain,
                    identity.AggregateId, sequence, correlationId));
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
            outcome = payloadProtectionService is Hexalith.EventStore.PayloadProtection.InteractionPayloadProtectionService
                ? await payloadProtectionService.TryUnprotectSnapshotAsync(
                identity, snapshot.State, metadata, SnapshotStateJsonContext.Default.JsonElement,
                new PayloadProtectionOccurrenceContext(checked((ulong)sequence), PayloadProtectionPayloadKind.Snapshot,
                    FoldedSnapshotTypeId), cancellationToken).ConfigureAwait(false)
                : await payloadProtectionService.TryUnprotectSnapshotAsync(identity, snapshot.State, metadata, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            LogUnreadableProtectedSnapshot(identity, sequence, correlationId,
                ProtectedDataReadabilityDecisionFactory.FromOutcome(
                    SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata),
                    ProtectedDataDecisionStage.SnapshotLoad, identity.TenantId, identity.Domain,
                    identity.AggregateId, sequence, correlationId));
            return SnapshotLoadResult.InfrastructureFailure("SnapshotUnprotectUnavailable");
        }
        SnapshotUnprotectionOutcome classifiedOutcome = outcome.IsReadable && outcome.State is null
            ? SnapshotUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ConsistencyMismatch,
                outcome.Metadata)
            : outcome;
        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecisionFactory.FromOutcome(
            classifiedOutcome, ProtectedDataDecisionStage.SnapshotLoad, identity.TenantId, identity.Domain,
            identity.AggregateId, sequence, correlationId);
        if (!decision.IsReadable)
        {
            LogUnreadableProtectedSnapshot(identity, sequence, correlationId, decision);
            return SnapshotLoadResult.UnreadableProtected("SnapshotUnreadableProtected");
        }
        JsonElement state;
        try
        {
            state = outcome.State is JsonElement element
                ? element : JsonSerializer.SerializeToElement(outcome.State, JsonSerializerOptions.Web);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return SnapshotLoadResult.InfrastructureFailure("SnapshotUnprotectUnavailable"); }
        catch
        {
            return SnapshotLoadResult.Corrupt("SnapshotStateInvalid");
        }
        if (state.ValueKind != JsonValueKind.Object || SnapshotEnvelopeInspector.HasDuplicatePropertyNames(state))
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
        if (metadata.ValueKind != JsonValueKind.Object
            || !SnapshotEnvelopeInspector.TryGetUniqueProperty(metadata, "state", out JsonElement state)
            || state.ValueKind != JsonValueKind.Number
            || !state.TryGetInt32(out int stateCode)
            || stateCode != (int)PayloadProtectionState.Unprotected)
        {
            return false;
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in metadata.EnumerateObject())
        {
            if (!seen.Add(property.Name)) { return false; }
        }
        try
        {
            EventStorePayloadProtectionMetadata? protection =
                metadata.Deserialize<EventStorePayloadProtectionMetadata>(JsonSerializerOptions.Web);
            return protection is not null
                && EventStorePayloadProtectionMetadataCarrier.TryValidate(protection, out _)
                && protection.State == PayloadProtectionState.Unprotected;
        }
        catch { return false; }
    }

    private static bool HasRawAddressedIdentity(JsonElement raw, AggregateIdentity identity)
        => SnapshotEnvelopeInspector.TryGetUniqueProperty(raw, "tenantId", out JsonElement tenant)
            && tenant.ValueKind == JsonValueKind.String
            && string.Equals(tenant.GetString(), identity.TenantId, StringComparison.Ordinal)
            && SnapshotEnvelopeInspector.TryGetUniqueProperty(raw, "domain", out JsonElement domain)
            && domain.ValueKind == JsonValueKind.String
            && string.Equals(domain.GetString(), identity.Domain, StringComparison.Ordinal)
            && SnapshotEnvelopeInspector.TryGetUniqueProperty(raw, "aggregateId", out JsonElement aggregate)
            && aggregate.ValueKind == JsonValueKind.String
            && string.Equals(aggregate.GetString(), identity.AggregateId, StringComparison.Ordinal);

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

    private static string SnapshotFailureClass(Exception exception)
        => exception is InvalidOperationException && exception.Message is
            ("SnapshotSerializerUnsupported" or "SnapshotSequenceInvalid" or
            "SnapshotStateShapeCollision" or "SnapshotStateInvalid" or
            "SnapshotCaptureLimitExceeded" or "SnapshotEnvelopeBoundExceeded" or
            "SnapshotStateChangedByNoOpProtection")
            ? exception.Message : "SnapshotCreateUnavailable";

    private void LogUnreadableProtectedSnapshot(
        AggregateIdentity identity, long sequence, string? correlationId,
        ProtectedDataReadabilityDecision decision)
        => Log.UnreadableProtectedSnapshot(logger, correlationId ?? string.Empty,
            identity.TenantId, identity.Domain, identity.AggregateId, sequence,
            decision.ReasonCode);

    private static partial class Log {
        [LoggerMessage(
            EventId = 7103,
            Level = LogLevel.Warning,
            Message = "Snapshot read unavailable: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, FailureClass=SnapshotReadUnavailable")]
        public static partial void ReadUnavailable(
            ILogger logger, string? correlationId, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 7104,
            Level = LogLevel.Warning,
            Message = "Corrupt snapshot removal unavailable: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, FailureClass=SnapshotRemovalUnavailable")]
        public static partial void RemovalUnavailable(
            ILogger logger, string? correlationId, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 7102,
            Level = LogLevel.Information,
            Message = "Snapshot replacement staged: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}, ReplacementReasonClass={ReplacementReasonClass}")]
        public static partial void ReplacementStaged(
            ILogger logger, string? correlationId, string tenantId, string domain,
            string aggregateId, long sequenceNumber, string replacementReasonClass);

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
