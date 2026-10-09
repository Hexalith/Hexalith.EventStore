using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>A complete set of folded values covered by one unchanged authenticated namespace cut.</summary>
/// <param name="Cut">The captured finite cut that covers every returned value.</param>
/// <param name="Values">Values folded from exact source prefixes in the cut.</param>
public sealed record SourceNamespaceSnapshot<T>(SourcePublicationCut Cut, IReadOnlyList<T> Values);
