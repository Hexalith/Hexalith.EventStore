
using Hexalith.EventStore.Client.Discovery;

namespace Hexalith.EventStore.Client.Registration;

/// <summary>
/// Represents the activation metadata for a single discovered domain, including convention-derived DAPR resource names.
/// </summary>
/// <param name="DomainName">The resolved domain name.</param>
/// <param name="Kind">Whether this domain is an aggregate or a projection.</param>
/// <param name="Type">The CLR type of the domain class.</param>
/// <param name="StateType">The state or read-model CLR type.</param>
/// <param name="StateStoreName">The convention-derived DAPR state store name (e.g., <c>counter-eventstore</c>).</param>
/// <param name="TopicPattern">The convention-derived pub/sub topic pattern (e.g., <c>counter.events</c>).</param>
/// <param name="DeadLetterTopicPattern">The convention-derived dead-letter topic pattern (e.g., <c>deadletter.counter.events</c>).</param>
/// <remarks>
/// <c>ActorTypeName</c> is intentionally excluded. The server uses a single generic <c>AggregateActor</c> type
/// for all domains (routed by domain name key), not per-domain actor types. This record can be extended
/// in the future if per-domain actors are needed (adding properties with defaults is non-breaking).
/// </remarks>
public sealed record EventStoreDomainActivation(
    string DomainName,
    DomainKind Kind,
    Type Type,
    Type StateType,
    string StateStoreName,
    string TopicPattern,
    string DeadLetterTopicPattern);
