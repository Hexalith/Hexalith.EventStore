using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists protected cross-aggregate legacy evidence without raw key material.</summary>
[DataContract]
public sealed record IdempotencyLegacyInventoryEntry(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string TenantPartition,
    [property: DataMember] string SourceAggregateActorId,
    [property: DataMember] string SourceEvidenceDigest,
    [property: DataMember] int LegacySchemaVersion,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest,
    [property: DataMember] string VerificationTag,
    [property: DataMember] string IntentDigest,
    [property: DataMember] IdempotencyReplayRetentionTier RetentionTier,
    [property: DataMember] DateTimeOffset FirstConsumedAt,
    [property: DataMember] DateTimeOffset LastObservedAt,
    [property: DataMember] DateTimeOffset ReplayExpiresAt,
    [property: DataMember] CommandProcessingResult ReplayResult,
    [property: DataMember] string ExecutionMessageId,
    [property: DataMember] string ExecutionCorrelationId,
    [property: DataMember] IdempotencyLegacyMigrationPhase Phase,
    [property: DataMember] string? TargetAdmissionActorId = null)
{
    /// <summary>Gets the only legacy inventory schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
