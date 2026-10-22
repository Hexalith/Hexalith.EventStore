using System.Runtime.Serialization;
using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persists tenant deletion, legal-hold, and protected-reference governance.</summary>
[DataContract]
public sealed record IdempotencyTenantLifecycleRecord(
    [property: DataMember] int SchemaVersion,
    [property: DataMember] string Tenant,
    [property: DataMember] IdempotencyTenantLifecycleState State,
    [property: DataMember] DateTimeOffset LastObservedAt,
    [property: DataMember] DateTimeOffset? DeletionApprovedAt,
    [property: DataMember] DateTimeOffset? DeleteAfter,
    [property: DataMember] TimeSpan? RemainingRetention,
    [property: DataMember] DateTimeOffset? LegalHoldStartedAt,
    [property: DataMember] IdempotencyTenantLifecycleReference[] References)
{
    /// <summary>Gets the only lifecycle schema understood by this implementation.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Gets the mandatory post-deletion retention interval.</summary>
    public static TimeSpan PostDeletionRetention { get; } = TimeSpan.FromDays(400);

    /// <summary>Indicates that trusted source, target, receipt, and collision evidence needs joint erasure.</summary>
    [DataMember]
    public bool HasTrustedEffectEvidence { get; init; }

    /// <summary>Indicates that the joint erasure authority completed its tenant operation.</summary>
    [DataMember]
    public bool TrustedEffectEvidenceErased { get; init; }

    /// <summary>Durable source and target inventory for one tenant erasure decision.</summary>
    [DataMember]
    public EffectIdentity[] TrustedEffects { get; init; } = [];

    /// <summary>Effect turns admitted before deletion and not yet durably settled.</summary>
    [DataMember]
    public string[] PendingTrustedEffectIds { get; init; } = [];
}
