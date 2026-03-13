
using Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// DAPR actor that tracks the current ETag for a projection+tenant pair.
/// Actor ID format: "{ProjectionType}:{TenantId}".
/// ~30-40 lines of production logic per FP-1.
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
        string newETag = GenerateETag();

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
                _currentETag = result.Value;
                Log.StateLoaded(logger, Host.Id.GetId(), true);
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
    /// Generates a base64url-encoded GUID (22 chars, FM-4).
    /// </summary>
    internal static string GenerateETag()
    {
        byte[] bytes = Guid.NewGuid().ToByteArray();
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
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
    }
}
