
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// In-process DAPR implementation of <see cref="IProjectionChangeNotifier"/>.
/// Calls <see cref="IETagActor.RegenerateAsync"/> directly via actor proxy (no pub/sub hop).
/// Used by <c>EventStoreProjection</c> auto-notify within the EventStore server process.
/// </summary>
public partial class DaprProjectionChangeNotifier(
    DaprClient daprClient,
    IActorProxyFactory actorProxyFactory,
    IProjectionChangedBroadcaster broadcaster,
    IOptions<ProjectionChangeNotifierOptions> options,
    ILogger<DaprProjectionChangeNotifier> logger) : IProjectionChangeNotifier {
    /// <inheritdoc/>
    public async Task NotifyProjectionChangedAsync(
        string projectionType,
        string tenantId,
        string? entityId = null,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        ProjectionChangeTransport transport = options.Value.Transport;
        string topic = NamingConventionEngine.GetProjectionChangedTopic(projectionType, tenantId);

        Log.NotificationReceived(logger, projectionType, tenantId, entityId, transport.ToString());

        if (transport == ProjectionChangeTransport.PubSub) {
            var notification = new ProjectionChangedNotification(projectionType, tenantId, entityId);
            await daprClient.PublishEventAsync(
                options.Value.PubSubName,
                topic,
                notification,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string actorId = $"{projectionType}:{tenantId}";

        IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
            new ActorId(actorId),
            ETagActor.ETagActorTypeName);

        _ = await proxy.RegenerateAsync().ConfigureAwait(false);

        // Broadcast to SignalR clients (fail-open — ADR-18.5a)
        try {
            await broadcaster.BroadcastChangedAsync(projectionType, tenantId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.BroadcastFailed(logger, projectionType, tenantId, ex.GetType().Name);
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1051,
            Level = LogLevel.Debug,
            Message = "Projection change notification received. ProjectionType: {ProjectionType}, TenantId: {TenantId}, EntityId: {EntityId}, Transport: {Transport}")]
        public static partial void NotificationReceived(ILogger logger, string projectionType, string tenantId, string? entityId, string transport);

        [LoggerMessage(
            EventId = 1088,
            Level = LogLevel.Warning,
            Message = "SignalR broadcast failed after ETag regeneration (fail-open). ProjectionType: {ProjectionType}, TenantId: {TenantId}, ExceptionType: {ExceptionType}")]
        public static partial void BroadcastFailed(ILogger logger, string projectionType, string tenantId, string exceptionType);
    }
}
