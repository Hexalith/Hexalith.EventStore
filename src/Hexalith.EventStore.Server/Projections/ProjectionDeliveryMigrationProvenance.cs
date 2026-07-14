namespace Hexalith.EventStore.Server.Projections;

/// <summary>Identifies how a versioned projection delivery row was initialized.</summary>
internal enum ProjectionDeliveryMigrationProvenance {
    /// <summary>No migration provenance is available.</summary>
    None = 0,

    /// <summary>The row was initialized from an absent or zero checkpoint.</summary>
    InitializedFromZero = 1,

    /// <summary>The row was hydrated from an authoritative persisted EventStore checkpoint.</summary>
    HydratedFromPersistedCheckpoint = 2,
}
