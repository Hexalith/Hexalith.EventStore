using System.Runtime.Serialization;

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
}
