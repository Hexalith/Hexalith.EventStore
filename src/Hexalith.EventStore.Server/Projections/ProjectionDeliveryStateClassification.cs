namespace Hexalith.EventStore.Server.Projections;

/// <summary>Classifies persisted projection delivery rows before mutation.</summary>
internal enum ProjectionDeliveryStateClassification {
    /// <summary>No projection-scoped row exists.</summary>
    Absent = 0,

    /// <summary>The row uses the current schema and writer protocol.</summary>
    Current = 1,

    /// <summary>A pre-v2 five-field row has a zero checkpoint and can initialize safely.</summary>
    LegacyZero = 2,

    /// <summary>A pre-v2 five-field row has a non-zero checkpoint and needs hydration.</summary>
    LegacyNonZero = 3,

    /// <summary>A row regressed after the store-level v2 cutover.</summary>
    SchemaRegression = 4,

    /// <summary>The row uses an unsupported future or malformed schema.</summary>
    Unsupported = 5,
}
