namespace Hexalith.EventStore.Server.Actors;

/// <summary>Admission decision returned by <see cref="IProjectionLifecycleActor.BeginEraseAsync"/>.</summary>
/// <param name="Kind">Whether the operation was admitted, resumed, or refused.</param>
/// <param name="PerTargetOutcomes">Per-target erase outcomes recorded so far (empty except on <see cref="ProjectionEraseAdmissionKind.Resume"/>).</param>
public sealed record ProjectionEraseAdmission(
    ProjectionEraseAdmissionKind Kind,
    IReadOnlyDictionary<string, string> PerTargetOutcomes);
