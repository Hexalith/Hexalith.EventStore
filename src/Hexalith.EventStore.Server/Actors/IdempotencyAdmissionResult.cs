using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Carries an admission decision, fence, and optional replay result.</summary>
/// <param name="Decision">The safe admission action.</param>
/// <param name="FencingToken">The current monotonically increasing fence.</param>
/// <param name="ReplayResult">The replayable command result when terminal and equivalent.</param>
[DataContract]
public sealed record IdempotencyAdmissionResult(
    [property: DataMember] IdempotencyAdmissionDecision Decision,
    [property: DataMember] long FencingToken = 0,
    [property: DataMember] CommandProcessingResult? ReplayResult = null);
