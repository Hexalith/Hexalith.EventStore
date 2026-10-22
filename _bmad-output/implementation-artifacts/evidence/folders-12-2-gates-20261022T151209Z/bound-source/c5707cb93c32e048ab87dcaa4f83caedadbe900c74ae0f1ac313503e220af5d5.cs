namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Complete protection identity; aliases alone never authorize another interaction or tenant.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="AgentInteractionId">Exact interaction.</param>
/// <param name="TargetProtectionKeyAlias">Explicit exact DEK alias.</param>
public sealed record ProtectionTarget(string TenantId, string AgentInteractionId, string TargetProtectionKeyAlias);
