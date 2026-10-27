using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Complete protection identity; aliases alone never authorize another interaction or tenant.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="AgentInteractionId">Exact interaction.</param>
/// <param name="TargetProtectionKeyAlias">Explicit exact DEK alias.</param>
[DataContract]
public sealed record ProtectionTarget([property: DataMember] string TenantId, [property: DataMember] string AgentInteractionId, [property: DataMember] string TargetProtectionKeyAlias);
