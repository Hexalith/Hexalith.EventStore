using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Requests exact pre-redirect rollback of an unactivated prepared target.</summary>
[DataContract]
public sealed record IdempotencyLegacyMigrationRollbackRequest(
    [property: DataMember] string InventoryId,
    [property: DataMember] string MigrationId,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest,
    [property: DataMember] IdempotencyLegacyMigrationPhase ExpectedPhase,
    [property: DataMember] string TargetAdmissionActorId,
    [property: DataMember] string TargetImportDigest);
