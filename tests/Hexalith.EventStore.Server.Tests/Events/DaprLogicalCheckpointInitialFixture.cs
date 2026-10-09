using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Composes actual persisted checkpoint candidates with the distinct unsigned initial owner.</summary>
internal sealed class DaprLogicalCheckpointInitialFixture : IAsyncDisposable
{
    /// <summary>Gets the actual candidate source, origin and dedicated checkpoint actor.</summary>
    internal DaprLogicalCheckpointCandidateFixture Candidate { get; } = new();
    /// <summary>Gets the retained shared checkpoint budget.</summary>
    internal EventBufferBudget Budget => Candidate.Checkpoint.Budget;
    /// <summary>Gets the exact original covered-target source.</summary>
    internal DaprLogicalSourceBinding Checkpoint => Candidate.Checkpoint.Binding;
    /// <summary>Gets the exact local declaration whose state the actual completed origin reconstructed.</summary>
    internal DaprLogicalProjectionFold Fold => Candidate.Checkpoint.Fold;

    /// <summary>Persists actual coverage two under a separately selected fixed metadata head.</summary>
    internal async Task PrepareAsync(long head = 2)
    {
        Candidate.Checkpoint.Replay.Source.Metadata = Candidate.Checkpoint.Replay.Source.Metadata!with
        {
            CurrentSequence = head
        };
        await Candidate.PrepareAsync();
    }

    /// <summary>Takes a fresh actual candidate into unsigned preparation, preserving the original operation token.</summary>
    internal async Task<DaprLogicalCheckpointInitial> AdoptAsync(long? target = null, CancellationToken token = default)
    {
        DaprLogicalCheckpointCandidate candidate = await Candidate.AcquireAsync(token);
        return await DaprLogicalCheckpointInitial.AdoptAsync(DaprLogicalCheckpointInitialCodec.ModelId, candidate, Fold, Checkpoint, Checkpoint with { TargetSequence = target ?? Checkpoint.TargetSequence }, Budget, token);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Candidate.DisposeAsync();
}
