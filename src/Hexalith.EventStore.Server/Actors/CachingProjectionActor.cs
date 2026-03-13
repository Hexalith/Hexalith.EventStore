
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Abstract base class for projection actors with ETag-based in-memory caching (Gate 2).
/// Developers inherit and implement <see cref="ExecuteQueryAsync"/> with actual query logic.
/// Caching is automatic — no DAPR state store persistence for cache data.
/// </summary>
/// <remarks>
/// Optional: developers may implement <see cref="IProjectionActor"/> directly for non-cached projections.
/// </remarks>
public abstract partial class CachingProjectionActor(
    ActorHost host,
    IETagService eTagService,
    ILogger logger)
    : Actor(host), IProjectionActor
{
    private string? _cachedETag;
    private JsonElement? _cachedPayload;

    /// <inheritdoc/>
    public async Task<QueryResult> QueryAsync(QueryEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // IETagService handles actor ID derivation, proxy timeout, and fail-open (returns null on error)
        string? currentETag = await eTagService
            .GetCurrentETagAsync(envelope.Domain, envelope.TenantId)
            .ConfigureAwait(false);

        // Cache hit: ETag is non-null, matches cached, and payload exists
        if (currentETag is not null && currentETag == _cachedETag && _cachedPayload is not null)
        {
            Log.CacheHit(logger, envelope.CorrelationId, Id.GetId(), _cachedETag[..Math.Min(8, _cachedETag.Length)]);
            return new QueryResult(true, _cachedPayload.Value);
        }

        // Cache miss: execute the actual query
        QueryResult result = await ExecuteQueryAsync(envelope).ConfigureAwait(false);

        if (result.Success && currentETag is not null)
        {
            // CRITICAL: Clone() creates an independent copy safe for long-lived caching.
            // Without it, the JsonElement becomes a dangling reference when the original JsonDocument is disposed.
            _cachedPayload = result.Payload.Clone();
            _cachedETag = currentETag;
            Log.CacheMiss(logger, envelope.CorrelationId, Id.GetId(), currentETag[..Math.Min(8, currentETag.Length)]);
        }
        else if (currentETag is null)
        {
            Log.CacheSkipped(logger, envelope.CorrelationId, Id.GetId());
        }

        return result;
    }

    /// <summary>
    /// Executes the actual query logic against the domain service / read model.
    /// Implementers return a <see cref="QueryResult"/> with the projection data.
    /// </summary>
    /// <param name="envelope">The query envelope containing routing and payload data.</param>
    /// <returns>The query result from the domain service.</returns>
    protected abstract Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope);

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1070,
            Level = LogLevel.Debug,
            Message = "Query actor cache hit: CorrelationId={CorrelationId}, ActorId={ActorId}, CachedETag={CachedETagPrefix}, Stage=CacheHit")]
        public static partial void CacheHit(ILogger logger, string correlationId, string actorId, string cachedETagPrefix);

        [LoggerMessage(
            EventId = 1071,
            Level = LogLevel.Debug,
            Message = "Query actor cache miss: CorrelationId={CorrelationId}, ActorId={ActorId}, NewETag={NewETagPrefix}, Stage=CacheMiss")]
        public static partial void CacheMiss(ILogger logger, string correlationId, string actorId, string newETagPrefix);

        [LoggerMessage(
            EventId = 1073,
            Level = LogLevel.Debug,
            Message = "Cache skipped (null ETag): CorrelationId={CorrelationId}, ActorId={ActorId}, Stage=CacheSkipped")]
        public static partial void CacheSkipped(ILogger logger, string correlationId, string actorId);
    }
}
