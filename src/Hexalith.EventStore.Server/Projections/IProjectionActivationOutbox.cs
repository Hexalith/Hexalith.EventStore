using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Durable write-ahead activation outbox for projection delivery.</summary>
public interface IProjectionActivationOutbox {
    /// <summary>Persists activation before a command may commit aggregate events.</summary>
    Task EnsureAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Gets the current activation revision for a delivery attempt.</summary>
    Task<ProjectionActivationWorkItem?> GetAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default);

    /// <summary>Completes activation only when the observed revision is still current.</summary>
    Task CompleteAsync(
        ProjectionActivationWorkItem workItem,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a bounded deterministic due set.</summary>
    Task<IReadOnlyList<ProjectionActivationWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default);

    /// <summary>Defers an activation that remains outstanding.</summary>
    Task DeferAsync(
        ProjectionActivationWorkItem workItem,
        DateTimeOffset nextDueUtc,
        CancellationToken cancellationToken = default);
}
