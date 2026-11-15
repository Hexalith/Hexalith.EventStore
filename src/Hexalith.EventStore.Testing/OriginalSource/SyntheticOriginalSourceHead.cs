using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Atomic committed source head referencing an immutable original page chain.</summary>
internal sealed record SyntheticOriginalSourceHead(string RunId, string PageId, NativeSourceProof Proof,
    SyntheticOriginalSourcePageAddress[] Pages);
