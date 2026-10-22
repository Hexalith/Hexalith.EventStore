namespace Hexalith.EventStore.Client.Projections;

/// <summary>A terminal fold disposition retained in the same tenant/family epoch as accepted delivery.</summary>
/// <param name="SourceStream">The parked source identity.</param>
/// <param name="EnvelopePosition">The parked source position.</param>
/// <param name="ObservedEpoch">The epoch in which parking became durable.</param>
/// <param name="FailureCount">The bounded failed-attempt count.</param>
/// <param name="ResolvedByOperationId">The newer covering authoritative repair, or null while unresolved.</param>
public sealed record SharedProjectionParkingEvidence(string SourceStream, long EnvelopePosition,
    long ObservedEpoch, int FailureCount, string? ResolvedByOperationId = null);
