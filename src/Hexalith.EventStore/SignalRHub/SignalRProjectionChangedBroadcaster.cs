using System.Diagnostics;
using System.Text;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// SignalR implementation of <see cref="IProjectionChangedBroadcaster"/>.
/// Sends signal-only "changed" messages — and metadata-rich, optionally scoped "changed detail"
/// messages — to the SignalR group matching the projection+tenant(+scope) tuple.
/// </summary>
public partial class SignalRProjectionChangedBroadcaster : IProjectionChangedBroadcaster {
    private const string BroadcastActivityName = "EventStore.SignalR.BroadcastProjectionChanged";
    private const string BroadcastDetailActivityName = "EventStore.SignalR.BroadcastProjectionChangedDetail";
    private const string BroadcastResultSuccess = "Success";
    private const string BroadcastResultFailOpenFailure = "FailOpenFailure";
    private const string CategoryName = "Hexalith.EventStore.SignalRHub.SignalRProjectionChangedBroadcaster";
    private const string TagElapsedMilliseconds = "eventstore.signalr.elapsed_ms";
    private const string TagExceptionType = "eventstore.signalr.exception_type";
    private const string TagGroupName = "eventstore.signalr.group_name";
    private const string TagGroupScope = "eventstore.signalr.group_scope";
    private const string TagMetadataCount = "eventstore.signalr.metadata_count";
    private const string TagProjectionType = "eventstore.signalr.projection_type";
    private const string TagResult = "eventstore.signalr.result";
    private const string TagTenantId = "eventstore.signalr.tenant_id";

    private readonly IHubContext<ProjectionChangedHub, IProjectionChangedClient> _hubContext;
    private readonly ILogger<SignalRProjectionChangedBroadcaster> _logger;
    private readonly SignalROptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRProjectionChangedBroadcaster"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    /// <param name="options">SignalR options carrying the metadata bounds for the detail path.</param>
    /// <param name="logger">The logger.</param>
    public SignalRProjectionChangedBroadcaster(
        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext,
        IOptions<SignalROptions> options,
        ILogger<SignalRProjectionChangedBroadcaster> logger) {
        ArgumentNullException.ThrowIfNull(options);
        _hubContext = hubContext;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRProjectionChangedBroadcaster"/> class
    /// with default metadata bounds. Provided for the signal-only path and simple test construction.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    /// <param name="logger">The logger.</param>
    public SignalRProjectionChangedBroadcaster(
        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext,
        ILogger<SignalRProjectionChangedBroadcaster> logger)
        : this(hubContext, Options.Create(new SignalROptions()), logger) {
    }

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
            _logger,
            projectionType,
            tenantId,
            groupName,
            startedUtc,
            traceId,
            spanId,
            CategoryName);

        try {
            await _hubContext.Clients
                .Group(groupName)
                .ProjectionChanged(projectionType, tenantId)
                .ConfigureAwait(false);

            double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
            DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
            _ = activity?.SetTag(TagResult, BroadcastResultSuccess);
            _ = activity?.SetTag(TagElapsedMilliseconds, elapsedMilliseconds);
            Log.BroadcastCompleted(
                _logger,
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
                _logger,
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

    /// <inheritdoc/>
    public async Task BroadcastChangedAsync(
        ProjectionChangedDetail detail,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(detail);

        // Bound + redact: keep the channel metadata-only and bounded. Metadata VALUES are never
        // logged above Debug; only the entry count appears in evidence.
        (IReadOnlyDictionary<string, string> metadata, bool clipped, int originalCount) =
            BoundMetadata(detail.Metadata);

        string groupName = ProjectionChangedHub.BuildGroupName(detail.ProjectionType, detail.TenantId, detail.GroupScope);
        long startedTimestamp = Stopwatch.GetTimestamp();
        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(BroadcastDetailActivityName, ActivityKind.Producer);
        _ = activity?.SetTag(TagProjectionType, detail.ProjectionType);
        _ = activity?.SetTag(TagTenantId, detail.TenantId);
        _ = activity?.SetTag(TagGroupScope, detail.GroupScope);
        _ = activity?.SetTag(TagGroupName, groupName);
        _ = activity?.SetTag(TagMetadataCount, metadata.Count);
        string? traceId = activity?.TraceId.ToString();
        string? spanId = activity?.SpanId.ToString();

        if (clipped) {
            Log.DetailMetadataClipped(_logger, groupName, originalCount, metadata.Count, CategoryName);
        }

        Log.DetailBroadcastStarted(_logger, detail.ProjectionType, detail.TenantId, detail.GroupScope, groupName, metadata.Count, traceId, spanId, CategoryName);

        try {
            await _hubContext.Clients
                .Group(groupName)
                .ProjectionChangedDetail(detail.ProjectionType, detail.TenantId, detail.GroupScope, metadata)
                .ConfigureAwait(false);

            double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
            _ = activity?.SetTag(TagResult, BroadcastResultSuccess);
            _ = activity?.SetTag(TagElapsedMilliseconds, elapsedMilliseconds);
            Log.DetailBroadcastCompleted(_logger, detail.ProjectionType, detail.TenantId, groupName, elapsedMilliseconds, BroadcastResultSuccess, traceId, spanId, CategoryName);
        }
        catch (Exception ex) {
            // Fail-open: broadcast failure must not break the caller (ADR-18.5a).
            double elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
            string exceptionType = ex.GetType().Name;
            _ = activity?.SetTag(TagResult, BroadcastResultFailOpenFailure);
            _ = activity?.SetTag(TagExceptionType, exceptionType);
            _ = activity?.SetTag(TagElapsedMilliseconds, elapsedMilliseconds);
            Log.DetailBroadcastFailed(_logger, ex, detail.ProjectionType, detail.TenantId, groupName, elapsedMilliseconds, BroadcastResultFailOpenFailure, exceptionType, traceId, spanId, CategoryName);
        }
    }

    /// <summary>
    /// Clips the metadata map to the configured entry-count and byte-size caps. Entries are taken
    /// in ordinal key order so clipping is deterministic. Returns the bounded map, whether any
    /// entry was dropped, and the original entry count. Values are never inspected for logging.
    /// </summary>
    private (IReadOnlyDictionary<string, string> Bounded, bool Clipped, int OriginalCount) BoundMetadata(
        IReadOnlyDictionary<string, string>? metadata) {
        if (metadata is null || metadata.Count == 0) {
            return (EmptyMetadata, false, 0);
        }

        int maxEntries = _options.MaxDetailMetadataEntries;
        int maxBytes = _options.MaxDetailMetadataBytes;
        var bounded = new Dictionary<string, string>(StringComparer.Ordinal);
        int totalBytes = 0;
        bool clipped = false;

        foreach (KeyValuePair<string, string> entry in metadata.OrderBy(e => e.Key, StringComparer.Ordinal)) {
            if (bounded.Count >= maxEntries) {
                clipped = true;
                break;
            }

            int entryBytes = Encoding.UTF8.GetByteCount(entry.Key) + Encoding.UTF8.GetByteCount(entry.Value);
            if (totalBytes + entryBytes > maxBytes) {
                clipped = true;
                break;
            }

            totalBytes += entryBytes;
            bounded[entry.Key] = entry.Value;
        }

        return (bounded, clipped, metadata.Count);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(StringComparer.Ordinal);

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

        [LoggerMessage(EventId = 1093, Level = LogLevel.Information,
            Message = "SignalR detail broadcast started. ProjectionType: {ProjectionType}, TenantId: {TenantId}, GroupScope: {GroupScope}, Group: {GroupName}, MetadataCount: {MetadataCount}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void DetailBroadcastStarted(ILogger logger, string projectionType, string tenantId, string? groupScope, string groupName, int metadataCount, string? traceId, string? spanId, string categoryName);

        [LoggerMessage(EventId = 1094, Level = LogLevel.Information,
            Message = "SignalR detail broadcast completed. ProjectionType: {ProjectionType}, TenantId: {TenantId}, Group: {GroupName}, ElapsedMilliseconds: {ElapsedMilliseconds}, Result: {Result}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void DetailBroadcastCompleted(ILogger logger, string projectionType, string tenantId, string groupName, double elapsedMilliseconds, string result, string? traceId, string? spanId, string categoryName);

        [LoggerMessage(EventId = 1095, Level = LogLevel.Warning,
            Message = "SignalR detail broadcast failed (fail-open). ProjectionType: {ProjectionType}, TenantId: {TenantId}, Group: {GroupName}, ElapsedMilliseconds: {ElapsedMilliseconds}, Result: {Result}, ExceptionType: {ExceptionType}, TraceId: {TraceId}, SpanId: {SpanId}, CategoryName: {CategoryName}")]
        public static partial void DetailBroadcastFailed(ILogger logger, Exception exception, string projectionType, string tenantId, string groupName, double elapsedMilliseconds, string result, string exceptionType, string? traceId, string? spanId, string categoryName);

        [LoggerMessage(EventId = 1096, Level = LogLevel.Warning,
            Message = "SignalR detail broadcast metadata clipped to configured bounds. Group: {GroupName}, OriginalCount: {OriginalCount}, BoundedCount: {BoundedCount}, CategoryName: {CategoryName}")]
        public static partial void DetailMetadataClipped(ILogger logger, string groupName, int originalCount, int boundedCount, string categoryName);
    }
}
