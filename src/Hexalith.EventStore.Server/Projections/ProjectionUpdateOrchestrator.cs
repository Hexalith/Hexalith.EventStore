
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
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
public partial class ProjectionUpdateOrchestrator(
    IActorProxyFactory actorProxyFactory,
    DaprClient daprClient,
    IDomainServiceResolver resolver,
    IOptions<ProjectionOptions> projectionOptions,
    ILogger<ProjectionUpdateOrchestrator> logger) : IProjectionUpdateOrchestrator {
    /// <inheritdoc/>
    public async Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);

        int refreshIntervalMs = projectionOptions.Value.GetRefreshIntervalMs(identity.Domain);
        if (refreshIntervalMs > 0) {
            Log.PollingModeDeferred(logger, identity.TenantId, identity.Domain, refreshIntervalMs);
            return;
        }

        try {
            Log.UpdateStarted(logger, identity.TenantId, identity.Domain, identity.AggregateId);

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
                "AggregateActor");

            // TODO: Story 11-4+ checkpoint tracking -- replace GetEventsAsync(0) with GetEventsAsync(lastCheckpoint)
            EventEnvelope[] events = await aggregateProxy
                .GetEventsAsync(0)
                .ConfigureAwait(false);

            if (events.Length == 0) {
                Log.NoEventsFound(logger, identity.TenantId, identity.Domain, identity.AggregateId);
                return;
            }

            // Step 3: Map EventEnvelope[] to ProjectionEventDto[]
            var projectionEvents = new ProjectionEventDto[events.Length];
            for (int i = 0; i < events.Length; i++) {
                EventEnvelope e = events[i];
                projectionEvents[i] = new ProjectionEventDto(
                    e.EventTypeName,
                    e.Payload,
                    e.SerializationFormat,
                    e.SequenceNumber,
                    e.Timestamp,
                    e.CorrelationId);
            }

            // Step 4: Invoke domain service /project endpoint via DAPR
            var request = new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, projectionEvents);
            ProjectionResponse response = await daprClient
                .InvokeMethodAsync<ProjectionRequest, ProjectionResponse>(
                    registration.AppId,
                    "project",
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(response.ProjectionType)) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, "ProjectionType is null or empty.");
                return;
            }

            if (response.State.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) {
                Log.InvalidProjectionResponse(logger, identity.TenantId, identity.Domain, identity.AggregateId, "State is null or undefined.");
                return;
            }

            // Clone JsonElement to avoid holding a view over an ephemeral JsonDocument.
            JsonElement clonedState = response.State.Clone();

            Log.DomainServiceInvocationSucceeded(logger, identity.TenantId, identity.Domain, identity.AggregateId, registration.AppId);

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
                .UpdateProjectionAsync(new ProjectionState(response.ProjectionType, identity.TenantId, clonedState))
                .ConfigureAwait(false);

            Log.ProjectionStateUpdated(logger, identity.TenantId, identity.Domain, identity.AggregateId, response.ProjectionType, projectionActorId);
        }
        catch (Exception ex) {
            Log.ProjectionUpdateFailed(logger, ex, identity.TenantId, identity.Domain, identity.AggregateId);
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1110,
            Level = LogLevel.Debug,
            Message = "Projection update started: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionUpdateStarted")]
        public static partial void UpdateStarted(ILogger logger, string tenantId, string domain, string aggregateId);

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
            EventId = 1115,
            Level = LogLevel.Warning,
            Message = "Projection update failed: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionUpdateFailed")]
        public static partial void ProjectionUpdateFailed(ILogger logger, Exception ex, string tenantId, string domain, string aggregateId);

        [LoggerMessage(
            EventId = 1116,
            Level = LogLevel.Warning,
            Message = "Invalid projection response: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Reason={Reason}, Stage=InvalidProjectionResponse")]
        public static partial void InvalidProjectionResponse(ILogger logger, string tenantId, string domain, string aggregateId, string reason);

        [LoggerMessage(
            EventId = 1117,
            Level = LogLevel.Debug,
            Message = "Projection polling mode configured (RefreshIntervalMs={RefreshIntervalMs}), skipping immediate trigger: TenantId={TenantId}, Domain={Domain}, Stage=PollingModeDeferred")]
        public static partial void PollingModeDeferred(ILogger logger, string tenantId, string domain, int refreshIntervalMs);
    }
}
