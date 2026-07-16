using System.Diagnostics;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Default fail-closed normal-delivery coordinator for named projection handlers.</summary>
internal sealed partial class NamedProjectionDispatchCoordinator(
    INamedProjectionRouteCatalog routeCatalog,
    IProjectionDeliveryCheckpointStore checkpointStore,
    IProjectionLifecycleGateway lifecycleGateway,
    IActorProxyFactory actorProxyFactory,
    DaprClient daprClient,
    IHttpClientFactory httpClientFactory,
    IOptions<ProjectionDispatchOptions> options,
    ILogger<NamedProjectionDispatchCoordinator> logger,
    IProjectionDeliveryRetryScheduler? retryScheduler = null,
    TimeProvider? timeProvider = null,
    INamedProjectionCatalogRefresher? catalogRefresher = null,
    IProjectionDeliveryIdempotencyCoordinator? idempotencyCoordinator = null) : INamedProjectionDispatchCoordinator {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<NamedProjectionRebuildResult> AcquireRebuildLifecyclesAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        string operationId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        NamedProjectionRouteCatalogEntry? catalogEntry = await ResolveCatalogEntryAsync(
                identity,
                registration,
                cancellationToken)
            .ConfigureAwait(false);
        if (catalogEntry is null) {
            return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []);
        }

        string[] requiredRoutes = [.. catalogEntry.ProjectionTypes.Order(StringComparer.Ordinal)];
        var acquired = new List<string>(requiredRoutes.Length);
        try {
            foreach (string projectionType in requiredRoutes) {
                if (!await lifecycleGateway
                    .BeginRebuildAsync(identity, projectionType, operationId, cancellationToken)
                    .ConfigureAwait(false)) {
                    ProjectionDispatchOutcome[] denied = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                        route,
                        ProjectionDispatchStatus.Retryable,
                        null,
                        ProjectionDispatchReasonCodes.DeliveryInProgress))];
                    return new NamedProjectionRebuildResult(Owned: true, Succeeded: false, denied, acquired);
                }

                acquired.Add(projectionType);
            }

            ProjectionDispatchOutcome[] outcomes = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                route,
                ProjectionDispatchStatus.Completed,
                null,
                null))];
            return new NamedProjectionRebuildResult(Owned: true, Succeeded: true, outcomes, acquired);
        }
        catch {
            await ReleaseRebuildLifecyclesAsync(identity, operationId, acquired).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<NamedProjectionRebuildResult> TryRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(projectionEvents);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        ProjectionDispatchOptions dispatchOptions = options.Value;
        dispatchOptions.Validate();
        string serviceVersion = string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;
        if (!routeCatalog.Current.TryGet(
            registration.AppId,
            serviceVersion,
            identity.Domain,
            out NamedProjectionRouteCatalogEntry? catalogEntry)
            || catalogEntry is null) {
            if (catalogRefresher is null
                || !await catalogRefresher.RefreshAsync(registration, cancellationToken).ConfigureAwait(false)
                || !routeCatalog.Current.TryGet(
                    registration.AppId,
                    serviceVersion,
                    identity.Domain,
                    out catalogEntry)
                || catalogEntry is null) {
                return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []);
            }
        }

        string[] requiredRoutes = [.. catalogEntry.ProjectionTypes.Order(StringComparer.Ordinal)];
        var lifecycleProjectionTypes = new List<string>(requiredRoutes.Length);
        try {
            foreach (string projectionType in requiredRoutes) {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await lifecycleGateway
                    .BeginRebuildAsync(identity, projectionType, operationId, cancellationToken)
                    .ConfigureAwait(false)) {
                    ProjectionDispatchOutcome[] denied = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                    route,
                    ProjectionDispatchStatus.Retryable,
                    null,
                    ProjectionDispatchReasonCodes.DeliveryInProgress))];
                    return new NamedProjectionRebuildResult(
                        Owned: true,
                        Succeeded: false,
                        denied,
                        lifecycleProjectionTypes);
                }

                lifecycleProjectionTypes.Add(projectionType);
            }

            var request = new ProjectionDispatchRequest(
                new ProjectionRequest(
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionEvents),
                requiredRoutes,
                operationId,
                catalogEntry.CatalogFingerprint);
            ProjectionDispatchResponse? response = await InvokeAsync(
                    registration.AppId,
                    "project/rebuild/stage/v1",
                    request,
                    dispatchOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            if (response is null) {
                ProjectionDispatchOutcome[] indeterminate = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                route,
                ProjectionDispatchStatus.Indeterminate,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure))];
                return new NamedProjectionRebuildResult(
                    Owned: true,
                    Succeeded: false,
                    indeterminate,
                    lifecycleProjectionTypes);
            }

            Dictionary<string, ProjectionDispatchOutcome> validOutcomes = ValidateOutcomes(
                response,
                requiredRoutes,
                dispatchOptions);
            ProjectionDispatchOutcome[] orderedOutcomes = [.. requiredRoutes.Select(route =>
            validOutcomes.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                ? outcome
                : new ProjectionDispatchOutcome(
                    route,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.MalformedOutcome))];
            bool succeeded = orderedOutcomes.Length == requiredRoutes.Length
                && orderedOutcomes.All(static outcome =>
                    outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted);
            return new NamedProjectionRebuildResult(
                Owned: true,
                succeeded,
                orderedOutcomes,
                lifecycleProjectionTypes);
        }
        catch {
            foreach (string projectionType in lifecycleProjectionTypes) {
                try {
                    bool completed = await lifecycleGateway
                        .CompleteRebuildAsync(identity, projectionType, operationId, CancellationToken.None)
                        .ConfigureAwait(false);
                    if (!completed) {
                        Log.LifecycleReleaseFailed(
                            logger,
                            identity.TenantId,
                            identity.Domain,
                            identity.AggregateId,
                            projectionType,
                            operationId,
                            "completion-rejected");
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    Log.LifecycleReleaseFailed(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        operationId,
                        exception.GetType().Name);
                }
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryDispatchAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        EventEnvelope[] events,
        ProjectionEventDto[] projectionEvents,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(projectionEvents);
        if (events.Length == 0) {
            throw new ArgumentException("Named projection delivery requires a non-empty persisted event history.", nameof(events));
        }

        ProjectionDispatchOptions dispatchOptions = options.Value;
        dispatchOptions.Validate();
        string serviceVersion = string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;
        if (!routeCatalog.Current.TryGet(
            registration.AppId,
            serviceVersion,
            identity.Domain,
            out NamedProjectionRouteCatalogEntry? catalogEntry)
            || catalogEntry is null) {
            if (catalogRefresher is null
                || !await catalogRefresher.RefreshAsync(registration, cancellationToken).ConfigureAwait(false)
                || !routeCatalog.Current.TryGet(
                    registration.AppId,
                    serviceVersion,
                    identity.Domain,
                    out catalogEntry)
                || catalogEntry is null) {
                return false;
            }
        }

        EventEnvelope head = events.OrderBy(static item => item.SequenceNumber).Last();
        long highestSequence = head.SequenceNumber;
        if (string.IsNullOrWhiteSpace(head.MessageId)) {
            // The stable dispatch identity derives from the head event's message id. Without it we
            // cannot form a valid work item; this domain is v2-owned, so defer rather than fall
            // through to v1. A later delivery trigger with a valid head reconciles this aggregate.
            Log.DispatchDeferred(
                logger,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                ProjectionDispatchReasonCodes.MalformedOutcome);
            return true;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var proposedWork = new ProjectionDeliveryRetryWorkItem(
            ProjectionDeliveryRetryWorkItem.CreateWorkId(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                highestSequence),
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            registration.AppId,
            serviceVersion,
            highestSequence,
            head.MessageId,
            [.. catalogEntry.ProjectionTypes.Order(StringComparer.Ordinal)],
            [],
            head.MessageId,
            catalogEntry.CatalogFingerprint,
            0,
            now + dispatchOptions.RetryWorkerInterval,
            null);
        ProjectionDeliveryRetryWorkItem scheduledWork;
        if (retryScheduler is null) {
            scheduledWork = proposedWork;
        }
        else {
            try {
                scheduledWork = await retryScheduler.ScheduleAsync(proposedWork, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception exception) {
                Log.DispatchDeferred(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    ProjectionDispatchReasonCodes.PartialRetry);
                throw new InvalidOperationException(
                    "Named projection retry activation could not be persisted.",
                    exception);
            }
        }

        if (retryScheduler is not null) {
            ProjectionDeliveryRetryWorkItem? claimedWork = await retryScheduler
                .TryAcquireAsync(
                    scheduledWork,
                    Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                    now,
                    dispatchOptions.RetryLeaseDuration,
                    cancellationToken)
                .ConfigureAwait(false);
            if (claimedWork is null) {
                return true;
            }

            scheduledWork = claimedWork;
        }

        if (!string.Equals(scheduledWork.HeadMessageId, head.MessageId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.DispatchId, head.MessageId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.CatalogFingerprint, catalogEntry.CatalogFingerprint, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.AppId, registration.AppId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.ServiceVersion, serviceVersion, StringComparison.Ordinal)) {
            await DeferAsync(
                    scheduledWork,
                    ProjectionDispatchReasonCodes.PartialRetry,
                    dispatchOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        HashSet<string> pendingRoutes = new(scheduledWork.PendingRoutes, StringComparer.Ordinal);
        List<string> admittedRoutes = [];
        var settledRoutes = new HashSet<string>(StringComparer.Ordinal);
        var completedRoutes = new HashSet<string>(StringComparer.Ordinal);
        var terminalRoutes = new HashSet<string>(scheduledWork.TerminalRoutes, StringComparer.Ordinal);
        var reservations = new Dictionary<string, ProjectionDeliveryReservation>(StringComparer.Ordinal);
        var lifecycleLeases = new HashSet<string>(StringComparer.Ordinal);
        try {
            foreach (string projectionType in catalogEntry.ProjectionTypes
                .Where(pendingRoutes.Contains)
                .Order(StringComparer.Ordinal)) {
                cancellationToken.ThrowIfCancellationRequested();
                if (idempotencyCoordinator is null) {
                    long deliveredSequence = await checkpointStore
                        .ReadDeliveredSequenceAsync(identity, projectionType, cancellationToken)
                        .ConfigureAwait(false);
                    if (deliveredSequence > highestSequence) {
                        Log.RouteNotAdmitted(
                            logger,
                            identity.TenantId,
                            identity.Domain,
                            identity.AggregateId,
                            projectionType,
                            ProjectionReasonCodes.CheckpointDrift);

                        // A checkpoint already ahead of this head will never need this delivery. Treat the
                        // route as terminally settled for this work item (mirroring the v1 terminal no-op)
                        // so the item can converge and be deleted rather than deferring this route forever.
                        _ = settledRoutes.Add(projectionType);
                        continue;
                    }
                }

                if (!await lifecycleGateway
                    .BeginDeliveryWriteAsync(
                        identity,
                        projectionType,
                        scheduledWork.DispatchId,
                        cancellationToken)
                    .ConfigureAwait(false)) {
                    Log.RouteNotAdmitted(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        ProjectionReasonCodes.DeliveryDeferredForErase);

                    // Lifecycle denial (e.g. erase in progress) may lift later; keep the route pending.
                    continue;
                }

                _ = lifecycleLeases.Add(projectionType);

                if (idempotencyCoordinator is not null) {
                    ProjectionDeliveryAdmissionResult admission = await idempotencyCoordinator
                        .TryAdmitAsync(
                            identity,
                            projectionType,
                            projectionEvents,
                            reclaimSafe: true,
                            cancellationToken,
                            scheduledWork.ReservationFencingTokens.TryGetValue(projectionType, out long token)
                                ? token
                                : null)
                        .ConfigureAwait(false);
                    ProjectionDeliveryDiagnostics.RecordAdmission(projectionType, admission);
                    if (admission.Disposition == ProjectionDeliveryAdmissionDisposition.Dispatch) {
                        admittedRoutes.Add(projectionType);
                        reservations.Add(projectionType, admission.Reservation!);
                        continue;
                    }

                    if (!await lifecycleGateway
                        .CompleteDeliveryWriteAsync(
                            identity,
                            projectionType,
                            scheduledWork.DispatchId,
                            CancellationToken.None)
                        .ConfigureAwait(false)) {
                        throw new InvalidOperationException("Named projection delivery lifecycle lease completion was rejected.");
                    }

                    _ = lifecycleLeases.Remove(projectionType);

                    Log.RouteNotAdmitted(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        admission.ReasonCode ?? ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
                    if (admission.Disposition == ProjectionDeliveryAdmissionDisposition.AlreadyCompleted) {
                        _ = completedRoutes.Add(projectionType);
                    }
                    else if (admission.Disposition == ProjectionDeliveryAdmissionDisposition.Failed) {
                        _ = terminalRoutes.Add(projectionType);
                    }

                    continue;
                }

                admittedRoutes.Add(projectionType);
            }

            if (admittedRoutes.Count == 0) {
                // Nothing to dispatch this round. Prune settled (drift-ahead) routes: if only lifecycle-
                // deferred routes remain this re-schedules with backoff, otherwise it deletes the item.
                await ReconcileRetryLedgerAsync(
                        scheduledWork,
                        completedRoutes,
                        terminalRoutes,
                        settledRoutes,
                        reservations,
                        dispatchOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var projectionRequest = new ProjectionRequest(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                projectionEvents);
            var dispatchRequest = new ProjectionDispatchRequest(
                projectionRequest,
                admittedRoutes,
                scheduledWork.DispatchId,
                scheduledWork.CatalogFingerprint);

            ProjectionDispatchResponse? dispatchResponse = await InvokeAsync(
                registration.AppId,
                "project/v2",
                dispatchRequest,
                dispatchOptions,
                cancellationToken).ConfigureAwait(false);
            if (dispatchResponse is null) {
                await DeferAsync(
                        scheduledWork,
                        ProjectionDispatchReasonCodes.PartialRetry,
                        dispatchOptions,
                        cancellationToken,
                        reservations)
                    .ConfigureAwait(false);
                return true;
            }

            Dictionary<string, ProjectionDispatchOutcome> validOutcomes = ValidateOutcomes(
                dispatchResponse,
                admittedRoutes,
                dispatchOptions);
            foreach (string projectionType in admittedRoutes) {
                if (idempotencyCoordinator is null) {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                long started = Stopwatch.GetTimestamp();
                if (!validOutcomes.TryGetValue(projectionType, out ProjectionDispatchOutcome? outcome)) {
                    Log.RouteOutcome(
                        logger,
                        identity.TenantId,
                        identity.Domain,
                        identity.AggregateId,
                        projectionType,
                        ProjectionDispatchStatus.Indeterminate,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    continue;
                }

                if (outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted) {
                    try {
                        using CancellationTokenSource? finalization = idempotencyCoordinator is null
                            ? null
                            : new CancellationTokenSource(TimeSpan.FromSeconds(30), _timeProvider);
                        CancellationToken finalizationToken = finalization?.Token ?? cancellationToken;
                        if (outcome.State is not null) {
                            await WriteLegacyActorStateAsync(identity, projectionType, outcome.State.Value, finalizationToken)
                                .ConfigureAwait(false);
                        }

                        if (idempotencyCoordinator is not null) {
                            ProjectionDeliveryCompletion completion = await idempotencyCoordinator
                                .CompleteAsync(
                                    identity,
                                    projectionType,
                                    projectionEvents,
                                    reservations[projectionType],
                                    finalizationToken)
                                .ConfigureAwait(false);
                            ProjectionDeliveryDiagnostics.RecordCompletion(projectionType, completion);
                            if (completion is ProjectionDeliveryCompletion.Completed or ProjectionDeliveryCompletion.AlreadyCompleted) {
                                _ = completedRoutes.Add(projectionType);
                            }
                        }
                        else if (await checkpointStore
                            .SaveDeliveredSequenceAsync(identity, projectionType, highestSequence, finalizationToken)
                            .ConfigureAwait(false)) {
                            _ = completedRoutes.Add(projectionType);
                        }
                    }
                    catch (OperationCanceledException) {
                        throw;
                    }
                    catch (Exception) {
                        // Durable handler work may already exist. Leave this projection pending for stable-id retry.
                    }
                }
                else if (outcome.Status == ProjectionDispatchStatus.Failed) {
                    if (idempotencyCoordinator is not null) {
                        using var release = new CancellationTokenSource(TimeSpan.FromSeconds(30), _timeProvider);
                        _ = await idempotencyCoordinator
                            .TryReleaseAsync(identity, projectionType, reservations[projectionType], release.Token)
                            .ConfigureAwait(false);
                    }

                    _ = terminalRoutes.Add(projectionType);
                }

                Log.RouteOutcome(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionType,
                    outcome.Status,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }

            // The durable work item is the crash-recovery carrier for the stable dispatch id. Keep it
            // until every lifecycle lease has been released; deleting it first can orphan Delivering
            // forever if the process stops between ledger reconciliation and the finally block.
            await CompleteDeliveryLifecycleLeasesAsync(
                    identity,
                    scheduledWork.DispatchId,
                    lifecycleLeases)
                .ConfigureAwait(false);

            await ReconcileRetryLedgerAsync(
                    scheduledWork,
                    completedRoutes,
                terminalRoutes,
                settledRoutes,
                reservations,
                dispatchOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        finally {
            if (lifecycleLeases.Count > 0) {
                await CompleteDeliveryLifecycleLeasesAsync(
                        identity,
                        scheduledWork.DispatchId,
                        lifecycleLeases)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public Task<NamedProjectionRebuildResult> CommitRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken)
        => InvokePreparedRebuildAsync(
            identity,
            registration,
            projectionEvents,
            operationId,
            "project/rebuild/commit/v1",
            cancellationToken);

    /// <inheritdoc/>
    public Task<NamedProjectionRebuildResult> AbortRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken)
        => InvokePreparedRebuildAsync(
            identity,
            registration,
            projectionEvents,
            operationId,
            "project/rebuild/abort/v1",
            cancellationToken);

    /// <inheritdoc/>
    public Task<NamedProjectionRebuildResult> VerifyRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        CancellationToken cancellationToken)
        => InvokePreparedRebuildAsync(
            identity,
            registration,
            projectionEvents,
            operationId,
            "project/rebuild/verify/v1",
            cancellationToken);

    private async Task CompleteDeliveryLifecycleLeasesAsync(
        AggregateIdentity identity,
        string dispatchId,
        ISet<string> lifecycleLeases) {
        Exception? lifecycleCompletionFailure = null;
        foreach (string projectionType in lifecycleLeases.ToArray()) {
            try {
                if (!await lifecycleGateway
                    .CompleteDeliveryWriteAsync(
                        identity,
                        projectionType,
                        dispatchId,
                        CancellationToken.None)
                    .ConfigureAwait(false)) {
                    lifecycleCompletionFailure ??= new InvalidOperationException(
                        "Named projection delivery lifecycle lease completion was rejected.");
                    continue;
                }

                _ = lifecycleLeases.Remove(projectionType);
            }
            catch (Exception exception) {
                lifecycleCompletionFailure ??= exception;
            }
        }

        if (lifecycleCompletionFailure is not null) {
            throw new InvalidOperationException(
                "One or more named projection delivery lifecycle leases could not be completed.",
                lifecycleCompletionFailure);
        }
    }

    /// <summary>
    /// Reconciles the durable retry work item after one dispatch round: prunes completed and
    /// drift-settled routes, retains lifecycle-deferred and terminal routes, and deletes the item
    /// only when every route has converged. A ledger fault is logged and swallowed so it never
    /// surfaces as a dropped delivery; idempotent retry reconciles the stale item on the next tick.
    /// </summary>
    private async Task ReconcileRetryLedgerAsync(
        ProjectionDeliveryRetryWorkItem scheduledWork,
        IReadOnlySet<string> completedRoutes,
        IReadOnlySet<string> terminalRoutes,
        IReadOnlySet<string> settledRoutes,
        IReadOnlyDictionary<string, ProjectionDeliveryReservation> reservations,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken) {
        if (retryScheduler is null) {
            return;
        }

        try {
            string[] remainingRoutes = [.. scheduledWork.PendingRoutes
                .Where(route => !completedRoutes.Contains(route)
                    && !terminalRoutes.Contains(route)
                    && !settledRoutes.Contains(route))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            if (remainingRoutes.Length == 0 && terminalRoutes.Count == 0) {
                _ = await retryScheduler.TryDeleteAsync(scheduledWork, cancellationToken).ConfigureAwait(false);
                return;
            }

            int attempt = Math.Min(scheduledWork.Attempt + 1, dispatchOptions.MaxRetryAttempts);
            var updated = scheduledWork with {
                PendingRoutes = remainingRoutes,
                TerminalRoutes = [.. terminalRoutes.Order(StringComparer.Ordinal)],
                ReservationFencingTokens = reservations
                    .Where(pair => remainingRoutes.Contains(pair.Key, StringComparer.Ordinal))
                    .ToDictionary(static pair => pair.Key, static pair => pair.Value.FencingToken, StringComparer.Ordinal),
                Attempt = attempt,
                NextDueUtc = _timeProvider.GetUtcNow() + GetRetryDelay(attempt, dispatchOptions),
                LastReasonCode = terminalRoutes.Count > 0
                    ? ProjectionDispatchReasonCodes.HandlerFailure
                    : ProjectionDispatchReasonCodes.PartialRetry,
            };
            _ = await retryScheduler.TryUpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            Log.DispatchDeferred(
                logger,
                scheduledWork.TenantId,
                scheduledWork.Domain,
                scheduledWork.AggregateId,
                ProjectionDispatchReasonCodes.PartialRetry);
        }
    }

    private async Task DeferAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        string reasonCode,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, ProjectionDeliveryReservation>? reservations = null) {
        if (retryScheduler is null) {
            return;
        }

        int attempt = Math.Min(workItem.Attempt + 1, dispatchOptions.MaxRetryAttempts);
        try {
            _ = await retryScheduler.TryUpdateAsync(
                workItem with {
                    ReservationFencingTokens = reservations is null
                        ? workItem.ReservationFencingTokens
                        : reservations.ToDictionary(
                            static pair => pair.Key,
                            static pair => pair.Value.FencingToken,
                            StringComparer.Ordinal),
                    Attempt = attempt,
                    NextDueUtc = _timeProvider.GetUtcNow() + GetRetryDelay(attempt, dispatchOptions),
                    LastReasonCode = reasonCode,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            // A ledger fault while recording backoff must not surface as a dropped delivery; the
            // work item stays at its prior due time and is reconciled on a later trigger/tick.
            Log.DispatchDeferred(
                logger,
                workItem.TenantId,
                workItem.Domain,
                workItem.AggregateId,
                reasonCode);
        }
    }

    private static TimeSpan GetRetryDelay(int attempt, ProjectionDispatchOptions dispatchOptions) {
        double multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        double ticks = Math.Min(
            dispatchOptions.RetryMaxDelay.Ticks,
            dispatchOptions.RetryBaseDelay.Ticks * multiplier);
        return TimeSpan.FromTicks((long)ticks);
    }

    private async Task<NamedProjectionRebuildResult> InvokePreparedRebuildAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        ProjectionEventDto[] projectionEvents,
        string operationId,
        string methodName,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(projectionEvents);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ProjectionDispatchOptions dispatchOptions = options.Value;
        dispatchOptions.Validate();
        NamedProjectionRouteCatalogEntry? catalogEntry = await ResolveCatalogEntryAsync(
                identity,
                registration,
                cancellationToken)
            .ConfigureAwait(false);
        if (catalogEntry is null) {
            return new NamedProjectionRebuildResult(Owned: false, Succeeded: true, [], []);
        }

        string[] requiredRoutes = [.. catalogEntry.ProjectionTypes.Order(StringComparer.Ordinal)];
        var request = new ProjectionDispatchRequest(
            new ProjectionRequest(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                projectionEvents),
            requiredRoutes,
            operationId,
            catalogEntry.CatalogFingerprint);
        ProjectionDispatchResponse? response = await InvokeAsync(
                registration.AppId,
                methodName,
                request,
                dispatchOptions,
                cancellationToken)
            .ConfigureAwait(false);
        if (response is null) {
            ProjectionDispatchOutcome[] indeterminate = [.. requiredRoutes.Select(route => new ProjectionDispatchOutcome(
                route,
                ProjectionDispatchStatus.Indeterminate,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure))];
            return new NamedProjectionRebuildResult(Owned: true, Succeeded: false, indeterminate, requiredRoutes);
        }

        Dictionary<string, ProjectionDispatchOutcome> validOutcomes = ValidateOutcomes(
            response,
            requiredRoutes,
            dispatchOptions);
        ProjectionDispatchOutcome[] orderedOutcomes = [.. requiredRoutes.Select(route =>
            validOutcomes.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                ? outcome
                : new ProjectionDispatchOutcome(
                    route,
                    ProjectionDispatchStatus.Indeterminate,
                    null,
                    ProjectionDispatchReasonCodes.MalformedOutcome))];
        bool succeeded = orderedOutcomes.All(static outcome =>
            outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted);
        return new NamedProjectionRebuildResult(Owned: true, succeeded, orderedOutcomes, requiredRoutes);
    }

    private async Task<NamedProjectionRouteCatalogEntry?> ResolveCatalogEntryAsync(
        AggregateIdentity identity,
        DomainServiceRegistration registration,
        CancellationToken cancellationToken) {
        string serviceVersion = string.IsNullOrWhiteSpace(registration.Version) ? "v1" : registration.Version;
        if (routeCatalog.Current.TryGet(
            registration.AppId,
            serviceVersion,
            identity.Domain,
            out NamedProjectionRouteCatalogEntry? catalogEntry)
            && catalogEntry is not null) {
            return catalogEntry;
        }

        return catalogRefresher is not null
            && await catalogRefresher.RefreshAsync(registration, cancellationToken).ConfigureAwait(false)
            && routeCatalog.Current.TryGet(
                registration.AppId,
                serviceVersion,
                identity.Domain,
                out catalogEntry)
            ? catalogEntry
            : null;
    }

    private async Task ReleaseRebuildLifecyclesAsync(
        AggregateIdentity identity,
        string operationId,
        IEnumerable<string> projectionTypes) {
        foreach (string projectionType in projectionTypes) {
            try {
                _ = await lifecycleGateway
                    .CompleteRebuildAsync(identity, projectionType, operationId, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                Log.LifecycleReleaseFailed(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionType,
                    operationId,
                    exception.GetType().Name);
            }
        }
    }

    private async Task<ProjectionDispatchResponse?> InvokeAsync(
        string appId,
        string methodName,
        ProjectionDispatchRequest request,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken) {
        try {
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(appId, methodName, request);
            HttpClient client = httpClientFactory.CreateClient();
            using HttpResponseMessage response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode
                || response.Content.Headers.ContentType?.MediaType is not string mediaType
                || (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                    && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))) {
                return null;
            }

            byte[] payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (payload.Length == 0 || payload.Length > dispatchOptions.MaxOutcomeEnvelopeBytes) {
                return null;
            }

            return JsonSerializer.Deserialize<ProjectionDispatchResponse>(payload, SerializerOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception) {
            return null;
        }
    }

    private static Dictionary<string, ProjectionDispatchOutcome> ValidateOutcomes(
        ProjectionDispatchResponse response,
        IReadOnlyList<string> admittedRoutes,
        ProjectionDispatchOptions dispatchOptions) {
        var valid = new Dictionary<string, ProjectionDispatchOutcome>(StringComparer.Ordinal);
        if (response.Version != ProjectionDispatchProtocol.Version
            || response.Outcomes is null
            || response.Outcomes.Count > dispatchOptions.MaxOutcomes) {
            return valid;
        }

        HashSet<string> admitted = new(admittedRoutes, StringComparer.Ordinal);
        foreach (IGrouping<string, ProjectionDispatchOutcome> group in response.Outcomes
            .Where(outcome => outcome is not null && admitted.Contains(outcome.ProjectionType))
            .GroupBy(static outcome => outcome.ProjectionType, StringComparer.Ordinal)) {
            ProjectionDispatchOutcome[] outcomes = [.. group];
            if (outcomes.Length != 1 || !IsValidOutcome(outcomes[0], dispatchOptions)) {
                continue;
            }

            valid[group.Key] = outcomes[0];
        }

        return valid;
    }

    private static bool IsValidOutcome(
        ProjectionDispatchOutcome outcome,
        ProjectionDispatchOptions dispatchOptions)
        => Enum.IsDefined(outcome.Status)
            && (outcome.State is null
                || outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted)
            && (outcome.ReasonCode is null
                || (outcome.ReasonCode.Length <= dispatchOptions.MaxReasonCodeBytes
                    && outcome.ReasonCode.All(static character => character <= 0x7f)));

    private async Task WriteLegacyActorStateAsync(
        AggregateIdentity identity,
        string projectionType,
        JsonElement state,
        CancellationToken cancellationToken) {
        if (state.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
            throw new InvalidOperationException("A state-bearing projection outcome must contain JSON state.");
        }

        string projectionActorId = QueryActorIdHelper.DeriveActorId(
            projectionType,
            identity.TenantId,
            identity.AggregateId,
            []);
        IProjectionWriteActor writeProxy = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(
            new ActorId(projectionActorId),
            QueryRouter.ProjectionActorTypeName);
        cancellationToken.ThrowIfCancellationRequested();
        await writeProxy
            .UpdateProjectionAsync(ProjectionState.FromJsonElement(projectionType, identity.TenantId, state))
            .ConfigureAwait(false);

        IETagActor eTagProxy = actorProxyFactory.CreateActorProxy<IETagActor>(
            new ActorId($"{projectionType}:{identity.TenantId}"),
            ETagActor.ETagActorTypeName);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await eTagProxy.RegenerateAsync().ConfigureAwait(false);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 4650,
            Level = LogLevel.Warning,
            Message = "Named projection route not admitted: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, ReasonCode={ReasonCode}, Stage=NamedProjectionAdmission")]
        public static partial void RouteNotAdmitted(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string projectionType,
            string reasonCode);

        [LoggerMessage(
            EventId = 4651,
            Level = LogLevel.Information,
            Message = "Named projection outcome reconciled: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, Status={Status}, DurationMs={DurationMs}, Stage=NamedProjectionOutcome")]
        public static partial void RouteOutcome(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string projectionType,
            ProjectionDispatchStatus status,
            double durationMs);

        [LoggerMessage(
            EventId = 4652,
            Level = LogLevel.Warning,
            Message = "Named projection dispatch deferred: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ReasonCode={ReasonCode}, Stage=NamedProjectionDispatchDeferred")]
        public static partial void DispatchDeferred(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string reasonCode);

        [LoggerMessage(
            EventId = 4653,
            Level = LogLevel.Error,
            Message = "Named projection lifecycle release failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ProjectionType={ProjectionType}, OperationId={OperationId}, ReasonCode={ReasonCode}, Stage=NamedProjectionLifecycleRelease")]
        public static partial void LifecycleReleaseFailed(
            ILogger logger,
            string tenantId,
            string domain,
            string aggregateId,
            string projectionType,
            string operationId,
            string reasonCode);
    }
}
