using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>An actual catalog participant and the exact incoming transport closure declared by its owning AppHost.</summary>
/// <typeparam name="TProject">The .NET application resource type.</typeparam>
public sealed record DevelopmentRouteCatalogReceiver<TProject>(IResourceBuilder<TProject> Project, IReadOnlyList<DevelopmentRouteCatalogInvocation<TProject>> Invocations)
    where TProject : class, IDotnetProgramResource, IResourceWithEnvironment
{
    /// <summary>Gets whether this SDK host explicitly enables the separately versioned technical MVC composite.</summary>
    public bool TechnicalHttp { get; init; }

    /// <summary>Gets actual selected owner projects whose existing native actor-state/metadata reads this Admin receiver needs.</summary>
    public IReadOnlyList<IResourceBuilder<TProject>> ActorStateOwners { get; init; } = [];
}
