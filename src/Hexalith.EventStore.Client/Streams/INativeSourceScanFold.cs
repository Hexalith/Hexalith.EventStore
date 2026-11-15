using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>A domain-owned bounded-memory selector, with no persistence or source-proof authority.</summary>
public interface INativeSourceScanFold<out T>
{
    /// <summary>Applies one contiguous readable source position; false refuses unsupported or inconsistent source.</summary>
    bool TryApply(StreamReadEvent sourceEvent);

    /// <summary>Returns the selected facts; the scanner withholds them until the whole observation is verified.</summary>
    T Complete();
}
