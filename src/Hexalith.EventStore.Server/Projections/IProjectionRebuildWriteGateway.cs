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

    /// <summary>Restores the live state captured before matching promotion.</summary>
    Task<bool> RollbackAsync(string actorId, string operationId, CancellationToken cancellationToken);

    /// <summary>Removes matching rollback evidence after coordinated verification.</summary>
    Task<bool> FinalizeAsync(string actorId, string operationId, CancellationToken cancellationToken);

    /// <summary>Reads back the current persisted live actor state.</summary>
    Task<ProjectionState?> ReadAsync(string actorId, CancellationToken cancellationToken);
}
