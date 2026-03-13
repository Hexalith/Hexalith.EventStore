
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
    private string? _discoveredProjectionType;

    /// <inheritdoc/>
    public async Task<QueryResult> QueryAsync(QueryEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // IETagService handles actor ID derivation, proxy timeout, and fail-open (returns null on error)
        string? currentETag = await eTagService
            .GetCurrentETagAsync(GetEffectiveProjectionType(envelope.Domain), envelope.TenantId)
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
            // Runtime projection type discovery (FR63)
            if (!string.IsNullOrWhiteSpace(result.ProjectionType)
                && IsValidProjectionType(result.ProjectionType))
            {
                if (_discoveredProjectionType is null)
                {
                    bool projectionTypeDiffersFromDomain =
                        !string.Equals(result.ProjectionType, envelope.Domain, StringComparison.Ordinal);

                    _discoveredProjectionType = result.ProjectionType;
                    Log.ProjectionTypeDiscovered(logger, envelope.CorrelationId, Id.GetId(), result.ProjectionType);

                    if (projectionTypeDiffersFromDomain)
                    {
                        // ETag was fetched using envelope.Domain — may be wrong projection.
                        // Don't cache. Next request will use correct _discoveredProjectionType.
                        return result;
                    }
                }
                else if (!string.Equals(_discoveredProjectionType, result.ProjectionType, StringComparison.Ordinal))
                {
                    Log.ProjectionTypeMismatch(logger, envelope.CorrelationId, Id.GetId(),
                        _discoveredProjectionType, result.ProjectionType);
                    // DO NOT update — first discovery wins until actor deactivation
                }
            }

            // CRITICAL: Clone() creates an independent copy safe for long-lived caching.
            // Without it, the JsonElement becomes a dangling reference when the original JsonDocument is disposed.
            _cachedPayload = result.Payload.Clone();
            _cachedETag = currentETag;
            Log.CacheMiss(logger, envelope.CorrelationId, Id.GetId(), currentETag[..Math.Min(8, currentETag.Length)]);
        }
        else if (currentETag is null)
        {
            // Still discover projection type even when ETag is null
            if (result.Success
                && !string.IsNullOrWhiteSpace(result.ProjectionType)
                && IsValidProjectionType(result.ProjectionType)
                && _discoveredProjectionType is null)
            {
                _discoveredProjectionType = result.ProjectionType;
                Log.ProjectionTypeDiscovered(logger, envelope.CorrelationId, Id.GetId(), result.ProjectionType);
            }

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
    /// <remarks>
    /// Set <see cref="QueryResult.ProjectionType"/> on the returned <see cref="QueryResult"/> for optimal ETag caching.
    /// If <c>ProjectionType</c> is null, the actor falls back to <c>envelope.Domain</c> for ETag lookups —
    /// which is correct only when domain name equals projection type. Use the same short kebab-case name
    /// as your <c>IQueryContract.ProjectionType</c>.
    /// </remarks>
    protected abstract Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope);

    private string GetEffectiveProjectionType(string fallbackDomain)
        => _discoveredProjectionType ?? fallbackDomain;

    private static bool IsValidProjectionType(string projectionType)
        => projectionType.Length <= 100 && !projectionType.Contains(':');

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

        [LoggerMessage(
            EventId = 1074,
            Level = LogLevel.Debug,
            Message = "Projection type discovered: CorrelationId={CorrelationId}, ActorId={ActorId}, ProjectionType={ProjectionType}, Stage=ProjectionTypeDiscovered")]
        public static partial void ProjectionTypeDiscovered(ILogger logger, string correlationId, string actorId, string projectionType);

        [LoggerMessage(
            EventId = 1075,
            Level = LogLevel.Warning,
            Message = "Projection type mismatch: CorrelationId={CorrelationId}, ActorId={ActorId}, CachedProjectionType={CachedProjectionType}, NewProjectionType={NewProjectionType}, Stage=ProjectionTypeMismatch")]
        public static partial void ProjectionTypeMismatch(ILogger logger, string correlationId, string actorId, string cachedProjectionType, string newProjectionType);
    }
}
