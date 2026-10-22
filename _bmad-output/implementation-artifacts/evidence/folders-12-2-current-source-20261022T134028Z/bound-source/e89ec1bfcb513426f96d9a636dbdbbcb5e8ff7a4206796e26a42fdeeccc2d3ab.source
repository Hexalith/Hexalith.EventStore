namespace Hexalith.EventStore.Server.Projections;

/// <summary>Conditional frozen-replay references on the existing activation outbox.</summary>
internal interface IProjectionLegacyDeliveryOutbox
{
    /// <summary>Binds an initial intent or its immutable route/candidate references to the sampled activation.</summary>
    Task<ProjectionActivationWorkItem?> TryBindLegacyDeliveryAsync(ProjectionActivationWorkItem expected,
        ProjectionLegacyDeliveryIntent intent, CancellationToken cancellationToken);

    /// <summary>Clears only the exact completed intent, preserving any newer activation revision.</summary>
    Task<bool> TryCompleteLegacyDeliveryAsync(ProjectionActivationWorkItem expected,
        CancellationToken cancellationToken);
}
