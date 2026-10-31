using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>An actual catalog participant and the exact incoming transport closure declared by its owning AppHost.</summary>
public sealed record DevelopmentRouteCatalogReceiver(IResourceBuilder<ProjectResource> Project, IReadOnlyList<DevelopmentRouteCatalogInvocation> Invocations)
{
    /// <summary>Gets whether this SDK host explicitly enables the separately versioned technical MVC composite.</summary>
    public bool TechnicalHttp { get; init; }

    /// <summary>Gets actual selected owner projects whose existing native actor-state/metadata reads this Admin receiver needs.</summary>
    public IReadOnlyList<IResourceBuilder<ProjectResource>> ActorStateOwners { get; init; } = [];
}
