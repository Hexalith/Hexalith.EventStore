namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Signals that the temporary complete-prefix rebuild strategy exceeded an approved safety ceiling.
/// </summary>
internal sealed class ProjectionRebuildPrefixSafetyLimitExceededException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionRebuildPrefixSafetyLimitExceededException"/> class.
    /// </summary>
    public ProjectionRebuildPrefixSafetyLimitExceededException()
        : base("Projection rebuild prefix exceeded an approved safety limit.") {
    }
}
