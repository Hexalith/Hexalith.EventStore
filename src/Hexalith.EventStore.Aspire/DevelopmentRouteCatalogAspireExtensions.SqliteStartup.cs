using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Aspire;

public static partial class DevelopmentRouteCatalogAspireExtensions
{
    /// <summary>Orders genuine local sidecar initialization against one explicitly selected SQLite discovery registry.</summary>
    /// <remarks>The existing native resolver performs every migration and registration. This startup-only gate
    /// observes its initialization result; it does not grant authority or replace process/socket/membership verification.</remarks>
    /// <param name="builder">The local Development AppHost.</param>
    /// <param name="peers">The actual sidecar owners, in startup order.</param>
    /// <returns>The existing builder.</returns>
    public static IDistributedApplicationBuilder AddEventStoreDevelopmentSqliteStartupOrdering(
        this IDistributedApplicationBuilder builder, params IResourceBuilder<ProjectResource>[] peers)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(peers);
        if (builder.ExecutionContext.IsPublishMode || !builder.Environment.IsDevelopment()
            || peers.Length < 2 || peers.Any(peer => peer is null)
            || peers.Select(peer => peer.Resource.Name).Distinct(StringComparer.Ordinal).Count() != peers.Length)
            throw new InvalidOperationException("SQLite startup ordering requires distinct real local Development peers.");

        string? registry = null;
        List<(ProjectResource Owner, string ResourceName, string AppId, string HealthKey)> selected = [];
        foreach (var peer in peers)
        {
            if (!builder.Resources.Contains(peer.Resource))
                throw new InvalidOperationException("A selected SQLite peer does not belong to this AppHost.");
            var options = Sidecar(peer.Resource).Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            var resolver = ReadYamlOrJson(options.Config ?? throw new InvalidOperationException("An explicit discovery configuration is required."))
                ["spec"]?["nameResolution"];
            string? connection = resolver?["configuration"]?["connectionString"]?.GetValue<string>();
            if (resolver?["component"]?.GetValue<string>() != "sqlite" || resolver?["version"]?.GetValue<string>() != "v1"
                || string.IsNullOrWhiteSpace(connection) || registry is not null && registry != connection)
                throw new InvalidOperationException("The selected peers must use the same explicit SQLite v1 discovery registry.");
            registry = connection;
            selected.Add((peer.Resource, peer.Resource.Name + "-dapr-cli", AppId(peer.Resource), "sqlite-discovery-startup-" + peer.Resource.Name));
        }
        if (selected.Select(peer => peer.AppId).Distinct(StringComparer.Ordinal).Count() != peers.Length)
            throw new InvalidOperationException("SQLite startup ordering requires distinct actual Dapr application identities.");

        foreach (var peer in selected)
        {
            builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(peer.HealthKey,
                services => new DaprSqliteDiscoveryInitializationHealthCheck(
                    services.GetRequiredService<ResourceLoggerService>(), services.GetRequiredService<DistributedApplicationModel>(),
                    peer.ResourceName, peer.AppId), HealthStatus.Unhealthy, tags: []));
        }
        builder.Services.AddSingleton<IDistributedApplicationEventingSubscriber>(
            new DevelopmentSqliteDiscoveryStartupSubscriber(builder, selected.Select(peer => (peer.Owner, peer.ResourceName, peer.HealthKey)).ToArray()));
        return builder;
    }
}
