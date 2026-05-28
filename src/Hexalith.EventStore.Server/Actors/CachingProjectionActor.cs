using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Abstract base class for projection actors with ETag-based in-memory caching (Gate 2).
/// Developers inherit and implement <see cref="ExecuteQueryAsync"/> with actual query logic.
/// Caching is automatic — no DAPR state store persistence for cache data.
/// </summary>
/// <remarks>
/// Optional: DAPR hosting projects may implement <see cref="IDaprProjectionActor"/> directly for non-cached projections.
/// </remarks>
public abstract partial class CachingProjectionActor(
    ActorHost host,
    IETagService eTagService,
    ILogger logger)
    : Actor(host), IDaprProjectionActor {
    // The actor instance is shared across QueryTypes for the same (projectionType, tenantId, entityId)
    // — see QueryActorIdHelper.DeriveActorId. The cache MUST therefore be keyed by (QueryType, Payload,
    // UserId) too, otherwise the first query to populate the cache poisons the response of every other
    // query type on the same actor.
    private const int MaxCacheEntries = 32;

    private readonly Dictionary<CacheEntryKey, byte[]> _payloadCache = [];
    private string? _cachedETag;
    private string? _discoveredProjectionType;

    /// <inheritdoc/>
    public Task<QueryResult> QueryAsync(QueryEnvelope envelope) =>
        QueryAsync(envelope, CancellationToken.None);

    /// <summary>
    /// Serves a projection query with an EventStore-owned cancellation boundary.
    /// </summary>
    /// <param name="envelope">The query envelope containing routing metadata and UTF-8 JSON payload bytes.</param>
    /// <param name="cancellationToken">The token to observe before cache, ETag, execution, and cache-store work.</param>
    /// <returns>The query result containing payload bytes or an adapter-edge failure.</returns>
    public async Task<QueryResult> QueryAsync(QueryEnvelope envelope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(envelope);

        // IETagService handles actor ID derivation, proxy timeout, and fail-open (returns null on error)
        string? currentETag = await eTagService
            .GetCurrentETagAsync(GetEffectiveProjectionType(envelope.Domain), envelope.TenantId, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // ETag changed → every cached entry is now stale (the underlying read model moved). Wipe
        // the whole map and track the new ETag as the validity stamp for subsequent stores.
        if (currentETag is not null && !string.Equals(currentETag, _cachedETag, StringComparison.Ordinal)) {
            _payloadCache.Clear();
            _cachedETag = currentETag;
        }

        var cacheKey = new CacheEntryKey(
            envelope.QueryType,
            QueryActorIdHelper.ComputeChecksum(envelope.Payload),
            envelope.UserId);

        // Cache hit: ETag is non-null AND we have an entry matching this (QueryType, Payload, UserId).
        if (currentETag is not null
            && _payloadCache.TryGetValue(cacheKey, out byte[]? cachedBytes)) {
            cancellationToken.ThrowIfCancellationRequested();
            Log.CacheHit(logger, envelope.CorrelationId, Id.GetId(), currentETag[..Math.Min(8, currentETag.Length)]);
            return new QueryResult(true, cachedBytes, ProjectionType: _discoveredProjectionType);
        }

        // Cache miss: execute the actual query
        cancellationToken.ThrowIfCancellationRequested();
        QueryResult result = await ExecuteQueryAsync(envelope, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (result.Success) {
            string? projectionType = ValidateProjectionTypeOrNull(envelope.CorrelationId, Id.GetId(), result.ProjectionType);
            if (!string.Equals(projectionType, result.ProjectionType, StringComparison.Ordinal)) {
                result = result with { ProjectionType = projectionType };
            }
        }

        if (result.Success && currentETag is not null) {
            // Runtime projection type discovery (FR63)
            if (!string.IsNullOrWhiteSpace(result.ProjectionType)
                && IsValidProjectionType(result.ProjectionType)) {
                if (_discoveredProjectionType is null) {
                    bool projectionTypeDiffersFromDomain =
                        !string.Equals(result.ProjectionType, envelope.Domain, StringComparison.Ordinal);

                    _discoveredProjectionType = result.ProjectionType;
                    Log.ProjectionTypeDiscovered(logger, envelope.CorrelationId, Id.GetId(), result.ProjectionType);

                    if (projectionTypeDiffersFromDomain) {
                        // ETag was fetched using envelope.Domain — may be wrong projection.
                        // Don't cache. Next request will use correct _discoveredProjectionType.
                        return result;
                    }
                }
                else if (!string.Equals(_discoveredProjectionType, result.ProjectionType, StringComparison.Ordinal)) {
                    Log.ProjectionTypeMismatch(logger, envelope.CorrelationId, Id.GetId(),
                        _discoveredProjectionType, result.ProjectionType);
                    // DO NOT update — first discovery wins until actor deactivation
                }
            }

            // Bound per-actor memory. Purge-on-overflow is the simplest correct policy: an actor
            // typically serves ~5 query types with a handful of cursor values; 32 covers that
            // headroom. ETag invalidation already ensures we never serve stale data after a wipe.
            if (_payloadCache.Count >= MaxCacheEntries) {
                _payloadCache.Clear();
            }

            cancellationToken.ThrowIfCancellationRequested();
            _payloadCache[cacheKey] = result.PayloadBytes!;
            Log.CacheMiss(logger, envelope.CorrelationId, Id.GetId(), currentETag[..Math.Min(8, currentETag.Length)]);
        }
        else if (currentETag is null) {
            // Still discover projection type even when ETag is null
            if (result.Success
                && !string.IsNullOrWhiteSpace(result.ProjectionType)
                && IsValidProjectionType(result.ProjectionType)
                && _discoveredProjectionType is null) {
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

    /// <summary>
    /// Executes the actual query logic against the domain service / read model with cancellation support.
    /// </summary>
    /// <param name="envelope">The query envelope containing routing and payload data.</param>
    /// <param name="cancellationToken">The token to pass to downstream query/state reads.</param>
    /// <returns>The query result from the domain service.</returns>
    protected virtual Task<QueryResult> ExecuteQueryAsync(QueryEnvelope envelope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return ExecuteQueryAsync(envelope);
    }

    private string GetEffectiveProjectionType(string fallbackDomain)
        => _discoveredProjectionType ?? fallbackDomain;

    private string? ValidateProjectionTypeOrNull(string correlationId, string actorId, string? projectionType) {
        if (projectionType is null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(projectionType)) {
            Log.InvalidProjectionType(logger, correlationId, actorId, projectionType, "empty or whitespace");
            return null;
        }

        if (!IsValidProjectionType(projectionType)) {
            string reason = projectionType.Contains(':', StringComparison.Ordinal)
                ? "contains ':'"
                : "exceeds 100 characters";

            Log.InvalidProjectionType(logger, correlationId, actorId, projectionType, reason);
            return null;
        }

        return projectionType;
    }

    private static bool IsValidProjectionType(string projectionType)
        => projectionType.Length <= 100 && !projectionType.Contains(':');

    private readonly record struct CacheEntryKey(string QueryType, string PayloadChecksum, string UserId);

    private static partial class Log {
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

        [LoggerMessage(
            EventId = 1076,
            Level = LogLevel.Warning,
            Message = "Projection type rejected. Falling back to envelope domain: CorrelationId={CorrelationId}, ActorId={ActorId}, ProjectionType={ProjectionType}, Reason={Reason}, Stage=ProjectionTypeRejected")]
        public static partial void InvalidProjectionType(ILogger logger, string correlationId, string actorId, string? projectionType, string reason);
    }
}
