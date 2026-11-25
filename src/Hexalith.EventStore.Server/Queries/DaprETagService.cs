
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// DAPR actor-based implementation of <see cref="IETagService"/>.
/// Wraps ETag actor proxy creation with fail-open error handling.
/// </summary>
public partial class DaprETagService : IETagService {
    private readonly Func<IActorProxyFactory> _getActorProxyFactory;
    private readonly ILogger<DaprETagService> _logger;
    private readonly TimeSpan _requestTimeout;

    /// <summary>Creates a service that reads ETags using the supplied Dapr actor proxy factory.</summary>
    /// <param name="actorProxyFactory">The actor proxy factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="requestTimeout">The override for the default three-second request timeout.</param>
    public DaprETagService(
        IActorProxyFactory actorProxyFactory,
        ILogger<DaprETagService> logger,
        TimeSpan? requestTimeout = null)
        : this(() => actorProxyFactory, logger, requestTimeout) {
    }

    /// <summary>Creates a service that resolves its proxy factory within the fail-open read.</summary>
    /// <param name="getActorProxyFactory">Resolves the configured actor proxy factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="requestTimeout">The override for the default three-second request timeout.</param>
    internal DaprETagService(
        Func<IActorProxyFactory> getActorProxyFactory,
        ILogger<DaprETagService> logger,
        TimeSpan? requestTimeout = null) {
        _getActorProxyFactory = getActorProxyFactory;
        _logger = logger;

        // Keep the default bounded and allow live-sidecar tests to accommodate cold activation.
        // HttpClient cancels on whole milliseconds, so reject sub-millisecond and infinite values.
        _requestTimeout = requestTimeout switch {
            null => TimeSpan.FromSeconds(3),
            TimeSpan timeout when timeout >= TimeSpan.FromMilliseconds(1) && timeout <= TimeSpan.FromMinutes(5) => timeout,
            _ => throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                requestTimeout,
                "The ETag request timeout must be at least 1 millisecond and at most 5 minutes."),
        };
    }

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
            // Cancellation is honoured by the pre-checks plus the bounded proxy request timeout;
            // the remoting interface intentionally exposes no per-call CancellationToken.
            // See sprint-change-proposal-2026-05-25-etag-actor-proxy-nre.md.
            IActorProxyFactory actorProxyFactory = _getActorProxyFactory();
            IETagActor proxy = actorProxyFactory.CreateActorProxy<IETagActor>(
                new ActorId(actorId), ETagActor.ETagActorTypeName, CreateProxyOptions(actorProxyFactory));
            cancellationToken.ThrowIfCancellationRequested();
            string? etag = await proxy.GetCurrentETagAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return etag;
        }
        catch (OperationCanceledException ex)
            when (cancellationToken.IsCancellationRequested || ex.InnerException is not TimeoutException) {
            // HttpClient wraps its own request timeout in an OperationCanceledException. Preserve
            // caller cancellation and other bare cancellation exceptions as a separate outcome.
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (Exception ex) {
            Log.ETagFetchFailed(_logger, actorId, ex.GetType().Name);
            return null;
        }
    }

    private ActorProxyOptions CreateProxyOptions(IActorProxyFactory actorProxyFactory) {
        // Dapr replaces its defaults whenever explicit options are supplied. Copy the defaults
        // exposed by the factory registered by AddActors without changing its shared instance.
        var options = new ActorProxyOptions { RequestTimeout = _requestTimeout };
        if (actorProxyFactory is ActorProxyFactory factory) {
            ActorProxyOptions defaults = factory.DefaultOptions;
            options.HttpEndpoint = defaults.HttpEndpoint;
            options.DaprApiToken = defaults.DaprApiToken;
            options.JsonSerializerOptions = defaults.JsonSerializerOptions;
            options.UseJsonSerialization = defaults.UseJsonSerialization;
        }

        return options;
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1061,
            Level = LogLevel.Warning,
            Message = "ETag actor fetch failed: ActorId={ActorId}, ExceptionType={ExceptionType}. Proceeding without ETag (fail-open).")]
        public static partial void ETagFetchFailed(ILogger logger, string actorId, string exceptionType);
    }
}
