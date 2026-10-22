namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Independently authenticated immutable first-event/permit and exact owner facts. Caller classification is never sufficient.</summary>
/// <param name="TenantId">Exact TenantId.</param>
/// <param name="AgentInteractionId">Exact AgentInteractionId.</param>
/// <param name="FirstEventSourceConversationId">Exact FirstEventSourceConversationId.</param>
/// <param name="PermitSourceConversationId">Exact PermitSourceConversationId.</param>
/// <param name="PermitOwnerId">Exact PermitOwnerId.</param>
/// <param name="PermitId">Exact PermitId.</param>
/// <param name="EffectCapabilityId">Exact EffectCapabilityId.</param>
/// <param name="CapabilityOwnerRevision">Exact CapabilityOwnerRevision.</param>
/// <param name="Kind">Exact Kind.</param>
/// <param name="EpochId">Exact EpochId.</param>
/// <param name="SourceReceiptId">Exact SourceReceiptId.</param>
public sealed record GovernanceWriteFacts(string TenantId, string AgentInteractionId, string FirstEventSourceConversationId, string PermitSourceConversationId, string PermitOwnerId, string PermitId, string EffectCapabilityId, long CapabilityOwnerRevision, DirectoryWriteKind Kind, string EpochId, string SourceReceiptId);
