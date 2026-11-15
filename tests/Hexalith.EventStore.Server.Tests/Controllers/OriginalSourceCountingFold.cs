using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>Bounded counter for the independently supplied original source qualification.</summary>
internal sealed class OriginalSourceCountingFold : INativeSourceScanFold<int>
{
    private int _count;
    /// <inheritdoc/>
    public bool TryApply(StreamReadEvent source) => source.SequenceNumber == ++_count;
    /// <inheritdoc/>
    public int Complete() => _count;
}
