using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>
/// Describes a domain-module project resource after DAPR sidecar composition.
/// </summary>
/// <param name="Project">The project resource builder.</param>
/// <param name="AppId">The stable DAPR application id.</param>
/// <param name="InfrastructureMode">The shared or isolated infrastructure mode.</param>
public sealed record AspireDaprDomainModuleResource(
    IResourceBuilder<ProjectResource> Project,
    string AppId,
    AspireDaprInfrastructureMode InfrastructureMode);
