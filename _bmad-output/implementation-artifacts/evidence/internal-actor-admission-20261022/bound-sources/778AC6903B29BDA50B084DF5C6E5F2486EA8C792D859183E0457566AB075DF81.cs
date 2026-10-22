namespace Hexalith.EventStore.Server.Projections;

/// <summary>Classifies an authorized projection delivery hydration attempt.</summary>
internal enum ProjectionDeliveryReconciliationStatus {
    /// <summary>Exact evidence was hydrated without advancing the checkpoint.</summary>
    Completed = 0,

    /// <summary>The requested logical scope did not match the persisted row.</summary>
    ScopeDenied = 1,

    /// <summary>Authoritative history could not prove the persisted prefix.</summary>
    RebuildRequired = 2,

    /// <summary>State or history remained transiently unavailable.</summary>
    StateUnavailable = 3,
}
