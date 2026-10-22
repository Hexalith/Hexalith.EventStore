using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Validates separate registered callables with an addressed fence between their invocation leases.</summary>
/// <param name="domain">The admitted registry domain.</param>
/// <param name="canonicalType">The exact canonical event type.</param>
/// <param name="version">The exact registered version.</param>
/// <param name="format">The registered serialization format.</param>
/// <param name="payload">The private owner from which individual invocation leases are borrowed.</param>
/// <param name="sourceFence">The optional actual source and trust check.</param>
/// <param name="token">The originating operation token.</param>
internal delegate ValueTask EventVersionValidatorAsync(string domain, string canonicalType, int version,
    string format, IReadOnlyPayload payload, Func<CancellationToken, Task>? sourceFence, CancellationToken token);
