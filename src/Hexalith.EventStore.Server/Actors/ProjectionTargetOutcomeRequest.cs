namespace Hexalith.EventStore.Server.Actors;

/// <summary>Request to record the erase outcome for a single target.</summary>
/// <param name="OperationId">The in-flight erase operation identifier (must match the admitted operation).</param>
/// <param name="TargetKey">The target being erased.</param>
/// <param name="Outcome">The outcome recorded for the target.</param>
public sealed record ProjectionTargetOutcomeRequest(string OperationId, string TargetKey, string Outcome);
