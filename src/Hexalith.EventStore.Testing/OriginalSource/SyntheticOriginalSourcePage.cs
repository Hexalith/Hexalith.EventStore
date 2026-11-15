using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Immutable independently committed original envelopes and their owner-computed native chain boundary.</summary>
internal sealed record SyntheticOriginalSourcePage(string RunId, string? PreviousPageId,
    NativeSourceProof Before, EventEnvelope[] Events, NativeSourceProof After);
