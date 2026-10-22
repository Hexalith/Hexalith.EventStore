using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Actor-local denial of trusted-effect dispatch and receipt reads until terminal erasure.</summary>
/// <param name="Tenant">Tenant whose registered partition is fenced.</param>
/// <param name="DeletionApprovedAt">Accountable deletion decision timestamp.</param>
/// <param name="InventoryDigest">Complete effect inventory at fence issuance.</param>
[DataContract]
public sealed record TrustedEffectDeletionFence(
    [property: DataMember] string Tenant,
    [property: DataMember] DateTimeOffset DeletionApprovedAt,
    [property: DataMember] string InventoryDigest)
{
    /// <summary>Gets the actor state key for the permanent deletion fence.</summary>
    public const string StateName = "trusted_effect_deletion_fence";
}
