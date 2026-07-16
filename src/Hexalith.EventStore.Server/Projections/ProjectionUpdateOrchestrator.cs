using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Orchestrates projection updates: reads new events from the aggregate actor,
/// sends them to the domain service's /project endpoint via DAPR service invocation,
/// and stores the returned state in the EventReplayProjectionActor.
/// </summary>
/// <remarks>
/// Entire method is wrapped in try/catch — fire-and-forget safe. Any exception is
/// swallowed after logging. The projection stays at last known state on failure.
/// </remarks>
internal partial class ProjectionUpdateOrchestrator(
    IActorProxyFactory actorProxyFactory,
    DaprClient daprClient,
    IHttpClientFactory httpClientFactory,
    IDomainServiceResolver resolver,
    IProjectionCheckpointTracker checkpointTracker,
    IOptions<ProjectionOptions> projectionOptions,
    ILogger<ProjectionUpdateOrchestrator> logger,
    IProjectionRebuildCheckpointStore? rebuildCheckpointStore = null,
    IEventPayloadProtectionService? payloadProtectionService = null,
    IOptions<EventStoreActorOptions>? actorOptions = null,
    IProjectionDeliveryCheckpointStore? deliveryCheckpointStore = null,
    IProjectionLifecycleGateway? lifecycleGateway = null,
    INamedProjectionDispatchCoordinator? namedProjectionDispatchCoordinator = null,
    IProjectionActivationOutbox? projectionActivationOutbox = null,
    IProjectionRebuildWriteGateway? rebuildWriteGateway = null) : IProjectionUpdateOrchestrator, IProjectionPollerDeliveryGateway, IProjectionRebuildOrchestrator {
    // Per-aggregate serialization across orchestrator instances. Entries are evicted and the
    // underlying SemaphoreSlim disposed when the last holder releases, so a multi-tenant server
    // with many short-lived aggregates does not accumulate kernel handles indefinitely.
    internal static readonly KeyedSemaphore<string> ProjectionLocks = new(StringComparer.Ordinal);
    private const string RebuildPromotionPendingReasonCode = "rebuild-promotion-pending";
    private readonly IEventPayloadProtectionService _payloadProtectionService = payloadProtectionService ?? new NoOpEventPayloadProtectionService();
    private string AggregateActorTypeName => actorOptions?.Value.AggregateActorTypeName ?? nameof(AggregateActor);

    /// <inheritdoc/>
    public async Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);

        int refreshIntervalMs = projectionOptions.Value.GetRefreshIntervalMs(identity.Domain);
        if (refreshIntervalMs > 0) {
            try {
                await checkpointTracker.TrackIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
                Log.PollingWorkRegistered(logger, identity.TenantId, identity.Domain, identity.AggregateId, refreshIntervalMs);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                Log.PollingWorkRegistrationFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
            }

            return;
        }

        await DeliverProjectionAsync(identity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeliverProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        bool namedActivationDurable = namedProjectionDispatchCoordinator is null;
        ProjectionActivationWorkItem? activationWorkItem = null;

        using IDisposable projectionLock = await ProjectionLocks.AcquireAsync(identity.ActorId, cancellationToken).ConfigureAwait(false);
        try {
            if (projectionActivationOutbox is not null) {
                activationWorkItem = await projectionActivationOutbox
                    .GetAsync(identity, cancellationToken)
                    .ConfigureAwait(false);
            }

            Log.UpdateStarted(logger, identity.TenantId, identity.Domain, identity.AggregateId);

            // D3-B: route through the active-rebuilds index instead of probing by
            // (tenant, domain, domain) which incorrectly assumed projectionName == domain.
            if (rebuildCheckpointStore is not null
                && await rebuildCheckpointStore
                    .HasActiveOperatorRebuildForDomainAsync(identity.TenantId, identity.Domain, cancellationToken)
                    .ConfigureAwait(false)) {
                Log.PollerRebuildConflict(logger, identity.TenantId, identity.Domain, identity.AggregateId, StreamReplayReasonCodes.PollerRebuildConflict);
                return;
            }

            // Step 1: Resolve domain service registration
            DomainServiceRegistration? registration = await resolver
                .ResolveAsync(identity.TenantId, identity.Domain, "v1", cancellationToken)
                .ConfigureAwait(false);

            if (registration is null) {
                Log.NoDomainServiceRegistered(logger, identity.TenantId, identity.Domain);
                return;
            }

            // Step 2: Create aggregate actor proxy and read events
            IAggregateActor aggregateProxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId),
                AggregateActorTypeName);

            // Full replay remains the safe immediate-delivery contract until
            // projection handlers receive prior state or become explicitly incremental-aware.
            EventEnvelope[] events = await aggregateProxy
                .GetEventsAsync(0)
                .ConfigureAwait(false);

            long lastDeliveredSequence = 0;
            try {
                lastDeliveredSequence = await checkpointTracker
                    .ReadLastDeliveredSequenceAsync(identity, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                Log.CheckpointReadFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
            }

            if (events.Length == 0) {
                // Drift detection covers the canonical "stale checkpoint + empty stream" case
                // (e.g., state-store backup/restore mismatch) that the original deferred-work
                // entry called out: without this branch the orchestrator would log NoEventsFound
                // and silently return, hiding the drift indefinitely.
                //
                // This branch stays on the released aggregate-wide checkpoint (lastDeliveredSequence
                // read above) and is diagnostic-only: with no events there is no /project call and
                // therefore no projection name to scope the checkpoint by.
                if (lastDeliveredSequence > 0) {
                    Log.CheckpointDriftDetected(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        ProjectionReasonCodes.CheckpointDrift,
                        lastDeliveredSequence,
                        0);
                    return;
                }

                Log.NoEventsFound(logger, identity.TenantId, identity.Domain, identity.AggregateId);
                namedActivationDurable = true;
                return;
            }

            long highestAvailableSequence = events.Max(e => e.SequenceNumber);

            // Option A: the non-empty-stream drift check moved AFTER /project returns so it can read
            // the projection-scoped checkpoint (keyed by response.ProjectionType). See the drift
            // branch below, just before the projection write.
            ProjectionEventReadabilityResult projectionReadability = await ProjectionEventWireBuilder
                .BuildAsync(_payloadProtectionService, identity, events, cancellationToken)
                .ConfigureAwait(false);
            if (projectionReadability.UnreadableReason is not null) {
                Log.UnreadableProtectedEvent(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionReadability.SequenceNumber ?? 0,
                    UnreadableProtectedDataReasonCodes.From(projectionReadability.UnreadableReason.Value),
                    "projection-delivery");
                return;
            }

            // Step 4: Invoke domain service /project endpoint via DAPR
            var request = new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, projectionReadability.Events!);
            if (namedProjectionDispatchCoordinator is not null) {
                try {
                    _ = await namedProjectionDispatchCoordinator
                        .TryDispatchAsync(
                            identity,
                            registration,
                            events,
                            projectionReadability.Events!,
                        cancellationToken)
                        .ConfigureAwait(false);
                    namedActivationDurable = true;
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    Log.NamedProjectionDispatchFailed(
                        logger,
                        ex,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        ex.GetType().Name);
                }
            }

            // Named v2 routes and an unmapped legacy v1 handler may intentionally coexist for the
            // same domain. Named delivery therefore does not short-circuit the released v1 path.
            // A named-only service returns a support-safe non-success response from /project.
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(
                registration.AppId,
                "project",
                request);
            HttpClient httpClient = httpClientFactory.CreateClient();
            HttpResponseMessage? httpResponse = await SendProjectRequestAsync(
                    httpClient,
                    httpRequest,
                    registration.AppId,
                    identity,
                    cancellationToken)
                .ConfigureAwait(false);
            if (httpResponse is null) {
                return;
            }

            using HttpResponseMessage responseHandle = httpResponse;
            if (!responseHandle.IsSuccessStatusCode) {
                Log.ProjectInvocationRejected(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    registration.AppId,
                    GetUpstreamReasonCode(responseHandle.StatusCode),
                    ((int)responseHandle.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    GetContentTypeForLog(responseHandle.Content));
                return;
            }

            ProjectionResponse? response = await ReadProjectResponseAsync(
                    responseHandle,
                    registration.AppId,
                    identity,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response is null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(response.ProjectionType)) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, ProjectionReasonCodes.ProjectInvalidProjectionType);
                return;
            }

            // Option A: projection-scoped drift check. The projection name is only known once
            // /project returns response.ProjectionType, so drift suppression happens here — after the
            // side-effect-free /project compute and before the projection WRITE. The write and the
            // checkpoint save below are suppressed when the projection-scoped checkpoint is ahead of
            // the available stream. When no delivery store is supplied (legacy path used by the
            // orchestrator unit tests) the aggregate-wide value read earlier is used instead.
            long scopedLastDelivered = deliveryCheckpointStore is not null
                ? await deliveryCheckpointStore
                    .ReadDeliveredSequenceAsync(identity, response.ProjectionType, cancellationToken)
                    .ConfigureAwait(false)
                : lastDeliveredSequence;
            if (scopedLastDelivered > highestAvailableSequence) {
                Log.CheckpointDriftDetected(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    ProjectionReasonCodes.CheckpointDrift,
                    scopedLastDelivered,
                    highestAvailableSequence);
                return;
            }

            if (response.State.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || (response.State.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(response.State.GetString()))) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, ProjectionReasonCodes.ProjectInvalidState);
                return;
            }

            Log.DomainServiceInvocationSucceeded(logger, identity.TenantId, identity.Domain, identity.AggregateId, registration.AppId);

            string deliveryOperationId = CreateDurableDeliveryOperationId(
                identity,
                response.ProjectionType,
                events,
                highestAvailableSequence);
            bool deliveryLeaseAcquired = lifecycleGateway is null
                || await lifecycleGateway
                    .BeginDeliveryWriteAsync(
                        identity,
                        response.ProjectionType,
                        deliveryOperationId,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (!deliveryLeaseAcquired) {
                Log.ProjectionDeliveryDeferredForErase(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    response.ProjectionType,
                    ProjectionReasonCodes.DeliveryDeferredForErase);
                return;
            }

            try {
                // Step 5: Derive projection actor ID and update state
                string projectionActorId = QueryActorIdHelper.DeriveActorId(
                    response.ProjectionType,
                    identity.TenantId,
                    identity.AggregateId,
                    []);

                IProjectionWriteActor writeProxy = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(
                    new ActorId(projectionActorId),
                    QueryRouter.ProjectionActorTypeName);

                await writeProxy
                    .UpdateProjectionAsync(ProjectionState.FromJsonElement(response.ProjectionType, identity.TenantId, response.State))
                    .ConfigureAwait(false);

                await RegenerateProjectionETagAsync(identity, response.ProjectionType, cancellationToken)
                    .ConfigureAwait(false);

                long highestDeliveredSequence = highestAvailableSequence;
                try {
                    // Save to the projection-scoped checkpoint (keyed by response.ProjectionType) when a
                    // delivery store is supplied; otherwise fall back to the released aggregate-wide
                    // checkpoint (legacy path used by the orchestrator unit tests).
                    bool checkpointSaved = deliveryCheckpointStore is not null
                        ? await deliveryCheckpointStore
                            .SaveDeliveredSequenceAsync(identity, response.ProjectionType, highestDeliveredSequence, cancellationToken)
                            .ConfigureAwait(false)
                        : await checkpointTracker
                            .SaveDeliveredSequenceAsync(identity, highestDeliveredSequence, cancellationToken)
                            .ConfigureAwait(false);
                    if (!checkpointSaved) {
                        Log.CheckpointSaveExhausted(logger, identity.TenantId, identity.Domain, identity.AggregateId, highestDeliveredSequence);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    Log.CheckpointSaveFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId, ex.GetType().Name);
                }

                Log.ProjectionStateUpdated(logger, identity.TenantId, identity.Domain, identity.AggregateId, response.ProjectionType, projectionActorId);
            }
            finally {
                if (lifecycleGateway is not null
                    && !await lifecycleGateway
                        .CompleteDeliveryWriteAsync(
                            identity,
                            response.ProjectionType,
                            deliveryOperationId,
                            CancellationToken.None)
                        .ConfigureAwait(false)) {
                    throw new InvalidOperationException("Projection delivery lifecycle lease completion was rejected.");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.ProjectionUpdateFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId);
        }
        finally {
            if (namedActivationDurable && projectionActivationOutbox is not null && activationWorkItem is not null) {
                try {
                    await projectionActivationOutbox.CompleteAsync(activationWorkItem, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    Log.ProjectionActivationCompletionFailed(
                        logger,
                        ex,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        ex.GetType().Name);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task RebuildProjectionAsync(
        ProjectionRebuildCheckpointScope scope,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(scope);
        if (rebuildCheckpointStore is null) {
            return;
        }

        ProjectionOptions rebuildOptions = projectionOptions.Value;
        rebuildOptions.Validate();

        // P10-6P: a transient store failure on the initial ReadAsync (DaprException etc.)
        // would propagate uncaught before reaching the H1-5P catch (which is inside the
        // foreach try block). The controller's Running write has already populated the
        // active-rebuilds index, so without this guard the index entry would persist
        // forever and the poller would be blocked indefinitely for (tenant, domain).
        // Wrap the initial read so transient failures route through the H1-5P Failed
        // write path uniformly.
        ProjectionRebuildCheckpoint? initial;
        try {
            initial = await rebuildCheckpointStore
                .ReadAsync(scope, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            // P10-8P (pass-8): wrap the ResetAsync cleanup in try/catch so a secondary transient
            // exception from the failure-audit write does not overwrite the original diagnostic
            // (`ex`) on its way out of the method. Mirrors the H1-5P / cancel-cleanup pattern
            // below.
            try {
                ProjectionRebuildCheckpointSaveResult initFailSave = await rebuildCheckpointStore
                    .ResetAsync(
                        scope,
                        lastAppliedSequence: 0,
                        ProjectionRebuildStatus.Failed,
                        failureReasonCode: ex.GetType().Name,
                        cancellationToken: CancellationToken.None,
                        toPosition: null)
                    .ConfigureAwait(false);
                if (!initFailSave.Succeeded) {
                    Log.RebuildTerminalFailWriteRejected(logger, scope.Tenant, scope.Domain, scope.ProjectionName, initFailSave.ReasonCode ?? "unknown", ex.GetType().Name);
                }
            }
            catch (Exception cleanupEx) when (cleanupEx is not OperationCanceledException) {
                Log.RebuildCancelCleanupFailed(logger, cleanupEx, scope.Tenant, scope.Domain, scope.ProjectionName, cleanupEx.GetType().Name);
            }

            throw;
        }

        if (!CanRunRebuild(initial)) {
            return;
        }

        // D3-A: operator-scope row owns lifecycle status; per-aggregate rows own per-aggregate
        // progress. ScopeForCheckpoint preserves operator scope's AggregateId and only overlays
        // OperationId so the operator-scope key remains stable.
        ProjectionRebuildCheckpointScope operatorScope = ScopeForCheckpoint(scope, initial!);
        bool matchedAny = false;
        bool allMatchedWorkComplete = true;
        long highestMatchedProgress = initial!.LastAppliedSequence;
        var activeLifecycleScopes = new Dictionary<string, RebuildLifecycleScope>(StringComparer.Ordinal);
        // P1-7P (pass-7): track which per-aggregate scopes were touched during iteration so the
        // OCE cancel-cleanup can reset them to Canceled too. Without this, a domain-wide rebuild
        // that has already advanced per-aggregate rows for aggregates A and B leaves those rows
        // at Running with the old OperationId on cancel — blocking the next operator's resume.
        var touchedAggregateIds = new HashSet<string>(StringComparer.Ordinal);
        try {
            await foreach (AggregateIdentity identity in checkpointTracker.EnumerateTrackedIdentitiesAsync(cancellationToken).ConfigureAwait(false)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!MatchesRebuildScope(scope, identity)) {
                    continue;
                }

                _ = touchedAggregateIds.Add(identity.AggregateId);

                // Re-read OPERATOR scope to honor mid-iteration Pause/Cancel intent.
                ProjectionRebuildCheckpoint? operatorSnap = await rebuildCheckpointStore
                    .ReadAsync(operatorScope, cancellationToken)
                    .ConfigureAwait(false);
                if (!CanRunRebuild(operatorSnap)) {
                    _ = await ClearRebuildLifecyclesAsync(
                            activeLifecycleScopes.Values,
                            operatorScope,
                            throwOnFailure: false)
                        .ConfigureAwait(false);
                    activeLifecycleScopes.Clear();
                    return;
                }

                // D3-A/D3-E: read PER-AGGREGATE progress from the rebuild checkpoint store, not
                // from the poller checkpoint tracker. First-run perAggregateRow is null → progress 0.
                ProjectionRebuildCheckpointScope perAggregateScope = operatorScope with { AggregateId = identity.AggregateId };
                ProjectionRebuildCheckpoint? perAggregateRow = await rebuildCheckpointStore
                    .ReadAsync(perAggregateScope, cancellationToken)
                    .ConfigureAwait(false);
                long perAggregateProgress = perAggregateRow?.LastAppliedSequence ?? 0;
                long? toPosition = operatorSnap!.ToPosition;

                if (lifecycleGateway is not null) {
                    string operationId = operatorScope.OperationId
                        ?? throw new InvalidOperationException("A running projection rebuild requires an operation identity.");
                    if (!await lifecycleGateway
                        .BeginRebuildAsync(
                            identity,
                            operatorScope.ProjectionName,
                            operationId,
                            cancellationToken)
                        .ConfigureAwait(false)) {
                        await FailRebuildAsync(
                                operatorScope,
                                perAggregateProgress,
                                StreamReplayReasonCodes.OperationInFlight,
                                toPosition)
                            .ConfigureAwait(false);
                        return;
                    }

                    TrackLifecycleScope(activeLifecycleScopes, identity, operatorScope.ProjectionName);
                }

                matchedAny = true;
                if (perAggregateRow is not null
                    && string.Equals(perAggregateRow.OperationId, operatorScope.OperationId, StringComparison.Ordinal)
                    && perAggregateRow.ToPosition is long durableTarget
                    && perAggregateRow.FailureReasonCode is null
                    && perAggregateProgress >= durableTarget) {
                    highestMatchedProgress = Math.Max(highestMatchedProgress, perAggregateProgress);
                    continue;
                }

                RebuildDeliveryResult delivery = await DeliverProjectionForRebuildAsync(
                        identity,
                        operatorScope,
                        perAggregateScope,
                        perAggregateRow,
                        perAggregateProgress,
                        toPosition,
                        activeLifecycleScopes,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (delivery.Interrupted) {
                    ProjectionRebuildCheckpoint? interruptedSnapshot = await rebuildCheckpointStore
                        .ReadAsync(operatorScope, CancellationToken.None)
                        .ConfigureAwait(false);
                    if (IsTerminalStatus(interruptedSnapshot?.Status ?? ProjectionRebuildStatus.Running)) {
                        await ClearRebuildLifecyclesAsync(
                                activeLifecycleScopes.Values,
                                operatorScope)
                            .ConfigureAwait(false);
                        activeLifecycleScopes.Clear();
                    }

                    return;
                }

                highestMatchedProgress = Math.Max(highestMatchedProgress, delivery.LastAppliedSequence);
                allMatchedWorkComplete &= delivery.PageComplete;
            }
        }
        catch (OperationCanceledException) {
            // C3-5P (pass-5): wrap cancel-cleanup in try/catch so a transient DaprException /
            // HttpRequestException during the ReadAsync / ResetAsync calls does not silently
            // replace the OperationCanceledException on its way out of the method. Losing the OCE
            // would cause callers to see a transport exception instead of the original cancel and
            // the active-rebuilds index entry would persist indefinitely.
            //
            // P10-7P (pass-7) limitation: if the cleanup ResetAsync ITSELF throws transiently
            // (state-store hiccup), the OCE is rethrown but the operator-scope row stays Running
            // AND active-rebuilds index still holds the projection. HasActiveOperatorRebuildForDomainAsync
            // returns true forever (modulo P11-7P TTL cache). Healing options today: (a) the next
            // operator action (Retry/Replay) overwrites the row via ResetAsync, OR (b) the P11-7P
            // process-local fail-closed TTL expires (5s) once the index store recovers. A timer-
            // driven deferred cleanup or an admin "force-clear-active-index" endpoint is tracked
            // as a follow-up (see deferred-work.md W4-7P-NOTE).
            try {
                // DEC3-P/P14/P15: cancel-cleanup must reach a terminal lifecycle write even when the
                // store's lifecycle guards (IsLifecycleProtected) would reject SaveAsync. Use ResetAsync,
                // which is the documented trust boundary for operator-intentional terminal writes.
                ProjectionRebuildCheckpoint? canceledSnapshot = await rebuildCheckpointStore
                    .ReadAsync(operatorScope, CancellationToken.None)
                    .ConfigureAwait(false);
                if (canceledSnapshot is not null && !IsTerminalStatus(canceledSnapshot.Status)) {
                    // P15-7P (pass-7): mirror the P14-7P / M7-5P carve-out — domain-wide rebuilds
                    // do NOT inflate operator-scope LastAppliedSequence to the largest aggregate's
                    // progress on cancel-cleanup. Per-aggregate rows already carry truthful progress.
                    // For aggregate-scoped rebuilds the operator-scope IS the per-aggregate row,
                    // so Math.Max is correct.
                    long cancelLastApplied = operatorScope.AggregateId is null
                        ? canceledSnapshot.LastAppliedSequence
                        : Math.Max(canceledSnapshot.LastAppliedSequence, highestMatchedProgress);

                    ProjectionRebuildCheckpointSaveResult cancelSave = await rebuildCheckpointStore
                        .ResetAsync(
                            operatorScope,
                            cancelLastApplied,
                            ProjectionRebuildStatus.Canceled,
                            failureReasonCode: StreamReplayReasonCodes.RebuildCanceled,
                            cancellationToken: CancellationToken.None,
                            toPosition: canceledSnapshot.ToPosition)
                        .ConfigureAwait(false);
                    if (cancelSave is null || !cancelSave.Succeeded) {
                        Log.RebuildCancelCleanupRejected(logger, scope.Tenant, scope.Domain, scope.ProjectionName, cancelSave?.ReasonCode ?? "unknown");
                    }

                    // P1-7P (pass-7): cancel each touched per-aggregate row (domain-wide rebuilds
                    // only — for aggregate-scoped the operator-scope row IS the per-aggregate row).
                    // Without this, per-aggregate rows advanced during iteration remain at Running
                    // with the old OperationId, blocking the next operator's resume against the
                    // same scope.
                    if (operatorScope.AggregateId is null) {
                        foreach (string touchedAggregateId in touchedAggregateIds) {
                            ProjectionRebuildCheckpointScope touchedScope = operatorScope with { AggregateId = touchedAggregateId };
                            ProjectionRebuildCheckpoint? touchedSnap = await rebuildCheckpointStore
                                .ReadAsync(touchedScope, CancellationToken.None)
                                .ConfigureAwait(false);
                            if (touchedSnap is null || IsTerminalStatus(touchedSnap.Status)) {
                                continue;
                            }

                            // P3-8P (pass-8): preserve the per-aggregate row's pre-existing
                            // FailureReasonCode (e.g., a ProjectionApplyRejected / NoDomainService
                            // reason that landed mid-iteration) instead of clobbering it with the
                            // generic RebuildCanceled. The cancellation reason should only be used
                            // when the per-aggregate row has no prior failure reason of its own.
                            string touchedReasonCode = touchedSnap.FailureReasonCode ?? StreamReplayReasonCodes.RebuildCanceled;
                            ProjectionRebuildCheckpointSaveResult touchedCancelResult = await rebuildCheckpointStore
                                .ResetAsync(
                                    touchedScope,
                                    touchedSnap.LastAppliedSequence,
                                    ProjectionRebuildStatus.Canceled,
                                    failureReasonCode: touchedReasonCode,
                                    cancellationToken: CancellationToken.None,
                                    toPosition: touchedSnap.ToPosition)
                                .ConfigureAwait(false);
                            // P3-8P (pass-8): surface cleanup failures instead of silently
                            // discarding the result. Operators rely on this log to detect orphaned
                            // per-aggregate rows that the future P-DEC1-8P cleanup service should
                            // sweep.
                            if (touchedCancelResult is null || !touchedCancelResult.Succeeded) {
                                Log.RebuildCancelCleanupRejected(logger, scope.Tenant, scope.Domain, scope.ProjectionName, touchedCancelResult?.ReasonCode ?? "unknown");
                            }
                        }
                    }
                }
            }
            catch (Exception cleanupEx) when (cleanupEx is not OperationCanceledException) {
                Log.RebuildCancelCleanupFailed(logger, cleanupEx, scope.Tenant, scope.Domain, scope.ProjectionName, cleanupEx.GetType().Name);
            }

            await ClearRebuildLifecyclesAsync(activeLifecycleScopes.Values, operatorScope, throwOnFailure: false)
                .ConfigureAwait(false);

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // H1-5P (pass-5): catch transient/programmer exceptions at the rebuild boundary so the
            // operator-scope row reaches a terminal Failed status and the active-rebuilds index is
            // cleared. Without this catch a single DaprException from EnumerateTrackedIdentitiesAsync
            // or the inner ReadAsync leaves the row at Running and the poller is permanently
            // blocked for (tenant, domain). ResetAsync bypasses the monotonic guard so the
            // operator-scope Failed write lands regardless of accumulated highestMatchedProgress.
            // P4-6P: wrap the cleanup ResetAsync in try/catch (mirroring the OCE-branch cleanup
            // at line ~354) so a secondary transient exception from the cleanup write does not
            // silently replace the original exception on its way out of the method.
            // P8-6P: use Math.Max(initial.LastAppliedSequence, highestMatchedProgress) so the
            // Failed audit row does NOT regress below per-aggregate progress that landed during
            // the iteration. Previously, the operator-scope row could end up with
            // LastAppliedSequence < some-per-aggregate-row.LastAppliedSequence — breaking the
            // invariant that operator-scope is the high-water mark.
            try {
                // P14-7P (pass-7): mirror the M7-5P happy-path carve-out — domain-wide rebuilds
                // do NOT inflate operator-scope LastAppliedSequence to the largest aggregate's
                // progress on Failed-cleanup. Per-aggregate rows already carry truthful progress.
                // For aggregate-scoped rebuilds the operator-scope IS the per-aggregate row, so
                // Math.Max is correct.
                // P23-7P (pass-7 MEDIUM): re-read operator-scope inside the catch so a concurrent
                // in-process orchestrator (different scope, or same scope on a different node)
                // that has advanced operator-scope to a higher value is not silently regressed by
                // our ResetAsync (which bypasses monotonic guards). Take the max of {initial,
                // latest-stored, accumulated highestMatchedProgress (aggregate-scoped only)}.
                ProjectionRebuildCheckpoint? latestOperator = null;
                try {
                    latestOperator = await rebuildCheckpointStore
                        .ReadAsync(operatorScope, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception readEx) when (readEx is not OperationCanceledException) {
                    // Best-effort read; fall through with `latestOperator = null` and the
                    // pre-pass-7 behavior (initial.LastAppliedSequence only).
                    Log.RebuildCancelCleanupFailed(logger, readEx, scope.Tenant, scope.Domain, scope.ProjectionName, readEx.GetType().Name);
                }

                long failLastApplied = operatorScope.AggregateId is null
                    ? Math.Max(initial.LastAppliedSequence, latestOperator?.LastAppliedSequence ?? 0)
                    : Math.Max(
                        Math.Max(initial.LastAppliedSequence, highestMatchedProgress),
                        latestOperator?.LastAppliedSequence ?? 0);

                ProjectionRebuildCheckpointSaveResult failSave = await rebuildCheckpointStore
                    .ResetAsync(
                        operatorScope,
                        failLastApplied,
                        ProjectionRebuildStatus.Failed,
                        failureReasonCode: ex.GetType().Name,
                        cancellationToken: CancellationToken.None,
                        toPosition: initial.ToPosition)
                    .ConfigureAwait(false);
                if (failSave is null || !failSave.Succeeded) {
                    Log.RebuildTerminalFailWriteRejected(logger, scope.Tenant, scope.Domain, scope.ProjectionName, failSave?.ReasonCode ?? "unknown", ex.GetType().Name);
                }
            }
            catch (Exception cleanupEx) when (cleanupEx is not OperationCanceledException) {
                Log.RebuildCancelCleanupFailed(logger, cleanupEx, scope.Tenant, scope.Domain, scope.ProjectionName, cleanupEx.GetType().Name);
            }

            await ClearRebuildLifecyclesAsync(activeLifecycleScopes.Values, operatorScope, throwOnFailure: false)
                .ConfigureAwait(false);

            throw;
        }

        // P7/P9: terminal-write logic.
        // - matchedAny=false (no tracked aggregates for the scope) → nothing-to-do is success.
        //   Without this the operator-scope row stays Running indefinitely → poller blocked.
        // - matchedAny=true + all work complete + reached bound → Succeeded.
        // - matchedAny=true + work incomplete → leave Running for next invocation (D2a scheduler
        //   or operator re-invocation will pick up from per-aggregate progress).
        // P2-7P (pass-7): finalSnapshot read uses CancellationToken.None to mirror the H3-5P
        // Succeeded-write durability intent. Without this, a request CT cancellation between
        // iteration completion and the final read dropped to cancel-cleanup which stamped
        // Canceled despite all work being logically complete.
        ProjectionRebuildCheckpoint? finalSnapshot = await rebuildCheckpointStore
            .ReadAsync(operatorScope, CancellationToken.None)
            .ConfigureAwait(false)
            ?? initial;
        if (!CanRunRebuild(finalSnapshot)) {
            _ = await ClearRebuildLifecyclesAsync(
                    activeLifecycleScopes.Values,
                    operatorScope,
                    throwOnFailure: false)
                .ConfigureAwait(false);
            activeLifecycleScopes.Clear();
            return;
        }

        if (!matchedAny || allMatchedWorkComplete) {
            // Keep the operator row active until lifecycle cleanup is durable. If cleanup fails,
            // the rebuild remains resumable instead of recording terminal success while queries
            // stay permanently Rebuilding.
            if (!await ClearRebuildLifecyclesAsync(
                    activeLifecycleScopes.Values,
                    operatorScope,
                    throwOnFailure: false)
                .ConfigureAwait(false)) {
                return;
            }

            activeLifecycleScopes.Clear();
            // H3-5P (pass-5): use CancellationToken.None for the terminal Succeeded write so a
            // cancel race between iteration exit and terminal write does not silently drop the
            // audit trail. Mirrors the C2-5P / NoDomainService / ProjectionApplyRejected pattern
            // documented in DeliverProjectionForRebuildAsync. Without this, an ASP.NET request
            // timeout disposing the request CT just before this write would propagate as OCE and
            // the cancel-cleanup catch would mis-stamp a successful rebuild as Canceled.
            ProjectionRebuildCheckpointSaveResult succeededSave = await rebuildCheckpointStore
                .SaveAsync(
                    operatorScope,
                    operatorScope.AggregateId is null
                        ? finalSnapshot!.LastAppliedSequence
                        : Math.Max(finalSnapshot!.LastAppliedSequence, highestMatchedProgress),
                    ProjectionRebuildStatus.Succeeded,
                    failureReasonCode: null,
                    CancellationToken.None,
                    finalSnapshot.ToPosition)
                .ConfigureAwait(false);
            if (succeededSave is null || !succeededSave.Succeeded) {
                Log.RebuildTerminalFailWriteRejected(
                    logger,
                    operatorScope.Tenant,
                    operatorScope.Domain,
                    operatorScope.ProjectionName,
                    succeededSave?.ReasonCode ?? "unknown",
                    nameof(ProjectionRebuildStatus.Succeeded));
                return;
            }

        }
    }

    private async Task<bool> ClearRebuildLifecyclesAsync(
        IEnumerable<RebuildLifecycleScope> lifecycleScopes,
        ProjectionRebuildCheckpointScope operatorScope,
        bool throwOnFailure = true) {
        if (lifecycleGateway is null || string.IsNullOrWhiteSpace(operatorScope.OperationId)) {
            return true;
        }

        bool succeeded = true;
        foreach (RebuildLifecycleScope lifecycleScope in lifecycleScopes.DistinctBy(static item => item.Key)) {
            try {
                // A process can disappear after durable promotion while the lifecycle's promotion
                // fence is still set. Release that same-operation fence first; a non-fenced actor
                // returns false and the normal completion below remains the source of truth.
                _ = await lifecycleGateway
                    .CompleteRebuildPromotionAsync(
                        lifecycleScope.Identity,
                        lifecycleScope.ProjectionName,
                        operatorScope.OperationId,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                bool completed = await lifecycleGateway
                    .CompleteRebuildAsync(
                        lifecycleScope.Identity,
                        lifecycleScope.ProjectionName,
                        operatorScope.OperationId,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (!completed) {
                    ProjectionLifecyclePhase phase = await lifecycleGateway
                        .ReadPhaseAsync(
                            lifecycleScope.Identity,
                            lifecycleScope.ProjectionName,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    if (phase != ProjectionLifecyclePhase.Idle) {
                        throw new InvalidOperationException("Projection rebuild lifecycle completion was rejected.");
                    }
                }
            }
            catch (Exception exception) {
                succeeded = false;
                Log.RebuildCancelCleanupFailed(
                    logger,
                    exception,
                    operatorScope.Tenant,
                    operatorScope.Domain,
                    lifecycleScope.ProjectionName,
                    exception.GetType().Name);
                if (throwOnFailure) {
                    throw;
                }
            }
        }

        return succeeded;
    }

    private async Task<RebuildDeliveryResult> DeliverProjectionForRebuildAsync(
        AggregateIdentity identity,
        ProjectionRebuildCheckpointScope operatorScope,
        ProjectionRebuildCheckpointScope perAggregateScope,
        ProjectionRebuildCheckpoint? initialPerAggregateRow,
        long perAggregateProgress,
        long? toPosition,
        IDictionary<string, RebuildLifecycleScope> activeLifecycleScopes,
        CancellationToken cancellationToken) {
        using IDisposable projectionLock = await ProjectionLocks.AcquireAsync(identity.ActorId, cancellationToken).ConfigureAwait(false);
        try {
            Log.RebuildDeliveryStarted(logger, identity.TenantId, identity.Domain, identity.AggregateId);

            DomainServiceRegistration? registration = await resolver
                .ResolveAsync(identity.TenantId, identity.Domain, "v1", cancellationToken)
                .ConfigureAwait(false);

            if (registration is null) {
                // DEC2-P: NoDomainServiceRegistered is a permanent configuration error, not a
                // transient interrupt. Write Failed terminal status to operator scope so the
                // rebuild lifecycle stops and the active-rebuilds index is cleared. P10 +
                // CancellationToken.None on the failure write so cancel-race does not lose the
                // audit trail.
                //
                // C2-5P (pass-5): use ResetAsync so the new StaleCheckpoint guard in SaveAsync
                // (rejects status=Failed with lastAppliedSequence < existing.LastAppliedSequence)
                // does not silently drop the audit write. ResetAsync explicitly bypasses
                // monotonic guards per its XML doc; lifecycle reaches Failed reliably.
                Log.NoDomainServiceRegistered(logger, identity.TenantId, identity.Domain);
                await FailRebuildAsync(
                        operatorScope,
                        lastAppliedSequence: 0,
                        StreamReplayReasonCodes.NoDomainService,
                        toPosition)
                    .ConfigureAwait(false);
                return RebuildDeliveryResult.Interrupt();
            }

            string operationId = operatorScope.OperationId
                ?? throw new InvalidOperationException("A running projection rebuild requires an operation identity.");
            if (namedProjectionDispatchCoordinator is not null) {
                NamedProjectionRebuildResult? namedLease = await namedProjectionDispatchCoordinator
                    .AcquireRebuildLifecyclesAsync(
                        identity,
                        registration,
                        operationId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (namedLease is not null) {
                    foreach (string projectionType in namedLease.LifecycleProjectionTypes) {
                        TrackLifecycleScope(activeLifecycleScopes, identity, projectionType);
                    }

                    if (namedLease.Owned && !namedLease.Succeeded) {
                        return RebuildDeliveryResult.Interrupt();
                    }
                }
            }

            IAggregateActor aggregateProxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(identity.ActorId),
                AggregateActorTypeName);

            long currentStreamHead = await aggregateProxy.GetCurrentSequenceAsync().ConfigureAwait(false);
            long frozenTarget = initialPerAggregateRow is not null
                && string.Equals(initialPerAggregateRow.OperationId, operatorScope.OperationId, StringComparison.Ordinal)
                && initialPerAggregateRow.ToPosition is long persistedTarget
                    ? persistedTarget
                    : Math.Min(toPosition ?? currentStreamHead, currentStreamHead);
            ProjectionRebuildCheckpointScope progressScope = operatorScope.AggregateId is null
                ? perAggregateScope
                : operatorScope;

            // Freeze is itself a durable resumable boundary. A retry must never re-sample a newer
            // stream head after a crash between the read and the later promotion/checkpoint write.
            if (rebuildWriteGateway is not null) {
                ProjectionRebuildCheckpointSaveResult frozenSave = await rebuildCheckpointStore!
                    .SaveAsync(
                        progressScope,
                        perAggregateProgress,
                        ProjectionRebuildStatus.Running,
                        failureReasonCode: null,
                        CancellationToken.None,
                        frozenTarget,
                        isPerAggregateProgress: operatorScope.AggregateId is null)
                    .ConfigureAwait(false);
                if (frozenSave is null || !frozenSave.Succeeded) {
                    return RebuildDeliveryResult.Interrupt();
                }

                ProjectionRebuildCheckpoint? frozenReadBack = await rebuildCheckpointStore
                    .ReadAsync(progressScope, CancellationToken.None)
                    .ConfigureAwait(false);
                if (frozenReadBack is null
                    || frozenReadBack.ToPosition != frozenTarget
                    || frozenReadBack.LastAppliedSequence != perAggregateProgress
                    || !string.Equals(frozenReadBack.OperationId, operationId, StringComparison.Ordinal)) {
                    return RebuildDeliveryResult.Interrupt();
                }

                initialPerAggregateRow = frozenReadBack;
            }

            // The legacy /project handler is stateless full-replay. Paging is therefore only a
            // bounded read mechanism: reconstruct the complete prefix from sequence 1 before the
            // handler is invoked. The response remains an in-memory, operation-scoped candidate
            // until the complete prefix has been read, so a later-page failure or cancellation
            // cannot overwrite the last complete live projection.
            RebuildPrefixReadResult prefix = await ReadCompleteRebuildPrefixAsync(
                    aggregateProxy,
                    identity,
                    frozenTarget,
                    projectionOptions.Value.RebuildPageSize,
                    projectionOptions.Value.RebuildMaxPrefixEventCount,
                    projectionOptions.Value.RebuildMaxPrefixBytes,
                    cancellationToken)
                .ConfigureAwait(false);

            if (prefix.FailureReasonCode is not null) {
                await FailRebuildAsync(
                        operatorScope,
                        perAggregateProgress,
                        prefix.FailureReasonCode,
                        frozenTarget)
                    .ConfigureAwait(false);
                return RebuildDeliveryResult.Interrupt();
            }

            if (prefix.Events.Length == 0) {
                // P8: empty-events path no longer masquerades as work-complete. PageComplete is
                // explicit: we have reached the bound iff progress meets ToPosition (or there is
                // no bound and the actor reports nothing more).
                if (perAggregateProgress > 0) {
                    Log.CheckpointDriftDetected(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        ProjectionReasonCodes.CheckpointDrift,
                        perAggregateProgress,
                        0);
                }

                Log.NoEventsFound(logger, identity.TenantId, identity.Domain, identity.AggregateId);
                return RebuildDeliveryResult.Complete(perAggregateProgress, pageComplete: true);
            }

            var request = new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, prefix.Events);
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(
                registration.AppId,
                "project",
                request);
            HttpClient httpClient = httpClientFactory.CreateClient();
            HttpResponseMessage? httpResponse = await SendProjectRequestAsync(
                    httpClient,
                    httpRequest,
                    registration.AppId,
                    identity,
                    cancellationToken)
                .ConfigureAwait(false);
            if (httpResponse is null) {
                await FailRebuildAsync(
                        operatorScope,
                        perAggregateProgress,
                        StreamReplayReasonCodes.ProjectionApplyRejected,
                        frozenTarget)
                    .ConfigureAwait(false);
                return RebuildDeliveryResult.Interrupt();
            }

            using HttpResponseMessage responseHandle = httpResponse;
            if (!responseHandle.IsSuccessStatusCode) {
                Log.ProjectInvocationRejected(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    registration.AppId,
                    GetUpstreamReasonCode(responseHandle.StatusCode),
                    ((int)responseHandle.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    GetContentTypeForLog(responseHandle.Content));
                // P10: durability of failure recording — use CancellationToken.None so a cancel
                // race during the failure write does not silently drop the audit trail.
                //
                // C2-5P (pass-5): use ResetAsync so the new StaleCheckpoint guard in SaveAsync
                // (rejects status=Failed with lastAppliedSequence < existing.LastAppliedSequence)
                // does not silently drop the audit write when a prior aggregate's per-aggregate
                // progress has already advanced operator-scope's LastAppliedSequence above this
                // aggregate's progress. ResetAsync explicitly bypasses monotonic guards.
                await FailRebuildAsync(
                        operatorScope,
                        perAggregateProgress,
                        StreamReplayReasonCodes.ProjectionApplyRejected,
                        frozenTarget)
                    .ConfigureAwait(false);
                return RebuildDeliveryResult.Interrupt();
            }

            ProjectionResponse? response = await ReadProjectResponseAsync(
                    responseHandle,
                    registration.AppId,
                    identity,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response is null) {
                await FailRebuildAsync(
                        operatorScope,
                        perAggregateProgress,
                        StreamReplayReasonCodes.ProjectionApplyRejected,
                        frozenTarget)
                    .ConfigureAwait(false);
                return RebuildDeliveryResult.Interrupt();
            }

            if (string.IsNullOrWhiteSpace(response.ProjectionType)
                || !string.Equals(response.ProjectionType, operatorScope.ProjectionName, StringComparison.Ordinal)) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, ProjectionReasonCodes.ProjectInvalidProjectionType);
                await FailRebuildAsync(
                        operatorScope,
                        perAggregateProgress,
                        StreamReplayReasonCodes.ProjectionApplyRejected,
                        frozenTarget)
                    .ConfigureAwait(false);
                return RebuildDeliveryResult.Interrupt();
            }

            if (response.State.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || (response.State.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(response.State.GetString()))) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, ProjectionReasonCodes.ProjectInvalidState);
                await FailRebuildAsync(
                        operatorScope,
                        perAggregateProgress,
                        StreamReplayReasonCodes.ProjectionApplyRejected,
                        frozenTarget)
                    .ConfigureAwait(false);
                return RebuildDeliveryResult.Interrupt();
            }

            Log.DomainServiceInvocationSucceeded(logger, identity.TenantId, identity.Domain, identity.AggregateId, registration.AppId);

            string projectionActorId = QueryActorIdHelper.DeriveActorId(
                response.ProjectionType,
                identity.TenantId,
                identity.AggregateId,
                []);

            var projectionActor = new ActorId(projectionActorId);
            IProjectionWriteActor writeProxy = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(
                projectionActor,
                QueryRouter.ProjectionActorTypeName);
            ProjectionState candidateState = ProjectionState.FromJsonElement(
                response.ProjectionType,
                identity.TenantId,
                response.State);

            // P19: re-check operator-scope lifecycle AND verify the operator is still the same one
            // (OperationId match) BEFORE writing the projection state. Without the OperationId
            // check, a concurrent operator B that issued Reset+Replay between this iteration's
            // initial read and this point would silently inherit our stale page application.
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionRebuildCheckpoint? preSaveOperator = await rebuildCheckpointStore!
                .ReadAsync(operatorScope, cancellationToken)
                .ConfigureAwait(false);
            if (!CanRunRebuild(preSaveOperator)
                || !string.Equals(preSaveOperator!.OperationId, operatorScope.OperationId, StringComparison.Ordinal)) {
                return RebuildDeliveryResult.Interrupt();
            }

            if (operatorScope.AggregateId is null) {
                ProjectionRebuildCheckpoint? preSavePerAggregate = await rebuildCheckpointStore
                    .ReadAsync(perAggregateScope, cancellationToken)
                    .ConfigureAwait(false);
                if (PerAggregateProgressChanged(initialPerAggregateRow, preSavePerAggregate)) {
                    // P8-8P (pass-8): operator was preempted by a concurrent per-aggregate write
                    // (different OperationId, advanced LastAppliedSequence, status drift, or
                    // ToPosition shift). Without this Canceled write, the operator-scope row
                    // stayed at Running and the active-rebuilds index entry leaked permanently.
                    // The outer foreach catch in RebuildProjectionAsync only fires on `throw`,
                    // not on Interrupt() returns. Use ResetAsync (bypasses monotonic guards) and
                    // CancellationToken.None so a request cancel race does not silently drop the
                    // audit trail.
                    try {
                        ProjectionRebuildCheckpointSaveResult preemptSave = await rebuildCheckpointStore
                            .ResetAsync(
                                operatorScope,
                                lastAppliedSequence: preSavePerAggregate?.LastAppliedSequence ?? perAggregateProgress,
                                ProjectionRebuildStatus.Canceled,
                                failureReasonCode: StreamReplayReasonCodes.OperatorPreempted,
                                cancellationToken: CancellationToken.None,
                                toPosition: toPosition)
                            .ConfigureAwait(false);
                        if (!preemptSave.Succeeded) {
                            Log.RebuildCancelCleanupRejected(logger, operatorScope.Tenant, operatorScope.Domain, operatorScope.ProjectionName, preemptSave.ReasonCode ?? "unknown");
                        }
                    }
                    catch (Exception preemptEx) when (preemptEx is not OperationCanceledException) {
                        Log.RebuildCancelCleanupFailed(logger, preemptEx, operatorScope.Tenant, operatorScope.Domain, operatorScope.ProjectionName, preemptEx.GetType().Name);
                    }

                    return RebuildDeliveryResult.Interrupt();
                }
            }

            if (rebuildWriteGateway is null) {
                if (namedProjectionDispatchCoordinator is not null) {
                    NamedProjectionRebuildResult legacyNamedResult = await namedProjectionDispatchCoordinator
                        .TryRebuildAsync(
                            identity,
                            registration,
                            prefix.Events,
                            operationId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    foreach (string projectionType in legacyNamedResult.LifecycleProjectionTypes) {
                        TrackLifecycleScope(activeLifecycleScopes, identity, projectionType);
                    }

                    LogNamedRebuildOutcomes(identity, legacyNamedResult.Outcomes);
                    if (legacyNamedResult.Owned && !legacyNamedResult.Succeeded) {
                        if (legacyNamedResult.IsTerminalFailure) {
                            string reasonCode = legacyNamedResult.Outcomes
                                .Where(static outcome => outcome.Status == ProjectionDispatchStatus.Failed)
                                .Select(static outcome => outcome.ReasonCode)
                                .FirstOrDefault(static reason => !string.IsNullOrWhiteSpace(reason))
                                ?? StreamReplayReasonCodes.ProjectionApplyRejected;
                            await FailRebuildAsync(
                                    operatorScope,
                                    perAggregateProgress,
                                    reasonCode,
                                    frozenTarget)
                                .ConfigureAwait(false);
                        }

                        return RebuildDeliveryResult.Interrupt();
                    }
                }

                await writeProxy.UpdateProjectionAsync(candidateState).ConfigureAwait(false);
                _ = await RegenerateProjectionETagAsync(
                        identity,
                        response.ProjectionType,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                long legacyHighestAppliedSequence = prefix.Events[^1].SequenceNumber;
                ProjectionRebuildCheckpointSaveResult legacySave = await rebuildCheckpointStore!
                    .SaveAsync(
                        progressScope,
                        legacyHighestAppliedSequence,
                        ProjectionRebuildStatus.Running,
                        failureReasonCode: null,
                        CancellationToken.None,
                        frozenTarget,
                        isPerAggregateProgress: operatorScope.AggregateId is null)
                    .ConfigureAwait(false);
                if (legacySave is null || !legacySave.Succeeded) {
                    return RebuildDeliveryResult.Interrupt();
                }

                return RebuildDeliveryResult.Complete(legacyHighestAppliedSequence, pageComplete: true);
            }

            bool actorStaged = false;
            bool actorPromoted = false;
            bool namedOwned = false;
            bool namedStaged = false;
            bool namedCommitAttempted = false;
            bool namedCommitAborted = false;
            bool namedCommitted = false;
            bool completed = false;
            bool retainPromotionFences = false;
            List<RebuildLifecycleScope> promotionFences = [];
            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (rebuildWriteGateway is not null) {
                    await rebuildWriteGateway
                        .StageAsync(
                            projectionActorId,
                            new ProjectionRebuildCandidate(operationId, candidateState),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    actorStaged = true;
                }

                if (namedProjectionDispatchCoordinator is not null) {
                    NamedProjectionRebuildResult namedResult = await namedProjectionDispatchCoordinator
                        .TryRebuildAsync(
                            identity,
                            registration,
                            prefix.Events,
                            operationId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    namedOwned = namedResult.Owned;
                    namedStaged = namedResult.Owned;
                    foreach (string projectionType in namedResult.LifecycleProjectionTypes) {
                        TrackLifecycleScope(activeLifecycleScopes, identity, projectionType);
                    }

                    LogNamedRebuildOutcomes(identity, namedResult.Outcomes);
                    if (namedResult.Owned && !namedResult.Succeeded) {
                        if (namedResult.IsTerminalFailure) {
                            string reasonCode = namedResult.Outcomes
                                .Where(static outcome => outcome.Status == ProjectionDispatchStatus.Failed)
                                .Select(static outcome => outcome.ReasonCode)
                                .FirstOrDefault(static reason => !string.IsNullOrWhiteSpace(reason))
                                ?? StreamReplayReasonCodes.ProjectionApplyRejected;
                            await FailRebuildAsync(
                                    operatorScope,
                                    perAggregateProgress,
                                    reasonCode,
                                    frozenTarget)
                                .ConfigureAwait(false);
                        }

                        return RebuildDeliveryResult.Interrupt();
                    }
                }

                if (!await OwnsRebuildAsync(operatorScope, operationId).ConfigureAwait(false)) {
                    return RebuildDeliveryResult.Interrupt();
                }

                promotionFences = await AcquirePromotionFencesAsync(
                        activeLifecycleScopes.Values,
                        operationId)
                    .ConfigureAwait(false);
                if (lifecycleGateway is not null
                    && promotionFences.Count != activeLifecycleScopes.Count) {
                    return RebuildDeliveryResult.Interrupt();
                }

                // The lifecycle actors now reject terminal cleanup until these fences are released.
                // Re-read ownership inside that fenced region so cancel/preempt cannot win after
                // validation and before the actor promotion.
                if (!await OwnsRebuildAsync(operatorScope, operationId).ConfigureAwait(false)) {
                    return RebuildDeliveryResult.Interrupt();
                }

                if (rebuildWriteGateway is not null) {
                    actorPromoted = await rebuildWriteGateway
                        .PromoteAsync(projectionActorId, operationId, CancellationToken.None)
                        .ConfigureAwait(false);
                    if (!actorPromoted) {
                        return RebuildDeliveryResult.Interrupt();
                    }

                    ProjectionState? promotedReadBack = await rebuildWriteGateway
                        .ReadAsync(projectionActorId, CancellationToken.None)
                        .ConfigureAwait(false);
                    if (!ProjectionStatesEqual(candidateState, promotedReadBack)) {
                        return RebuildDeliveryResult.Interrupt();
                    }
                }
                else {
                    await writeProxy.UpdateProjectionAsync(candidateState).ConfigureAwait(false);
                    actorPromoted = true;
                }

                if (!await RegenerateProjectionETagAsync(
                        identity,
                        response.ProjectionType,
                        CancellationToken.None,
                        requireReadBack: rebuildWriteGateway is not null)
                    .ConfigureAwait(false)) {
                    return RebuildDeliveryResult.Interrupt();
                }

                long highestAppliedSequence = prefix.Events[^1].SequenceNumber;
                ProjectionRebuildCheckpointSaveResult pendingSave = await rebuildCheckpointStore!
                    .SaveAsync(
                        progressScope,
                        highestAppliedSequence,
                        ProjectionRebuildStatus.Running,
                        RebuildPromotionPendingReasonCode,
                        CancellationToken.None,
                        frozenTarget,
                        isPerAggregateProgress: operatorScope.AggregateId is null)
                    .ConfigureAwait(false);
                if (!pendingSave.Succeeded
                    || !await VerifyRebuildCheckpointAsync(
                            progressScope,
                            operationId,
                            highestAppliedSequence,
                            frozenTarget,
                            RebuildPromotionPendingReasonCode)
                        .ConfigureAwait(false)) {
                    return RebuildDeliveryResult.Interrupt();
                }

                if (namedOwned) {
                    namedCommitAttempted = true;
                    NamedProjectionRebuildResult commit = await namedProjectionDispatchCoordinator!
                        .CommitRebuildAsync(
                            identity,
                            registration,
                            prefix.Events,
                            operationId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    LogNamedRebuildOutcomes(identity, commit.Outcomes);
                    if (!commit.Succeeded) {
                        NamedProjectionRebuildResult verification = await namedProjectionDispatchCoordinator
                            .VerifyRebuildAsync(
                                identity,
                                registration,
                                prefix.Events,
                                operationId,
                                CancellationToken.None)
                            .ConfigureAwait(false);
                        if (!verification.Succeeded) {
                            NamedProjectionRebuildResult abort = await namedProjectionDispatchCoordinator
                                .AbortRebuildAsync(
                                    identity,
                                    registration,
                                    prefix.Events,
                                    operationId,
                                    CancellationToken.None)
                                .ConfigureAwait(false);
                            namedCommitAborted = abort.Succeeded;
                            retainPromotionFences = !namedCommitAborted;
                            return RebuildDeliveryResult.Interrupt();
                        }
                    }

                    namedCommitted = true;
                    NamedProjectionRebuildResult namedReadBack = await namedProjectionDispatchCoordinator
                        .VerifyRebuildAsync(
                            identity,
                            registration,
                            prefix.Events,
                            operationId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    if (!namedReadBack.Succeeded) {
                        retainPromotionFences = true;
                        return RebuildDeliveryResult.Interrupt();
                    }
                }

                ProjectionRebuildCheckpointSaveResult completedSave = await rebuildCheckpointStore
                    .SaveAsync(
                        progressScope,
                        highestAppliedSequence,
                        ProjectionRebuildStatus.Running,
                        failureReasonCode: null,
                        CancellationToken.None,
                        frozenTarget,
                        isPerAggregateProgress: operatorScope.AggregateId is null)
                    .ConfigureAwait(false);
                if (!completedSave.Succeeded
                    || !await VerifyRebuildCheckpointAsync(
                            progressScope,
                            operationId,
                            highestAppliedSequence,
                            frozenTarget,
                            failureReasonCode: null)
                        .ConfigureAwait(false)) {
                    retainPromotionFences = namedCommitted;
                    return RebuildDeliveryResult.Interrupt();
                }

                if (rebuildWriteGateway is not null
                    && !await rebuildWriteGateway
                        .FinalizeAsync(projectionActorId, operationId, CancellationToken.None)
                        .ConfigureAwait(false)) {
                    retainPromotionFences = true;
                    return RebuildDeliveryResult.Interrupt();
                }

                completed = true;
                Log.ProjectionStateUpdated(logger, identity.TenantId, identity.Domain, identity.AggregateId, response.ProjectionType, projectionActorId);
                return RebuildDeliveryResult.Complete(highestAppliedSequence, pageComplete: true);
            }
            catch (OperationCanceledException) when (namedCommitAttempted) {
                // Once named commit may have crossed the visibility marker, cancellation cannot
                // safely classify the operation as canceled. Keep the durable receipt, pending
                // checkpoint, and lifecycle fences for same-operation reconciliation.
                retainPromotionFences = true;
                return RebuildDeliveryResult.Interrupt();
            }
            catch (Exception exception) when (namedCommitAttempted) {
                retainPromotionFences = true;
                Log.ProjectionUpdateFailed(
                    logger,
                    exception,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId);
                return RebuildDeliveryResult.Interrupt();
            }
            finally {
                if (!completed && (namedCommitted || (namedCommitAttempted && !namedCommitAborted))) {
                    retainPromotionFences = true;
                }

                if (!completed && !retainPromotionFences) {
                    if (namedStaged && !namedCommitted && !namedCommitAttempted) {
                        _ = await namedProjectionDispatchCoordinator!
                            .AbortRebuildAsync(
                                identity,
                                registration,
                                prefix.Events,
                                operationId,
                                CancellationToken.None)
                            .ConfigureAwait(false);
                    }

                    if (rebuildWriteGateway is not null) {
                        if (actorPromoted) {
                            _ = await rebuildWriteGateway
                                .RollbackAsync(projectionActorId, operationId, CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        else if (actorStaged) {
                            _ = await rebuildWriteGateway
                                .DiscardAsync(projectionActorId, operationId, CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                    }
                }

                if (!retainPromotionFences) {
                    _ = await CompletePromotionFencesAsync(promotionFences, operationId).ConfigureAwait(false);
                }
            }
        }
        catch (ProjectionRebuildPrefixSafetyLimitExceededException) {
            await FailRebuildAsync(
                    operatorScope,
                    perAggregateProgress,
                    StreamReplayReasonCodes.RebuildPrefixSafetyLimitExceeded,
                    toPosition)
                .ConfigureAwait(false);
            return RebuildDeliveryResult.Interrupt();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // P5-7P (pass-7): rethrow so the outer H1-5P catch in RebuildProjectionAsync writes
            // Failed via ResetAsync and clears the active-rebuilds index. The prior `return
            // Interrupt()` made the outer foreach `return` early without an exception, leaving
            // operator-scope row at Running and the active-index entry orphaned indefinitely
            // (until P11-7P TTL expired or a future operator action healed it). The other
            // Interrupt() returns in this method are PLANNED (NoDomainService, IsSuccessStatusCode
            // false, invalid response) and write their own ResetAsync(Failed) audit row before
            // returning — only the unhandled-exception path was silent.
            Log.ProjectionUpdateFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId);
            throw;
        }
    }

    private async Task<RebuildPrefixReadResult> ReadCompleteRebuildPrefixAsync(
        IAggregateActor aggregateProxy,
        AggregateIdentity identity,
        long targetPosition,
        int pageSize,
        int maxEventCount,
        long maxSerializedBytes,
        CancellationToken cancellationToken) {
        var events = new List<ProjectionEventDto>();
        using var serializedSize = new SizeLimitedWriteStream(maxSerializedBytes);
        long cursor = 0;
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            EventEnvelope[] page = await aggregateProxy
                .ReadEventsRangeAsync(cursor, targetPosition, pageSize)
                .ConfigureAwait(false);
            if (page.Length == 0) {
                if (cursor != targetPosition) {
                    throw new InvalidOperationException("Projection rebuild ended before the frozen stream boundary.");
                }

                return new RebuildPrefixReadResult([.. events], FailureReasonCode: null);
            }

            if (page.Length > pageSize) {
                throw new InvalidOperationException("Projection rebuild actor returned more events than the requested page size.");
            }

            long expectedSequence = cursor + 1;
            foreach (EventEnvelope item in page) {
                if (item.SequenceNumber != expectedSequence
                    || item.SequenceNumber > targetPosition) {
                    throw new InvalidOperationException("Projection rebuild page is not a contiguous ordered aggregate sequence.");
                }

                expectedSequence++;
            }

            long nextCursor = page[^1].SequenceNumber;
            if (nextCursor <= cursor) {
                throw new InvalidOperationException("Projection rebuild page did not advance the aggregate sequence.");
            }

            if (events.Count > maxEventCount - page.Length) {
                throw new ProjectionRebuildPrefixSafetyLimitExceededException();
            }

            long remainingBytes = maxSerializedBytes - serializedSize.Length;
            foreach (EventEnvelope envelope in page) {
                if (envelope.Payload.LongLength > remainingBytes) {
                    throw new ProjectionRebuildPrefixSafetyLimitExceededException();
                }

                remainingBytes -= envelope.Payload.LongLength;
            }

            foreach (EventEnvelope envelope in page) {
                if (envelope.Payload.LongLength > maxSerializedBytes - serializedSize.Length) {
                    throw new ProjectionRebuildPrefixSafetyLimitExceededException();
                }

                ProjectionEventReadabilityResult readability = await ProjectionEventWireBuilder
                    .BuildAsync(_payloadProtectionService, identity, [envelope], cancellationToken)
                    .ConfigureAwait(false);
                if (readability.UnreadableReason is not null) {
                    string reasonCode = UnreadableProtectedDataReasonCodes.From(readability.UnreadableReason.Value);
                    Log.UnreadableProtectedEvent(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        readability.SequenceNumber ?? 0,
                        reasonCode,
                        "projection-rebuild");
                    return new RebuildPrefixReadResult([], reasonCode);
                }

                await JsonSerializer
                    .SerializeAsync(
                        serializedSize,
                        readability.Events![0],
                        daprClient.JsonSerializerOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                events.Add(readability.Events[0]);
            }
            cursor = nextCursor;
            if (cursor >= targetPosition) {
                return new RebuildPrefixReadResult([.. events], FailureReasonCode: null);
            }

            if (page.Length < pageSize) {
                throw new InvalidOperationException("Projection rebuild ended before the frozen stream boundary.");
            }
        }
    }

    private sealed record RebuildPrefixReadResult(
        ProjectionEventDto[] Events,
        string? FailureReasonCode);

    private sealed record RebuildDeliveryResult(
        long LastAppliedSequence,
        bool PageComplete,
        bool Interrupted) {
        public static RebuildDeliveryResult Complete(long lastAppliedSequence, bool pageComplete)
            => new(lastAppliedSequence, pageComplete, Interrupted: false);

        public static RebuildDeliveryResult Interrupt()
            => new(0, PageComplete: false, Interrupted: true);
    }

    private sealed record RebuildLifecycleScope(
        AggregateIdentity Identity,
        string ProjectionName) {
        public string Key => string.Concat(Identity.ActorId, "\0", ProjectionName);
    }

    private static void TrackLifecycleScope(
        IDictionary<string, RebuildLifecycleScope> lifecycleScopes,
        AggregateIdentity identity,
        string projectionName) {
        var lifecycleScope = new RebuildLifecycleScope(identity, projectionName);
        lifecycleScopes[lifecycleScope.Key] = lifecycleScope;
    }

    private async Task<bool> OwnsRebuildAsync(
        ProjectionRebuildCheckpointScope operatorScope,
        string operationId) {
        ProjectionRebuildCheckpoint? checkpoint = await rebuildCheckpointStore!
            .ReadAsync(operatorScope, CancellationToken.None)
            .ConfigureAwait(false);
        return CanRunRebuild(checkpoint)
            && string.Equals(checkpoint!.OperationId, operationId, StringComparison.Ordinal);
    }

    private async Task<List<RebuildLifecycleScope>> AcquirePromotionFencesAsync(
        IEnumerable<RebuildLifecycleScope> lifecycleScopes,
        string operationId) {
        var acquired = new List<RebuildLifecycleScope>();
        if (lifecycleGateway is null) {
            return acquired;
        }

        foreach (RebuildLifecycleScope lifecycleScope in lifecycleScopes.DistinctBy(static item => item.Key)) {
            if (!await lifecycleGateway
                .BeginRebuildPromotionAsync(
                    lifecycleScope.Identity,
                    lifecycleScope.ProjectionName,
                    operationId,
                    CancellationToken.None)
                .ConfigureAwait(false)) {
                _ = await CompletePromotionFencesAsync(acquired, operationId).ConfigureAwait(false);
                return [];
            }

            acquired.Add(lifecycleScope);
        }

        return acquired;
    }

    private async Task<bool> CompletePromotionFencesAsync(
        IEnumerable<RebuildLifecycleScope> lifecycleScopes,
        string operationId) {
        if (lifecycleGateway is null) {
            return true;
        }

        bool succeeded = true;
        foreach (RebuildLifecycleScope lifecycleScope in lifecycleScopes.Reverse()) {
            try {
                bool completed = await lifecycleGateway
                    .CompleteRebuildPromotionAsync(
                        lifecycleScope.Identity,
                        lifecycleScope.ProjectionName,
                        operationId,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                succeeded &= completed;
            }
            catch (Exception exception) {
                succeeded = false;
                Log.RebuildCancelCleanupFailed(
                    logger,
                    exception,
                    lifecycleScope.Identity.TenantId,
                    lifecycleScope.Identity.Domain,
                    lifecycleScope.ProjectionName,
                    exception.GetType().Name);
            }
        }

        return succeeded;
    }

    private async Task<bool> VerifyRebuildCheckpointAsync(
        ProjectionRebuildCheckpointScope scope,
        string operationId,
        long lastAppliedSequence,
        long frozenTarget,
        string? failureReasonCode) {
        ProjectionRebuildCheckpoint? readBack = await rebuildCheckpointStore!
            .ReadAsync(scope, CancellationToken.None)
            .ConfigureAwait(false);
        return readBack is not null
            && readBack.Status == ProjectionRebuildStatus.Running
            && readBack.LastAppliedSequence == lastAppliedSequence
            && readBack.ToPosition == frozenTarget
            && string.Equals(readBack.OperationId, operationId, StringComparison.Ordinal)
            && string.Equals(readBack.FailureReasonCode, failureReasonCode, StringComparison.Ordinal);
    }

    private void LogNamedRebuildOutcomes(
        AggregateIdentity identity,
        IEnumerable<ProjectionDispatchOutcome> outcomes) {
        foreach (ProjectionDispatchOutcome outcome in outcomes) {
            Log.NamedRebuildRouteOutcome(
                logger,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                outcome.ProjectionType,
                outcome.Status,
                outcome.ReasonCode ?? string.Empty);
        }
    }

    private static bool ProjectionStatesEqual(ProjectionState expected, ProjectionState? actual)
        => actual is not null
            && string.Equals(expected.ProjectionType, actual.ProjectionType, StringComparison.Ordinal)
            && string.Equals(expected.TenantId, actual.TenantId, StringComparison.Ordinal)
            && expected.StateBytes.AsSpan().SequenceEqual(actual.StateBytes);

    private async Task FailRebuildAsync(
        ProjectionRebuildCheckpointScope operatorScope,
        long lastAppliedSequence,
        string reasonCode,
        long? toPosition) {
        ProjectionRebuildCheckpointSaveResult failure = await rebuildCheckpointStore!
            .ResetAsync(
                operatorScope,
                lastAppliedSequence,
                ProjectionRebuildStatus.Failed,
                failureReasonCode: reasonCode,
                cancellationToken: CancellationToken.None,
                toPosition: toPosition)
            .ConfigureAwait(false);
        if (failure is null || !failure.Succeeded) {
            throw new InvalidOperationException(
                $"Projection rebuild terminalization was rejected: {failure?.ReasonCode ?? "unknown"}.");
        }
    }

    private static bool MatchesRebuildScope(ProjectionRebuildCheckpointScope scope, AggregateIdentity identity)
        => string.Equals(scope.Tenant, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(scope.Domain, identity.Domain, StringComparison.Ordinal)
            && (scope.AggregateId is null
                || string.Equals(scope.AggregateId, identity.AggregateId, StringComparison.Ordinal));

    private static string CreateDurableDeliveryOperationId(
        AggregateIdentity identity,
        string projectionType,
        IReadOnlyList<EventEnvelope> events,
        long highestAvailableSequence) {
        string headMessageId = events
            .Where(item => item.SequenceNumber == highestAvailableSequence)
            .Select(static item => item.MessageId)
            .FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item))
            ?? highestAvailableSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string material = string.Join(
            '\0',
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            projectionType,
            headMessageId,
            highestAvailableSequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return "legacy-delivery-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    // P19-6P: NotStarted removed from the can-run set. The original "include NotStarted so
    // cancel-cleanup arriving before the first iteration still triggers the terminal write
    // path" rationale (P14) is redundant: the cancel-cleanup catch block runs regardless of
    // CanRunRebuild and writes terminal Canceled when the snapshot status is non-terminal
    // (NotStarted is non-terminal so cancel-cleanup still handles it). Permitting NotStarted
    // here allowed a racing direct invocation to proceed without the controller having
    // populated the active-rebuilds index, opening a poller-vs-rebuild race window. The
    // controller's Reset+Replay path always writes Running before invoking the orchestrator,
    // so a legitimate caller will pass this gate.
    private static bool CanRunRebuild(ProjectionRebuildCheckpoint? checkpoint)
        => checkpoint?.Status is ProjectionRebuildStatus.Running
            or ProjectionRebuildStatus.Resuming
            or ProjectionRebuildStatus.Retrying;

    private static bool IsTerminalStatus(ProjectionRebuildStatus status)
        => status is ProjectionRebuildStatus.Succeeded
            or ProjectionRebuildStatus.Failed
            or ProjectionRebuildStatus.Canceled;

    // P15-8P (pass-8): explicit null-asymmetry contract. The pre-pass-8 XOR encoding treated
    // (initial=non-null, current=null) — i.e., row deletion between the page read and the
    // pre-save check — as "progress changed", forcing an Interrupt+P8-8P preempt cleanup. Row
    // deletion is NOT a documented signal in this system (only ResetAsync writes a fresh row;
    // no API deletes rows). We preserve the existing behavior for safety (treat deletion as
    // concurrent takeover) but document the intent here. The (initial=null, current=non-null)
    // direction means a per-aggregate row was created between read and pre-save check by
    // another orchestrator — also concurrent takeover.
    //
    // P17-7P (pass-7 MEDIUM): expanded field comparison. Pre-pass-7 only checked OperationId /
    // LastAppliedSequence / Status / ToPosition. A racing process briefly writing Failed-then-
    // Running with a non-null FailureReasonCode passed the equality check (only the
    // intermediate state changed). FailureReasonCode is now included. UpdatedAt is excluded
    // because it changes on every write (would always flag drift).
    private static bool PerAggregateProgressChanged(
        ProjectionRebuildCheckpoint? initial,
        ProjectionRebuildCheckpoint? current) {
        if (initial is null || current is null) {
            // Row deletion or creation between read and pre-save check: concurrent takeover.
            return initial is not null || current is not null;
        }

        return !string.Equals(initial.OperationId, current.OperationId, StringComparison.Ordinal)
            || initial.LastAppliedSequence != current.LastAppliedSequence
            || initial.Status != current.Status
            || initial.ToPosition != current.ToPosition
            || !string.Equals(initial.FailureReasonCode, current.FailureReasonCode, StringComparison.Ordinal);
    }

    // Bounded page size for operator rebuild reads. Prevents O(N) full-stream replays per iteration
    // and bounds the per-page apply work. Domain-wide rebuild enumerates aggregates separately;
    // each aggregate reads from its own checkpointTracker progress.

    private static ProjectionRebuildCheckpointScope ScopeForCheckpoint(
        ProjectionRebuildCheckpointScope scope,
        ProjectionRebuildCheckpoint checkpoint)
        => scope with {
            // Preserve operator scope's AggregateId. Overwriting it from `checkpoint.AggregateId`
            // caused scope/key drift for domain-wide rebuild where operator scope has AggregateId=null
            // but a prior aggregate-specific run persisted a non-null value.
            OperationId = checkpoint.OperationId,
        };

    private async Task<HttpResponseMessage?> SendProjectRequestAsync(
        HttpClient httpClient,
        HttpRequestMessage httpRequest,
        string appId,
        AggregateIdentity identity,
        CancellationToken cancellationToken) {
        try {
            return await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (TaskCanceledException ex) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.ProjectTimeout,
                ex.GetType().Name,
                "0",
                "none");
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.Unknown,
                ex.GetType().Name,
                "0",
                "none");
            return null;
        }
    }

    private async Task<bool> RegenerateProjectionETagAsync(
        AggregateIdentity identity,
        string projectionType,
        CancellationToken cancellationToken,
        bool requireReadBack = false) {
        cancellationToken.ThrowIfCancellationRequested();

        string actorId = $"{projectionType}:{identity.TenantId}";
        try {
            IETagActor eTagProxy = actorProxyFactory.CreateActorProxy<IETagActor>(
                new ActorId(actorId),
                ETagActor.ETagActorTypeName);

            cancellationToken.ThrowIfCancellationRequested();
            string regenerated = await eTagProxy.RegenerateAsync().ConfigureAwait(false);
            if (!requireReadBack) {
                return true;
            }

            string? readBack = await eTagProxy.GetCurrentETagAsync().ConfigureAwait(false);
            return !string.IsNullOrWhiteSpace(regenerated)
                && string.Equals(regenerated, readBack, StringComparison.Ordinal);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            Log.ProjectionETagRegenerationFailed(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                projectionType,
                ex.GetType().Name);
            return false;
        }
    }

    private async Task<ProjectionResponse?> ReadProjectResponseAsync(
        HttpResponseMessage httpResponse,
        string appId,
        AggregateIdentity identity,
        CancellationToken cancellationToken) {
        string contentType = GetContentTypeForLog(httpResponse.Content);
        string httpStatus = ((int)httpResponse.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!IsJsonContent(httpResponse.Content)) {
            Log.ProjectInvocationRejected(
                logger,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.ProjectUnsupportedContentType,
                httpStatus,
                contentType);
            return null;
        }

        string? charset = httpResponse.Content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charset)) {
            try {
                _ = Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException) {
                _ = ex;
                Log.ProjectInvocationRejected(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    appId,
                    ProjectionReasonCodes.ProjectInvalidCharset,
                    httpStatus,
                    contentType);
                return null;
            }
        }

        try {
            return await httpResponse.Content
                .ReadFromJsonAsync<ProjectionResponse>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (JsonException ex) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.ProjectMalformedJson,
                ex.GetType().Name,
                httpStatus,
                contentType);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            Log.ProjectInvocationException(
                logger,
                ex,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                appId,
                ProjectionReasonCodes.Unknown,
                ex.GetType().Name,
                httpStatus,
                contentType);
            return null;
        }
    }

    private static bool IsJsonContent(HttpContent content) {
        string? mediaType = content.Headers.ContentType?.MediaType;
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || (mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string GetContentTypeForLog(HttpContent? content) =>
        content?.Headers.ContentType?.ToString() ?? "none";

    private static string GetUpstreamReasonCode(HttpStatusCode statusCode) =>
        (int)statusCode is >= 400 and <= 499
            ? ProjectionReasonCodes.ProjectUpstream4xx
            : (int)statusCode is >= 500 and <= 599
                ? ProjectionReasonCodes.ProjectUpstream5xx
                : ProjectionReasonCodes.ProjectUnexpectedStatus;

    private static partial class Log {
        [LoggerMessage(
            EventId = 1110,
            Level = LogLevel.Debug,
            Message = "Projection update started: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionUpdateStarted")]
        public static partial void UpdateStarted(ILogger logger, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 11101,
            Level = LogLevel.Debug,
            Message = "Projection rebuild delivery started: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionRebuildDeliveryStarted")]
        public static partial void RebuildDeliveryStarted(ILogger logger, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1111,
            Level = LogLevel.Information,
            Message = "No domain service registered for projection update: TenantId={TenantId}, Domain={Domain}, Stage=NoDomainServiceRegistered")]
        public static partial void NoDomainServiceRegistered(ILogger logger, string tenantId, string domain);

        [LoggerMessage(
            EventId = 1112,
            Level = LogLevel.Debug,
            Message = "No events found for projection update: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=NoEventsFound")]
        public static partial void NoEventsFound(ILogger logger, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1113,
            Level = LogLevel.Debug,
            Message = "Domain service invocation succeeded for projection: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AppId={AppId}, Stage=DomainServiceInvocationSucceeded")]
        public static partial void DomainServiceInvocationSucceeded(ILogger logger, string tenantId, string domain, string aggregateId, string appId);

        [LoggerMessage(
            EventId = 1114,
            Level = LogLevel.Debug,
            Message = "Projection state updated: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, ActorId={ActorId}, Stage=ProjectionStateUpdated")]
        public static partial void ProjectionStateUpdated(ILogger logger, string tenantId, string domain, string aggregateId, string projectionType, string actorId);

        [LoggerMessage(
            EventId = 1151,
            Level = LogLevel.Warning,
            Message = "Projection ETag regeneration failed after state update: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, ExceptionType={ExceptionType}, Stage=ProjectionETagRegenerationFailed")]
        public static partial void ProjectionETagRegenerationFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string projectionType, string exceptionType);

        [LoggerMessage(
            EventId = 1115,
            Level = LogLevel.Warning,
            Message = "Projection update failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionUpdateFailed")]
        public static partial void ProjectionUpdateFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1116,
            Level = LogLevel.Warning,
            Message = "Invalid projection response: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, Stage=InvalidProjectionResponse")]
        public static partial void InvalidProjectionResponse(ILogger logger, string tenantId, string domain, string aggregateId, string reasonCode);

        [LoggerMessage(
            EventId = 1117,
            Level = LogLevel.Debug,
            Message = "Projection polling work registered: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RefreshIntervalMs={RefreshIntervalMs}, Stage=ProjectionPollingWorkRegistered")]
        public static partial void PollingWorkRegistered(ILogger logger, string tenantId, string domain, string aggregateId, int refreshIntervalMs);

        [LoggerMessage(
            EventId = 1121,
            Level = LogLevel.Warning,
            Message = "Projection polling work registration failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionPollingWorkRegistrationFailed")]
        public static partial void PollingWorkRegistrationFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string exceptionType);

        [LoggerMessage(
            EventId = 1118,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint read failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionCheckpointReadFailed")]
        public static partial void CheckpointReadFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string exceptionType);

        [LoggerMessage(
            EventId = 1119,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint save failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionCheckpointSaveFailed")]
        public static partial void CheckpointSaveFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string exceptionType);

        [LoggerMessage(
            EventId = 1120,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint save exhausted optimistic-concurrency attempts: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AttemptedSequence={AttemptedSequence}, Stage=ProjectionCheckpointSaveExhausted")]
        public static partial void CheckpointSaveExhausted(ILogger logger, string tenantId, string domain, string aggregateId, long attemptedSequence);

        [LoggerMessage(
            EventId = 1141,
            Level = LogLevel.Warning,
            Message = "Projection /project response rejected: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AppId={AppId}, ReasonCode={ReasonCode}, HttpStatus={HttpStatus}, ContentType={ContentType}, Stage=ProjectInvocationRejected")]
        public static partial void ProjectInvocationRejected(ILogger logger, string tenantId, string domain, string aggregateId, string appId, string reasonCode, string httpStatus, string contentType);

        [LoggerMessage(
            EventId = 1142,
            Level = LogLevel.Warning,
            Message = "Projection /project invocation failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, AppId={AppId}, ReasonCode={ReasonCode}, ExceptionType={ExceptionType}, HttpStatus={HttpStatus}, ContentType={ContentType}, Stage=ProjectInvocationFailed")]
        public static partial void ProjectInvocationException(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId, string appId, string reasonCode, string exceptionType, string httpStatus, string contentType);

        [LoggerMessage(
            EventId = 1143,
            Level = LogLevel.Warning,
            Message = "Projection checkpoint drift detected: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, LastDeliveredSequence={LastDeliveredSequence}, HighestEventSequence={HighestEventSequence}, Stage=ProjectionCheckpointDrift")]
        public static partial void CheckpointDriftDetected(ILogger logger, string tenantId, string domain, string aggregateId, string reasonCode, long lastDeliveredSequence, long highestEventSequence);

        [LoggerMessage(
            EventId = 1145,
            Level = LogLevel.Information,
            Message = "Projection delivery skipped because an operator rebuild is active: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, Stage=ProjectionPollerRebuildConflict")]
        public static partial void PollerRebuildConflict(ILogger logger, string tenantId, string domain, string aggregateId, string reasonCode);

        [LoggerMessage(
            EventId = 1146,
            Level = LogLevel.Information,
            Message = "Projection delivery deferred because an erase is in progress: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, ReasonCode={ReasonCode}, Stage=ProjectionDeliveryDeferredForErase")]
        public static partial void ProjectionDeliveryDeferredForErase(ILogger logger, string tenantId, string domain, string aggregateId, string projectionType, string reasonCode);

        [LoggerMessage(
            EventId = 1147,
            Level = LogLevel.Warning,
            Message = "Projection rebuild cancel-cleanup write rejected: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ReasonCode={ReasonCode}, Stage=ProjectionRebuildCancelCleanupRejected")]
        public static partial void RebuildCancelCleanupRejected(ILogger logger, string tenantId, string domain, string projectionName, string reasonCode);

        [LoggerMessage(
            EventId = 1148,
            Level = LogLevel.Error,
            Message = "Projection rebuild cancel-cleanup failed: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ExceptionType={ExceptionType}, Stage=ProjectionRebuildCancelCleanupFailed")]
        public static partial void RebuildCancelCleanupFailed(ILogger logger, Exception exception, string tenantId, string domain, string projectionName, string exceptionType);

        [LoggerMessage(
            EventId = 1149,
            Level = LogLevel.Error,
            Message = "Projection rebuild terminal Failed write rejected: TenantId={TenantId}, Domain={Domain}, ProjectionName={ProjectionName}, ReasonCode={ReasonCode}, ExceptionType={ExceptionType}, Stage=ProjectionRebuildTerminalFailWriteRejected")]
        public static partial void RebuildTerminalFailWriteRejected(ILogger logger, string tenantId, string domain, string projectionName, string reasonCode, string exceptionType);

        [LoggerMessage(
            EventId = 1150,
            Level = LogLevel.Warning,
            Message = "Unreadable protected event blocked projection delivery: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, SequenceNumber={SequenceNumber}, ReasonCode={ReasonCode}, Stage={Stage}")]
        public static partial void UnreadableProtectedEvent(ILogger logger, string tenantId, string domain, string aggregateId, long sequenceNumber, string reasonCode, string stage);

        [LoggerMessage(
            EventId = 4659,
            Level = LogLevel.Warning,
            Message = "Named projection dispatch failed; legacy projection delivery continues: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=NamedProjectionDispatchFailed")]
        public static partial void NamedProjectionDispatchFailed(
            ILogger logger,
            Exception exception,
            string tenantId,
            string domain,
            string aggregateId,
            string exceptionType);

        [LoggerMessage(
            EventId = 4660,
            Level = LogLevel.Information,
            Message = "Named projection rebuild outcome: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, Status={Status}, ReasonCode={ReasonCode}, Stage=NamedProjectionRebuildOutcome")]
        public static partial void NamedRebuildRouteOutcome(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string projectionType,
            ProjectionDispatchStatus status,
            string reasonCode);

        [LoggerMessage(
            EventId = 4658,
            Level = LogLevel.Warning,
            Message = "Projection activation completion failed and remains recoverable: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}, Stage=ProjectionActivationCompletionFailed")]
        public static partial void ProjectionActivationCompletionFailed(
            ILogger logger,
            Exception exception,
            string tenantId,
            string domain,
            string aggregateId,
            string exceptionType);
    }
}
