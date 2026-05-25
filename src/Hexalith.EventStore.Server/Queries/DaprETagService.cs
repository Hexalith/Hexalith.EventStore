
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
        cancellationToken.ThrowIfCancellationRequested();

        string actorId = $"{projectionType}:{tenantId}";
        try {
            // Invoke through the strongly-typed IETagActor (remoting) interface. We must NOT cast
            // to the base ActorProxy and call the weakly-typed InvokeMethodAsync<T>(method, ct):
            // that is the *non-remoting* invocation path, and on a proxy built by the remoting
            // CreateActorProxy<IETagActor> overload its non-remoting interactor is null, so the
            // call throws NullReferenceException inside Dapr.Actors.Client.ActorProxy.InvokeMethodAsync
            // (which the fail-open catch below then silently turned into a null ETag on every fetch).
            // Cancellation is honoured by the pre-checks plus the 3 s RequestTimeout in _proxyOptions;
            // the remoting interface intentionally exposes no per-call CancellationToken.
            // See sprint-change-proposal-2026-05-25-etag-actor-proxy-nre.md.
            IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
                new ActorId(actorId), ETagActor.ETagActorTypeName, _proxyOptions);
            cancellationToken.ThrowIfCancellationRequested();
            return await proxy.GetCurrentETagAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            // Bare OperationCanceledException is rethrown so cancellation remains distinguishable from
            // adapter-edge failures (AC9). DAPR can wrap OCE inside ActorMethodInvocationException or
            // RpcException; those wrapped cases fall through to the generic catch below and the
            // documented fail-open path (return null).
            throw;
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
