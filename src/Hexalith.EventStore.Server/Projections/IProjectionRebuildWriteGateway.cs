using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Server-owned gateway for operation-scoped projection-actor rebuild candidates.</summary>
internal interface IProjectionRebuildWriteGateway {
    /// <summary>Persists a non-live candidate.</summary>
    Task StageAsync(string actorId, ProjectionRebuildCandidate candidate, CancellationToken cancellationToken);

    /// <summary>Promotes the matching candidate into the live actor slot.</summary>
    Task<bool> PromoteAsync(string actorId, string operationId, CancellationToken cancellationToken);

    /// <summary>Discards the matching non-live candidate.</summary>
    Task<bool> DiscardAsync(string actorId, string operationId, CancellationToken cancellationToken);
}
