using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Identifies a validated EventStore CloudEvent in one admitted subscription.</summary>
/// <param name="PubSubName">The component that carries the live subscription.</param>
/// <param name="TopicName">The original topic.</param>
/// <param name="SubscriptionRoute">The subscriber's exact callback path.</param>
/// <param name="Source">The received CloudEvent source.</param>
/// <param name="Id">The received CloudEvent ID.</param>
/// <param name="Envelope">The original EventStore event envelope.</param>
public sealed record EventStoreSubscriberDelivery(string PubSubName, string TopicName, string SubscriptionRoute,
    string Source, string Id, EventStoreDomainEventEnvelope Envelope)
{
    /// <summary>Gets the admitted subscriber application ID when supplied by the transport owner.</summary>
    public string? SubscriberAppId { get; init; }

    /// <summary>Validates source and EventStore identity before the delivery can be recorded or processed.</summary>
    /// <returns>Whether the retained identity is unambiguous.</returns>
    public bool IsValid()
    {
        if (Envelope is null || Envelope.Payload is null || Envelope.Timestamp == default || Envelope.Domain is null
            || Id != Envelope.MessageId || Source != $"hexalith-eventstore/{Envelope.TenantId}/{Envelope.Domain}")
        {
            return false;
        }

        try
        {
            EventIdentityValidator.ValidateSubscription(Envelope.TenantId, Envelope.Domain, Envelope.AggregateId,
                Envelope.AggregateType, Envelope.EventTypeName, Envelope.MessageId, Envelope.CorrelationId,
                Envelope.CausationId, Envelope.SequenceNumber);
            return true;
        }
        catch (EventIdentityValidationException)
        {
            return false;
        }
    }

    /// <summary>Computes the retained delivery identity without serializing the transported payload.</summary>
    /// <returns>The content and scope fingerprint.</returns>
    public string Fingerprint()
        => EventStorePayloadFingerprint.Compute(JsonSerializer.SerializeToUtf8Bytes(
            new[] { SubscriberAppId, PubSubName, TopicName, SubscriptionRoute, Source, Id, EventStorePayloadFingerprint.ComputeDelivery(Envelope) }));
}
