using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Binds source and pinned target protected identities for serialized migration.</summary>
[DataContract]
internal sealed record IdempotencyLegacyMigrationRequest(
    [property: DataMember] IdempotencyAdmissionDirectoryAlias[] Aliases,
    [property: DataMember] IdempotencyTenantLifecycleReference Target,
    [property: DataMember] string TargetVerificationTag,
    [property: DataMember] string TargetIntentDigest,
    [property: DataMember] IdempotencyReplayRetentionTier TargetRetentionTier,
    [property: DataMember] string SourceVerificationTag,
    [property: DataMember] string SourceIntentDigest,
    [property: DataMember] IdempotencyReplayRetentionTier SourceRetentionTier);
