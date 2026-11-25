namespace Hexalith.EventStore.Client.Tests.Subscriptions;

internal sealed class HiddenPayloadIdentityEvent : BasePayloadIdentityEvent {
    public new int TenantId { get; init; }
}
