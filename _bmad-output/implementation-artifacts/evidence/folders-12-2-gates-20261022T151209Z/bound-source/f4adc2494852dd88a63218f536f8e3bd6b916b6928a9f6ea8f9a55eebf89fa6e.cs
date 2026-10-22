using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Reads only native actor metadata through the existing inspection method, without reading events or state-store keys.</summary>
/// <remarks>Independent resource authorization remains mandatory. Cold actor activation can run existing recovery;
/// this seam makes no installation-wide or persisted-zero-write qualification claim.</remarks>
public interface INativeSourceMetadataReader
{
    /// <summary>Returns supported native incarnation/prefix/erasure evidence for the exact source, or null when unavailable.</summary>
    Task<NativeSourceObservation?> ReadAsync(AggregateIdentity identity, CancellationToken cancellationToken = default);
}
