
using Dapr;
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.Controllers;

/// <summary>
/// Cross-process notification receiver for DAPR pub/sub.
/// External domain services publish <see cref="ProjectionChangedNotification"/>
/// to topic "{tenantId}.{projectionType}.projection-changed". DAPR delivers here.
/// </summary>
[ApiController]
[Route("projections")]
public partial class ProjectionNotificationController(
    IActorProxyFactory actorProxyFactory,
    IProjectionChangedBroadcaster broadcaster,
    ILogger<ProjectionNotificationController> logger) : ControllerBase {
    /// <summary>
    /// Receives a projection change notification from DAPR pub/sub and triggers ETag regeneration.
    /// Returns non-200 on actor failure to trigger DAPR retry (CM-1).
    /// </summary>
    [HttpPost("changed")]
    [Topic(ProjectionChangeNotifierOptions.DefaultPubSubName, "*.*.projection-changed")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> OnProjectionChanged(
        [FromBody] ProjectionChangedNotification notification,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(notification);

        if (string.IsNullOrWhiteSpace(notification.ProjectionType) ||
            string.IsNullOrWhiteSpace(notification.TenantId)) {
            Log.InvalidNotification(logger);
            return BadRequest();
        }

        string actorId = $"{notification.ProjectionType}:{notification.TenantId}";

        Log.NotificationReceived(logger, notification.ProjectionType, notification.TenantId, notification.EntityId);

        try {
            IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
                new ActorId(actorId),
                ETagActor.ETagActorTypeName);

            _ = await proxy.RegenerateAsync().ConfigureAwait(false);

            // Broadcast to SignalR clients (fail-open — ADR-18.5a)
            try {
                await broadcaster.BroadcastChangedAsync(
                    notification.ProjectionType, notification.TenantId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) {
                Log.BroadcastFailed(logger, notification.ProjectionType, notification.TenantId, ex.GetType().Name);
            }

            return Ok();
        }
        catch (Exception ex) {
            // CM-1: Return non-200 to trigger DAPR pub/sub retry
            Log.ActorInvocationFailed(logger, actorId, ex.GetType().Name);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1051,
            Level = LogLevel.Debug,
            Message = "Projection change notification received via pub/sub. ProjectionType: {ProjectionType}, TenantId: {TenantId}, EntityId: {EntityId}")]
        public static partial void NotificationReceived(ILogger logger, string projectionType, string tenantId, string? entityId);

        [LoggerMessage(
            EventId = 1056,
            Level = LogLevel.Warning,
            Message = "Invalid projection change notification received: missing ProjectionType or TenantId.")]
        public static partial void InvalidNotification(ILogger logger);

        [LoggerMessage(
            EventId = 1055,
            Level = LogLevel.Error,
            Message = "ETag actor invocation failed for actor {ActorId}. ExceptionType: {ExceptionType}. Returning non-200 for DAPR retry.")]
        public static partial void ActorInvocationFailed(ILogger logger, string actorId, string exceptionType);

        [LoggerMessage(
            EventId = 1086,
            Level = LogLevel.Warning,
            Message = "SignalR broadcast failed after ETag regeneration (fail-open). ProjectionType: {ProjectionType}, TenantId: {TenantId}, ExceptionType: {ExceptionType}")]
        public static partial void BroadcastFailed(ILogger logger, string projectionType, string tenantId, string exceptionType);
    }
}
