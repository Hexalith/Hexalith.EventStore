using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Binds the lifecycle-serialized pre-redirect target and inventory rollback.</summary>
[DataContract]
internal sealed record IdempotencyLegacyLifecycleRollbackRequest(
    [property: DataMember] IdempotencyTenantLifecycleReference Target,
    [property: DataMember] string SourceAggregateActorId,
    [property: DataMember] string SourceEvidenceDigest,
    [property: DataMember] string InventoryId,
    [property: DataMember] string MigrationId,
    [property: DataMember] string SourceDigestKeyVersion,
    [property: DataMember] string SourceKeyDigest,
    [property: DataMember] IdempotencyLegacyMigrationPhase ExpectedPhase,
    [property: DataMember] string TargetImportDigest);
