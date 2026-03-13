
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Queries;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor that tracks the current ETag for a projection+tenant pair.
/// Actor ID format: "{ProjectionType}:{TenantId}".
/// ETag value format: "{base64url(projectionType)}.{base64url-guid}" (self-routing).
/// </summary>
public partial class ETagActor(ActorHost host, ILogger<ETagActor> logger)
    : Actor(host), IETagActor
{
    /// <summary>
    /// The actor type name used for DAPR actor registration.
    /// </summary>
    public const string ETagActorTypeName = "ETagActor";

    private const string ETagStateKey = "etag";

    private string? _currentETag;

    /// <inheritdoc/>
    public Task<string?> GetCurrentETagAsync() => Task.FromResult(_currentETag);

    /// <inheritdoc/>
    public async Task<string> RegenerateAsync()
    {
        string projectionType = ExtractProjectionType();
        string newETag = SelfRoutingETag.GenerateNew(projectionType);

        await StateManager.SetStateAsync(ETagStateKey, newETag).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        // Cache only after successful persistence (FM-1)
        _currentETag = newETag;

        Log.ETagRegenerated(logger, Host.Id.GetId(), newETag[..Math.Min(8, newETag.Length)]);

        return newETag;
    }

    /// <inheritdoc/>
    protected override async Task OnActivateAsync()
    {
        try
        {
            Dapr.Actors.Runtime.ConditionalValue<string> result =
                await StateManager.TryGetStateAsync<string>(ETagStateKey).ConfigureAwait(false);

            if (result.HasValue && !string.IsNullOrEmpty(result.Value))
            {
                // Detect old-format ETag (no '.' separator) and migrate to self-routing format
                if (!result.Value.Contains('.'))
                {
                    Log.OldFormatDetected(logger, Host.Id.GetId());
                    await MigrateToSelfRoutingFormatAsync().ConfigureAwait(false);
                }
                else
                {
                    _currentETag = result.Value;
                    Log.StateLoaded(logger, Host.Id.GetId(), true);
                }
            }
            else
            {
                _currentETag = null;
                Log.ColdStartDetected(logger, Host.Id.GetId());
            }
        }
        catch (Exception ex)
        {
            // FM-2: Fall back to cold start on state read failure
            _currentETag = null;
            Log.StateLoadFailed(logger, Host.Id.GetId(), ex.GetType().Name);
        }
    }

    /// <summary>
    /// Extracts the projection type from the actor ID.
    /// Actor ID format: "{ProjectionType}:{TenantId}".
    /// </summary>
    private string ExtractProjectionType()
    {
        string actorIdStr = Host.Id.GetId();
        int colonIndex = actorIdStr.IndexOf(':');
        return actorIdStr[..colonIndex];
    }

    /// <summary>
    /// Migrates an old-format ETag to self-routing format.
    /// Uses the same persist-then-cache pattern as <see cref="RegenerateAsync"/>.
    /// </summary>
    private async Task MigrateToSelfRoutingFormatAsync()
    {
        try
        {
            string projectionType = ExtractProjectionType();
            string newETag = SelfRoutingETag.GenerateNew(projectionType);

            await StateManager.SetStateAsync(ETagStateKey, newETag).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);

            _currentETag = newETag;
            Log.ETagMigrated(logger, Host.Id.GetId(), newETag[..Math.Min(8, newETag.Length)]);
        }
        catch (Exception ex)
        {
            // Safe degradation: if migration fails, fall back to cold start
            _currentETag = null;
            Log.MigrationFailed(logger, Host.Id.GetId(), ex.GetType().Name);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1050,
            Level = LogLevel.Information,
            Message = "ETag regenerated for actor {ActorId}. New ETag prefix: {ETagPrefix}")]
        public static partial void ETagRegenerated(ILogger logger, string actorId, string eTagPrefix);

        [LoggerMessage(
            EventId = 1052,
            Level = LogLevel.Debug,
            Message = "ETag state loaded on activation for actor {ActorId}. HasExistingETag: {HasExistingETag}")]
        public static partial void StateLoaded(ILogger logger, string actorId, bool hasExistingETag);

        [LoggerMessage(
            EventId = 1053,
            Level = LogLevel.Information,
            Message = "Cold start detected for ETag actor {ActorId}. No prior state found.")]
        public static partial void ColdStartDetected(ILogger logger, string actorId);

        [LoggerMessage(
            EventId = 1054,
            Level = LogLevel.Warning,
            Message = "State load failure for ETag actor {ActorId}. ExceptionType: {ExceptionType}. Falling back to cold start.")]
        public static partial void StateLoadFailed(ILogger logger, string actorId, string exceptionType);

        [LoggerMessage(
            EventId = 1055,
            Level = LogLevel.Information,
            Message = "Old-format ETag detected for actor {ActorId}. Migrating to self-routing format.")]
        public static partial void OldFormatDetected(ILogger logger, string actorId);

        [LoggerMessage(
            EventId = 1056,
            Level = LogLevel.Information,
            Message = "ETag migrated to self-routing format for actor {ActorId}. New ETag prefix: {ETagPrefix}")]
        public static partial void ETagMigrated(ILogger logger, string actorId, string eTagPrefix);

        [LoggerMessage(
            EventId = 1057,
            Level = LogLevel.Warning,
            Message = "ETag migration failed for actor {ActorId}. ExceptionType: {ExceptionType}. Falling back to cold start.")]
        public static partial void MigrationFailed(ILogger logger, string actorId, string exceptionType);
    }
}
