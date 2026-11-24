using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>Contributes a domain solely through synchronous projection discovery.</summary>
public sealed class DiagnosticsOnlySyncProjection : IDomainProjectionHandler
{
    /// <inheritdoc/>
    public string Domain => "sync-projection-only";

    /// <inheritdoc/>
    public ProjectionResponse Project(ProjectionRequest request)
        => new(Domain, JsonSerializer.SerializeToElement(new { }));
}
