namespace Hexalith.EventStore.DomainService;

/// <summary>Opaque immutable shared-projection candidate owned by one rebuild handler.</summary>
public sealed class DomainSharedProjectionRebuildCandidate {
    private readonly byte[] _state;

    /// <summary>Initializes a candidate from its handler-defined durable state.</summary>
    /// <param name="state">The opaque candidate bytes.</param>
    public DomainSharedProjectionRebuildCandidate(ReadOnlySpan<byte> state) => _state = state.ToArray();

    /// <summary>Gets the immutable handler-defined candidate state.</summary>
    public ReadOnlyMemory<byte> State => _state;

    internal byte[] CopyState() => _state.ToArray();
}
