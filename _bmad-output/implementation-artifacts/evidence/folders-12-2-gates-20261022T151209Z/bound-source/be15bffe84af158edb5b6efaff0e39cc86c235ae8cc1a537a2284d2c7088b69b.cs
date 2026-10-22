using System.Runtime.CompilerServices;

using Hexalith.EventStore.Indexes;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>
/// Provides targeted service-registration overrides for in-process test hosts.
/// </summary>
internal static class WebApplicationFactoryServiceOverrides {
    /// <summary>
    /// Prevents the operational index from starting in tests that intentionally run without a Dapr sidecar.
    /// </summary>
    /// <param name="services">The application service registrations.</param>
    internal static void RemoveAdminOperationalIndexHostedService(IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);

        ServiceDescriptor singleton = GetSingleDescriptor(
            services,
            static descriptor => descriptor.ServiceType == typeof(AdminOperationalIndexHostedService)
                && descriptor.Lifetime == ServiceLifetime.Singleton
                && descriptor.ImplementationType == typeof(AdminOperationalIndexHostedService),
            "concrete AdminOperationalIndexHostedService singleton");
        int singletonIndex = services.IndexOf(singleton);
        if (singletonIndex + 2 >= services.Count) {
            throw UnexpectedLayout("the refresher and hosted aliases are missing after the concrete singleton");
        }

        ServiceDescriptor refresherAlias = services[singletonIndex + 1];
        ValidateFactoryAlias(
            refresherAlias,
            typeof(INamedProjectionCatalogRefresher),
            "the INamedProjectionCatalogRefresher alias");

        ServiceDescriptor hostedService = services[singletonIndex + 2];
        ValidateFactoryAlias(hostedService, typeof(IHostedService), "the IHostedService alias");

        var probe = (AdminOperationalIndexHostedService)RuntimeHelpers.GetUninitializedObject(
            typeof(AdminOperationalIndexHostedService));
        var probeProvider = new AdminOperationalIndexProbeServiceProvider(probe);
        ValidateFactoryResult(refresherAlias, probeProvider, probe, "the refresher alias");
        ValidateFactoryResult(hostedService, probeProvider, probe, "the hosted alias");

        _ = services.Remove(hostedService);
    }

    private static ServiceDescriptor GetSingleDescriptor(
        IServiceCollection services,
        Func<ServiceDescriptor, bool> predicate,
        string description) {
        ServiceDescriptor[] matches = [.. services.Where(predicate)];
        if (matches.Length != 1) {
            throw UnexpectedLayout($"expected exactly one {description}, but found {matches.Length}");
        }

        return matches[0];
    }

    private static void ValidateFactoryAlias(
        ServiceDescriptor descriptor,
        Type expectedServiceType,
        string description) {
        if (descriptor.ServiceType != expectedServiceType
            || descriptor.Lifetime != ServiceLifetime.Singleton
            || descriptor.ImplementationFactory is null) {
            throw UnexpectedLayout($"expected {description} immediately after the concrete singleton");
        }
    }

    private static void ValidateFactoryResult(
        ServiceDescriptor descriptor,
        IServiceProvider probeProvider,
        AdminOperationalIndexHostedService probe,
        string description) {
        object? result;
        try {
            result = descriptor.ImplementationFactory!(probeProvider);
        }
        catch (Exception exception) {
            throw UnexpectedLayout($"{description} does not resolve AdminOperationalIndexHostedService", exception);
        }

        if (!ReferenceEquals(result, probe)) {
            throw UnexpectedLayout($"{description} does not resolve AdminOperationalIndexHostedService");
        }
    }

    private static InvalidOperationException UnexpectedLayout(string detail, Exception? innerException = null) =>
        new($"Unexpected AdminOperationalIndexHostedService registration layout: {detail}.", innerException);

    private sealed class AdminOperationalIndexProbeServiceProvider(AdminOperationalIndexHostedService service)
        : IServiceProvider {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(AdminOperationalIndexHostedService) ? service : null;
    }
}
