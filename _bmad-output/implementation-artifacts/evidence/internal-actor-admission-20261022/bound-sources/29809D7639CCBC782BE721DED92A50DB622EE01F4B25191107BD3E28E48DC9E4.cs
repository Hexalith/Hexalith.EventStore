namespace Hexalith.EventStore.Server.Projections;

/// <summary>Classifies writer protocol activation.</summary>
internal enum ProjectionDeliveryCutoverStatus {
    /// <summary>The v2 marker was activated or was already active for the exact commit.</summary>
    Activated = 0,

    /// <summary>Required maintenance attestations were incomplete.</summary>
    PreconditionsFailed = 1,

    /// <summary>A conflicting marker or conditional write prevented activation.</summary>
    Conflict = 2,

    /// <summary>The state store was unavailable.</summary>
    StateUnavailable = 3,
}
