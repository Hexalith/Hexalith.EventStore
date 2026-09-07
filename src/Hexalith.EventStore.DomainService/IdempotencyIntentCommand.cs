namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Domain-neutral command view supplied to a trusted <see cref="IIdempotencyIntentAdapter"/>.
/// </summary>
/// <param name="CommandType">The exact command type discriminator.</param>
/// <param name="Tenant">The managed tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="Payload">The serialized command payload.</param>
/// <param name="Extensions">Optional envelope extensions. Correlation, bearer tokens, clocks, and retry metadata are untrusted.</param>
public sealed record IdempotencyIntentCommand(
    string CommandType,
    string Tenant,
    string Domain,
    string AggregateId,
    byte[] Payload,
    IReadOnlyDictionary<string, string>? Extensions);
