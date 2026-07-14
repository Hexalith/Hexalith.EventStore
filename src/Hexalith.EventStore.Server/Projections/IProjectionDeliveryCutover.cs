namespace Hexalith.EventStore.Server.Projections;

/// <summary>Maintenance-only activation of the store-global delivery writer protocol.</summary>
internal interface IProjectionDeliveryCutover {
    /// <summary>Activates v2 only after explicit backup, quiescence, and downgrade attestations.</summary>
    Task<ProjectionDeliveryCutoverStatus> ActivateAsync(
        ProjectionDeliveryCutoverRequest request,
        CancellationToken cancellationToken = default);
}
