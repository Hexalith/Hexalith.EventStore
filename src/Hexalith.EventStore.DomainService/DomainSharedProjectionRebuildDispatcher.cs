using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Coordinates durable tenant/domain shared-projection rebuild sessions.</summary>
public static class DomainSharedProjectionRebuildDispatcher {
    private static readonly JsonSerializerOptions s_deliveryJson = new(JsonSerializerDefaults.Web);
    internal const int ChunkHashesPerPage = 128;

    // A source event can materialize into more than one bounded candidate chunk.
    // Keep the scratch count bounded independently of the captured event count.
    private static long MaxStreamedOwnerChunkCount(ProjectionDispatchOptions options)
        => Math.Min(int.MaxValue, (long)options.MaxRebuildEventCount * 3 + 8);

    /// <summary>Executes one validated shared rebuild session transition.</summary>
    /// <param name="serviceProvider">The scoped request service provider.</param>
    /// <param name="request">The versioned shared rebuild request.</param>
    /// <param name="options">The validated projection dispatch and rebuild bounds.</param>
    /// <param name="identityOptions">The authoritative local service binding.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    /// <returns>The last proven durable session outcome.</returns>
    public static async Task<DomainSharedProjectionRebuildResponse> DispatchAsync(
        IServiceProvider serviceProvider,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identityOptions,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(identityOptions);
        options.Validate();
        identityOptions.Validate();
        ValidateRequest(request, options);

        IAsyncDomainSharedProjectionRebuildHandler handler = ResolveHandler(
            serviceProvider,
            request.Identity,
            options,
            identityOptions);
        ValidateComponent(handler.RebuildStoreName);
        IReadModelStore? sessionStore = serviceProvider.GetService<IReadModelStore>();
        if (sessionStore is null) {
            return Failure(
                DomainSharedProjectionRebuildPhase.Accumulating,
                ProjectionDispatchStatus.Failed,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        try {
            return request.Action switch {
                DomainSharedProjectionRebuildAction.Begin => await BeginAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.BeginPart => await BeginPartAsync(
                    serviceProvider, sessionStore, handler, request, cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Accumulate => await AccumulateAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.AccumulatePart => await AccumulatePartAsync(
                    serviceProvider, sessionStore, handler, request, options, cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.AccumulateComplete => await AccumulateCompleteAsync(
                    serviceProvider, sessionStore, handler, request, options, cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Finalize => await FinalizeAsync(
                    sessionStore,
                    handler,
                    request,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Stage => await StageAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Commit => await CommitAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Verify => await VerifyAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
                DomainSharedProjectionRebuildAction.Inspect => await InspectAsync(
                    sessionStore, handler, request, options, cancellationToken).ConfigureAwait(false),
                _ => await AbortAsync(
                    serviceProvider,
                    sessionStore,
                    handler,
                    request.Identity,
                    options,
                    cancellationToken).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception) {
            return Failure(
                DomainSharedProjectionRebuildPhase.Accumulating,
                ProjectionDispatchStatus.Indeterminate,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure);
        }
    }

    private static async Task<DomainSharedProjectionRebuildResponse> InspectAsync(
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken)
    {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
            .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName,
                DomainSharedProjectionRebuildSessionKey.Compute(identity), cancellationToken)
            .ConfigureAwait(false);
        if (!TryValidateState(entry.Value, identity, options, out DomainSharedProjectionRebuildResponse? failure))
            return failure!;
        return StringComparer.Ordinal.Equals(entry.Value!.CaptureInventoryFingerprint,
            request.CaptureInventoryFingerprint)
            ? FromState(entry.Value, ProjectionDispatchStatus.Completed, null)
            : FromState(entry.Value, ProjectionDispatchStatus.Failed,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
    }

    private static async Task<DomainSharedProjectionRebuildResponse> AbortAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        if (handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true } epochHandler) {
            return await FencedAbortAsync(serviceProvider, sessionStore, epochHandler, identity, options, cancellationToken)
                .ConfigureAwait(false);
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Aborted) {
                return FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (state.Phase == DomainSharedProjectionRebuildPhase.Accumulating) {
                DomainSharedProjectionRebuildSessionState aborted = state with {
                    Phase = DomainSharedProjectionRebuildPhase.Aborted,
                };
                if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, aborted, entry.ETag, cancellationToken)
                    .ConfigureAwait(false)) {
                    return FromState(aborted, ProjectionDispatchStatus.Completed, null);
                }

                continue;
            }

            IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
            if (stagingStore is null) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.UnsupportedCapability);
            }

            ReadModelBatch batch = await BuildBatchAsync(sessionStore, handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult result = await stagingStore.AbortAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.Status != ReadModelBatchStagingStatus.Aborted) {
                return FromStaging(state, result);
            }

            if (!FingerprintMatches(state, result.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Aborted,
                BatchFingerprint = result.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<DomainSharedProjectionRebuildResponse> AccumulatePartAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken)
    {
        if (handler is not IAsyncDomainSharedProjectionStreamedOwnerHandler { EpochEnabled: true } partitioned)
            return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Failed,
                0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                ProjectionDispatchReasonCodes.UnsupportedCapability);

        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        long ownerOrdinal = request.AggregateOrdinal!.Value;
        int partOrdinal = request.PartOrdinal!.Value;
        byte[] partBytes = JsonSerializer.SerializeToUtf8Bytes(request.Events, s_deliveryJson);
        string digest = Convert.ToHexString(SHA256.HashData(partBytes));
        const int fragmentSize = 128 * 1024;
        int fragmentCount = (partBytes.Length + fragmentSize - 1) / fragmentSize;
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++)
        {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure))
                return failure!;
            if (!state!.Partitioned || state.Phase != DomainSharedProjectionRebuildPhase.Accumulating)
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            if (ownerOrdinal < state.AcceptedAggregateCount)
            {
                ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> accepted = await sessionStore
                    .GetAsync<DomainSharedProjectionStoredPartitionManifest>(handler.RebuildStoreName,
                        PartitionManifestKey(sessionKey, ownerOrdinal), cancellationToken).ConfigureAwait(false);
                DomainSharedProjectionTransportPart? priorPart = (await sessionStore
                    .GetAsync<DomainSharedProjectionTransportPart>(handler.RebuildStoreName,
                        TransportPartKey(sessionKey, ownerOrdinal, partOrdinal), cancellationToken)
                    .ConfigureAwait(false)).Value;
                byte[]? savedBytes = priorPart is null ? null : await ReadTransportPartBytesAsync(
                    sessionStore, handler.RebuildStoreName, sessionKey, priorPart,
                    cancellationToken).ConfigureAwait(false);
                return accepted.Value is { } acceptedManifest
                    && acceptedManifest.AggregateId == request.AggregateId
                    && acceptedManifest.TransportPartCount > partOrdinal
                    && priorPart is { } acceptedPart && acceptedPart.Digest == digest
                    && acceptedPart.ByteLength == partBytes.Length
                    && savedBytes is not null && savedBytes.AsSpan().SequenceEqual(partBytes)
                    ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : FromState(state, ProjectionDispatchStatus.Failed,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (ownerOrdinal > state.AcceptedAggregateCount)
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
            if (state.TransportOwnerOrdinal != ownerOrdinal)
            {
                if (state.TransportOwnerOrdinal >= state.AcceptedAggregateCount)
                    return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                if (!await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey,
                    state with { TransportOwnerOrdinal = ownerOrdinal,
                        TransportAggregateId = request.AggregateId, TransportNextPart = 0,
                        TransportReservedPartCount = 0, TransportReservedFragmentCount = 0 },
                    entry.ETag, cancellationToken).ConfigureAwait(false)) continue;
                continue;
            }

            if (state.TransportAggregateId != request.AggregateId)
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            if (partOrdinal > state.TransportNextPart)
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
            if (partOrdinal == state.TransportNextPart && state.TransportReservedPartCount <= partOrdinal)
            {
                if (!await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey,
                    state with { TransportReservedPartCount = partOrdinal + 1,
                        TransportReservedFragmentCount = fragmentCount },
                    entry.ETag, cancellationToken).ConfigureAwait(false)) continue;
                continue;
            }
            if (partOrdinal == state.TransportNextPart
                && state.TransportReservedFragmentCount != fragmentCount)
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            string partKey = TransportPartKey(sessionKey, ownerOrdinal, partOrdinal);
            ReadModelEntry<DomainSharedProjectionTransportPart> stored = await sessionStore
                .GetAsync<DomainSharedProjectionTransportPart>(handler.RebuildStoreName, partKey, cancellationToken)
                .ConfigureAwait(false);
            if (stored.Value is { } prior)
            {
                if (prior.OwnerOrdinal != ownerOrdinal || prior.AggregateId != request.AggregateId
                    || prior.PartOrdinal != partOrdinal || prior.Digest != digest
                    || prior.FragmentCount != fragmentCount || prior.ByteLength != partBytes.Length)
                    return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                byte[]? savedBytes = await ReadTransportPartBytesAsync(sessionStore, handler.RebuildStoreName,
                    sessionKey, prior, cancellationToken).ConfigureAwait(false);
                if (savedBytes is null || !savedBytes.AsSpan().SequenceEqual(partBytes))
                    return FromState(state, ProjectionDispatchStatus.Failed,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }
            else if (partOrdinal < state.TransportNextPart)
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            else
            {
                SharedProjectionEpochCoordinator coordinator = serviceProvider
                    .GetRequiredService<SharedProjectionEpochCoordinator>();
                bool retry = false;
                for (int fragment = 0; fragment < fragmentCount; fragment++)
                {
                    byte[] data = partBytes.AsSpan(fragment * fragmentSize,
                        Math.Min(fragmentSize, partBytes.Length - fragment * fragmentSize)).ToArray();
                    string fragmentKey = TransportFragmentKey(sessionKey, ownerOrdinal, partOrdinal, fragment);
                    ReadModelEntry<DomainSharedProjectionTransportFragment> existing = await sessionStore
                        .GetAsync<DomainSharedProjectionTransportFragment>(handler.RebuildStoreName,
                            fragmentKey, cancellationToken).ConfigureAwait(false);
                    if (existing.Value is { } saved)
                    {
                        if (!saved.Data.AsSpan().SequenceEqual(data))
                            return FromState(state, ProjectionDispatchStatus.Failed,
                                ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                        continue;
                    }
                    bool fragmentWritten = await coordinator.TryWriteScratchAsync(
                        FencedScope(partitioned, identity), identity.OperationId,
                        fragmentKey, new DomainSharedProjectionTransportFragment(data),
                        TransportFragmentBatchId(identity, ownerOrdinal, partOrdinal, fragment),
                        ReadModelBatchConcurrency.CreateOnly, cancellationToken).ConfigureAwait(false);
                    if (!fragmentWritten) { retry = true; break; }
                }
                if (retry) continue;
                var part = new DomainSharedProjectionTransportPart(ownerOrdinal, request.AggregateId!,
                    partOrdinal, fragmentCount, partBytes.Length, digest);
                bool written = await coordinator
                    .TryWriteScratchAsync(FencedScope(partitioned, identity), identity.OperationId,
                        partKey, part, TransportPartBatchId(identity, ownerOrdinal, partOrdinal),
                        ReadModelBatchConcurrency.CreateOnly, cancellationToken).ConfigureAwait(false);
                if (!written) continue;
            }

            if (partOrdinal < state.TransportNextPart)
                return FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey,
                state with { TransportNextPart = partOrdinal + 1 }, entry.ETag, cancellationToken)
                .ConfigureAwait(false))
                return FromState(state, ProjectionDispatchStatus.Completed, null);
        }

        return RetryExhausted();
    }

    private static async Task<DomainSharedProjectionRebuildResponse> AccumulateCompleteAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken)
    {
        if (handler is not IAsyncDomainSharedProjectionStreamedOwnerHandler { EpochEnabled: true } streamed)
            return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Failed,
                0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                ProjectionDispatchReasonCodes.UnsupportedCapability);
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
            .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
            .ConfigureAwait(false);
        DomainSharedProjectionRebuildSessionState? state = entry.Value;
        if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure))
            return failure!;
        long ownerOrdinal = request.AggregateOrdinal!.Value;
        if (ownerOrdinal < state!.AcceptedAggregateCount)
        {
            ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> accepted = await sessionStore
                .GetAsync<DomainSharedProjectionStoredPartitionManifest>(handler.RebuildStoreName,
                    PartitionManifestKey(sessionKey, ownerOrdinal), cancellationToken).ConfigureAwait(false);
            return accepted.Value is { } manifest && manifest.AggregateId == request.AggregateId
                && manifest.HistoryFingerprint == request.ExpectedHistoryFingerprint
                && manifest.TransportPartCount == request.PartCount
                ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                : FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (ownerOrdinal > state.AcceptedAggregateCount)
            return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
        if (state.Phase != DomainSharedProjectionRebuildPhase.Accumulating
            || request.PartCount != 0 && (state.TransportOwnerOrdinal != ownerOrdinal
                || state.TransportAggregateId != request.AggregateId
                || state.TransportNextPart != request.PartCount))
            return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
        if (request.IsErased)
        {
            if (request.ExpectedHistoryFingerprint != DomainSharedProjectionRebuildFingerprint.ComputeHistory(
                    request.AggregateId!, true, [], request.ExpectedHistoryFingerprint))
                return FromState(state, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            return await AccumulateAsync(serviceProvider, sessionStore, handler,
                request with { Action = DomainSharedProjectionRebuildAction.Accumulate, Events = [] },
                options, cancellationToken).ConfigureAwait(false);
        }
        return await AccumulateStreamedOwnerAsync(serviceProvider, sessionStore, streamed, state, request,
            options, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DomainSharedProjectionRebuildResponse> AccumulateStreamedOwnerAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionStreamedOwnerHandler handler,
        DomainSharedProjectionRebuildSessionState observedState,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken)
    {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        long ownerOrdinal = request.AggregateOrdinal!.Value;
        string owner = request.AggregateId!;
        int partCount = request.PartCount!.Value;
        SharedProjectionEpochCoordinator coordinator = serviceProvider.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = FencedScope(handler, identity);
        if (!await coordinator.MatchesCapturedOwnerErasureAsync(scope, identity.OperationId,
            handler.SourceStreamKey(identity.TenantId, owner),
            request.IsErased, cancellationToken).ConfigureAwait(false))
            return FromState(observedState, ProjectionDispatchStatus.Failed,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict);

        int expectedCount = await ReadExpectedOwnerEventCountAsync(sessionStore,
            handler.RebuildStoreName, sessionKey, ownerOrdinal, owner, partCount,
            cancellationToken).ConfigureAwait(false);
        if (!request.IsErased && (expectedCount < 1 || expectedCount > options.MaxRebuildEventCount))
            return FromState(observedState, ProjectionDispatchStatus.Failed,
                ProjectionDispatchReasonCodes.MalformedOutcome);
        var positions = new long[expectedCount];
        var capturedDigests = new byte[checked(expectedCount * 32)];
        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        long precedingPosition = 0;
        int eventCount = 0;
        using (var fingerprint = new DomainSharedProjectionHistoryFingerprintBuilder(owner,
            request.IsErased, expectedCount, request.ExpectedHistoryFingerprint))
        {
            for (int partOrdinal = 0; partOrdinal < partCount; partOrdinal++)
            {
                ProjectionEventDto[] partEvents = await ReadOwnerPartAsync(sessionStore,
                    handler.RebuildStoreName, sessionKey, ownerOrdinal, owner, partOrdinal,
                    cancellationToken).ConfigureAwait(false);
                ProjectionRequest evolved = DomainProjectionDispatcher.UpcastRequest(serviceProvider,
                    new ProjectionRequest(identity.TenantId, identity.Domain, owner, partEvents));
                if (evolved.Events.Length != partEvents.Length)
                    throw new InvalidOperationException("Owner payload evolution changed the event count.");
                byte[] partDigests = ComputeCapturedEventDigests(handler, identity, owner, evolved.Events);
                if (partDigests.Length > capturedDigests.Length - eventCount * 32)
                    return FromState(observedState, ProjectionDispatchStatus.Failed,
                        ProjectionDispatchReasonCodes.MalformedOutcome);
                partDigests.CopyTo(capturedDigests.AsSpan(eventCount * 32));
                foreach (ProjectionEventDto item in partEvents)
                {
                    if (item is null || item.SequenceNumber != eventCount + 1
                        || item.GlobalPosition <= precedingPosition || item.Payload is null
                        || string.IsNullOrWhiteSpace(item.EventTypeName)
                        || string.IsNullOrWhiteSpace(item.SerializationFormat)
                        || !string.IsNullOrWhiteSpace(item.MessageId)
                        && !messageIds.Add(Convert.ToHexString(SHA256.HashData(
                            Encoding.UTF8.GetBytes(item.MessageId)))))
                        return FromState(observedState, ProjectionDispatchStatus.Failed,
                            ProjectionDispatchReasonCodes.MalformedOutcome);
                    positions[eventCount++] = item.GlobalPosition;
                    precedingPosition = item.GlobalPosition;
                    fingerprint.Append(item);
                }
            }

            if (eventCount != expectedCount || fingerprint.Finish() != request.ExpectedHistoryFingerprint)
                return FromState(observedState, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (!request.IsErased && !await coordinator.MatchesCapturedOwnerPositionAsync(scope,
            identity.OperationId, handler.SourceStreamKey(identity.TenantId, owner), positions,
            cancellationToken).ConfigureAwait(false))
            return FromState(observedState, ProjectionDispatchStatus.Failed,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict);

        int foldedEvents = 0;
        async IAsyncEnumerable<ProjectionEventDto> EvolvedEvents(
            [EnumeratorCancellation] CancellationToken token = default)
        {
            for (int partOrdinal = 0; partOrdinal < partCount; partOrdinal++)
            {
                ProjectionEventDto[] partEvents = await ReadOwnerPartAsync(sessionStore,
                    handler.RebuildStoreName, sessionKey, ownerOrdinal, owner, partOrdinal,
                    token).ConfigureAwait(false);
                ProjectionRequest evolved = DomainProjectionDispatcher.UpcastRequest(serviceProvider,
                    new ProjectionRequest(identity.TenantId, identity.Domain, owner, partEvents));
                byte[] evolvedDigests = ComputeCapturedEventDigests(handler, identity, owner, evolved.Events);
                if (!evolvedDigests.AsSpan().SequenceEqual(capturedDigests.AsSpan(foldedEvents * 32,
                    evolvedDigests.Length)))
                    throw new InvalidOperationException("Owner evolution changed across streamed fold passes.");
                foreach (ProjectionEventDto item in evolved.Events)
                {
                    foldedEvents++;
                    yield return item;
                }
            }
        }

        var chunkHashes = new List<string>();
        if (!request.IsErased)
        {
            await foreach (ReadOnlyMemory<byte> prepared in handler.StreamPartitionChunksAsync(identity,
                owner, EvolvedEvents(cancellationToken), cancellationToken).WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                byte[] bytes = prepared.ToArray();
                int chunkOrdinal = chunkHashes.Count;
                if (bytes.Length == 0 || bytes.Length > options.MaxSharedRebuildCandidateBytes
                    || chunkOrdinal >= MaxStreamedOwnerChunkCount(options))
                    throw new InvalidOperationException("A streamed owner chunk exceeds its configured limit.");
                if (!await WriteStreamedOwnerChunkAsync(sessionStore, coordinator, handler,
                    identity, sessionKey, ownerOrdinal, owner, request.ExpectedHistoryFingerprint!,
                    chunkOrdinal, bytes, options, cancellationToken).ConfigureAwait(false))
                    return RetryExhausted();
                chunkHashes.Add(Convert.ToHexString(SHA256.HashData(bytes)));
            }

            if (chunkHashes.Count == 0)
                throw new InvalidOperationException("A streamed owner returned no contribution chunk.");
            if (foldedEvents != expectedCount)
                throw new InvalidOperationException("A streamed owner fold did not consume its full sealed history.");
        }

        var pageDigests = new List<string>();
        for (int pageOrdinal = 0; (long)pageOrdinal * ChunkHashesPerPage < chunkHashes.Count; pageOrdinal++)
        {
            var page = new DomainSharedProjectionChunkHashPage(ownerOrdinal, owner, pageOrdinal,
                [.. chunkHashes.Skip(pageOrdinal * ChunkHashesPerPage).Take(ChunkHashesPerPage)]);
            string pageKey = PartitionHashPageKey(sessionKey, ownerOrdinal, pageOrdinal);
            string digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page)));
            ReadModelEntry<DomainSharedProjectionChunkHashPage> priorPage = await sessionStore
                .GetAsync<DomainSharedProjectionChunkHashPage>(handler.RebuildStoreName, pageKey,
                    cancellationToken).ConfigureAwait(false);
            if (priorPage.Value is null)
            {
                if (!await coordinator.TryWriteScratchAsync(scope, identity.OperationId, pageKey,
                    page, OwnerHashPageBatchId(identity, ownerOrdinal, pageOrdinal),
                    ReadModelBatchConcurrency.CreateOnly, cancellationToken).ConfigureAwait(false))
                    return RetryExhausted();
            }
            else if (priorPage.Value.OwnerOrdinal != ownerOrdinal || priorPage.Value.AggregateId != owner
                || priorPage.Value.PageOrdinal != pageOrdinal
                || !priorPage.Value.Hashes.SequenceEqual(page.Hashes, StringComparer.Ordinal))
                return FromState(observedState, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            pageDigests.Add(digest);
        }

        var manifest = new DomainSharedProjectionStoredPartitionManifest(ownerOrdinal, owner,
            request.ExpectedHistoryFingerprint!, request.IsErased, [], expectedCount,
            request.IsErased ? null : capturedDigests, partCount, chunkHashes.Count, [.. pageDigests]);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++)
        {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName,
                    sessionKey, cancellationToken).ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure))
                return failure!;
            if (ownerOrdinal < state!.AcceptedAggregateCount)
            {
                DomainSharedProjectionStoredPartitionManifest? accepted = (await sessionStore
                    .GetAsync<DomainSharedProjectionStoredPartitionManifest>(handler.RebuildStoreName,
                        PartitionManifestKey(sessionKey, ownerOrdinal), cancellationToken).ConfigureAwait(false)).Value;
                return accepted == manifest || accepted is not null
                    && accepted.AggregateId == owner && accepted.HistoryFingerprint == manifest.HistoryFingerprint
                    && accepted.TransportPartCount == partCount
                    ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : FromState(state, ProjectionDispatchStatus.Failed,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }
            if (state.Phase != DomainSharedProjectionRebuildPhase.Accumulating
                || ownerOrdinal != state.AcceptedAggregateCount
                || state.PendingScratchChunkCount != chunkHashes.Count)
                return FromState(state, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            string manifestKey = PartitionManifestKey(sessionKey, ownerOrdinal);
            ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> prior = await sessionStore
                .GetAsync<DomainSharedProjectionStoredPartitionManifest>(handler.RebuildStoreName,
                    manifestKey, cancellationToken).ConfigureAwait(false);
            if (prior.Value is null)
            {
                if (!await coordinator.TryWriteScratchAsync(scope, identity.OperationId, manifestKey,
                    manifest, OwnerScratchBatchId(identity, ownerOrdinal, -1),
                    ReadModelBatchConcurrency.CreateOnly, cancellationToken).ConfigureAwait(false))
                    continue;
            }
            else if (prior.Value.Ordinal != manifest.Ordinal || prior.Value.AggregateId != manifest.AggregateId
                || prior.Value.HistoryFingerprint != manifest.HistoryFingerprint
                || prior.Value.IsErased != manifest.IsErased
                || prior.Value.LastSequenceNumber != manifest.LastSequenceNumber
                || prior.Value.TransportPartCount != manifest.TransportPartCount
                || prior.Value.StreamedChunkCount != manifest.StreamedChunkCount
                || !(prior.Value.ChunkHashPageDigests ?? []).SequenceEqual(
                    manifest.ChunkHashPageDigests ?? [], StringComparer.Ordinal)
                || !prior.Value.ChunkHashes.SequenceEqual(manifest.ChunkHashes, StringComparer.Ordinal)
                || !(prior.Value.CapturedEventDigests ?? []).AsSpan()
                    .SequenceEqual(manifest.CapturedEventDigests ?? []))
                return FromState(state, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            DomainSharedProjectionRebuildSessionState updated = state with
            {
                AcceptedAggregateCount = state.AcceptedAggregateCount + 1,
                InventoryFingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                    state.InventoryFingerprint, ownerOrdinal, manifest.HistoryFingerprint),
                LastAggregateId = owner,
                PendingScratchChunkCount = -1
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey,
                updated, entry.ETag, cancellationToken).ConfigureAwait(false))
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
        }

        return RetryExhausted();
    }

    private static async Task<bool> WriteStreamedOwnerChunkAsync(
        IReadModelStore store, SharedProjectionEpochCoordinator coordinator,
        IAsyncDomainSharedProjectionStreamedOwnerHandler handler,
        DomainSharedProjectionRebuildIdentity identity, string sessionKey,
        long ownerOrdinal, string owner, string historyFingerprint, int chunkOrdinal,
        byte[] bytes, ProjectionDispatchOptions options, CancellationToken cancellationToken)
    {
        string key = PartitionChunkKey(sessionKey, ownerOrdinal, chunkOrdinal);
        var chunk = new DomainSharedProjectionStoredPartition(ownerOrdinal, chunkOrdinal,
            owner, historyFingerprint, bytes);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++)
        {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await store
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName,
                    sessionKey, cancellationToken).ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out _)
                || state!.Phase != DomainSharedProjectionRebuildPhase.Accumulating
                || state.AcceptedAggregateCount != ownerOrdinal || !state.Partitioned)
                return false;
            if (state.PendingScratchChunkCount <= chunkOrdinal)
            {
                if (state.PendingScratchChunkCount != chunkOrdinal
                    && !(chunkOrdinal == 0 && state.PendingScratchChunkCount == -1))
                    return false;
                if (!await TrySaveAsync(store, handler.RebuildStoreName, sessionKey,
                    state with { PendingScratchChunkCount = chunkOrdinal + 1 }, entry.ETag,
                    cancellationToken).ConfigureAwait(false)) continue;
                continue;
            }
            ReadModelEntry<DomainSharedProjectionStoredPartition> prior = await store
                .GetAsync<DomainSharedProjectionStoredPartition>(handler.RebuildStoreName,
                    key, cancellationToken).ConfigureAwait(false);
            if (prior.Value is { } saved)
                return saved.Ordinal == ownerOrdinal && saved.ChunkOrdinal == chunkOrdinal
                    && saved.AggregateId == owner && saved.HistoryFingerprint == historyFingerprint
                    && saved.State.AsSpan().SequenceEqual(bytes);
            if (await coordinator.TryWriteScratchAsync(FencedScope(handler, identity),
                identity.OperationId, key, chunk, OwnerScratchBatchId(identity, ownerOrdinal,
                    chunkOrdinal), ReadModelBatchConcurrency.CreateOnly, cancellationToken).ConfigureAwait(false))
                return true;
        }

        return false;
    }

    private static async Task<ProjectionEventDto[]> ReadOwnerPartAsync(
        IReadModelStore store, string storeName, string sessionKey,
        long ownerOrdinal, string owner, int partOrdinal, CancellationToken cancellationToken)
    {
        DomainSharedProjectionTransportPart part = (await store
            .GetAsync<DomainSharedProjectionTransportPart>(storeName,
                TransportPartKey(sessionKey, ownerOrdinal, partOrdinal), cancellationToken)
            .ConfigureAwait(false)).Value
            ?? throw new InvalidOperationException("A private owner transport part is missing.");
        if (part.OwnerOrdinal != ownerOrdinal || part.AggregateId != owner || part.PartOrdinal != partOrdinal)
            throw new InvalidOperationException("A private owner transport part conflicts with the sealed owner.");
        byte[] bytes = await ReadTransportPartBytesAsync(store, storeName, sessionKey,
            part, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A private owner transport part is invalid.");
        ProjectionEventDto[] events = JsonSerializer.Deserialize<ProjectionEventDto[]>(bytes, s_deliveryJson)
            ?? throw new InvalidOperationException("A private owner transport part is empty.");
        if (events.Length is < 1 or > 256)
            throw new InvalidOperationException("A private owner transport part has an invalid event count.");
        return events;
    }

    private static async Task<int> ReadExpectedOwnerEventCountAsync(
        IReadModelStore store, string storeName, string sessionKey,
        long ownerOrdinal, string owner, int partCount, CancellationToken cancellationToken)
    {
        if (partCount == 0) return 0;
        ProjectionEventDto[] tail = await ReadOwnerPartAsync(store, storeName, sessionKey,
            ownerOrdinal, owner, partCount - 1, cancellationToken).ConfigureAwait(false);
        return tail[^1] is { } last && last.SequenceNumber is > 0 and <= int.MaxValue
            ? (int)last.SequenceNumber : -1;
    }

    private static string TransportPartKey(string sessionKey, long ownerOrdinal, int partOrdinal)
        => sessionKey + ":transport:" + ownerOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":" + partOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string TransportFragmentKey(string sessionKey, long ownerOrdinal,
        int partOrdinal, int fragmentOrdinal)
        => TransportPartKey(sessionKey, ownerOrdinal, partOrdinal) + ":fragment:"
            + fragmentOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<byte[]?> ReadTransportPartBytesAsync(IReadModelStore store,
        string storeName, string sessionKey, DomainSharedProjectionTransportPart part,
        CancellationToken cancellationToken)
    {
        if (part.FragmentCount is < 1 or > 16 || part.ByteLength is < 1 or > 2_097_152)
            return null;
        using var bytes = new MemoryStream(part.ByteLength);
        for (int fragment = 0; fragment < part.FragmentCount; fragment++)
        {
            DomainSharedProjectionTransportFragment? stored = (await store
                .GetAsync<DomainSharedProjectionTransportFragment>(storeName,
                    TransportFragmentKey(sessionKey, part.OwnerOrdinal, part.PartOrdinal, fragment),
                    cancellationToken).ConfigureAwait(false)).Value;
            if (stored?.Data is null || stored.Data.Length == 0 || stored.Data.Length > 128 * 1024)
                return null;
            bytes.Write(stored.Data);
            if (bytes.Length > part.ByteLength) return null;
        }
        byte[] result = bytes.ToArray();
        return result.Length == part.ByteLength
            && string.Equals(Convert.ToHexString(SHA256.HashData(result)), part.Digest, StringComparison.Ordinal)
            ? result : null;
    }

    private static string TransportPartBatchId(DomainSharedProjectionRebuildIdentity identity,
        long ownerOrdinal, int partOrdinal)
        => "transport-scratch:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.OperationId)))
            + ":" + ownerOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":" + partOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string TransportFragmentBatchId(DomainSharedProjectionRebuildIdentity identity,
        long ownerOrdinal, int partOrdinal, int fragmentOrdinal)
        => TransportPartBatchId(identity, ownerOrdinal, partOrdinal) + ":fragment:"
            + fragmentOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<DomainSharedProjectionRebuildResponse> AccumulateAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string historyFingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory(
            request.AggregateId!,
            request.IsErased,
            request.Events!, request.ExpectedHistoryFingerprint);
        long ordinal = request.AggregateOrdinal!.Value;
        if (handler is IAsyncDomainSharedProjectionPartitionedRebuildHandler { EpochEnabled: true } partitioned) {
            return await AccumulatePartitionAsync(
                serviceProvider,
                sessionStore,
                partitioned,
                request,
                historyFingerprint,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (ordinal < state!.AcceptedAggregateCount) {
                DomainSharedProjectionRebuildReceipt receipt = state.Receipts[(int)ordinal];
                return string.Equals(receipt.HistoryFingerprint, historyFingerprint, StringComparison.Ordinal)
                    ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (ordinal > state.AcceptedAggregateCount) {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
            }

            if (state.Phase != DomainSharedProjectionRebuildPhase.Accumulating) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (state.AcceptedAggregateCount >= options.MaxSharedRebuildAggregateCount
                || (state.LastAggregateId is not null
                    && string.CompareOrdinal(request.AggregateId, state.LastAggregateId) <= 0)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.MalformedOutcome);
            }

            byte[] candidateState = state.CandidateState;
            if (!request.IsErased) {
                var candidate = new DomainSharedProjectionRebuildCandidate(state.CandidateState);
                DomainSharedProjectionRebuildCandidate accumulated = await handler
                    .AccumulateAsync(
                        identity,
                        candidate,
                        DomainProjectionDispatcher.UpcastRequest(serviceProvider,
                            new ProjectionRequest(identity.TenantId, identity.Domain, request.AggregateId!, request.Events!)),
                        cancellationToken)
                    .ConfigureAwait(false);
                candidateState = CopyAndValidateCandidate(accumulated, options);
            }

            var newReceipt = new DomainSharedProjectionRebuildReceipt(ordinal, historyFingerprint);
            DomainSharedProjectionRebuildSessionState updated = state with {
                CandidateState = candidateState,
                AcceptedAggregateCount = state.AcceptedAggregateCount + 1,
                InventoryFingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                    state.InventoryFingerprint,
                    ordinal,
                    historyFingerprint),
                LastAggregateId = request.AggregateId,
                Receipts = [.. state.Receipts, newReceipt],
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<DomainSharedProjectionRebuildResponse> AccumulatePartitionAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionPartitionedRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        string historyFingerprint,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        long ordinal = request.AggregateOrdinal!.Value;
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (!state!.Partitioned) {
                return CorruptState();
            }

            if (ordinal < state.AcceptedAggregateCount) {
                ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> accepted = await sessionStore
                    .GetAsync<DomainSharedProjectionStoredPartitionManifest>(
                        handler.RebuildStoreName, PartitionManifestKey(sessionKey, ordinal), cancellationToken)
                    .ConfigureAwait(false);
                return accepted.Value is { } receipt
                    && receipt.Ordinal == ordinal
                    && string.Equals(receipt.AggregateId, request.AggregateId, StringComparison.Ordinal)
                    && string.Equals(receipt.HistoryFingerprint, historyFingerprint, StringComparison.Ordinal)
                    ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (ordinal > state.AcceptedAggregateCount) {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
            }

            if (state.Phase != DomainSharedProjectionRebuildPhase.Accumulating
                || state.AcceptedAggregateCount >= options.MaxSharedRebuildAggregateCount
                || (state.LastAggregateId is not null
                    && string.CompareOrdinal(request.AggregateId, state.LastAggregateId) <= 0)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            SharedProjectionEpochCoordinator captureCoordinator = serviceProvider.GetRequiredService<SharedProjectionEpochCoordinator>();
            if (!await captureCoordinator.MatchesCapturedOwnerErasureAsync(
                FencedScope(handler, identity), identity.OperationId,
                handler.SourceStreamKey(identity.TenantId, request.AggregateId!), request.IsErased,
                cancellationToken).ConfigureAwait(false))
            {
                return FromState(state, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (!request.IsErased)
            {
                long[] eventPositions = [.. request.Events!.Select(static item => item.GlobalPosition)];
                if (!await captureCoordinator.MatchesCapturedOwnerPositionAsync(
                    FencedScope(handler, identity), identity.OperationId,
                    handler.SourceStreamKey(identity.TenantId, request.AggregateId!), eventPositions,
                    cancellationToken).ConfigureAwait(false))
                {
                    return FromState(state, ProjectionDispatchStatus.Failed,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }
            }

            ProjectionRequest? evolvedOwner = request.IsErased ? null : DomainProjectionDispatcher.UpcastRequest(
                serviceProvider, new ProjectionRequest(identity.TenantId, identity.Domain, request.AggregateId!, request.Events!));
            IReadOnlyList<ReadOnlyMemory<byte>> prepared = request.IsErased
                ? []
                : await handler.PreparePartitionChunksAsync(
                    identity,
                    evolvedOwner!,
                    cancellationToken).ConfigureAwait(false);
            if (prepared is null || prepared.Count > options.MaxRebuildEventCount
                || (!request.IsErased && prepared.Count == 0)) {
                throw new InvalidOperationException("A shared rebuild owner returned an invalid chunk count.");
            }

            byte[][] chunks = prepared.Select(static chunk => chunk.ToArray()).ToArray();
            if (chunks.Any(chunk => chunk.Length == 0 || chunk.Length > options.MaxSharedRebuildCandidateBytes)) {
                throw new InvalidOperationException("A shared rebuild owner chunk exceeds the configured byte limit or is empty.");
            }

            // Reserve the next ordinal before writing any private record. An interrupted
            // accumulation can then reclaim even a chunk prefix without a manifest.
            if (state.PendingScratchChunkCount < 0)
            {
                if (!await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey,
                    state with { PendingScratchChunkCount = chunks.Length }, entry.ETag, cancellationToken)
                    .ConfigureAwait(false))
                {
                    continue;
                }

                continue;
            }

            if (state.PendingScratchChunkCount != chunks.Length)
            {
                return FromState(state, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            string[] chunkHashes = chunks.Select(static chunk => Convert.ToHexString(SHA256.HashData(chunk))).ToArray();
            bool retryChunkSave = false;
            for (int chunkOrdinal = 0; chunkOrdinal < chunks.Length; chunkOrdinal++) {
                var chunk = new DomainSharedProjectionStoredPartition(
                    ordinal, chunkOrdinal, request.AggregateId!, historyFingerprint, chunks[chunkOrdinal]);
                string chunkKey = PartitionChunkKey(sessionKey, ordinal, chunkOrdinal);
                ReadModelEntry<DomainSharedProjectionStoredPartition> priorChunk = await sessionStore
                    .GetAsync<DomainSharedProjectionStoredPartition>(handler.RebuildStoreName, chunkKey, cancellationToken)
                    .ConfigureAwait(false);
                if (priorChunk.Value is null) {
                    if (!await serviceProvider.GetRequiredService<SharedProjectionEpochCoordinator>()
                        .TryWriteScratchAsync(FencedScope(handler, identity), identity.OperationId,
                            chunkKey, chunk, OwnerScratchBatchId(identity, ordinal, chunkOrdinal),
                            ReadModelBatchConcurrency.CreateOnly, cancellationToken).ConfigureAwait(false)) {
                        retryChunkSave = true;
                        break;
                    }
                }
                else if (priorChunk.Value.Ordinal != chunk.Ordinal
                    || priorChunk.Value.ChunkOrdinal != chunk.ChunkOrdinal
                    || !string.Equals(priorChunk.Value.AggregateId, chunk.AggregateId, StringComparison.Ordinal)
                    || !string.Equals(priorChunk.Value.HistoryFingerprint, chunk.HistoryFingerprint, StringComparison.Ordinal)
                    || !priorChunk.Value.State.AsSpan().SequenceEqual(chunk.State)) {
                    return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }
            }

            if (retryChunkSave) {
                continue;
            }

            var manifest = new DomainSharedProjectionStoredPartitionManifest(
                ordinal, request.AggregateId!, historyFingerprint, request.IsErased, chunkHashes,
                request.Events!.LongLength,
                request.IsErased ? null : ComputeCapturedEventDigests(handler, identity,
                    request.AggregateId!, evolvedOwner!.Events),
                request.PartCount ?? 0);
            string manifestKey = PartitionManifestKey(sessionKey, ordinal);
            ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> priorManifest = await sessionStore
                .GetAsync<DomainSharedProjectionStoredPartitionManifest>(handler.RebuildStoreName, manifestKey, cancellationToken)
                .ConfigureAwait(false);
            if (priorManifest.Value is null) {
                if (!await serviceProvider.GetRequiredService<SharedProjectionEpochCoordinator>()
                    .TryWriteScratchAsync(FencedScope(handler, identity), identity.OperationId,
                        manifestKey, manifest, OwnerScratchBatchId(identity, ordinal, -1),
                        ReadModelBatchConcurrency.CreateOnly, cancellationToken).ConfigureAwait(false)) {
                    continue;
                }
            }
            else if (priorManifest.Value.Ordinal != manifest.Ordinal
                || !string.Equals(priorManifest.Value.AggregateId, manifest.AggregateId, StringComparison.Ordinal)
                || !string.Equals(priorManifest.Value.HistoryFingerprint, manifest.HistoryFingerprint, StringComparison.Ordinal)
                || priorManifest.Value.IsErased != manifest.IsErased
                || priorManifest.Value.LastSequenceNumber != manifest.LastSequenceNumber
                || priorManifest.Value.TransportPartCount != manifest.TransportPartCount
                || !priorManifest.Value.ChunkHashes.SequenceEqual(manifest.ChunkHashes, StringComparer.Ordinal)
                || !(priorManifest.Value.CapturedEventDigests ?? []).AsSpan()
                    .SequenceEqual(manifest.CapturedEventDigests ?? [])) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                AcceptedAggregateCount = state.AcceptedAggregateCount + 1,
                InventoryFingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                    state.InventoryFingerprint, ordinal, historyFingerprint),
                LastAggregateId = request.AggregateId,
                PendingScratchChunkCount = -1,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    /// <summary>Computes the durable manifest key for one captured owner partition.</summary>
    /// <param name="sessionKey">The rebuild session key.</param>
    /// <param name="ordinal">The captured owner ordinal.</param>
    /// <returns>The manifest state key.</returns>
    internal static string PartitionManifestKey(string sessionKey, long ordinal)
        => sessionKey + ":partition:" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":manifest";

    /// <summary>Computes the durable key for one captured owner scratch chunk.</summary>
    /// <param name="sessionKey">The rebuild session key.</param>
    /// <param name="ordinal">The captured owner ordinal.</param>
    /// <param name="chunkOrdinal">The chunk ordinal within that owner.</param>
    /// <returns>The scratch chunk state key.</returns>
    internal static string PartitionChunkKey(string sessionKey, long ordinal, int chunkOrdinal)
        => sessionKey + ":partition:" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":chunk:" + chunkOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static string PartitionHashPageKey(string sessionKey, long ordinal, int pageOrdinal)
        => sessionKey + ":partition:" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":hash-page:" + pageOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string OwnerHashPageBatchId(DomainSharedProjectionRebuildIdentity identity, long ordinal,
        int pageOrdinal)
        => OwnerScratchBatchId(identity, ordinal, pageOrdinal) + ":hash-page";

    private static async Task DeleteHashPagesAsync(IReadModelStore store, IReadModelBatchStore batches,
        SharedProjectionEpochCoordinator coordinator, SharedProjectionScope scope,
        DomainSharedProjectionRebuildIdentity identity, string storeName, string sessionKey,
        long ownerOrdinal, int chunkCount, CancellationToken cancellationToken)
    {
        int pageCount = (int)(((long)chunkCount + ChunkHashesPerPage - 1) / ChunkHashesPerPage);
        for (int pageOrdinal = 0; pageOrdinal < pageCount; pageOrdinal++)
        {
            await DeleteScratchKeysAsync(store, batches, identity, storeName,
                "hash-page:" + ownerOrdinal + ":" + pageOrdinal,
                [PartitionHashPageKey(sessionKey, ownerOrdinal, pageOrdinal)],
                cancellationToken).ConfigureAwait(false);
            await coordinator.ReclaimScratchReceiptAsync(scope,
                OwnerHashPageBatchId(identity, ownerOrdinal, pageOrdinal), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string OwnerScratchBatchId(DomainSharedProjectionRebuildIdentity identity, long ordinal,
        int chunkOrdinal)
        => "owner-scratch:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.OperationId)))
            + ":" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":" + chunkOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Resumes recoverable private scratch cleanup for a terminal rebuild session.</summary>
    /// <param name="serviceProvider">The service provider for batch and epoch services.</param>
    /// <param name="sessionStore">The durable rebuild session store.</param>
    /// <param name="handler">The rebuild handler that owns the session.</param>
    /// <param name="identity">The terminal rebuild identity.</param>
    /// <param name="options">The bounded retry options.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>A task that completes when terminal cleanup has been reconciled.</returns>
    internal static async Task ReconcilePartitionScratchAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken)
    {
        if (handler is not IAsyncDomainSharedProjectionPartitionedRebuildHandler { EpochEnabled: true })
        {
            return;
        }

        IReadModelBatchStore batches = serviceProvider.GetService<IReadModelBatchStore>()
            ?? throw new InvalidOperationException("Partition scratch cleanup requires a read-model batch store.");
        SharedProjectionEpochCoordinator coordinator = serviceProvider.GetRequiredService<SharedProjectionEpochCoordinator>();
        SharedProjectionScope scope = FencedScope((IAsyncDomainSharedProjectionEpochHandler)handler, identity);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++)
        {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState state = entry.Value
                ?? throw new InvalidOperationException("Partition scratch session disappeared.");
            if (state.Phase is not (DomainSharedProjectionRebuildPhase.Committed or DomainSharedProjectionRebuildPhase.Aborted))
            {
                return;
            }

            if (!state.ScratchIndexesCleaned)
            {
                if (handler is IAsyncDomainSharedProjectionScratchCleanupHandler scratchHandler)
                {
                    var cursor = new DomainSharedProjectionPartitionCursor(sessionStore, handler.RebuildStoreName, state);
                    var page = new List<string>(32);
                    int pageOrdinal = 0;
                    await foreach (string key in scratchHandler.EnumerateScratchKeysAsync(
                        identity, cursor, cancellationToken).ConfigureAwait(false))
                    {
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            throw new InvalidOperationException("A private rebuild index key is invalid.");
                        }

                        page.Add(key);
                        if (page.Count == 32)
                        {
                            await DeleteScratchKeysAsync(sessionStore, batches, identity, handler.RebuildStoreName,
                                "index:" + pageOrdinal++, page, cancellationToken).ConfigureAwait(false);
                            page.Clear();
                        }
                    }

                    if (page.Count != 0)
                    {
                        await DeleteScratchKeysAsync(sessionStore, batches, identity, handler.RebuildStoreName,
                            "index:" + pageOrdinal, page, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (!await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey,
                    state with { ScratchIndexesCleaned = true }, entry.ETag, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                continue;
            }

            if (state.ScratchCleanedOwnerCount > 0)
            {
                await DeleteScratchKeysAsync(sessionStore, batches, identity, handler.RebuildStoreName,
                    "manifest:" + (state.ScratchCleanedOwnerCount - 1),
                    [PartitionManifestKey(sessionKey, state.ScratchCleanedOwnerCount - 1)],
                    cancellationToken).ConfigureAwait(false);
                await coordinator.ReclaimScratchReceiptAsync(scope,
                    OwnerScratchBatchId(identity, state.ScratchCleanedOwnerCount - 1, -1),
                    cancellationToken).ConfigureAwait(false);
            }

            if (state.ScratchCleanedOwnerCount == state.AcceptedAggregateCount)
            {
                if (state.TransportOwnerOrdinal >= state.AcceptedAggregateCount)
                {
                    for (int part = 0; part < state.TransportReservedPartCount; part++)
                    {
                        await DeleteTransportPartAsync(sessionStore, batches, coordinator, scope,
                            identity, handler.RebuildStoreName, sessionKey, state.TransportOwnerOrdinal,
                            part, part == state.TransportNextPart
                                ? state.TransportReservedFragmentCount : 0,
                            cancellationToken).ConfigureAwait(false);
                    }
                }

                if (state.PendingScratchChunkCount >= 0)
                {
                    long pendingOrdinal = state.AcceptedAggregateCount;
                    if (handler is IAsyncDomainSharedProjectionStreamedOwnerHandler)
                        await DeleteHashPagesAsync(sessionStore, batches, coordinator, scope, identity,
                            handler.RebuildStoreName, sessionKey, pendingOrdinal,
                            state.PendingScratchChunkCount, cancellationToken).ConfigureAwait(false);
                    for (int first = 0; first < state.PendingScratchChunkCount; first += 32)
                    {
                        string[] pendingKeys = [.. Enumerable.Range(first,
                            Math.Min(32, state.PendingScratchChunkCount - first))
                            .Select(chunk => PartitionChunkKey(sessionKey, pendingOrdinal, chunk))];
                        await DeleteScratchKeysAsync(sessionStore, batches, identity, handler.RebuildStoreName,
                            "pending-chunks:" + pendingOrdinal + ":" + first, pendingKeys,
                            cancellationToken).ConfigureAwait(false);
                        for (int chunk = first; chunk < first + pendingKeys.Length; chunk++)
                        {
                            await coordinator.ReclaimScratchReceiptAsync(scope,
                                OwnerScratchBatchId(identity, pendingOrdinal, chunk), cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }

                    await DeleteScratchKeysAsync(sessionStore, batches, identity, handler.RebuildStoreName,
                        "pending-manifest:" + pendingOrdinal,
                        [PartitionManifestKey(sessionKey, pendingOrdinal)], cancellationToken).ConfigureAwait(false);
                    await coordinator.ReclaimScratchReceiptAsync(scope,
                        OwnerScratchBatchId(identity, pendingOrdinal, -1), cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            long ordinal = state.ScratchCleanedOwnerCount;
            ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> manifest = await sessionStore
                .GetAsync<DomainSharedProjectionStoredPartitionManifest>(
                    handler.RebuildStoreName, PartitionManifestKey(sessionKey, ordinal), cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionStoredPartitionManifest value = manifest.Value
                ?? throw new InvalidOperationException("A private owner manifest is missing before cleanup.");
            for (int part = 0; part < value.TransportPartCount; part++)
            {
                await DeleteTransportPartAsync(sessionStore, batches, coordinator, scope,
                    identity, handler.RebuildStoreName, sessionKey, ordinal, part, 0,
                    cancellationToken).ConfigureAwait(false);
            }

            if (value.StreamedChunkCount > 0)
                await DeleteHashPagesAsync(sessionStore, batches, coordinator, scope, identity,
                    handler.RebuildStoreName, sessionKey, ordinal, value.MaterializedChunkCount,
                    cancellationToken).ConfigureAwait(false);

            for (int first = 0; first < value.MaterializedChunkCount; first += 32)
            {
                string[] chunkKeys = [.. Enumerable.Range(first, Math.Min(32, value.MaterializedChunkCount - first))
                    .Select(chunk => PartitionChunkKey(sessionKey, ordinal, chunk))];
                await DeleteScratchKeysAsync(sessionStore, batches, identity, handler.RebuildStoreName,
                    "chunks:" + ordinal + ":" + first, chunkKeys, cancellationToken).ConfigureAwait(false);
                for (int chunk = first; chunk < first + chunkKeys.Length; chunk++)
                {
                    await coordinator.ReclaimScratchReceiptAsync(scope,
                        OwnerScratchBatchId(identity, ordinal, chunk), cancellationToken).ConfigureAwait(false);
                }
            }

            if (!await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey,
                state with { ScratchCleanedOwnerCount = ordinal + 1 }, entry.ETag, cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            attempt = -1;
        }

        throw new InvalidOperationException("Partition scratch cleanup retry budget was exhausted.");
    }

    private static async Task DeleteTransportPartAsync(IReadModelStore sessionStore,
        IReadModelBatchStore batches, SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope, DomainSharedProjectionRebuildIdentity identity,
        string storeName, string sessionKey, long ownerOrdinal, int partOrdinal,
        int reservedFragmentCount, CancellationToken cancellationToken)
    {
        DomainSharedProjectionTransportPart? part = (await sessionStore
            .GetAsync<DomainSharedProjectionTransportPart>(storeName,
                TransportPartKey(sessionKey, ownerOrdinal, partOrdinal), cancellationToken)
            .ConfigureAwait(false)).Value;
        int fragmentCount = part?.FragmentCount ?? reservedFragmentCount;
        if (fragmentCount is < 0 or > 16)
            throw new InvalidOperationException("Private owner transport fragment count is invalid.");
        if (fragmentCount > 0)
        {
            string[] fragmentKeys = [.. Enumerable.Range(0, fragmentCount)
                .Select(fragment => TransportFragmentKey(sessionKey, ownerOrdinal, partOrdinal, fragment))];
            await DeleteScratchKeysAsync(sessionStore, batches, identity, storeName,
                "transport-fragments:" + ownerOrdinal + ":" + partOrdinal,
                fragmentKeys, cancellationToken).ConfigureAwait(false);
            for (int fragment = 0; fragment < fragmentCount; fragment++)
                await coordinator.ReclaimScratchReceiptAsync(scope,
                    TransportFragmentBatchId(identity, ownerOrdinal, partOrdinal, fragment),
                    cancellationToken).ConfigureAwait(false);
        }
        await DeleteScratchKeysAsync(sessionStore, batches, identity, storeName,
            "transport-part:" + ownerOrdinal + ":" + partOrdinal,
            [TransportPartKey(sessionKey, ownerOrdinal, partOrdinal)], cancellationToken).ConfigureAwait(false);
        await coordinator.ReclaimScratchReceiptAsync(scope,
            TransportPartBatchId(identity, ownerOrdinal, partOrdinal), cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteScratchKeysAsync(
        IReadModelStore sessionStore,
        IReadModelBatchStore batches,
        DomainSharedProjectionRebuildIdentity identity,
        string storeName,
        string pageId,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        var batch = new ReadModelBatch(
            new ReadModelBatchScope(storeName, identity.TenantId, identity.Domain,
                "shared-" + identity.ProjectionType, identity.ProjectionType,
                "scratch-cleanup:" + Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(identity.OperationId))) + ":" + pageId),
            [.. keys.Select(static key => ReadModelBatchOperation.Delete(
                key, ReadModelBatchConcurrency.IdempotentAbsent))]);
        ReadModelBatchResult result = await batches.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Private rebuild scratch cleanup is incomplete.");
        }

        IReadModelStore store = batches as IReadModelStore ?? sessionStore;
        IReadModelConditionalEraser? eraser = batches as IReadModelConditionalEraser
            ?? sessionStore as IReadModelConditionalEraser;
        if (eraser is null)
        {
            throw new InvalidOperationException("Private rebuild scratch cleanup requires conditional erasure.");
        }

        string markerKey = batch.Scope.ComputeMarkerKey();
        ReadModelEntry<ReadModelBatchMarker> marker = await store.GetAsync<ReadModelBatchMarker>(
            storeName, markerKey, cancellationToken).ConfigureAwait(false);
        if (marker.Value is not null
            && (marker.Value.Status is not (ReadModelBatchMarkerStatus.Completed or ReadModelBatchMarkerStatus.Aborted)
                || string.IsNullOrWhiteSpace(marker.ETag)
                || !await eraser.TryEraseAsync(storeName, markerKey, marker.ETag, cancellationToken)
                    .ConfigureAwait(false)))
        {
            throw new InvalidOperationException("Private rebuild scratch receipt cleanup is incomplete.");
        }
    }

    private static string BeginPageKey(DomainSharedProjectionRebuildIdentity identity, int ordinal)
        => DomainSharedProjectionRebuildSessionKey.Compute(identity) + ":begin:" + ordinal.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

    internal static string BeginLeaseKey(DomainSharedProjectionRebuildIdentity identity)
        => "shared-begin-lease:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            identity.TenantId + "\0" + identity.Domain + "\0" + identity.ProjectionType)));

    internal static async Task ReclaimBeginPagesAsync(IReadModelStore sessionStore,
        IReadModelBatchStore batches, IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity, CancellationToken cancellationToken)
    {
        for (int first = 0; first < 128; first += 32)
        {
            string[] keys = [.. Enumerable.Range(first, 32).Select(ordinal => BeginPageKey(identity, ordinal))];
            await DeleteScratchKeysAsync(sessionStore, batches, identity, handler.RebuildStoreName,
                "begin-abandoned:" + first, keys, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BeginPageDigest(IReadOnlyDictionary<string, long> heads,
        IReadOnlyList<string> erased)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Heads = heads.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            Erased = erased.OrderBy(item => item, StringComparer.Ordinal).ToArray()
        })));

    private static async Task<DomainSharedProjectionRebuildResponse> BeginPartAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        CancellationToken cancellationToken)
    {
        if (handler is not IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true }) return Unsupported();
        IReadModelBatchStore? batches = serviceProvider.GetService<IReadModelBatchStore>();
        if (batches is null) return Unsupported();
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(request.Identity);
        ReadModelEntry<DomainSharedProjectionRebuildSessionState> session = await sessionStore
            .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
            .ConfigureAwait(false);
        if (session.Value is { } existing)
            return FromState(existing, ProjectionDispatchStatus.AlreadyCompleted, null);
        string leaseKey = BeginLeaseKey(request.Identity);
        ReadModelEntry<DomainSharedProjectionRebuildIdentity> lease = await sessionStore
            .GetAsync<DomainSharedProjectionRebuildIdentity>(handler.RebuildStoreName, leaseKey, cancellationToken)
            .ConfigureAwait(false);
        if (lease.Value != request.Identity)
        {
            if (!await sessionStore.TrySaveAsync(handler.RebuildStoreName, leaseKey, request.Identity,
                lease.ETag ?? string.Empty, cancellationToken).ConfigureAwait(false))
                return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Retryable,
                    0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                    ProjectionDispatchReasonCodes.PartialRetry);
        }
        await DomainSharedProjectionBeginCleanup.RegisterAsync(sessionStore, handler.RebuildStoreName,
            request.Identity, cancellationToken).ConfigureAwait(false);
        int ordinal = request.PartOrdinal!.Value;
        Dictionary<string, long> heads = request.SourceHighWatermarks!
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        string[] erased = [.. (request.ErasedOwnerIds ?? []).OrderBy(item => item, StringComparer.Ordinal)];
        var proposed = new DomainSharedProjectionBeginInventoryPage(ordinal, heads, erased,
            BeginPageDigest(heads, erased));
        string key = BeginPageKey(request.Identity, ordinal);
        if (await sessionStore.TrySaveAsync(handler.RebuildStoreName, key, proposed, string.Empty,
            cancellationToken).ConfigureAwait(false))
        {
            ReadModelEntry<DomainSharedProjectionRebuildIdentity> currentLease = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildIdentity>(handler.RebuildStoreName, leaseKey, cancellationToken)
                .ConfigureAwait(false);
            if (currentLease.Value != request.Identity)
            {
                await DeleteScratchKeysAsync(sessionStore, batches, request.Identity, handler.RebuildStoreName,
                    "begin-displaced:" + ordinal, [key], cancellationToken).ConfigureAwait(false);
                return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Retryable,
                    0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                    ProjectionDispatchReasonCodes.PartialRetry);
            }
            return new(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Completed,
                0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null, null);
        }

        ReadModelEntry<DomainSharedProjectionBeginInventoryPage> stored = await sessionStore
            .GetAsync<DomainSharedProjectionBeginInventoryPage>(handler.RebuildStoreName, key, cancellationToken)
            .ConfigureAwait(false);
        return stored.Value is { } page && page.Ordinal == ordinal
            && StringComparer.Ordinal.Equals(page.Digest, proposed.Digest)
            ? new(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.AlreadyCompleted,
                0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null, null)
            : Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Failed,
                0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
    }

    private static async Task<DomainSharedProjectionRebuildResponse> BeginAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        if (request.PartCount is { } pageCount)
        {
            if (serviceProvider.GetService<IReadModelBatchStore>() is null) return Unsupported();
            string[] keys = [.. Enumerable.Range(0, pageCount).Select(ordinal => BeginPageKey(identity, ordinal))];
            string pagedSessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> session = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, pagedSessionKey,
                    cancellationToken).ConfigureAwait(false);
            if (session.Value is { } existing)
            {
                if (!StringComparer.Ordinal.Equals(existing.CaptureInventoryFingerprint,
                    request.CaptureInventoryFingerprint))
                    return FromState(existing, ProjectionDispatchStatus.Failed,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                return FromState(existing, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            ReadModelEntry<DomainSharedProjectionRebuildIdentity> lease = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildIdentity>(handler.RebuildStoreName,
                    BeginLeaseKey(identity), cancellationToken).ConfigureAwait(false);
            if (lease.Value != identity)
                return Failure(DomainSharedProjectionRebuildPhase.Accumulating,
                    ProjectionDispatchStatus.Failed, 0,
                    DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);

            var heads = new Dictionary<string, long>(StringComparer.Ordinal);
            var erased = new HashSet<string>(StringComparer.Ordinal);
            for (int ordinal = 0; ordinal < pageCount; ordinal++)
            {
                ReadModelEntry<DomainSharedProjectionBeginInventoryPage> entry = await sessionStore
                    .GetAsync<DomainSharedProjectionBeginInventoryPage>(handler.RebuildStoreName, keys[ordinal],
                        cancellationToken).ConfigureAwait(false);
                DomainSharedProjectionBeginInventoryPage? page = entry.Value;
                if (page is null || page.Ordinal != ordinal || page.Heads.Count == 0
                    || !StringComparer.Ordinal.Equals(page.Digest,
                        BeginPageDigest(page.Heads, page.ErasedOwners)))
                    return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Failed,
                        0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                foreach (KeyValuePair<string, long> item in page.Heads)
                {
                    if (!heads.TryAdd(item.Key, item.Value))
                        return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Failed,
                            0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                            ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }

                foreach (string owner in page.ErasedOwners)
                {
                    if (!page.Heads.ContainsKey(owner) || !erased.Add(owner))
                        return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Failed,
                            0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                            ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }
            }

            if (heads.Count != request.ExpectedAggregateCount)
                return Failure(DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Failed,
                    0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            DomainSharedProjectionRebuildResponse begun = await BeginAsync(serviceProvider, sessionStore, handler,
                request with { PartCount = null, ExpectedAggregateCount = null,
                    SourceHighWatermarks = heads, ErasedOwnerIds = [.. erased] }, options,
                cancellationToken).ConfigureAwait(false);
            return begun;
        }
        if (handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true } epochHandler) {
            SharedProjectionEpochCoordinator? coordinator = serviceProvider.GetService<SharedProjectionEpochCoordinator>();
            if (coordinator is null || request.SourceHighWatermarks is null
                || string.IsNullOrWhiteSpace(request.CaptureInventoryFingerprint)) {
                return Unsupported();
            }

            SharedProjectionScope scope = epochHandler.CreateScope(identity.TenantId);
            SharedProjectionEpochStatus prior = await coordinator.GetStatusAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            if (prior.OperationId is { Length: > 0 } priorOperation
                && (!string.Equals(priorOperation, identity.OperationId, StringComparison.Ordinal)
                    || epochHandler.GlobalEpochScope
                        && !string.Equals(prior.OperationTenantId, identity.TenantId, StringComparison.Ordinal))
                && !prior.IsBuilding && !prior.IsAborting)
            {
                // The previous Commit may have returned after durable promotion, before private
                // cleanup. Reconcile it before admitting another inventory so repeated rebuilds
                // retain at most one operation's scratch even across process restarts.
                string priorTenantId = prior.OperationTenantId
                    ?? (epochHandler.GlobalEpochScope
                        ? throw new InvalidOperationException("The prior global rebuild session has no tenant identity.")
                        : identity.TenantId);
                var priorIdentity = identity with { TenantId = priorTenantId, OperationId = priorOperation };
                ReadModelEntry<DomainSharedProjectionRebuildSessionState> priorEntry = await sessionStore
                    .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName,
                        DomainSharedProjectionRebuildSessionKey.Compute(priorIdentity), cancellationToken)
                    .ConfigureAwait(false);
                if (priorEntry.Value is { Partitioned: true,
                    Phase: DomainSharedProjectionRebuildPhase.Committed or DomainSharedProjectionRebuildPhase.Aborted } previous)
                {
                    await ReconcilePartitionScratchAsync(serviceProvider, sessionStore, handler,
                        previous.Identity, options, cancellationToken).ConfigureAwait(false);
                }
            }

            _ = await coordinator.BeginAsync(
                scope,
                identity.OperationId,
                request.CaptureInventoryFingerprint,
                request.SourceHighWatermarks.ToDictionary(
                    item => epochHandler.SourceStreamKey(identity.TenantId, item.Key),
                    static item => item.Value, StringComparer.Ordinal),
                cancellationToken,
                request.ErasedOwnerIds?.Select(owner =>
                    epochHandler.SourceStreamKey(identity.TenantId, owner)).ToArray(),
                allowCoveredJournal: epochHandler is IAsyncDomainSharedProjectionPartitionedRebuildHandler,
                operationTenantId: identity.TenantId)
                .ConfigureAwait(false);
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        byte[]? emptyCandidate = null;
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            if (entry.Value is not null) {
                if (handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true }
                    && !string.Equals(
                        entry.Value.CaptureInventoryFingerprint,
                        request.CaptureInventoryFingerprint,
                        StringComparison.Ordinal)) {
                    return FromState(entry.Value, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }

                return TryValidateState(entry.Value, identity, options, out DomainSharedProjectionRebuildResponse? failure)
                    ? FromState(entry.Value, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : failure!;
            }

            if (entry.ETag is not null) {
                return CorruptState();
            }

            if (emptyCandidate is null) {
                DomainSharedProjectionRebuildCandidate candidate = await handler
                    .CreateEmptyCandidateAsync(identity, cancellationToken)
                    .ConfigureAwait(false);
                emptyCandidate = CopyAndValidateCandidate(candidate, options);
            }

            var state = new DomainSharedProjectionRebuildSessionState(
                DomainSharedProjectionRebuildProtocol.Version,
                identity,
                DomainSharedProjectionRebuildPhase.Accumulating,
                emptyCandidate,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                [],
                null,
                null,
                null,
                null,
                request.CaptureInventoryFingerprint,
                handler is IAsyncDomainSharedProjectionPartitionedRebuildHandler { EpochEnabled: true });
            if (await sessionStore
                .TrySaveAsync(handler.RebuildStoreName, sessionKey, state, string.Empty, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(state, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<ReadModelBatch> BuildBatchAsync(
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildSessionState state,
        string sessionKey,
        CancellationToken cancellationToken) {
        DomainProjectionRebuildPlan plan = await BuildPlanAsync(sessionStore, handler, state, cancellationToken).ConfigureAwait(false);
        if (plan.Operations.Any(operation => string.Equals(operation.Key, sessionKey, StringComparison.Ordinal))) {
            throw new InvalidOperationException("The finalized shared rebuild manifest targets its private session key.");
        }

        return new ReadModelBatch(
            new ReadModelBatchScope(
                plan.StoreName,
                state.Identity.TenantId,
                state.Identity.Domain,
                "shared-" + state.Identity.ProjectionType,
                state.Identity.ProjectionType,
                state.Identity.OperationId),
            plan.Operations);
    }

    private static async Task<DomainProjectionRebuildPlan> BuildPlanAsync(
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildSessionState state,
        CancellationToken cancellationToken) {
        DomainProjectionRebuildPlan plan;
        if (handler is IAsyncDomainSharedProjectionPartitionedRebuildHandler { EpochEnabled: true } partitioned) {
            if (!state.Partitioned) {
                throw new InvalidOperationException("The shared rebuild session is not partitioned.");
            }

            var partitions = new List<DomainSharedProjectionRebuildPartition>();
            string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(state.Identity);
            string inventoryFingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
            string? previousAggregateId = null;
            for (long ordinal = 0; ordinal < state.AcceptedAggregateCount; ordinal++) {
                ReadModelEntry<DomainSharedProjectionStoredPartitionManifest> entry = await sessionStore
                    .GetAsync<DomainSharedProjectionStoredPartitionManifest>(
                        handler.RebuildStoreName,
                        PartitionManifestKey(sessionKey, ordinal),
                        cancellationToken).ConfigureAwait(false);
                DomainSharedProjectionStoredPartitionManifest manifest = entry.Value
                    ?? throw new InvalidOperationException("A shared rebuild owner manifest is missing.");
                if (manifest.Ordinal != ordinal
                    || manifest.ChunkHashes is null
                    || string.IsNullOrWhiteSpace(manifest.HistoryFingerprint)
                    || string.IsNullOrWhiteSpace(manifest.AggregateId)
                    || (manifest.IsErased && manifest.ChunkHashes.Length != 0)
                    || (!manifest.IsErased && manifest.ChunkHashes.Length == 0)
                    || (previousAggregateId is not null
                        && string.CompareOrdinal(previousAggregateId, manifest.AggregateId) >= 0)) {
                    throw new InvalidOperationException("A shared rebuild owner manifest does not match the accepted inventory.");
                }

                previousAggregateId = manifest.AggregateId;
                inventoryFingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                    inventoryFingerprint, ordinal, manifest.HistoryFingerprint);
                for (int chunkOrdinal = 0; chunkOrdinal < manifest.ChunkHashes.Length; chunkOrdinal++) {
                    ReadModelEntry<DomainSharedProjectionStoredPartition> chunkEntry = await sessionStore
                        .GetAsync<DomainSharedProjectionStoredPartition>(
                            handler.RebuildStoreName,
                            PartitionChunkKey(sessionKey, ordinal, chunkOrdinal),
                            cancellationToken).ConfigureAwait(false);
                    DomainSharedProjectionStoredPartition chunk = chunkEntry.Value
                        ?? throw new InvalidOperationException("A shared rebuild owner chunk is missing.");
                    if (chunk.Ordinal != ordinal || chunk.ChunkOrdinal != chunkOrdinal
                        || !string.Equals(chunk.AggregateId, manifest.AggregateId, StringComparison.Ordinal)
                        || !string.Equals(chunk.HistoryFingerprint, manifest.HistoryFingerprint, StringComparison.Ordinal)
                        || chunk.State is null || chunk.State.Length == 0
                        || !string.Equals(Convert.ToHexString(SHA256.HashData(chunk.State)),
                            manifest.ChunkHashes[chunkOrdinal], StringComparison.Ordinal)) {
                        throw new InvalidOperationException("A shared rebuild owner chunk conflicts with its manifest.");
                    }

                    partitions.Add(new DomainSharedProjectionRebuildPartition(
                        ordinal, manifest.AggregateId, chunk.State, chunkOrdinal));
                }
            }

            if (!string.Equals(inventoryFingerprint, state.InventoryFingerprint, StringComparison.Ordinal)
                || !string.Equals(previousAggregateId, state.LastAggregateId, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The shared rebuild partitions conflict with the accepted inventory.");
            }

            plan = await partitioned.FinalizePartitionsAsync(state.Identity, partitions, cancellationToken)
                .ConfigureAwait(false);
        }
        else {
            plan = await handler
                .FinalizeAsync(
                    state.Identity,
                    new DomainSharedProjectionRebuildCandidate(state.CandidateState),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (!string.Equals(plan.StoreName, handler.RebuildStoreName, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The finalized shared rebuild manifest is outside its admitted store boundary.");
        }

        return plan;
    }

    private static async Task<DomainSharedProjectionRebuildResponse> CommitAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        if (handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true } epochHandler) {
            return await FencedCommitAsync(serviceProvider, sessionStore, epochHandler, identity, options, cancellationToken)
                .ConfigureAwait(false);
        }

        IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
        if (stagingStore is null) {
            return Unsupported();
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                    handler,
                    state,
                    cancellationToken).ConfigureAwait(false);
                return incomplete ?? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase != DomainSharedProjectionRebuildPhase.Prepared) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            ReadModelBatch batch = await BuildBatchAsync(sessionStore, handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult committed = await stagingStore.CommitAsync(batch, cancellationToken).ConfigureAwait(false);
            if (committed.Status != ReadModelBatchStagingStatus.Committed) {
                return FromStaging(state, committed);
            }

            ReadModelBatchStagingResult verified = await stagingStore.VerifyAsync(batch, cancellationToken).ConfigureAwait(false);
            if (verified.Status != ReadModelBatchStagingStatus.Committed
                || !string.Equals(committed.Fingerprint, verified.Fingerprint, StringComparison.Ordinal)
                || !FingerprintMatches(state, committed.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.HandlerFailure);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Committed,
                BatchFingerprint = committed.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                    handler,
                    updated,
                    cancellationToken).ConfigureAwait(false);
                return incomplete ?? FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static byte[] CopyAndValidateCandidate(
        DomainSharedProjectionRebuildCandidate? candidate,
        ProjectionDispatchOptions options) {
        if (candidate is null || candidate.State.Length > options.MaxSharedRebuildCandidateBytes) {
            throw new InvalidOperationException("The shared rebuild handler returned an invalid or over-limit candidate.");
        }

        return candidate.CopyState();
    }

    private static byte[]? CopyAndValidateCompletionState(
        ReadOnlyMemory<byte> completionState,
        ProjectionDispatchOptions options) {
        if (completionState.IsEmpty) {
            return null;
        }

        if (completionState.Length > options.MaxSharedRebuildCandidateBytes) {
            throw new InvalidOperationException("The shared rebuild handler returned over-limit completion state.");
        }

        return completionState.ToArray();
    }

    private static async Task<DomainSharedProjectionRebuildResponse?> CompleteCommittedRebuildAsync(
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildSessionState state,
        CancellationToken cancellationToken) {
        if (handler is not IAsyncDomainSharedProjectionRebuildCompletionHandler completionHandler) {
            return null;
        }

        DomainProjectionHandlerResult result;
        try {
            result = await completionHandler
                .CompleteRebuildAsync(
                    state.Identity,
                    new DomainSharedProjectionRebuildCandidate(state.CandidateState),
                    state.CompletionState ?? ReadOnlyMemory<byte>.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception) {
            return FromState(
                state,
                ProjectionDispatchStatus.Indeterminate,
                ProjectionDispatchReasonCodes.HandlerFailure);
        }
        if (result.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted) {
            return null;
        }

        string reasonCode = string.IsNullOrWhiteSpace(result.ReasonCode)
            ? ProjectionDispatchReasonCodes.HandlerFailure
            : result.ReasonCode;
        return FromState(state, result.Status, reasonCode);
    }

    private static DomainSharedProjectionRebuildResponse CorruptState()
        => Failure(
            DomainSharedProjectionRebuildPhase.Accumulating,
            ProjectionDispatchStatus.Indeterminate,
            0,
            DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            null,
            ProjectionDispatchReasonCodes.HandlerFailure);

    private static async Task<DomainSharedProjectionRebuildResponse> FinalizeAsync(
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        DomainSharedProjectionRebuildIdentity identity = request.Identity;
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase != DomainSharedProjectionRebuildPhase.Accumulating) {
                return state.ExpectedAggregateCount == request.ExpectedAggregateCount
                    && string.Equals(
                        state.ExpectedInventoryFingerprint,
                        request.ExpectedInventoryFingerprint,
                        StringComparison.Ordinal)
                    ? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null)
                    : FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (state.PendingScratchChunkCount >= 0
                || state.AcceptedAggregateCount != request.ExpectedAggregateCount
                || !string.Equals(
                    state.InventoryFingerprint,
                    request.ExpectedInventoryFingerprint,
                    StringComparison.Ordinal)) {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryGap);
            }

            if (handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true }
                && !string.Equals(
                    state.CaptureInventoryFingerprint,
                    request.ExpectedInventoryFingerprint,
                    StringComparison.Ordinal)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainProjectionRebuildPlan? plan = null;
            if (handler is IAsyncDomainSharedProjectionStreamingRebuildHandler streaming && streaming.EpochEnabled) {
                var cursor = new DomainSharedProjectionPartitionCursor(sessionStore, handler.RebuildStoreName, state);
                await foreach (DomainSharedProjectionRebuildPartition _ in cursor.ReadAllAsync(cancellationToken).ConfigureAwait(false)) { }
            }
            else {
                plan = await BuildPlanAsync(sessionStore, handler, state, cancellationToken).ConfigureAwait(false);
                if (plan.Operations.Any(operation => string.Equals(operation.Key, sessionKey, StringComparison.Ordinal))) {
                    throw new InvalidOperationException("The finalized shared rebuild manifest targets its private session key.");
                }
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Finalized,
                ExpectedAggregateCount = request.ExpectedAggregateCount,
                ExpectedInventoryFingerprint = request.ExpectedInventoryFingerprint,
                CompletionState = CopyAndValidateCompletionState(plan?.CompletionState ?? ReadOnlyMemory<byte>.Empty, options),
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static bool FingerprintMatches(DomainSharedProjectionRebuildSessionState state, string fingerprint)
        => !string.IsNullOrWhiteSpace(fingerprint)
            && (state.BatchFingerprint is null
                || string.Equals(state.BatchFingerprint, fingerprint, StringComparison.Ordinal));

    private static DomainSharedProjectionRebuildResponse Failure(
        DomainSharedProjectionRebuildPhase phase,
        ProjectionDispatchStatus status,
        long count,
        string inventoryFingerprint,
        string? batchFingerprint,
        string reasonCode)
        => new(
            DomainSharedProjectionRebuildProtocol.Version,
            phase,
            status,
            count,
            inventoryFingerprint,
            batchFingerprint,
            reasonCode);

    private static DomainSharedProjectionRebuildResponse FromStaging(
        DomainSharedProjectionRebuildSessionState state,
        ReadModelBatchStagingResult result) {
        (ProjectionDispatchStatus Status, string Reason) mapped = result.Status switch {
            ReadModelBatchStagingStatus.Conflict => (
                ProjectionDispatchStatus.Failed,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict),
            _ => (ProjectionDispatchStatus.Indeterminate, ProjectionDispatchReasonCodes.HandlerFailure),
        };
        return FromState(state, mapped.Status, mapped.Reason);
    }

    private static DomainSharedProjectionRebuildResponse FromState(
        DomainSharedProjectionRebuildSessionState state,
        ProjectionDispatchStatus status,
        string? reasonCode)
        => new(
            DomainSharedProjectionRebuildProtocol.Version,
            state.Phase,
            status,
            state.AcceptedAggregateCount,
            state.InventoryFingerprint,
            state.BatchFingerprint,
            reasonCode);

    private static IAsyncDomainSharedProjectionRebuildHandler ResolveHandler(
        IServiceProvider serviceProvider,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        DomainProjectionIdentityOptions identityOptions) {
        IAsyncDomainProjectionHandler[] handlers = DomainProjectionHandlerRouteValidator
            .MaterializeAndValidateNamed(serviceProvider.GetServices<IAsyncDomainProjectionHandler>(), options)
            .ToArray();
        ProjectionDispatchRoute[] routes = [.. handlers
            .Where(handler => string.Equals(handler.Domain, identity.Domain, StringComparison.Ordinal))
            .Select(handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))];
        if (routes.Length == 0
            || !string.Equals(
                ProjectionRouteCatalogFingerprint.Compute(identityOptions.AppId, identityOptions.ServiceVersion, routes),
                identity.CatalogFingerprint,
                StringComparison.Ordinal)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        IAsyncDomainProjectionHandler? handler = handlers.SingleOrDefault(candidate =>
            string.Equals(candidate.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(candidate.ProjectionType, identity.ProjectionType, StringComparison.Ordinal));
        return handler as IAsyncDomainSharedProjectionRebuildHandler
            ?? throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
    }

    private static DomainSharedProjectionRebuildResponse RetryExhausted()
        => Failure(
            DomainSharedProjectionRebuildPhase.Accumulating,
            ProjectionDispatchStatus.Retryable,
            0,
            DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            null,
            ProjectionDispatchReasonCodes.PartialRetry);

    private static async Task<DomainSharedProjectionRebuildResponse> StageAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        if (handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true } epochHandler) {
            return await FencedStageAsync(serviceProvider, sessionStore, epochHandler, identity, options, cancellationToken)
                .ConfigureAwait(false);
        }

        IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
        if (stagingStore is null) {
            return Unsupported();
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                    handler,
                    state,
                    cancellationToken).ConfigureAwait(false);
                return incomplete ?? FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase is not DomainSharedProjectionRebuildPhase.Finalized
                and not DomainSharedProjectionRebuildPhase.Prepared) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            ReadModelBatch batch = await BuildBatchAsync(sessionStore, handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult result = await stagingStore.StageAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ReadModelBatchStagingStatus.Prepared
                and not ReadModelBatchStagingStatus.Committed) {
                return FromStaging(state, result);
            }

            if (!FingerprintMatches(state, result.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = result.Status == ReadModelBatchStagingStatus.Committed
                    ? DomainSharedProjectionRebuildPhase.Committed
                    : DomainSharedProjectionRebuildPhase.Prepared,
                BatchFingerprint = result.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                if (updated.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                    DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                        handler,
                        updated,
                        cancellationToken).ConfigureAwait(false);
                    if (incomplete is not null) {
                        return incomplete;
                    }
                }

                ProjectionDispatchStatus status = state.Phase == updated.Phase
                    ? ProjectionDispatchStatus.AlreadyCompleted
                    : ProjectionDispatchStatus.Completed;
                return FromState(updated, status, null);
            }
        }

        return RetryExhausted();
    }

    private static bool TryValidateState(
        DomainSharedProjectionRebuildSessionState? state,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        out DomainSharedProjectionRebuildResponse? failure) {
        if (state is null) {
            failure = Failure(
                DomainSharedProjectionRebuildPhase.Accumulating,
                ProjectionDispatchStatus.Retryable,
                0,
                DomainSharedProjectionRebuildFingerprint.EmptyInventory,
                null,
                ProjectionDispatchReasonCodes.DeliveryGap);
            return false;
        }

        if (state.Version != DomainSharedProjectionRebuildProtocol.Version
            || state.Identity != identity
            || !Enum.IsDefined(state.Phase)
            || state.CandidateState is null
            || state.CandidateState.Length > options.MaxSharedRebuildCandidateBytes
            || state.CompletionState?.Length > options.MaxSharedRebuildCandidateBytes
            || state.AcceptedAggregateCount < 0
            || state.AcceptedAggregateCount > options.MaxSharedRebuildAggregateCount
            || state.PendingScratchChunkCount < -1
            || state.PendingScratchChunkCount > MaxStreamedOwnerChunkCount(options)
            || state.Receipts is null
            || (state.Partitioned ? state.Receipts.Length != 0 : state.Receipts.LongLength != state.AcceptedAggregateCount)
            || (!state.Partitioned && state.Receipts.Where((receipt, index) => receipt is null || receipt.Ordinal != index).Any())
            || string.IsNullOrWhiteSpace(state.InventoryFingerprint)) {
            failure = CorruptState();
            return false;
        }

        failure = null;
        return true;
    }

    private static async Task<bool> TrySaveAsync(
        IReadModelStore sessionStore,
        string storeName,
        string sessionKey,
        DomainSharedProjectionRebuildSessionState state,
        string? etag,
        CancellationToken cancellationToken) {
        if (etag is null) {
            return false;
        }

        return await sessionStore
            .TrySaveAsync(storeName, sessionKey, state, etag, cancellationToken)
            .ConfigureAwait(false);
    }

    private static DomainSharedProjectionRebuildResponse Unsupported()
        => Failure(
            DomainSharedProjectionRebuildPhase.Accumulating,
            ProjectionDispatchStatus.Failed,
            0,
            DomainSharedProjectionRebuildFingerprint.EmptyInventory,
            null,
            ProjectionDispatchReasonCodes.UnsupportedCapability);

    private static void ValidateComponent(string value) {
        if (string.IsNullOrWhiteSpace(value)
            || Encoding.UTF8.GetByteCount(value) > ReadModelBatchScope.MaxComponentByteLength) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }
    }

    private static void ValidateEventHistory(IReadOnlyList<ProjectionEventDto> events, int maxEventCount) {
        if (events.Count > maxEventCount) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        long expectedSequence = 1;
        foreach (ProjectionEventDto item in events) {
            if (item is null
                || item.SequenceNumber != expectedSequence
                || item.Payload is null
                || string.IsNullOrWhiteSpace(item.EventTypeName)
                || string.IsNullOrWhiteSpace(item.SerializationFormat)) {
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
            }

            expectedSequence++;
        }
    }

    private static void ValidateRequest(
        DomainSharedProjectionRebuildRequest request,
        ProjectionDispatchOptions options) {
        if (request.Version != DomainSharedProjectionRebuildProtocol.Version) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedCapability);
        }

        if (!Enum.IsDefined(request.Action) || request.Identity is null) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        ValidateComponent(request.Identity.TenantId);
        ValidateComponent(request.Identity.Domain);
        ValidateComponent(request.Identity.ProjectionType);
        ValidateComponent(request.Identity.OperationId);
        ValidateComponent(request.Identity.CatalogFingerprint);
        try {
            NamingConventionEngine.ValidateKebabCase(request.Identity.Domain, nameof(request.Identity.Domain));
            NamingConventionEngine.ValidateKebabCase(request.Identity.ProjectionType, nameof(request.Identity.ProjectionType));
        }
        catch (ArgumentException) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.UnsupportedRoute);
        }

        bool hasAggregateFields = request.AggregateOrdinal is not null
            || request.AggregateId is not null
            || request.Events is not null
            || request.IsErased;
        bool hasFinalizeFields = request.ExpectedAggregateCount is not null
            || request.ExpectedInventoryFingerprint is not null;
        bool hasTransportFields = request.PartOrdinal is not null || request.PartCount is not null
            || request.ExpectedHistoryFingerprint is not null;
        if (request.Action is not (DomainSharedProjectionRebuildAction.Begin
            or DomainSharedProjectionRebuildAction.BeginPart or DomainSharedProjectionRebuildAction.Inspect)
            && (request.CaptureInventoryFingerprint is not null || request.SourceHighWatermarks is not null
                || request.ErasedOwnerIds is not null)) {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        if (request.Action == DomainSharedProjectionRebuildAction.BeginPart)
        {
            if (hasAggregateFields || hasFinalizeFields || request.PartOrdinal is null or < 0 or >= 128
                || request.PartCount is not null || request.ExpectedHistoryFingerprint is not null
                || request.CaptureInventoryFingerprint is not null
                || request.SourceHighWatermarks is not { Count: > 0 and <= 200 }
                || JsonSerializer.SerializeToUtf8Bytes(request).Length > 524_288)
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        if (request.Action == DomainSharedProjectionRebuildAction.Begin && request.PartCount is { } beginPartCount)
        {
            if (hasAggregateFields || request.PartOrdinal is not null || request.ExpectedHistoryFingerprint is not null
                || beginPartCount is < 1 or > 128 || request.ExpectedAggregateCount is null or < 1 or > 10_000
                || request.ExpectedInventoryFingerprint is not null
                || request.SourceHighWatermarks is not null || request.ErasedOwnerIds is not null
                || string.IsNullOrWhiteSpace(request.CaptureInventoryFingerprint))
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
            ValidateComponent(request.CaptureInventoryFingerprint!);
        }
        else if (request.Action == DomainSharedProjectionRebuildAction.Begin
            && (request.CaptureInventoryFingerprint is not null || request.SourceHighWatermarks is not null
                || request.ErasedOwnerIds is not null)
            && (string.IsNullOrWhiteSpace(request.CaptureInventoryFingerprint)
                || request.SourceHighWatermarks is null))
        {
            throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
        }

        if (request.Action is DomainSharedProjectionRebuildAction.Begin or DomainSharedProjectionRebuildAction.BeginPart
            && request.SourceHighWatermarks is { } captureHeads)
        {
            if (captureHeads.Any(item => string.IsNullOrWhiteSpace(item.Key)
                    || item.Value < 0
                    || Encoding.UTF8.GetByteCount(item.Key) > ReadModelBatchScope.MaxComponentByteLength)
                || request.ErasedOwnerIds is { } erasedOwners
                    && (erasedOwners.Count != erasedOwners.Distinct(StringComparer.Ordinal).Count()
                        || erasedOwners.Any(owner => string.IsNullOrWhiteSpace(owner)
                            || !captureHeads.ContainsKey(owner)))) {
                throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
            }
            if (request.Action == DomainSharedProjectionRebuildAction.Begin)
                ValidateComponent(request.CaptureInventoryFingerprint!);
        }

        switch (request.Action) {
            case DomainSharedProjectionRebuildAction.Inspect:
                if (hasAggregateFields || hasFinalizeFields || hasTransportFields
                    || string.IsNullOrWhiteSpace(request.CaptureInventoryFingerprint))
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                ValidateComponent(request.CaptureInventoryFingerprint!);
                break;
            case DomainSharedProjectionRebuildAction.Begin:
                if (hasAggregateFields || request.ExpectedInventoryFingerprint is not null
                    || request.ExpectedHistoryFingerprint is not null || request.PartOrdinal is not null
                    || request.PartCount is null && hasFinalizeFields)
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                break;
            case DomainSharedProjectionRebuildAction.BeginPart:
                break;
            case DomainSharedProjectionRebuildAction.Accumulate:
                if (request.AggregateOrdinal is null or < 0
                    || string.IsNullOrWhiteSpace(request.AggregateId)
                    || request.Events is null
                    || hasFinalizeFields || hasTransportFields) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                ValidateComponent(request.AggregateId);
                if (request.IsErased && request.Events.Length != 0) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                DomainProjectionEvolutionAdmission.RequireLegacyEvents(request.Events);
                ValidateEventHistory(request.Events, options.MaxRebuildEventCount);
                break;
            case DomainSharedProjectionRebuildAction.AccumulatePart:
                if (request.AggregateOrdinal is null or < 0
                    || string.IsNullOrWhiteSpace(request.AggregateId)
                    || request.IsErased || request.Events is not { Length: > 0 and <= 256 }
                    || request.PartOrdinal is null or < 0 or >= 10_000
                    || request.PartCount is not null || request.ExpectedHistoryFingerprint is not null
                    || hasFinalizeFields || JsonSerializer.SerializeToUtf8Bytes(request).Length > 2_097_152) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                ValidateComponent(request.AggregateId);
                DomainProjectionEvolutionAdmission.RequireLegacyEvents(request.Events);
                if (request.Events.Any(static item => item.SequenceNumber <= 0 || item.GlobalPosition <= 0)) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                break;
            case DomainSharedProjectionRebuildAction.AccumulateComplete:
                if (request.AggregateOrdinal is null or < 0
                    || string.IsNullOrWhiteSpace(request.AggregateId)
                    || request.Events is not null || request.PartOrdinal is not null
                    || request.PartCount is null or < 0 or > 10_000
                    || request.IsErased && request.PartCount != 0
                    || string.IsNullOrWhiteSpace(request.ExpectedHistoryFingerprint)
                    || hasFinalizeFields) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                ValidateComponent(request.AggregateId);
                ValidateComponent(request.ExpectedHistoryFingerprint!);
                break;
            case DomainSharedProjectionRebuildAction.Finalize:
                if (hasAggregateFields || hasTransportFields
                    || request.ExpectedAggregateCount is null or < 0
                    || string.IsNullOrWhiteSpace(request.ExpectedInventoryFingerprint)) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                break;
            default:
                if (hasAggregateFields || hasFinalizeFields || hasTransportFields) {
                    throw new ProjectionDispatchValidationException(ProjectionDispatchReasonCodes.MalformedOutcome);
                }

                break;
        }
    }

    private static async Task<DomainSharedProjectionRebuildResponse> VerifyAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionRebuildHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        if (handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true } epochHandler) {
            return await FencedVerifyAsync(serviceProvider, sessionStore, epochHandler, identity, options, cancellationToken)
                .ConfigureAwait(false);
        }

        IReadModelBatchStagingStore? stagingStore = serviceProvider.GetService<IReadModelBatchStagingStore>();
        if (stagingStore is null) {
            return Unsupported();
        }

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase is DomainSharedProjectionRebuildPhase.Accumulating or DomainSharedProjectionRebuildPhase.Aborted) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            ReadModelBatch batch = await BuildBatchAsync(sessionStore, handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
            ReadModelBatchStagingResult result = await stagingStore.VerifyAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ReadModelBatchStagingStatus.Prepared
                and not ReadModelBatchStagingStatus.Committed
                and not ReadModelBatchStagingStatus.Aborted) {
                return FromStaging(state, result);
            }

            if (!FingerprintMatches(state, result.Fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildPhase phase = result.Status switch {
                ReadModelBatchStagingStatus.Prepared => DomainSharedProjectionRebuildPhase.Prepared,
                ReadModelBatchStagingStatus.Committed => DomainSharedProjectionRebuildPhase.Committed,
                _ => DomainSharedProjectionRebuildPhase.Aborted,
            };
            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = phase,
                BatchFingerprint = result.Fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                if (updated.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                    DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                        handler,
                        updated,
                        cancellationToken).ConfigureAwait(false);
                    if (incomplete is not null) {
                        return incomplete;
                    }
                }

                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<DomainSharedProjectionRebuildResponse> FencedStageAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        SharedProjectionEpochCoordinator? coordinator = serviceProvider.GetService<SharedProjectionEpochCoordinator>();
        if (coordinator is null) {
            return Unsupported();
        }

        SharedProjectionScope scope = FencedScope(handler, identity);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                return FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase is not DomainSharedProjectionRebuildPhase.Finalized
                and not DomainSharedProjectionRebuildPhase.Prepared) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            SharedProjectionControlIndexIntent? intent = handler.ControlIndexName is { } indexName
                ? new SharedProjectionControlIndexIntent(indexName)
                : null;
            string fingerprint;
            if (handler is IAsyncDomainSharedProjectionStreamingRebuildHandler streaming && streaming.EpochEnabled) {
                var cursor = new DomainSharedProjectionPartitionCursor(sessionStore, handler.RebuildStoreName, state,
                    coordinator, scope);
                fingerprint = await coordinator.StageStreamingAsync(scope, identity.OperationId,
                    GuardStreamingOperations(streaming.StreamFinalizedOperationsAsync(identity, cursor, cancellationToken),
                        sessionKey, cancellationToken),
                    cancellationToken, intent).ConfigureAwait(false);
            }
            else {
                ReadModelBatch batch = await BuildBatchAsync(sessionStore, handler, state, sessionKey, cancellationToken).ConfigureAwait(false);
                fingerprint = await coordinator.StageAsync(scope, identity.OperationId, batch.Operations,
                    cancellationToken, intent).ConfigureAwait(false);
            }
            if (!FingerprintMatches(state, fingerprint)) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Prepared,
                BatchFingerprint = fingerprint,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                return FromState(updated,
                    state.Phase == DomainSharedProjectionRebuildPhase.Prepared
                        ? ProjectionDispatchStatus.AlreadyCompleted
                        : ProjectionDispatchStatus.Completed,
                    null);
            }
        }

        return RetryExhausted();
    }

    private static async IAsyncEnumerable<ReadModelBatchOperation> GuardStreamingOperations(
        IAsyncEnumerable<ReadModelBatchOperation> operations,
        string sessionKey,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        await foreach (ReadModelBatchOperation operation in operations.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if (operation is null || string.Equals(operation.Key, sessionKey, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The streamed shared rebuild targets its private session key.");
            }

            yield return operation;
        }
    }

    private static async Task<DomainSharedProjectionRebuildResponse> FencedCommitAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        SharedProjectionEpochCoordinator? coordinator = serviceProvider.GetService<SharedProjectionEpochCoordinator>();
        if (coordinator is null) {
            return Unsupported();
        }

        SharedProjectionScope scope = FencedScope(handler, identity);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            if (state!.Phase is not DomainSharedProjectionRebuildPhase.Prepared
                and not DomainSharedProjectionRebuildPhase.Committed) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            bool wasCommitted = state.Phase == DomainSharedProjectionRebuildPhase.Committed;
            if (state.Partitioned && (!state.ScratchIndexesCleaned
                || state.ScratchCleanedOwnerCount < state.AcceptedAggregateCount))
            {
                // Persist the recovery trigger before promotion. A worker can resume cleanup
                // after a crash even if this request never reaches its response.
                await DomainSharedProjectionScratchCleanup.RegisterAsync(sessionStore,
                    handler.RebuildStoreName, identity, cancellationToken).ConfigureAwait(false);
            }
            if (wasCommitted)
            {
                SharedProjectionEpochStatus selected = await coordinator.GetStatusAsync(scope, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(selected.OperationId, identity.OperationId, StringComparison.Ordinal))
                {
                    // A later epoch may start once the durable handoff is released. The old
                    // operation's private scratch still has its own recoverable session key.
                    await ReconcilePartitionScratchAsync(serviceProvider, sessionStore, handler, identity, options,
                        cancellationToken).ConfigureAwait(false);
                    return FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
                }

                // Verify and explicit Commit retries are durable cleanup triggers. The initial
                // Commit response below does not wait for a 10,000-owner private scratch sweep.
                await ReconcilePartitionScratchAsync(serviceProvider, sessionStore, handler, identity, options,
                    cancellationToken).ConfigureAwait(false);
            }
            if (!wasCommitted)
            {
                SharedProjectionEpochStatus beforePromotion = await coordinator.GetStatusAsync(scope, cancellationToken)
                    .ConfigureAwait(false);
                if (state.Partitioned
                    && beforePromotion.ActiveGeneration != beforePromotion.Epoch
                    && !await CanPromotePartitionOwnersAsync(sessionStore, coordinator, scope,
                        handler, identity, handler.RebuildStoreName, sessionKey, identity.OperationId,
                        state.AcceptedAggregateCount, cancellationToken).ConfigureAwait(false))
                {
                    return FromState(state, ProjectionDispatchStatus.Failed,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }

                await coordinator.CommitAsync(scope, identity.OperationId, cancellationToken,
                    requireOwnerSequenceHandoff: state.Partitioned).ConfigureAwait(false);
                if (state.Partitioned)
                {
                    await SeedPartitionOwnerSequencesAsync(sessionStore, coordinator, scope, handler, identity,
                        handler.RebuildStoreName,
                        sessionKey, identity.OperationId, state.AcceptedAggregateCount, cancellationToken).ConfigureAwait(false);
                }

                DomainSharedProjectionRebuildSessionState committed = state with
                {
                    Phase = DomainSharedProjectionRebuildPhase.Committed,
                };
                if (!await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, committed, entry.ETag,
                    cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                state = committed;
            }

            DomainSharedProjectionRebuildResponse? incomplete = await CompleteCommittedRebuildAsync(
                handler, state, cancellationToken).ConfigureAwait(false);
            if (incomplete is not null)
            {
                return incomplete;
            }

            if (state.Partitioned)
            {
                await coordinator.CompleteOwnerSequenceHandoffAsync(scope, identity.OperationId, cancellationToken)
                    .ConfigureAwait(false);
            }

            try
            {
                _ = await coordinator.CatchUpAsync(
                    scope,
                    (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation, coordinator, token),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (SharedProjectionRetryableFoldException)
            {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.PartialRetry);
            }
            SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
            if (status.ActiveGeneration != status.Epoch
                || status.PendingDeliveryCount != 0
                || !string.Equals(status.StageFingerprint, state.BatchFingerprint, StringComparison.Ordinal)) {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.PartialRetry);
            }

            return FromState(state, wasCommitted
                ? ProjectionDispatchStatus.AlreadyCompleted
                : ProjectionDispatchStatus.Completed, null);
        }

        return RetryExhausted();
    }

    private static async Task<bool> CanPromotePartitionOwnersAsync(
        IReadModelStore sessionStore,
        SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope,
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        string storeName,
        string sessionKey,
        string operationId,
        long ownerCount,
        CancellationToken cancellationToken)
    {
        IReadOnlySet<string> pendingJournalOwners = await coordinator.GetPendingJournalOwnerIdsAsync(
            scope, operationId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> capturedOwners = await coordinator.GetCapturedOwnerIdsAsync(
            scope, operationId, cancellationToken).ConfigureAwait(false);
        if (capturedOwners.Count != ownerCount)
        {
            return false;
        }

        var acceptedOwners = new HashSet<string>(StringComparer.Ordinal);
        var acceptedSourceKeys = new HashSet<string>(StringComparer.Ordinal);
        for (long ordinal = 0; ordinal < ownerCount; ordinal++)
        {
            DomainSharedProjectionStoredPartitionManifest? manifest = (await sessionStore
                .GetAsync<DomainSharedProjectionStoredPartitionManifest>(storeName,
                    PartitionManifestKey(sessionKey, ordinal), cancellationToken).ConfigureAwait(false)).Value;
            if (manifest is null || manifest.Ordinal != ordinal
                || string.IsNullOrWhiteSpace(manifest.AggregateId)
                || !acceptedOwners.Add(manifest.AggregateId)
                || manifest.LastSequenceNumber <= 0 && !manifest.IsErased)
            {
                return false;
            }

            string sourceKey = handler.SourceStreamKey(identity.TenantId, manifest.AggregateId);
            if (!acceptedSourceKeys.Add(sourceKey))
            {
                return false;
            }
            if (!await coordinator.MatchesCapturedOwnerErasureAsync(scope, operationId,
                    sourceKey, manifest.IsErased, cancellationToken).ConfigureAwait(false)
                || pendingJournalOwners.Contains(sourceKey)
                    && !await coordinator.CanPromoteCapturedJournalOwnerAsync(
                    scope, operationId, sourceKey, manifest.LastSequenceNumber,
                    manifest.CapturedEventDigests, cancellationToken).ConfigureAwait(false)
                || manifest.LastSequenceNumber > 0
                && !await coordinator.CanPromoteCapturedOwnerAsync(scope, operationId,
                    sourceKey, manifest.LastSequenceNumber, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return capturedOwners.All(acceptedSourceKeys.Contains);
    }

    private static byte[] ComputeCapturedEventDigests(
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        string owner,
        IReadOnlyList<ProjectionEventDto> events)
    {
        byte[] digests = new byte[checked(events.Count * 32)];
        for (int index = 0; index < events.Count; index++)
        {
            var singleEventRequest = new ProjectionRequest(identity.TenantId.ToLowerInvariant(),
                identity.Domain.ToLowerInvariant(), owner, [events[index]]);
            byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(singleEventRequest, s_deliveryJson);
            SharedProjectionCapturedEventIdentity.Compute(
                handler.SourceStreamKey(identity.TenantId, owner), events[index].GlobalPosition,
                events[index].SequenceNumber, canonical).CopyTo(digests.AsSpan(index * 32, 32));
        }

        return digests;
    }

    private static async Task SeedPartitionOwnerSequencesAsync(
        IReadModelStore sessionStore,
        SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope,
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        string storeName,
        string sessionKey,
        string operationId,
        long ownerCount,
        CancellationToken cancellationToken)
    {
        for (long ordinal = 0; ordinal < ownerCount; ordinal++)
        {
            DomainSharedProjectionStoredPartitionManifest manifest = (await sessionStore
                .GetAsync<DomainSharedProjectionStoredPartitionManifest>(storeName,
                    PartitionManifestKey(sessionKey, ordinal), cancellationToken).ConfigureAwait(false)).Value
                ?? throw new InvalidOperationException("A verified owner manifest is missing during checkpoint handoff.");
            if (manifest.Ordinal != ordinal || manifest.LastSequenceNumber < 0
                || (!manifest.IsErased && manifest.LastSequenceNumber == 0))
            {
                throw new InvalidOperationException("A verified owner manifest has no valid sequence watermark.");
            }

            if (manifest.LastSequenceNumber > 0)
            {
                await coordinator.SeedCapturedOwnerSequenceAsync(scope, operationId,
                    handler.SourceStreamKey(identity.TenantId, manifest.AggregateId),
                    manifest.LastSequenceNumber, cancellationToken,
                    manifest.CapturedEventDigests).ConfigureAwait(false);
            }
        }
    }

    private static async Task<DomainSharedProjectionRebuildResponse> FencedAbortAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        SharedProjectionEpochCoordinator? coordinator = serviceProvider.GetService<SharedProjectionEpochCoordinator>();
        if (coordinator is null) {
            return Unsupported();
        }

        SharedProjectionScope scope = FencedScope(handler, identity);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++) {
            ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
                .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildSessionState? state = entry.Value;
            if (state is null)
            {
                SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, cancellationToken)
                    .ConfigureAwait(false);
                if (string.Equals(status.OperationId, identity.OperationId, StringComparison.Ordinal)
                    && !string.Equals(status.OperationTenantId, identity.TenantId, StringComparison.Ordinal))
                {
                    return Failure(DomainSharedProjectionRebuildPhase.Accumulating,
                        ProjectionDispatchStatus.Failed, 0,
                        DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                }
                if (status.OperationId == identity.OperationId && (status.IsBuilding || status.IsAborting))
                {
                    try
                    {
                        await coordinator.AbortAsync(scope, identity.OperationId,
                            (delivery, generation, token) => handler.FoldCatchUpAsync(
                                delivery, generation, coordinator, token), cancellationToken, deferOpen: true)
                            .ConfigureAwait(false);
                        await coordinator.CompleteAbortAsync(scope, identity.OperationId, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (SharedProjectionRetryableFoldException)
                    {
                        return Failure(DomainSharedProjectionRebuildPhase.Accumulating,
                            ProjectionDispatchStatus.Retryable, 0,
                            DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                            ProjectionDispatchReasonCodes.PartialRetry);
                    }
                }
                else if (status.OperationId == identity.OperationId)
                    return Failure(DomainSharedProjectionRebuildPhase.Accumulating,
                        ProjectionDispatchStatus.Failed, 0,
                        DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                        ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
                IReadModelBatchStore? batches = serviceProvider.GetService<IReadModelBatchStore>();
                if (batches is null) return Unsupported();
                await ReclaimBeginPagesAsync(sessionStore, batches, handler, identity, cancellationToken)
                    .ConfigureAwait(false);
                return new(DomainSharedProjectionRebuildProtocol.Version,
                    DomainSharedProjectionRebuildPhase.Aborted, ProjectionDispatchStatus.Completed,
                    0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null, null);
            }
            if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
                return failure!;
            }

            SharedProjectionEpochStatus selected = await coordinator.GetStatusAsync(scope, cancellationToken)
                .ConfigureAwait(false);
            if (string.Equals(selected.OperationId, identity.OperationId, StringComparison.Ordinal)
                && !string.Equals(selected.OperationTenantId, identity.TenantId, StringComparison.Ordinal))
            {
                return FromState(state!, ProjectionDispatchStatus.Failed,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            if (state!.Phase == DomainSharedProjectionRebuildPhase.Aborted) {
                await ReconcilePartitionScratchAsync(serviceProvider, sessionStore, handler, identity, options,
                    cancellationToken).ConfigureAwait(false);
                await coordinator.CompleteAbortAsync(scope, identity.OperationId, cancellationToken)
                    .ConfigureAwait(false);
                return FromState(state, ProjectionDispatchStatus.AlreadyCompleted, null);
            }

            if (state.Phase == DomainSharedProjectionRebuildPhase.Committed) {
                return FromState(state, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            }

            try
            {
                await coordinator.AbortAsync(
                    scope,
                    identity.OperationId,
                    (delivery, generation, token) => handler.FoldCatchUpAsync(delivery, generation, coordinator, token),
                    cancellationToken, deferOpen: true).ConfigureAwait(false);
            }
            catch (SharedProjectionRetryableFoldException)
            {
                return FromState(state, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.PartialRetry);
            }
            DomainSharedProjectionRebuildSessionState updated = state with {
                Phase = DomainSharedProjectionRebuildPhase.Aborted,
            };
            if (await TrySaveAsync(sessionStore, handler.RebuildStoreName, sessionKey, updated, entry.ETag, cancellationToken)
                .ConfigureAwait(false)) {
                await ReconcilePartitionScratchAsync(serviceProvider, sessionStore, handler, identity, options,
                    cancellationToken).ConfigureAwait(false);
                await coordinator.CompleteAbortAsync(scope, identity.OperationId, cancellationToken)
                    .ConfigureAwait(false);
                return FromState(updated, ProjectionDispatchStatus.Completed, null);
            }
        }

        return RetryExhausted();
    }

    private static async Task<DomainSharedProjectionRebuildResponse> FencedVerifyAsync(
        IServiceProvider serviceProvider,
        IReadModelStore sessionStore,
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        SharedProjectionEpochCoordinator? coordinator = serviceProvider.GetService<SharedProjectionEpochCoordinator>();
        if (coordinator is null) {
            return Unsupported();
        }

        SharedProjectionScope scope = FencedScope(handler, identity);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        ReadModelEntry<DomainSharedProjectionRebuildSessionState> entry = await sessionStore
            .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName, sessionKey, cancellationToken)
            .ConfigureAwait(false);
        DomainSharedProjectionRebuildSessionState? state = entry.Value;
        if (!TryValidateState(state, identity, options, out DomainSharedProjectionRebuildResponse? failure)) {
            return failure!;
        }

        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(status.OperationId, identity.OperationId, StringComparison.Ordinal)) {
            return FromState(state!, ProjectionDispatchStatus.Failed, ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        }

        if (status.ActiveGeneration == status.Epoch && status.PendingDeliveryCount == 0) {
            return await FencedCommitAsync(serviceProvider, sessionStore, handler, identity, options, cancellationToken)
                .ConfigureAwait(false);
        }

        if (status.IsAborting) {
            return FromState(state!, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.PartialRetry);
        }

        return FromState(state!, ProjectionDispatchStatus.Retryable, ProjectionDispatchReasonCodes.DeliveryInProgress);
    }

    private static SharedProjectionScope FencedScope(
        IAsyncDomainSharedProjectionEpochHandler handler,
        DomainSharedProjectionRebuildIdentity identity) {
        SharedProjectionScope scope = handler.CreateScope(identity.TenantId);
        scope.Validate();
        if (!string.Equals(scope.StoreName, handler.RebuildStoreName, StringComparison.Ordinal)
            || (!handler.GlobalEpochScope
                && !string.Equals(scope.TenantId, identity.TenantId, StringComparison.Ordinal))
            || (handler.GlobalEpochScope
                && !string.Equals(scope.TenantId,
                    IAsyncDomainSharedProjectionEpochHandler.GlobalTenantSegment, StringComparison.Ordinal))
            || !string.Equals(scope.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(scope.Family, identity.ProjectionType, StringComparison.Ordinal)) {
            throw new InvalidOperationException("The shared projection epoch scope differs from its admitted route.");
        }

        return scope;
    }
}
