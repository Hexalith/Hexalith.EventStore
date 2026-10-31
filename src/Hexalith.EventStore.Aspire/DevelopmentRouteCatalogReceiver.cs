using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>An actual catalog participant and the exact incoming transport closure declared by its owning AppHost.</summary>
public sealed record DevelopmentRouteCatalogReceiver(IResourceBuilder<ProjectResource> Project, IReadOnlyList<DevelopmentRouteCatalogInvocation> Invocations);
