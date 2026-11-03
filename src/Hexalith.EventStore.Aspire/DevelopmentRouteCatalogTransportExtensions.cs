using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Aspire;

/// <summary>Explicit Development transport enrollment before catalog composition and Build.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public static class DevelopmentRouteCatalogTransportExtensions
{
    /// <summary>Starts the owner-controlled isolated native control plane for a Development run.</summary>
    public static Task<DevelopmentRouteCatalogTransport> AddEventStoreDevelopmentRouteCatalogTransportAsync(
        this IDistributedApplicationBuilder builder, DevelopmentRouteCatalogTransportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!builder.ExecutionContext.IsRunMode || !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("Owned route catalog transport requires an explicit Development run.");
        return DevelopmentRouteCatalogTransport.StartAsync(options, builder.Environment.EnvironmentName, cancellationToken);
    }

    /// <summary>Preserves ACLs and application credentials while binding every selected native sidecar to owned trust.</summary>
    public static void WithEventStoreDevelopmentRouteCatalogTransport(this IResourceBuilder<ProjectResource> project,
        DevelopmentRouteCatalogTransport transport)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(transport);
        var sidecar = project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        var annotation = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
        string configuration = transport.ConfigureReceiver(annotation.Options.Config
            ?? throw new InvalidOperationException("An actual closed receiver configuration is required."), project.Resource.Name);
        sidecar.Annotations[sidecar.Annotations.IndexOf(annotation)] = new DaprSidecarOptionsAnnotation(annotation.Options with
        {
            Config = configuration, RuntimePath = transport.RuntimeDirectory,
            PlacementHostAddress = transport.PlacementAddress, SchedulerHostAddress = transport.SchedulerAddress,
            DaprListenAddresses = "127.0.0.1",
        });
        var credentials = new[] { "DAPR_TRUST_ANCHORS", "DAPR_CERT_CHAIN", "DAPR_CERT_KEY" }.ToDictionary(name => name,
            name => project.ApplicationBuilder.AddParameter("transport-" + project.Resource.Name + "-" + name.ToLowerInvariant().Replace('_', '-'),
                () => transport.ReadSidecarEnvironment()[name], secret: true));
        sidecar.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            _ = transport.ReadSidecarEnvironment();
            foreach (var pair in credentials) context.EnvironmentVariables[pair.Key] = pair.Value.Resource;
            context.EnvironmentVariables["NAMESPACE"] = "default";
        }));
    }
}
