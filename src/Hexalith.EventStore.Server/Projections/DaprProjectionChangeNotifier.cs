
using System.Collections.ObjectModel;
using System.Text;

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
    private const int MaxGroupScopeLength = 64;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

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

    /// <inheritdoc/>
    public async Task NotifyProjectionChangedAsync(
        ProjectionChangedDetail detail,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail.ProjectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail.TenantId);

        if (detail.ProjectionType.Contains(':', StringComparison.Ordinal)
            || detail.TenantId.Contains(':', StringComparison.Ordinal)) {
            throw new ArgumentException("Projection type and tenant id must not contain colons.", nameof(detail));
        }

        string? groupScope = string.IsNullOrWhiteSpace(detail.GroupScope) ? null : detail.GroupScope;
        if (groupScope is not null && groupScope.Contains(':', StringComparison.Ordinal)) {
            throw new ArgumentException("Group scope must not contain colons.", nameof(detail));
        }

        if (groupScope is not null && groupScope.Length > MaxGroupScopeLength) {
            throw new ArgumentException($"Group scope must not exceed {MaxGroupScopeLength} characters.", nameof(detail));
        }

        ProjectionChangeNotifierOptions currentOptions = options.Value;
        (IReadOnlyDictionary<string, string> metadata, bool clipped, int originalCount) =
            BoundMetadata(detail.Metadata, currentOptions);
        ProjectionChangedDetail normalizedDetail = detail with {
            GroupScope = groupScope,
            Metadata = metadata,
        };

        string topic = NamingConventionEngine.GetProjectionChangedTopic(detail.ProjectionType, detail.TenantId);

        if (clipped) {
            Log.DetailMetadataClipped(
                logger,
                detail.ProjectionType,
                detail.TenantId,
                groupScope,
                originalCount,
                metadata.Count);
        }

        Log.DetailNotificationReceived(
            logger,
            detail.ProjectionType,
            detail.TenantId,
            groupScope,
            normalizedDetail.Metadata.Count,
            currentOptions.Transport.ToString());

        if (currentOptions.Transport == ProjectionChangeTransport.PubSub) {
            var notification = new ProjectionChangedNotification(
                detail.ProjectionType,
                detail.TenantId,
                EntityId: null,
                GroupScope: groupScope,
                Metadata: normalizedDetail.Metadata);

            await daprClient.PublishEventAsync(
                currentOptions.PubSubName,
                topic,
                notification,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string actorId = $"{detail.ProjectionType}:{detail.TenantId}";

        IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
            new ActorId(actorId),
            ETagActor.ETagActorTypeName);

        _ = await proxy.RegenerateAsync().ConfigureAwait(false);

        // Broadcast to SignalR clients (fail-open — ADR-18.5a)
        try {
            await broadcaster.BroadcastChangedAsync(normalizedDetail, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.DetailBroadcastFailed(logger, detail.ProjectionType, detail.TenantId, groupScope, ex.GetType().Name);
        }
    }

    private static (IReadOnlyDictionary<string, string> Bounded, bool Clipped, int OriginalCount) BoundMetadata(
        IReadOnlyDictionary<string, string>? metadata,
        ProjectionChangeNotifierOptions options) {
        if (metadata is null || metadata.Count == 0) {
            return (EmptyMetadata, false, 0);
        }

        var bounded = new Dictionary<string, string>(StringComparer.Ordinal);
        int totalBytes = 0;
        bool clipped = false;

        foreach (KeyValuePair<string, string> entry in metadata.OrderBy(e => e.Key, StringComparer.Ordinal)) {
            if (entry.Key is null || entry.Value is null) {
                throw new ArgumentException("Metadata keys and values must be non-null.", nameof(metadata));
            }

            if (bounded.Count >= options.MaxDetailMetadataEntries) {
                clipped = true;
                break;
            }

            int entryBytes = Encoding.UTF8.GetByteCount(entry.Key) + Encoding.UTF8.GetByteCount(entry.Value);
            if (totalBytes + entryBytes > options.MaxDetailMetadataBytes) {
                clipped = true;
                break;
            }

            totalBytes += entryBytes;
            bounded[entry.Key] = entry.Value;
        }

        return (bounded, clipped, metadata.Count);
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

        [LoggerMessage(
            EventId = 1089,
            Level = LogLevel.Debug,
            Message = "Projection detail change notification received. ProjectionType: {ProjectionType}, TenantId: {TenantId}, GroupScope: {GroupScope}, MetadataCount: {MetadataCount}, Transport: {Transport}")]
        public static partial void DetailNotificationReceived(ILogger logger, string projectionType, string tenantId, string? groupScope, int metadataCount, string transport);

        [LoggerMessage(
            EventId = 1090,
            Level = LogLevel.Warning,
            Message = "SignalR detail broadcast failed after ETag regeneration (fail-open). ProjectionType: {ProjectionType}, TenantId: {TenantId}, GroupScope: {GroupScope}, ExceptionType: {ExceptionType}")]
        public static partial void DetailBroadcastFailed(ILogger logger, string projectionType, string tenantId, string? groupScope, string exceptionType);

        [LoggerMessage(
            EventId = 1091,
            Level = LogLevel.Warning,
            Message = "Projection detail metadata clipped to configured bounds before publish. ProjectionType: {ProjectionType}, TenantId: {TenantId}, GroupScope: {GroupScope}, OriginalCount: {OriginalCount}, BoundedCount: {BoundedCount}")]
        public static partial void DetailMetadataClipped(ILogger logger, string projectionType, string tenantId, string? groupScope, int originalCount, int boundedCount);
    }
}
