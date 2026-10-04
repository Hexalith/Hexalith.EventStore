using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService;

/// <summary>Declares one explicitly bounded V1 serializer and exact legacy alias.</summary>
/// <remarks>Declaration alone is not options, dependency, schema, identity or producer-readiness attestation.</remarks>
internal sealed record BoundedV1EventSerialization(
    Type PayloadType,
    string ExactAlias,
    string Format,
    int MaximumPayloadBytes,
    Func<IEventPayload, Stream, CancellationToken, Task> SerializeAsync);
