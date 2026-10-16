using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Hosted worker that reloads bounded aggregate history for durable immediate-mode retry work.</summary>
internal sealed partial class ProjectionDeliveryRetryWorker(
    IProjectionDeliveryRetryScheduler scheduler,
    INamedProjectionDispatchCoordinator coordinator,
    IActorProxyFactory actorProxyFactory,
    IEventPayloadProtectionService payloadProtectionService,
    IOptions<EventStoreActorOptions> actorOptions,
    IOptions<ProjectionDispatchOptions> dispatchOptions,
    IProjectionRebuildCheckpointStore rebuildCheckpointStore,
    TimeProvider timeProvider,
    ILogger<ProjectionDeliveryRetryWorker> logger,
    IProjectionSourceGuardDelivery? sourceGuardDelivery = null) : BackgroundService {
    private const int ReadPageSize = 256;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(dispatchOptions.Value.RetryWorkerInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) {
            try {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                throw;
            }
            catch (Exception) {
                // A transient state-store/DAPR fault (including ledger optimistic-concurrency
                // exhaustion) must never fault this BackgroundService: the .NET default
                // BackgroundServiceExceptionBehavior.StopHost would otherwise terminate the host.
                // Log and continue; due work is retried on the next tick.
                Log.RetryActivationFailed(logger, ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
            }
        }
    }

    /// <summary>Runs one bounded retry activation.</summary>
    /// <param name="cancellationToken">Propagates worker shutdown.</param>
    internal async Task RunOnceAsync(CancellationToken cancellationToken) {
        ProjectionDispatchOptions options = dispatchOptions.Value;
        options.Validate();
        IReadOnlyList<ProjectionDeliveryRetryWorkItem> due = await scheduler
            .GetDueAsync(timeProvider.GetUtcNow(), options.RetryScanBatchSize, cancellationToken)
            .ConfigureAwait(false);
        foreach (ProjectionDeliveryRetryWorkItem workItem in due) {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessAsync(workItem, options, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken) {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.RetryActivations);
        var identity = new AggregateIdentity(workItem.TenantId, workItem.Domain, workItem.AggregateId);
        if (sourceGuardDelivery is not null)
        {
            CancellationTokenSource? cleanupBudget = null;
            ProjectionDeliveryRetryWorkItem? cleanup = null;
            try
            {
                ProjectionSourceGuardDeliveryResult guarded = await sourceGuardDelivery.TryDeliverAsync(identity, workItem,
                    ReadPageSize, cancellationToken).ConfigureAwait(false)
                    ?? await sourceGuardDelivery.TryDeliverAsync(identity, cancellationToken).ConfigureAwait(false);
                if (guarded.Owned)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    cleanupBudget = guarded.CreateCleanupBudget(timeProvider);
                    // The current probe itself performs no cleanup. This is separately scheduled
                    // reconciliation of the exact original work under its durable revision/lease.
                    string[] provenPending = [.. workItem.PendingRoutes.Where(guarded.ProvenFreshnessVersions.ContainsKey)];
                    bool allCompleted = guarded.AlreadyCurrent && workItem.TerminalRoutes.Count == 0;
                    if ((allCompleted || provenPending.Length > 0) && guarded.ObservedHeadSequence == workItem.HeadSequence
                        && guarded.ObservedHeadMessageId == workItem.HeadMessageId
                        && guarded.CatalogFingerprint == workItem.CatalogFingerprint
                        && (allCompleted
                            ? await sourceGuardDelivery.ReclaimCompletedReplayAsync(identity, cleanupBudget.Token).ConfigureAwait(false)
                            : await sourceGuardDelivery.ReclaimCompletedReplayAsync(identity, workItem, provenPending, cleanupBudget.Token).ConfigureAwait(false)))
                    {
                        cleanup = await scheduler.TryAcquireAsync(workItem,
                            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                            timeProvider.GetUtcNow(), options.RetryLeaseDuration, cleanupBudget.Token).ConfigureAwait(false);
                        if (cleanup is not null)
                        {
                            string[] remaining = allCompleted ? [] : [.. cleanup.PendingRoutes.Except(provenPending, StringComparer.Ordinal)];
                            if (remaining.Length > 0 || cleanup.TerminalRoutes.Count > 0)
                                await DeferClaimedAsync(cleanup with { PendingRoutes = remaining,
                                    ReservationFencingTokens = cleanup.ReservationFencingTokens.Where(pair => remaining.Contains(pair.Key, StringComparer.Ordinal))
                                        .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal) },
                                    options, guarded.ReasonCode ?? "delivery_reconciliation_required", cleanupBudget.Token).ConfigureAwait(false);
                            else if (!await scheduler.TryDeleteAsync(cleanup, cleanupBudget.Token).ConfigureAwait(false))
                                await DeferClaimedAsync(cleanup, options, "delivery_reconciliation_required", cleanupBudget.Token).ConfigureAwait(false);
                        }
                        return;
                    }
                    // The sampled revision fences this defer against work already updated by
                    // tail/finalization recovery. A stale revision cannot double-defer it.
                    await DeferAsync(workItem, options, cleanupBudget.Token,
                        guarded.ReasonCode ?? "delivery_reconciliation_required").ConfigureAwait(false);
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch
            {
                cleanupBudget ??= new(TimeSpan.FromSeconds(30), timeProvider);
                try
                {
                    if (cleanup is not null)
                        await DeferClaimedAsync(cleanup, options, "delivery_state_unavailable", cleanupBudget.Token).ConfigureAwait(false);
                    else
                        await DeferAsync(workItem, options, cleanupBudget.Token, "delivery_state_unavailable").ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { /* Pending durable work survives an unavailable or exhausted cleanup lane. */ }
                return;
            }
            finally { cleanupBudget?.Dispose(); }
        }
        using IDisposable projectionLock = await ProjectionUpdateOrchestrator.ProjectionLocks
            .AcquireAsync(identity.ActorId, cancellationToken)
            .ConfigureAwait(false);
        try {
            if (await rebuildCheckpointStore
                .HasActiveOperatorRebuildForDomainAsync(
                    identity.TenantId,
                    identity.Domain,
                    cancellationToken)
                .ConfigureAwait(false)) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (workItem.MappedOnlyStreamed) {
                var mappedRegistration = new DomainServiceRegistration(
                    workItem.AppId, "project/v2", workItem.TenantId, workItem.Domain,
                    workItem.ServiceVersion);
                bool mappedHandled = await coordinator.TryDispatchMappedOnlyAsync(identity,
                    mappedRegistration, workItem.HeadSequence, workItem.HeadMessageId,
                    cancellationToken).ConfigureAwait(false);
                if (!mappedHandled)
                    await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (workItem.ReplayRecoveryBindings is { Count: > 0 })
            {
                // This explicitly enrolled full-replay lane admits both byte ceilings while
                // decoding, and never retains another complete raw envelope prefix.
                ProjectionEventDto[] prefix = await LoadRecoveryPrefixAsync(identity, workItem, cancellationToken).ConfigureAwait(false);
                bool boundHandled = await coordinator.TryDispatchHeadAsync(identity,
                    new(workItem.AppId, "project/v2", identity.TenantId, identity.Domain, workItem.ServiceVersion),
                    workItem.HeadSequence, workItem.HeadMessageId, prefix, cancellationToken).ConfigureAwait(false);
                if (!boundHandled) await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            EventEnvelope[] events = await LoadHistoryAsync(identity, workItem, cancellationToken).ConfigureAwait(false);
            if (events.Length == 0
                || events[^1].SequenceNumber != workItem.HeadSequence
                || !string.Equals(events[^1].MessageId, workItem.HeadMessageId, StringComparison.Ordinal)) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            ProjectionEventReadabilityResult readability = await ProjectionEventWireBuilder
                .BuildAsync(payloadProtectionService, identity, events, cancellationToken)
                .ConfigureAwait(false);
            if (readability.Events is null) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }

            var registration = new DomainServiceRegistration(
                workItem.AppId,
                "project/v2",
                workItem.TenantId,
                workItem.Domain,
                workItem.ServiceVersion);
            bool handled = await coordinator
                .TryDispatchAsync(identity, registration, events, readability.Events, cancellationToken)
                .ConfigureAwait(false);
            if (!handled) {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        catch (ProjectionDeliveryHistoryValidationException exception) when (workItem.ReplayRecoveryBindings is { Count: > 0 })
        {
            string reason = exception.Message is "delivery_owner_limit_exceeded" or "delivery_page_limit_exceeded"
                or "delivery_byte_limit_exceeded" or "delivery_identity_conflict" or "delivery_gap" or "history_unavailable"
                or "source_conflict" ? exception.Message : ProjectionDispatchReasonCodes.DeliveryStateUnavailable;
            Log.RetryFailed(logger, reason);
            await DeferAsync(workItem, options, cancellationToken, reason).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            Log.RetryFailed(logger, ProjectionDispatchReasonCodes.PartialRetry);
            try {
                await DeferAsync(workItem, options, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
            }
        }
    }

    private async Task<EventEnvelope[]> LoadHistoryAsync(
        AggregateIdentity identity,
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken) {
        IAggregateActor aggregateProxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            actorOptions.Value.AggregateActorTypeName);
        var events = new List<EventEnvelope>();
        long afterSequence = 0;
        while (afterSequence < workItem.HeadSequence) {
            cancellationToken.ThrowIfCancellationRequested();
            EventEnvelope[] page = await aggregateProxy
                .ReadEventsRangeAsync(afterSequence, workItem.HeadSequence, ReadPageSize)
                .ConfigureAwait(false);
            long nextSequence = ProjectionStreamPageValidation.Validate(
                identity,
                afterSequence,
                workItem.HeadSequence,
                ReadPageSize,
                page);
            events.AddRange(page);
            afterSequence = nextSequence;
        }

        return [.. events];
    }

    private async Task DeferAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        ProjectionDispatchOptions options,
        CancellationToken cancellationToken,
        string reasonCode = ProjectionDispatchReasonCodes.PartialRetry) {
        ProjectionDeliveryRetryWorkItem? claimed = await scheduler.TryAcquireAsync(
            workItem,
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
            timeProvider.GetUtcNow(),
            options.RetryLeaseDuration,
            cancellationToken).ConfigureAwait(false);
        if (claimed is null) {
            return;
        }

        await DeferClaimedAsync(claimed, options, reasonCode, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeferClaimedAsync(ProjectionDeliveryRetryWorkItem claimed, ProjectionDispatchOptions options,
        string reasonCode, CancellationToken cancellationToken) {
        int attempt = claimed.Attempt < options.MaxRetryAttempts ? claimed.Attempt + 1 : options.MaxRetryAttempts;
        double multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        long delayTicks = (long)Math.Min(options.RetryMaxDelay.Ticks, options.RetryBaseDelay.Ticks * multiplier);
        _ = await scheduler.TryUpdateAsync(
            claimed with {
                Attempt = attempt,
                NextDueUtc = timeProvider.GetUtcNow() + TimeSpan.FromTicks(delayTicks),
                LastReasonCode = reasonCode,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 4660,
            Level = LogLevel.Warning,
            Message = "Named projection retry remains pending: ReasonCode={ReasonCode}, Stage=NamedProjectionRetry")]
        public static partial void RetryFailed(ILogger logger, string reasonCode);

        [LoggerMessage(
            EventId = 4661,
            Level = LogLevel.Error,
            Message = "Named projection retry activation failed; the worker continues on the next tick. ReasonCode={ReasonCode}, Stage=NamedProjectionRetryActivation")]
        public static partial void RetryActivationFailed(ILogger logger, string reasonCode);
    }
}
