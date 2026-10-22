using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// One-shot startup service that logs the (domain, query type) routes registered via
/// <see cref="SafeDenialQueryRoutingServiceCollectionExtensions.AddEventStoreSafeDenialQueryRouting"/>.
/// </summary>
/// <remarks>
/// Route registration happens purely inside an <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>
/// extension, before any <c>ILogger</c> exists -- a typo'd domain/query type would otherwise
/// silently receive no safe-denial protection with no operator-visible signal anywhere. Logging
/// once here, after the host has started and the fully composed <see cref="ISafeDenialQueryRoutePolicy"/>
/// is resolvable, closes that visibility gap.
/// </remarks>
internal sealed partial class SafeDenialQueryRouteStartupLogger(
    ISafeDenialQueryRoutePolicy policy,
    ILogger<SafeDenialQueryRouteStartupLogger> logger) : IHostedService {
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) {
        if (policy is SafeDenialQueryRouteRegistry registry) {
            foreach ((string Domain, string QueryType) route in registry.Routes) {
                Log.SafeDenialRouteRegistered(logger, route.Domain, route.QueryType);
            }

            Log.SafeDenialRouteCount(logger, registry.Routes.Count);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static partial class Log {
        [LoggerMessage(
            EventId = 1221,
            Level = LogLevel.Information,
            Message = "Safe-denial query route registered: Domain={Domain}, QueryType={QueryType}, Stage=SafeDenialRouteRegistered")]
        public static partial void SafeDenialRouteRegistered(ILogger logger, string domain, string queryType);

        [LoggerMessage(
            EventId = 1222,
            Level = LogLevel.Information,
            Message = "Safe-denial query routing configured with {RouteCount} opted-in route(s): Stage=SafeDenialRouteCount")]
        public static partial void SafeDenialRouteCount(ILogger logger, int routeCount);
    }
}
