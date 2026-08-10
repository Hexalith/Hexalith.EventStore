namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists a payload-free, migration-bound legacy source redirect.</summary>
internal sealed record IdempotencyLegacySourceRedirectRecord(
    int SchemaVersion,
    string TenantPartition,
    string InventoryId,
    string MigrationId,
    string SourceEvidenceDigest,
    string TargetAdmissionActorId)
{
    /// <summary>Gets the only legacy source redirect schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
