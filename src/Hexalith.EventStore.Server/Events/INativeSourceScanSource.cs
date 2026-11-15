using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Events;

/// <summary>An owner-selected native source used only after the scan receiver has authorized the exact request.</summary>
public interface INativeSourceScanSource
{
    /// <summary>Identifies an explicitly selected resource namespace; missing selected sources must refuse without actor fallback.</summary>
    bool IsSelected(AggregateIdentity identity);

    /// <summary>Reads a bounded page from independently committed original source, or refuses unavailable evidence.</summary>
    Task<StreamReadPage> ReadAsync(StreamReadRequest request, CancellationToken cancellationToken);
}
