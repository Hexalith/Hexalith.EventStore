using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Identifies one exact canonical named projection route.</summary>
/// <param name="Domain">The canonical domain.</param>
/// <param name="ProjectionType">The canonical projection type.</param>
public sealed record ProjectionDispatchRoute(string Domain, string ProjectionType)
{
    /// <summary>Gets an explicitly registered source guard descriptor; absence preserves historical catalog bytes.</summary>
    public ProjectionSourceGuardCapability? SourceGuard { get; init; }
}
