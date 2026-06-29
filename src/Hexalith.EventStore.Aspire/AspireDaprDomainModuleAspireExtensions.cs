using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

using System.Collections.Immutable;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Domain-neutral Aspire and DAPR hosting helpers for Hexalith domain modules.
/// </summary>
public static class AspireDaprDomainModuleAspireExtensions {
    /// <summary>
    /// Attaches project references, wait ordering, and a DAPR sidecar to a domain-module project resource.
    /// </summary>
    /// <param name="project">The domain-module project resource.</param>
    /// <param name="options">The domain-module hosting options.</param>
    /// <returns>A resource record describing the composed domain module.</returns>
    public static AspireDaprDomainModuleResource AddAspireDaprDomainModule(
        this IResourceBuilder<ProjectResource> project,
        AspireDaprDomainModuleOptions options) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AppId);

        if (options.InfrastructureMode == AspireDaprInfrastructureMode.Shared && options.SharedComponents is null) {
            throw new InvalidOperationException("Shared DAPR infrastructure mode requires shared state-store and pub/sub components.");
        }

        foreach (IResourceBuilder<ProjectResource> reference in options.References) {
            ArgumentNullException.ThrowIfNull(reference);
            _ = project.WithReference(reference);
        }

        foreach (IResourceBuilder<ProjectResource> waitFor in options.WaitFor) {
            ArgumentNullException.ThrowIfNull(waitFor);
            _ = project.WaitFor(waitFor);
        }

        _ = project.WithDaprSidecar(sidecar => {
            IResourceBuilder<IDaprSidecarResource> configured = sidecar.WithOptions(CreateSidecarOptions(options));
            if (options.InfrastructureMode == AspireDaprInfrastructureMode.Shared) {
                configured = configured
                    .WithReference(options.SharedComponents!.StateStore)
                    .WithReference(options.SharedComponents.PubSub);
            }
        });

        return new AspireDaprDomainModuleResource(project, options.AppId, options.InfrastructureMode);
    }

    private static DaprSidecarOptions CreateSidecarOptions(AspireDaprDomainModuleOptions options) {
        HashSet<string> resourcesPaths = new(
            options.ResourcesPaths.Where(static path => !string.IsNullOrWhiteSpace(path)),
            StringComparer.Ordinal);

        return new DaprSidecarOptions {
            AppId = options.AppId,
            Config = options.Config,
            ResourcesPaths = resourcesPaths.ToImmutableHashSet(StringComparer.Ordinal),
            AppHealthCheckPath = options.AppHealthCheckPath,
            EnableAppHealthCheck = options.EnableAppHealthCheck,
            PlacementHostAddress = options.PlacementHostAddress,
            SchedulerHostAddress = options.SchedulerHostAddress,
            DaprHttpPort = options.DaprHttpPort,
        };
    }
}
