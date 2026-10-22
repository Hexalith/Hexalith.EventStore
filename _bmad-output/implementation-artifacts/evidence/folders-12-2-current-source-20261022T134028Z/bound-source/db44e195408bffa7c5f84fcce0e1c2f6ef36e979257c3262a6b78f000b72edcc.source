using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Server-owned read-only recovery access to original actor candidate and promotion evidence.</summary>
internal interface IProjectionRebuildInspectionGateway
{
    /// <summary>Inspects one exact operation through the owning actor boundary.</summary>
    Task<ProjectionRebuildInspection> InspectAsync(string actorId, string operationId, CancellationToken cancellationToken);
}
