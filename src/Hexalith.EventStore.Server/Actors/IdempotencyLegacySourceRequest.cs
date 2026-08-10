using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Binds exact protected inventory evidence to one aggregate-local source record.</summary>
[DataContract]
internal sealed record IdempotencyLegacySourceRequest(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string TenantPartition,
    [property: DataMember] string InventoryId,
    [property: DataMember] string MigrationId,
    [property: DataMember] int LegacySchemaVersion,
    [property: DataMember] string SourceEvidenceDigest,
    [property: DataMember] string ExecutionMessageId,
    [property: DataMember] string ExecutionCorrelationId,
    [property: DataMember] DateTimeOffset FirstConsumedAt,
    [property: DataMember] DateTimeOffset ReplayExpiresAt,
    [property: DataMember] CommandProcessingResult ReplayResult)
{
    /// <summary>Gets the only source-request schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;
}
