using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// The three Hexalith.EventStore platform project resources added by
/// <see cref="HexalithEventStorePlatformExtensions.AddHexalithEventStorePlatformProjects"/>, exposed so the
/// consuming AppHost can further configure them (environment variables, references, auth wiring) before and
/// after the DAPR topology is wired with
/// <see cref="HexalithEventStoreExtensions.AddHexalithEventStore(IDistributedApplicationBuilder, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}?, string?, string?, string?, int, string?, string?, string?)"/>.
/// </summary>
/// <param name="EventStore">The EventStore command-gateway project resource builder.</param>
/// <param name="AdminServer">The EventStore Admin.Server.Host project resource builder.</param>
/// <param name="AdminUI">The EventStore Admin.UI project resource builder.</param>
public sealed record HexalithEventStorePlatformProjects(
    IResourceBuilder<ProjectResource> EventStore,
    IResourceBuilder<ProjectResource> AdminServer,
    IResourceBuilder<ProjectResource> AdminUI);

/// <summary>
/// Provides the Aspire hosting extension that adds the Hexalith.EventStore platform projects (command gateway,
/// Admin.Server.Host and Admin.UI) to a domain-module AppHost.
/// </summary>
/// <remarks>
/// <para>
/// Every Hexalith domain module (Tenants, FrontComposer, …) hosts its services on the shared EventStore
/// platform and therefore needs to add the same three EventStore projects to its AppHost. Previously each
/// AppHost hand-rolled identical <see cref="IProjectMetadata"/> classes plus a repository-path helper to do
/// this. That boilerplate now lives here, in the EventStore platform Aspire library, so domain modules call a
/// single helper instead of duplicating it.
/// </para>
/// <para>
/// The projects are referenced cross-repo from the consuming repository's <c>Hexalith.EventStore</c> submodule
/// with <see cref="IProjectMetadata.SuppressBuild"/> set to <see langword="true"/>; the EventStore platform is
/// built independently (Aspire runs children with <c>--no-build</c>). This helper only <i>adds</i> the
/// projects — the consumer wires the DAPR topology by passing the returned resources to
/// <see cref="HexalithEventStoreExtensions.AddHexalithEventStore(IDistributedApplicationBuilder, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}?, string?, string?, string?, int, string?, string?, string?)"/>.
/// </para>
/// </remarks>
public static class HexalithEventStorePlatformExtensions {
    /// <summary>
    /// Adds the three Hexalith.EventStore platform projects (command gateway, Admin.Server.Host and Admin.UI)
    /// to the distributed application, resolving each project file cross-repo from the consuming repository's
    /// <c>Hexalith.EventStore</c> submodule.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="eventStoreName">The Aspire resource name for the EventStore command gateway. Defaults to <c>"eventstore"</c>.</param>
    /// <param name="adminServerName">The Aspire resource name for the Admin.Server.Host. Defaults to <c>"eventstore-admin"</c>.</param>
    /// <param name="adminUiName">The Aspire resource name for the Admin.UI. Defaults to <c>"eventstore-admin-ui"</c>.</param>
    /// <returns>
    /// A <see cref="HexalithEventStorePlatformProjects"/> exposing the three project resource builders for
    /// further customization and for wiring the topology with <c>AddHexalithEventStore</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any resource name is <see langword="null"/> or whitespace.</exception>
    public static HexalithEventStorePlatformProjects AddHexalithEventStorePlatformProjects(
        this IDistributedApplicationBuilder builder,
        string eventStoreName = "eventstore",
        string adminServerName = "eventstore-admin",
        string adminUiName = "eventstore-admin-ui") {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventStoreName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminServerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminUiName);

        IResourceBuilder<ProjectResource> eventStore = builder.AddProject<EventStoreProjectMetadata>(eventStoreName);
        IResourceBuilder<ProjectResource> adminServer = builder.AddProject<EventStoreAdminServerHostProjectMetadata>(adminServerName);
        IResourceBuilder<ProjectResource> adminUI = builder.AddProject<EventStoreAdminUIProjectMetadata>(adminUiName);

        return new HexalithEventStorePlatformProjects(eventStore, adminServer, adminUI);
    }

    /// <summary>
    /// Adds <b>only</b> the Hexalith.EventStore command-gateway project (no Admin.Server / Admin.UI) to the
    /// distributed application, resolving the project file cross-repo from the consuming repository's
    /// <c>Hexalith.EventStore</c> submodule.
    /// </summary>
    /// <remarks>
    /// Use this for a <i>gateway-only</i> composition: a domain-module AppHost that hosts the EventStore command
    /// gateway without the admin surface. Pass the returned builder as the <c>eventStore</c> argument to
    /// <see cref="HexalithEventStoreExtensions.AddHexalithEventStore(IDistributedApplicationBuilder, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}, IResourceBuilder{ProjectResource}?, string?, string?, string?, int, string?, string?, string?)"/>
    /// with <c>adminServer: null</c> (and <c>adminUI: null</c>) — the helper then wires the gateway sidecar and
    /// shared state-store / pub/sub components and adds no <c>eventstore-admin</c> / <c>eventstore-admin-ui</c>
    /// resources. Unlike <see cref="AddHexalithEventStorePlatformProjects"/>, this never adds the admin projects.
    /// </remarks>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="eventStoreName">The Aspire resource name for the EventStore command gateway. Defaults to <c>"eventstore"</c>.</param>
    /// <returns>The EventStore command-gateway project resource builder for further customization and topology wiring.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventStoreName"/> is <see langword="null"/> or whitespace.</exception>
    public static IResourceBuilder<ProjectResource> AddHexalithEventStoreGatewayProject(
        this IDistributedApplicationBuilder builder,
        string eventStoreName = "eventstore") {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventStoreName);

        return builder.AddProject<EventStoreProjectMetadata>(eventStoreName);
    }
}
