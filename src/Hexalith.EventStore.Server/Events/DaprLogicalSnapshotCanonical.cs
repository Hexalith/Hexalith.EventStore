using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Server.Events;
/// <summary>Admits a privately copied snapshot image through the pinned canonical codec under retained graph capacity.</summary>
internal static class DaprLogicalSnapshotCanonical
{
    /// <summary>Requires an exact read/write roundtrip and originating-token fences before releasing private codec workspace.</summary>
    internal static async Task RequireAsync(ReadOnlyMemory<byte> state, int witnessBytes, RegisteredLogicalReplayBinding reconstruction, EventBufferBudget budget, Func<CancellationToken, Task> fence, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using EventBufferBudget partition = budget.CreatePartition(reconstruction.GetCommandCapacity(state.Length, witnessBytes, 0));
        using EventBufferReservation graphs = reconstruction.ReserveCommandGraphs(partition);
        EventBufferReservation copy = partition.Reserve(state.Length);
        ImmutablePayload canonical;
        try
        {
            canonical = new ImmutablePayload(state.ToArray(), state.Length, token, copy);
        }
        catch
        {
            copy.Dispose();
            throw;
        }

        using (canonical)
        {
            try
            {
                _ = await reconstruction.ReadCommandStateAsync(canonical, partition, fence, token).ConfigureAwait(false);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }
        }
    }
}
