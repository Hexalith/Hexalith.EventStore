namespace Hexalith.EventStore.Server.Actors;

/// <summary>Versioned lifecycle evidence used to detect a transition around a projection read.</summary>
/// <param name="Phase">The persisted lifecycle phase.</param>
/// <param name="Revision">A monotonic revision incremented by every lifecycle transition.</param>
public sealed record ProjectionLifecycleSnapshot(
    ProjectionLifecyclePhase Phase,
    long Revision);
