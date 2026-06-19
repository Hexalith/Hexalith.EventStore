using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Provides extension methods for adding the Hexalith EventStore topology
/// to an Aspire distributed application.
/// </summary>
public static class HexalithEventStoreExtensions {
    /// <summary>
    /// Adds the Hexalith EventStore topology to the distributed application builder.
    /// This configures DAPR state store (Redis-backed with actor support), DAPR pub/sub,
    /// and wires the EventStore and Admin.Server services with DAPR sidecars.
    /// Redis is provided externally by <c>dapr init</c> at localhost:6379.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="eventStore">The EventStore project resource builder.</param>
    /// <param name="adminServer">The Admin.Server.Host project resource builder.</param>
    /// <param name="adminUI">Optional Admin.UI project resource builder.</param>
    /// <param name="eventStoreDaprConfigPath">
    /// Path to the Dapr access control configuration file loaded by the EventStore sidecar.
    /// This config governs incoming invocations to EventStore.
    /// </param>
    /// <param name="adminServerDaprConfigPath">
    /// Path to the Dapr access control configuration file loaded by the Admin.Server sidecar.
    /// This config governs incoming invocations to Admin.Server.
    /// </param>
    /// <param name="resiliencyConfigPath">
    /// Absolute path to the DAPR resiliency YAML file (typically <c>DaprComponents/resiliency.yaml</c>).
    /// When provided, the path is injected into the Admin.Server as the
    /// <c>AdminServer__ResiliencyConfigPath</c> environment variable so the resiliency viewer
    /// page (<c>/dapr/resiliency</c>) can load and display the active policies without requiring
    /// any manual <c>appsettings.json</c> entry. Should be an absolute path produced by the
    /// AppHost's <c>ResolveDaprConfigPath</c> helper. <c>null</c> leaves the env var unset and
    /// the resiliency page falls back to its "configuration not available" empty state.
    /// Note: this env var (and the localhost-based <c>EventStoreDaprHttpEndpoint</c> /
    /// <c>TraceUrl</c> / <c>MetricsUrl</c> / <c>LogsUrl</c> Aspire-dashboard URLs) is only
    /// injected when the AppHost is in <i>run mode</i>. In <i>publish mode</i> (aspirate,
    /// Aspire publishers) these are skipped so they do not leak host-resolved paths or
    /// dashboard URLs into generated K8s ConfigMaps; operators wire the production values
    /// via their kustomize overlay / Helm values / appsettings.
    /// </param>
    /// <param name="eventStoreDaprHttpPort">
    /// DAPR HTTP port for the EventStore sidecar. Defaults to 3501. This port MUST be free on the host at
    /// startup — DAPR does not error on port conflicts, it silently binds to a different port, which breaks
    /// cross-sidecar metadata queries from Admin.Server. Override this parameter if 3501 is occupied (e.g.,
    /// by a prior daprd process or another DAPR app). Diagnostic: on Windows, run
    /// <c>netstat -ano | findstr :3501</c> before <c>aspire run</c>.
    /// </param>
    /// <param name="daprPlacementHostAddress">
    /// Optional DAPR placement service address, formatted as <c>host</c> or <c>host:port</c>.
    /// Leave <c>null</c> to use the DAPR CLI default.
    /// </param>
    /// <param name="daprSchedulerHostAddress">
    /// Optional DAPR scheduler service address, formatted as <c>host</c> or <c>host:port</c>.
    /// Leave <c>null</c> to use the DAPR CLI default.
    /// </param>
    /// <returns>A <see cref="HexalithEventStoreResources"/> containing the resource builders for further customization.</returns>
    public static HexalithEventStoreResources AddHexalithEventStore(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> eventStore,
        IResourceBuilder<ProjectResource> adminServer,
        IResourceBuilder<ProjectResource>? adminUI = null,
        string? eventStoreDaprConfigPath = null,
        string? adminServerDaprConfigPath = null,
        string? resiliencyConfigPath = null,
        int eventStoreDaprHttpPort = 3501,
        string? daprPlacementHostAddress = null,
        string? daprSchedulerHostAddress = null)
        => AddHexalithEventStore(
            builder,
            eventStore,
            adminServer,
            adminUI,
            eventStoreDaprConfigPath,
            adminServerDaprConfigPath,
            resiliencyConfigPath,
            stateStoreComponentPath: null,
            eventStoreDaprHttpPort: eventStoreDaprHttpPort,
            daprPlacementHostAddress: daprPlacementHostAddress,
            daprSchedulerHostAddress: daprSchedulerHostAddress);

    /// <summary>
    /// Adds the Hexalith EventStore topology to the distributed application builder.
    /// This overload allows the AppHost to provide an isolated DAPR component YAML for
    /// the local state store while preserving the original public overload for consumers.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="eventStore">The EventStore project resource builder.</param>
    /// <param name="adminServer">The Admin.Server.Host project resource builder.</param>
    /// <param name="adminUI">Optional Admin.UI project resource builder.</param>
    /// <param name="eventStoreDaprConfigPath">Path to the Dapr access control configuration file loaded by the EventStore sidecar.</param>
    /// <param name="adminServerDaprConfigPath">Path to the Dapr access control configuration file loaded by the Admin.Server sidecar.</param>
    /// <param name="resiliencyConfigPath">Absolute path to the DAPR resiliency YAML file.</param>
    /// <param name="stateStoreComponentPath">
    /// Absolute path to an isolated DAPR state-store component YAML. When provided, this file is the
    /// source of truth for the local state-store metadata loaded by the DAPR sidecars.
    /// </param>
    /// <param name="eventStoreDaprHttpPort">DAPR HTTP port for the EventStore sidecar. Defaults to 3501.</param>
    /// <param name="daprPlacementHostAddress">
    /// Optional DAPR placement service address, formatted as <c>host</c> or <c>host:port</c>.
    /// Leave <c>null</c> to use the DAPR CLI default.
    /// </param>
    /// <param name="daprSchedulerHostAddress">
    /// Optional DAPR scheduler service address, formatted as <c>host</c> or <c>host:port</c>.
    /// Leave <c>null</c> to use the DAPR CLI default.
    /// </param>
    /// <returns>A <see cref="HexalithEventStoreResources"/> containing the resource builders for further customization.</returns>
    public static HexalithEventStoreResources AddHexalithEventStore(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> eventStore,
        IResourceBuilder<ProjectResource> adminServer,
        IResourceBuilder<ProjectResource>? adminUI,
        string? eventStoreDaprConfigPath,
        string? adminServerDaprConfigPath,
        string? resiliencyConfigPath,
        string? stateStoreComponentPath,
        int eventStoreDaprHttpPort = 3501,
        string? daprPlacementHostAddress = null,
        string? daprSchedulerHostAddress = null) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(eventStore);
        ArgumentNullException.ThrowIfNull(adminServer);
        ArgumentOutOfRangeException.ThrowIfLessThan(eventStoreDaprHttpPort, 1024);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(eventStoreDaprHttpPort, 65535);

        const string LocalDaprRedisHost = "127.0.0.1:6379";

        // Redis is provided by `dapr init` at 127.0.0.1:6379, not managed by Aspire.
        // The checked-in statestore.yaml is the preferred local source of truth. The
        // fallback below exists for external consumers that use this hosting extension
        // without an AppHost-owned DAPR components directory.
        string? resolvedStateStoreComponentPath = string.IsNullOrWhiteSpace(stateStoreComponentPath)
            ? null
            : Path.GetFullPath(stateStoreComponentPath);
        if (resolvedStateStoreComponentPath is not null && !File.Exists(resolvedStateStoreComponentPath)) {
            throw new FileNotFoundException(
                "DAPR state-store component YAML not found.",
                resolvedStateStoreComponentPath);
        }

        IResourceBuilder<IDaprComponentResource> stateStore = string.IsNullOrWhiteSpace(stateStoreComponentPath)
            ? builder
                .AddDaprComponent("statestore", "state.redis")
                .WithMetadata("actorStateStore", "true")
                .WithMetadata("redisHost", LocalDaprRedisHost)
                .WithMetadata("keyPrefix", "none")
            : builder.AddDaprComponent(
                "statestore",
                "state.redis",
                new DaprComponentOptions { LocalPath = resolvedStateStoreComponentPath });
        IResourceBuilder<IDaprComponentResource> pubSub = builder
            .AddDaprPubSub("pubsub")
            .WithMetadata("redisHost", LocalDaprRedisHost);

        // Wire up EventStore with DAPR sidecar and component references.
        // AppPort is intentionally omitted so the CommunityToolkit auto-detects
        // the app's actual port from the Aspire resource model. Hardcoding AppPort
        // breaks Aspire Testing, which randomizes project ports.
        // DaprHttpPort is fixed (3501) so Admin.Server can query the EventStore
        // sidecar's metadata endpoint for actor type discovery (story 19-2).
        // IDaprSidecarResource does not implement IResourceWithEndpoints in
        // CommunityToolkit 13.0.0, so a dynamic endpoint reference is not possible.
        _ = eventStore
            .WithDaprSidecar(sidecar => sidecar
                .WithOptions(new DaprSidecarOptions {
                    AppId = "eventstore",
                    DaprHttpPort = eventStoreDaprHttpPort,
                    Config = eventStoreDaprConfigPath,
                    PlacementHostAddress = daprPlacementHostAddress,
                    SchedulerHostAddress = daprSchedulerHostAddress,
                })
                .WithReference(stateStore)
                .WithReference(pubSub));

        // Wire Admin.Server with DAPR sidecar.
        // Admin.Server needs state store for direct reads (health, admin indexes,
        // tenant projections written by the tenants service).
        // All services use keyPrefix:none so keys are shared across appIds.
        // It does not publish or subscribe directly, so it intentionally does not
        // reference the pub/sub component.
        _ = adminServer.WithReference(eventStore);

        // Aspire-orchestration-only env vars. These resolve to localhost-based endpoints
        // and the Aspire dashboard, which are valid under `dotnet aspire run` but NOT in
        // publish mode (aspirate-generated K8s manifests, Aspire publishers, etc.). In
        // publish mode the operator is responsible for wiring the equivalent values via
        // their own appsettings / Helm values / kustomize overlay, pointing at the
        // cluster-resolved endpoints and their actual observability stack (Grafana,
        // Datadog, Application Insights, etc.). Without this gate, aspirate captures
        // these dev-mode values and bakes them into production ConfigMaps where they
        // resolve nowhere -- breaking byte-determinism (host paths) and silently
        // misrouting observability links (localhost:17017 dashboard URLs).
        // The Aspire dashboard default port is 17017; override these env vars if you run
        // the dashboard on a different port.
        if (!builder.ExecutionContext.IsPublishMode) {
            const string AspireDashboardBaseUrl = "https://localhost:17017";
            string eventStoreEndpointUrl = "http://localhost:" + eventStoreDaprHttpPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _ = adminServer
                .WithEnvironment("AdminServer__EventStoreDaprHttpEndpoint", eventStoreEndpointUrl)
                .WithEnvironment("AdminServer__TraceUrl", AspireDashboardBaseUrl + "/traces")
                .WithEnvironment("AdminServer__MetricsUrl", AspireDashboardBaseUrl + "/metrics")
                .WithEnvironment("AdminServer__LogsUrl", AspireDashboardBaseUrl + "/structuredlogs");
        }

        _ = adminServer.WithDaprSidecar(sidecar => sidecar
            .WithOptions(new DaprSidecarOptions {
                AppId = "eventstore-admin",
                Config = adminServerDaprConfigPath,
                PlacementHostAddress = daprPlacementHostAddress,
                SchedulerHostAddress = daprSchedulerHostAddress,
            })
            .WithReference(stateStore));

        // Auto-inject the resiliency YAML path so /dapr/resiliency works out of the box under
        // Aspire orchestration. The AppHost owns this path because it knows the absolute on-disk
        // location of the DAPR resources directory; pushing it through an env var avoids
        // forcing operators to maintain a duplicated entry in Admin.Server.Host/appsettings.json
        // and avoids working-directory fragility (the resiliency.yaml lives in the AppHost
        // project, not the Admin.Server.Host project).
        // Aspire-orchestration-only: in publish mode the resolved absolute path is the
        // AppHost machine's home directory, not a container-internal path. Operators
        // running in K8s mount the resiliency YAML via Volume + ConfigMap and set this
        // env var in their kustomize overlay or Helm values.
        if (!builder.ExecutionContext.IsPublishMode && !string.IsNullOrWhiteSpace(resiliencyConfigPath)) {
            _ = adminServer.WithEnvironment("AdminServer__ResiliencyConfigPath", resiliencyConfigPath);
        }

        // Wire Admin.UI. It invokes Admin.Server via DAPR service invocation
        // (D13, supersedes the ADR-P4 HTTP deviation): the Admin.UI sidecar tags
        // outbound calls with `dapr-app-id: eventstore-admin`. The sidecar references
        // no state store / pub/sub component — service invocation only, so it has zero
        // direct infrastructure access (same isolation rationale as the sample sidecar).
        // WaitFor(adminServer) is retained so the UI starts after its invocation target.
        if (adminUI is not null) {
            _ = adminUI
                .WithReference(adminServer)
                .WaitFor(adminServer)
                .WithExternalHttpEndpoints()
                .WithDaprSidecar(sidecar => sidecar
                    .WithOptions(new DaprSidecarOptions {
                        AppId = "eventstore-admin-ui",
                        PlacementHostAddress = daprPlacementHostAddress,
                        SchedulerHostAddress = daprSchedulerHostAddress,
                    }));
        }

        return new HexalithEventStoreResources(stateStore, pubSub, eventStore, adminServer, adminUI);
    }

}
