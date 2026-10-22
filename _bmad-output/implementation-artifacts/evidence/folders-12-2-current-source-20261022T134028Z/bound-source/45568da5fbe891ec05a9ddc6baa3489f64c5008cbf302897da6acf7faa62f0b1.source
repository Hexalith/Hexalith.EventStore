using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Validates a privately owned version's schema, addressed identity and exact format.</summary>
/// <param name="domain">The admitted registry domain.</param>
/// <param name="canonicalType">The exact canonical event type.</param>
/// <param name="version">The exact registered version.</param>
/// <param name="format">The exact registered serialization format.</param>
/// <param name="payload">An invocation-scoped immutable copying facade.</param>
/// <param name="cancellationToken">The originating operation token.</param>
internal delegate void EventVersionValidator(string domain, string canonicalType, int version, string format, IReadOnlyPayload payload, CancellationToken cancellationToken);
