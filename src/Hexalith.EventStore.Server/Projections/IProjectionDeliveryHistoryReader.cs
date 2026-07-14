using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Loads an authoritative readable EventStore prefix without invoking projection handlers.</summary>
internal interface IProjectionDeliveryHistoryReader {
    /// <summary>Reads the exact contiguous prefix through the supplied aggregate-local sequence.</summary>
    Task<IReadOnlyList<ProjectionEventDto>> ReadAsync(
        AggregateIdentity identity,
        long throughSequence,
        CancellationToken cancellationToken = default);
}
