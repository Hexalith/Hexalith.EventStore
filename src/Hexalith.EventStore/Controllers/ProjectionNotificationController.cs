
using System.Collections.ObjectModel;

using Dapr;
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// Cross-process notification receiver for DAPR pub/sub.
/// Publishers send <see cref="ProjectionChangedNotification"/> to topic
/// "{tenantId}.{projectionType}.projection-changed". DAPR delivers here.
/// </summary>
/// <remarks>
/// The callback is never anonymous (FR28): delivery must present this host's Dapr application-channel token, and
/// the notification must carry signed publisher provenance that names an allowed publisher, grants the
/// projection-notify operation, and matches the notification's tenant, projection type, and topic. A forged,
/// stale, unbound, or mismatched callback performs no actor call, freshness change, or SignalR broadcast.
/// </remarks>
[ApiController]
[Authorize(Policy = EventStoreWorkloadAuthenticationDefaults.SidecarChannelPolicy)]
[Route("projections")]
public partial class ProjectionNotificationController(
    IActorProxyFactory actorProxyFactory,
    IProjectionChangedBroadcaster broadcaster,
    ProjectionNotificationProvenanceVerifier provenanceVerifier,
    ILogger<ProjectionNotificationController> logger) : ControllerBase {
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Receives a projection change notification from DAPR pub/sub and triggers ETag regeneration.
    /// Returns non-200 on actor failure to trigger DAPR retry (CM-1).
    /// </summary>
    [HttpPost("changed")]
    [Topic(ProjectionChangeNotifierOptions.DefaultPubSubName, "*.*.projection-changed")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> OnProjectionChanged(
        [FromBody] ProjectionChangedNotification notification,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(notification);

        if (string.IsNullOrWhiteSpace(notification.ProjectionType) ||
            string.IsNullOrWhiteSpace(notification.TenantId)) {
            Log.InvalidNotification(logger);
            return BadRequest();
        }

        string? provenanceDenial = await provenanceVerifier.VerifyAsync(notification, cancellationToken).ConfigureAwait(false);
        if (provenanceDenial is not null) {
            int statusCode = provenanceDenial == WorkloadAuthenticationReasons.VerifierUnavailable
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status403Forbidden;
            WorkloadAuthenticationTelemetry.RecordDenial(logger, HttpContext, ProvenanceScheme, provenanceDenial, statusCode);
            return StatusCode(statusCode);
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
                if (HasDetail(notification)) {
                    var detail = new ProjectionChangedDetail(
                        notification.ProjectionType,
                        notification.TenantId,
                        string.IsNullOrWhiteSpace(notification.GroupScope) ? null : notification.GroupScope,
                        notification.Metadata ?? EmptyMetadata);

                    await broadcaster.BroadcastChangedAsync(detail, cancellationToken)
                        .ConfigureAwait(false);
                }
                else {
                    await broadcaster.BroadcastChangedAsync(
                        notification.ProjectionType, notification.TenantId, cancellationToken)
                        .ConfigureAwait(false);
                }
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

    private const string ProvenanceScheme = "ProjectionNotificationProvenance";

    private static bool HasDetail(ProjectionChangedNotification notification)
        => !string.IsNullOrWhiteSpace(notification.GroupScope)
            || notification.Metadata is not null;

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
