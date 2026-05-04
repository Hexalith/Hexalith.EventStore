using System.Diagnostics;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// SignalR implementation of <see cref="IProjectionChangedBroadcaster"/>.
/// Sends signal-only "changed" messages to the SignalR group matching the projection+tenant pair.
/// </summary>
public partial class SignalRProjectionChangedBroadcaster(
    IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext,
    ILogger<SignalRProjectionChangedBroadcaster> logger) : IProjectionChangedBroadcaster {
    private const string BroadcastActivityName = "EventStore.SignalR.BroadcastProjectionChanged";
    private const string BroadcastResultSuccess = "Success";
    private const string BroadcastResultFailOpenFailure = "FailOpenFailure";
    private const string CategoryName = "Hexalith.EventStore.SignalRHub.SignalRProjectionChangedBroadcaster";
    private const string TagElapsedMilliseconds = "eventstore.signalr.elapsed_ms";
    private const string TagExceptionType = "eventstore.signalr.exception_type";
    private const string TagGroupName = "eventstore.signalr.group_name";
    private const string TagProjectionType = "eventstore.signalr.projection_type";
    private const string TagResult = "eventstore.signalr.result";
    private const string TagTenantId = "eventstore.signalr.tenant_id";

    /// <inheritdoc/>
    public async Task BroadcastChangedAsync(
        string projectionType,
        string tenantId,
        CancellationToken cancellationToken = default) {
        string groupName = $"{projectionType}:{tenantId}";
        DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
        long startedTimestamp = Stopwatch.GetTimestamp();
        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(BroadcastActivityName, ActivityKind.Producer);
        _ = activity?.SetTag(TagProjectionType, projectionType);
        _ = activity?.SetTag(TagTenantId, tenantId);
        _ = activity?.SetTag(TagGroupName, groupName);
        // Use the just-started broadcast activity for trace/span IDs. When no listener samples,
        // activity is null and we log null rather than Activity.Current, which could belong to
        // an unrelated outer scope and mislabel the broadcast trace.
        string? traceId = activity?.TraceId.ToString();
        string? spanId = activity?.SpanId.ToString();
        Log.BroadcastStarted(
            logger,
            projectionType,
            tenantId,
            groupName,
            startedUtc,
            traceId,
            spanId,
            CategoryName);

        try {
            await hubContext.Clients
                .Group(groupName)
                .ProjectionChanged(projectionType, tenantId)
                .ConfigureAwait(false);

            double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
            DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
            _ = activity?.SetTag(TagResult, BroadcastResultSuccess);
            _ = activity?.SetTag(TagElapsedMilliseconds, elapsedMilliseconds);
            Log.BroadcastCompleted(
                logger,
                projectionType,
                tenantId,
                groupName,
                completedUtc,
                elapsedMilliseconds,
                BroadcastResultSuccess,
                traceId,
                spanId,
                CategoryName);
        }
        catch (Exception ex) {
            // Fail-open: broadcast failure must not break ETag regeneration flow
            double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
            DateTimeOffset failedUtc = DateTimeOffset.UtcNow;
            string exceptionType = ex.GetType().Name;
            _ = activity?.SetTag(TagResult, BroadcastResultFailOpenFailure);
            _ = activity?.SetTag(TagExceptionType, exceptionType);
            _ = activity?.SetTag(TagElapsedMilliseconds, elapsedMilliseconds);
            Log.BroadcastFailed(
                logger,
                ex,
                projectionType,
                tenantId,
                groupName,
                failedUtc,
                elapsedMilliseconds,
                BroadcastResultFailOpenFailure,
                exceptionType,
                traceId,
                spanId,
                CategoryName);
        }
    }

    private static partial class Log {
        [LoggerMessage(EventId = 1090, Level = LogLevel.Information,
            Message = "SignalR broadcast started. ProjectionType: {ProjectionType}, TenantId: {TenantId}, Group: {GroupName}, StartedUtc: {StartedUtc}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void BroadcastStarted(ILogger logger, string projectionType, string tenantId, string groupName, DateTimeOffset startedUtc, string? traceId, string? spanId, string categoryName);

        [LoggerMessage(EventId = 1091, Level = LogLevel.Information,
            Message = "SignalR broadcast completed. ProjectionType: {ProjectionType}, TenantId: {TenantId}, Group: {GroupName}, CompletedUtc: {CompletedUtc}, ElapsedMilliseconds: {ElapsedMilliseconds}, Result: {Result}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void BroadcastCompleted(ILogger logger, string projectionType, string tenantId, string groupName, DateTimeOffset completedUtc, double elapsedMilliseconds, string result, string? traceId, string? spanId, string categoryName);

        [LoggerMessage(EventId = 1092, Level = LogLevel.Warning,
            Message = "SignalR broadcast failed (fail-open). ProjectionType: {ProjectionType}, TenantId: {TenantId}, Group: {GroupName}, FailedUtc: {FailedUtc}, ElapsedMilliseconds: {ElapsedMilliseconds}, Result: {Result}, ExceptionType: {ExceptionType}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void BroadcastFailed(ILogger logger, Exception exception, string projectionType, string tenantId, string groupName, DateTimeOffset failedUtc, double elapsedMilliseconds, string result, string exceptionType, string? traceId, string? spanId, string categoryName);
    }
}
