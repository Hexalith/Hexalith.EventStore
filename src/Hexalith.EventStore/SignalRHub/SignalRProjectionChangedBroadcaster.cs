using Hexalith.EventStore.Client.Projections;

using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// SignalR implementation of <see cref="IProjectionChangedBroadcaster"/>.
/// Sends signal-only "changed" messages to the SignalR group matching the projection+tenant pair.
/// </summary>
public partial class SignalRProjectionChangedBroadcaster(
    IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext,
    ILogger<SignalRProjectionChangedBroadcaster> logger) : IProjectionChangedBroadcaster {
    /// <inheritdoc/>
    public async Task BroadcastChangedAsync(
        string projectionType,
        string tenantId,
        CancellationToken cancellationToken = default) {
        string groupName = $"{projectionType}:{tenantId}";
        try {
            await hubContext.Clients
                .Group(groupName)
                .ProjectionChanged(projectionType, tenantId)
                .ConfigureAwait(false);
            Log.BroadcastSent(logger, projectionType, tenantId, groupName);
        }
        catch (Exception ex) {
            // Fail-open: broadcast failure must not break ETag regeneration flow
            Log.BroadcastFailed(logger, projectionType, tenantId, ex.GetType().Name);
        }
    }

    private static partial class Log {
        [LoggerMessage(EventId = 1084, Level = LogLevel.Debug,
            Message = "SignalR broadcast sent. ProjectionType: {ProjectionType}, TenantId: {TenantId}, Group: {GroupName}")]
        public static partial void BroadcastSent(ILogger logger, string projectionType, string tenantId, string groupName);

        [LoggerMessage(EventId = 1085, Level = LogLevel.Warning,
            Message = "SignalR broadcast failed (fail-open). ProjectionType: {ProjectionType}, TenantId: {TenantId}, ExceptionType: {ExceptionType}")]
        public static partial void BroadcastFailed(ILogger logger, string projectionType, string tenantId, string exceptionType);
    }
}
