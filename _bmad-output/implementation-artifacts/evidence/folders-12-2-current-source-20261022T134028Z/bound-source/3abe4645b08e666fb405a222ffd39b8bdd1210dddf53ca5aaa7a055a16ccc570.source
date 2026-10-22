using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Advances one exact durable legacy migration checkpoint.</summary>
[DataContract]
public sealed record IdempotencyLegacyMigrationAdvanceRequest(
    [property: DataMember] string InventoryId,
    [property: DataMember] string MigrationId,
    [property: DataMember] string DigestKeyVersion,
    [property: DataMember] string KeyDigest,
    [property: DataMember] IdempotencyLegacyMigrationPhase ExpectedPhase,
    [property: DataMember] string TargetAdmissionActorId,
    [property: DataMember] string TargetImportDigest,
    [property: DataMember] string? SourceRedirectDigest = null);
