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
    TimeProvider? timeProvider = null) : INamedProjectionDispatchCoordinator {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

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
            return false;
        }

        EventEnvelope head = events.OrderBy(static item => item.SequenceNumber).Last();
        long highestSequence = head.SequenceNumber;
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
            now,
            null);
        ProjectionDeliveryRetryWorkItem scheduledWork = retryScheduler is null
            ? proposedWork
            : await retryScheduler.ScheduleAsync(proposedWork, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(scheduledWork.HeadMessageId, head.MessageId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.DispatchId, head.MessageId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.CatalogFingerprint, catalogEntry.CatalogFingerprint, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.AppId, registration.AppId, StringComparison.Ordinal)
            || !string.Equals(scheduledWork.ServiceVersion, serviceVersion, StringComparison.Ordinal)) {
            return true;
        }

        HashSet<string> pendingRoutes = new(scheduledWork.PendingRoutes, StringComparer.Ordinal);
        List<string> admittedRoutes = [];
        foreach (string projectionType in catalogEntry.ProjectionTypes
            .Where(pendingRoutes.Contains)
            .Order(StringComparer.Ordinal)) {
            cancellationToken.ThrowIfCancellationRequested();
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
                continue;
            }

            if (!await lifecycleGateway
                .TryAdmitDeliveryWriteAsync(identity, projectionType, cancellationToken)
                .ConfigureAwait(false)) {
                Log.RouteNotAdmitted(
                    logger,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    projectionType,
                    ProjectionReasonCodes.DeliveryDeferredForErase);
                continue;
            }

            admittedRoutes.Add(projectionType);
        }

        if (admittedRoutes.Count == 0) {
            await DeferAsync(scheduledWork, ProjectionDispatchReasonCodes.PartialRetry, dispatchOptions, cancellationToken)
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
            dispatchRequest,
            dispatchOptions,
            cancellationToken).ConfigureAwait(false);
        if (dispatchResponse is null) {
            return true;
        }

        Dictionary<string, ProjectionDispatchOutcome> validOutcomes = ValidateOutcomes(
            dispatchResponse,
            admittedRoutes,
            dispatchOptions);
        var completedRoutes = new HashSet<string>(StringComparer.Ordinal);
        var terminalRoutes = new HashSet<string>(scheduledWork.TerminalRoutes, StringComparer.Ordinal);
        foreach (string projectionType in admittedRoutes) {
            cancellationToken.ThrowIfCancellationRequested();
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
                    if (outcome.State is not null) {
                        await WriteLegacyActorStateAsync(identity, projectionType, outcome.State.Value, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    bool checkpointSaved = await checkpointStore
                        .SaveDeliveredSequenceAsync(identity, projectionType, highestSequence, cancellationToken)
                        .ConfigureAwait(false);
                    if (checkpointSaved) {
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

        if (retryScheduler is not null) {
            string[] remainingRoutes = [.. scheduledWork.PendingRoutes
                .Where(route => !completedRoutes.Contains(route) && !terminalRoutes.Contains(route))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            if (remainingRoutes.Length == 0 && terminalRoutes.Count == 0) {
                await retryScheduler.DeleteAsync(scheduledWork.WorkId, cancellationToken).ConfigureAwait(false);
            }
            else {
                int attempt = Math.Min(scheduledWork.Attempt + 1, dispatchOptions.MaxRetryAttempts);
                var updated = scheduledWork with {
                    PendingRoutes = remainingRoutes,
                    TerminalRoutes = [.. terminalRoutes.Order(StringComparer.Ordinal)],
                    Attempt = attempt,
                    NextDueUtc = _timeProvider.GetUtcNow() + GetRetryDelay(attempt, dispatchOptions),
                    LastReasonCode = terminalRoutes.Count > 0
                        ? ProjectionDispatchReasonCodes.HandlerFailure
                        : ProjectionDispatchReasonCodes.PartialRetry,
                };
                await retryScheduler.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
            }
        }

        return true;
    }

    private async Task DeferAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        string reasonCode,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken) {
        if (retryScheduler is null) {
            return;
        }

        int attempt = Math.Min(workItem.Attempt + 1, dispatchOptions.MaxRetryAttempts);
        await retryScheduler.UpdateAsync(
            workItem with {
                Attempt = attempt,
                NextDueUtc = _timeProvider.GetUtcNow() + GetRetryDelay(attempt, dispatchOptions),
                LastReasonCode = reasonCode,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static TimeSpan GetRetryDelay(int attempt, ProjectionDispatchOptions dispatchOptions) {
        double multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        double ticks = Math.Min(
            dispatchOptions.RetryMaxDelay.Ticks,
            dispatchOptions.RetryBaseDelay.Ticks * multiplier);
        return TimeSpan.FromTicks((long)ticks);
    }

    private async Task<ProjectionDispatchResponse?> InvokeAsync(
        string appId,
        ProjectionDispatchRequest request,
        ProjectionDispatchOptions dispatchOptions,
        CancellationToken cancellationToken) {
        try {
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(appId, "project/v2", request);
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

        try {
            IETagActor eTagProxy = actorProxyFactory.CreateActorProxy<IETagActor>(
                new ActorId($"{projectionType}:{identity.TenantId}"),
                ETagActor.ETagActorTypeName);
            cancellationToken.ThrowIfCancellationRequested();
            _ = await eTagProxy.RegenerateAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception) {
            // Preserve the existing fail-open ETag policy; durable state still permits checkpoint advancement.
        }
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
    }
}
