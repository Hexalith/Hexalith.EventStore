using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Opt-in registration seam for the safe-denial query-routing boundary. Calling this extension is
/// the only way the boundary is applied — without it, Forbidden and not-found results keep their
/// existing, distinguishable behavior for every route.
/// </summary>
public static class SafeDenialQueryRoutingServiceCollectionExtensions {
    /// <summary>
    /// Wraps the currently registered <see cref="IQueryRouter"/> with <see cref="SafeDenialQueryRouter"/>,
    /// applying the safe-denial boundary only to the supplied (domain, query type) routes. Must be
    /// called after the base <see cref="IQueryRouter"/> registration (e.g. <c>AddEventStoreServer</c>,
    /// and <c>AddEventStoreDomainQueryRouting</c> when used) so the decorator wraps the fully
    /// composed router.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="routes">The (domain, query type) routes that opt into the safe-denial boundary.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddEventStoreSafeDenialQueryRouting(
        this IServiceCollection services,
        IEnumerable<(string Domain, string QueryType)> routes) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(routes);

        (string Domain, string QueryType)[] routeSnapshot = [.. routes];

        ServiceDescriptor? existing = services.LastOrDefault(d => d.ServiceType == typeof(IQueryRouter));
        if (existing is null) {
            throw new InvalidOperationException(
                "AddEventStoreSafeDenialQueryRouting must be called after an IQueryRouter registration (e.g. AddEventStoreServer).");
        }

        // A second (or later) call to this extension must merge its route list into whatever was
        // registered by an earlier call, not silently drop it: TryAddSingleton alone would leave
        // the first call's registry -- and its narrower route set -- in place for every
        // subsequent call, so those later routes would never actually opt into the boundary.
        // A prior ISafeDenialQueryRoutePolicy registration that is not a SafeDenialQueryRouteRegistry
        // (i.e. a caller registered a custom policy directly, not through this extension) cannot be
        // merged at all -- silently discarding it would drop whatever authorization decision that
        // custom policy made. Fail loudly instead of guessing.
        ServiceDescriptor? existingPolicy = services.LastOrDefault(d => d.ServiceType == typeof(ISafeDenialQueryRoutePolicy));
        IEnumerable<(string Domain, string QueryType)> mergedRoutes;
        if (existingPolicy is null) {
            mergedRoutes = routeSnapshot;
        } else if (existingPolicy.ImplementationInstance is SafeDenialQueryRouteRegistry existingRegistry) {
            mergedRoutes = [.. existingRegistry.Routes, .. routeSnapshot];
        } else {
            throw new InvalidOperationException(
                "AddEventStoreSafeDenialQueryRouting found an existing ISafeDenialQueryRoutePolicy registration " +
                "that was not created by a prior call to this extension (e.g. a custom policy registered " +
                "directly via services.AddSingleton<ISafeDenialQueryRoutePolicy>(...)). Merging routes into an " +
                "unknown policy implementation could silently discard its authorization decisions, so this is " +
                "rejected outright: either supply this extension's routes as the only ISafeDenialQueryRoutePolicy " +
                "registration, or compose the custom policy yourself instead of calling this extension.");
        }

        // Flatten repeated calls onto a single SafeDenialQueryRouter layer wrapping the true
        // original inner router. Without this, "existing" on a second call would already be the
        // prior call's own SafeDenialQueryRouter registration, and wrapping it again would nest
        // another decorator layer around the previous one on every call instead of replacing it --
        // behaviorally harmless (the inner layer resolves the same, most-recently-merged policy at
        // call time) but an ever-growing, wasteful decorator chain. A private marker registration
        // remembers the true original registration across calls so every call rewraps it directly.
        ServiceDescriptor? markerDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(OriginalInnerRouterMarker));
        ServiceDescriptor trueOriginal;
        if (markerDescriptor?.ImplementationInstance is OriginalInnerRouterMarker marker) {
            trueOriginal = marker.Descriptor;
        } else {
            trueOriginal = existing;
            services.Add(new ServiceDescriptor(typeof(OriginalInnerRouterMarker), new OriginalInnerRouterMarker(trueOriginal)));
        }

        _ = services.Remove(existing);
        if (existingPolicy is not null) {
            _ = services.Remove(existingPolicy);
        }

        services.AddSingleton<ISafeDenialQueryRoutePolicy>(new SafeDenialQueryRouteRegistry(mergedRoutes));

        // Idempotent via TryAddEnumerable inside AddHostedService: safe to call on every
        // registration, logs the final merged route list exactly once at host startup.
        services.AddHostedService<SafeDenialQueryRouteStartupLogger>();

        services.Add(ServiceDescriptor.Describe(
            typeof(IQueryRouter),
            serviceProvider => new SafeDenialQueryRouter(
                (IQueryRouter)ResolveInner(trueOriginal, serviceProvider),
                serviceProvider.GetRequiredService<ISafeDenialQueryRoutePolicy>(),
                serviceProvider.GetRequiredService<ILogger<SafeDenialQueryRouter>>()),
            trueOriginal.Lifetime));

        return services;
    }

    // Poor-man's decorator: instantiate whatever was previously registered for IQueryRouter
    // (instance, factory, or implementation type) so this extension composes correctly whether
    // the prior registration is the plain projection-actor QueryRouter or an existing decorator
    // chain (e.g. HandlerAwareQueryRouter).
    private static object ResolveInner(ServiceDescriptor descriptor, IServiceProvider serviceProvider) {
        if (descriptor.ImplementationInstance is not null) {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null) {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        return ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);
    }

    // Private DI-service-type marker used purely as bookkeeping: it is never resolved by
    // application code, only looked up by this extension itself to recover the true original
    // IQueryRouter registration across repeated AddEventStoreSafeDenialQueryRouting calls so
    // decorator nesting can be flattened instead of growing with every call.
    private sealed class OriginalInnerRouterMarker(ServiceDescriptor descriptor) {
        public ServiceDescriptor Descriptor { get; } = descriptor;
    }
}
