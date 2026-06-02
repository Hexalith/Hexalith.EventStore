namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Options for consuming a domain's published events via DAPR pub/sub.
/// </summary>
/// <remarks>
/// Generalizes the per-domain consumer options domain modules previously hand-wrote (e.g.
/// <c>HexalithTenantsOptions</c>). The defaults are derived from a domain name via
/// <see cref="ForDomain(string)"/>.
/// </remarks>
public class EventStoreDomainEventsOptions {
    /// <summary>The DAPR pub/sub component name carrying the domain's events.</summary>
    public string PubSubName { get; set; } = "pubsub";

    /// <summary>The DAPR topic carrying the domain's events.</summary>
    public string TopicName { get; set; } = "domain.events";

    /// <summary>The HTTP route the subscription endpoint is mapped to.</summary>
    public string SubscriptionRoute { get; set; } = "/domain-events";

    /// <summary>
    /// When set, the processor reflects a payload property of this name and rejects the event when its
    /// value does not equal the envelope's <see cref="EventStoreDomainEventEnvelope.AggregateId"/>
    /// (Ordinal). Leave <see langword="null"/> to skip the integrity check. Domains whose events carry,
    /// for example, a <c>TenantId</c> that must match the aggregate set this to <c>"TenantId"</c>.
    /// </summary>
    public string? PayloadAggregateIdPropertyName { get; set; }

    /// <summary>
    /// Returns options whose pub/sub topic and subscription route are derived from a domain name
    /// (topic <c>{domain}.events</c>, route <c>/{domain}/events</c>).
    /// </summary>
    /// <param name="domain">The kebab-case domain name.</param>
    /// <returns>The derived options.</returns>
    public static EventStoreDomainEventsOptions ForDomain(string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return new EventStoreDomainEventsOptions {
            TopicName = $"{domain}.events",
            SubscriptionRoute = $"/{domain}/events",
        };
    }
}
