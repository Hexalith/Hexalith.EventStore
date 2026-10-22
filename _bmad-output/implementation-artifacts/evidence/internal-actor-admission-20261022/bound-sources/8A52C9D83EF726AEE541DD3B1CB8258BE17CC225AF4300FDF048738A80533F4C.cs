namespace Hexalith.EventStore.Server.Projections;

/// <summary>Classifies a conditional delivery completion transition.</summary>
internal enum ProjectionDeliveryCompletion {
    /// <summary>The matching reservation completed and advanced the row.</summary>
    Completed = 0,

    /// <summary>The same exact completion was already durable.</summary>
    AlreadyCompleted = 1,

    /// <summary>The reservation fence was stale or no longer present.</summary>
    Fenced = 2,

    /// <summary>The transition exhausted or could not access delivery state.</summary>
    StateUnavailable = 3,
}
