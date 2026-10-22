namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A deterministic protocol-phase fault seam. The DAPR adapter passes <see langword="null"/>; the
/// in-memory fake passes a test-controlled injector to force crashes, cancellation, or partial progress at
/// exact protocol boundaries.
/// </summary>
internal interface IReadModelBatchFaultInjector {
    /// <summary>
    /// Invoked at each protocol phase. Implementations may throw to simulate a crash/transport failure,
    /// or cancel to simulate post-dispatch cancellation.
    /// </summary>
    /// <param name="phase">The current protocol phase.</param>
    /// <param name="ordinal">The operation ordinal for per-operation phases; otherwise -1.</param>
    /// <param name="cancellationToken">The protocol cancellation token.</param>
    /// <returns>A task representing the injection point.</returns>
    Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken);
}
