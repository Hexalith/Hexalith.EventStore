using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

internal class BasePayloadIdentityEvent : IEventPayload {
    [JsonPropertyName("baseTenantId")]
    public string? TenantId { get; init; }
}
