namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Complete provider-neutral target identity; an alias alone cannot identify coverage.</summary>
/// <param name="TenantId">Exact TenantId.</param>
/// <param name="AgentInteractionId">Exact AgentInteractionId.</param>
/// <param name="TargetProtectionKeyAlias">Exact TargetProtectionKeyAlias.</param>
public sealed record GovernanceProtectionTarget(string TenantId, string AgentInteractionId, string TargetProtectionKeyAlias);
