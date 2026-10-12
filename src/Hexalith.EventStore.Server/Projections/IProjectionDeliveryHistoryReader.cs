using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using System.Runtime.CompilerServices;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Loads an authoritative readable EventStore prefix without invoking projection handlers.</summary>
public interface IProjectionDeliveryHistoryReader {
    /// <summary>Reads the exact contiguous prefix through the supplied aggregate-local sequence.</summary>
    Task<IReadOnlyList<ProjectionEventDto>> ReadAsync(
        AggregateIdentity identity,
        long throughSequence,
        CancellationToken cancellationToken = default);

    /// <summary>Reads bounded authoritative pages through the exact owner sequence.</summary>
    async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesAsync(
        AggregateIdentity identity,
        long throughSequence,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return [.. await ReadAsync(identity, throughSequence, cancellationToken).ConfigureAwait(false)];
    }

    /// <summary>Reads bounded authoritative pages strictly after an accepted owner checkpoint.</summary>
    async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesFromAsync(
        AggregateIdentity identity,
        long afterSequence,
        long throughSequence,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(throughSequence, afterSequence);
        await foreach (ProjectionEventDto[] page in ReadPagesAsync(identity, throughSequence,
            cancellationToken).ConfigureAwait(false))
        {
            ProjectionEventDto[] suffix = [.. page.Where(item => item.SequenceNumber > afterSequence)];
            if (suffix.Length > 0)
                yield return suffix;
        }
    }
}
