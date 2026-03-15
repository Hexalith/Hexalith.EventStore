
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// DAPR actor-based implementation of <see cref="IETagService"/>.
/// Wraps ETag actor proxy creation with fail-open error handling.
/// </summary>
public partial class DaprETagService(
    IActorProxyFactory actorProxyFactory,
    ILogger<DaprETagService> logger) : IETagService {
    private static readonly ActorProxyOptions _proxyOptions = new() {
        RequestTimeout = TimeSpan.FromSeconds(3),
    };

    /// <inheritdoc/>
    public async Task<string?> GetCurrentETagAsync(
        string projectionType, string tenantId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionType);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        string actorId = $"{projectionType}:{tenantId}";
        try {
            IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
                new ActorId(actorId), ETagActor.ETagActorTypeName, _proxyOptions);
            return await proxy.GetCurrentETagAsync().ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ETagFetchFailed(logger, actorId, ex.GetType().Name);
            return null;
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1061,
            Level = LogLevel.Warning,
            Message = "ETag actor fetch failed: ActorId={ActorId}, ExceptionType={ExceptionType}. Proceeding without ETag (fail-open).")]
        public static partial void ETagFetchFailed(ILogger logger, string actorId, string exceptionType);
    }
}
