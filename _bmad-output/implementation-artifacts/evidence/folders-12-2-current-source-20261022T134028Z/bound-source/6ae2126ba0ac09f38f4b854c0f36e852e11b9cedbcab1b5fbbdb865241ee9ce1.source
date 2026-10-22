using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Returns the pinned migrated target or a stable fail-closed admission decision.</summary>
[DataContract]
internal sealed record IdempotencyLegacyMigrationResult(
    [property: DataMember] string TargetAdmissionActorId,
    [property: DataMember] IdempotencyAdmissionDecision? DeniedDecision = null);
