namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Closed canonical owner scope. Only exact interaction and source-deletion exact Conversation are implemented; human Conversation and class/time scopes require their separate accepted policy.</summary>
/// <param name="TenantId">Exact TenantId.</param>
/// <param name="Kind">Exact Kind.</param>
/// <param name="AgentInteractionId">Exact AgentInteractionId.</param>
/// <param name="SourceConversationId">Exact SourceConversationId.</param>
public sealed record GovernanceScopeV1(string TenantId, string Kind, string AgentInteractionId, string SourceConversationId);
