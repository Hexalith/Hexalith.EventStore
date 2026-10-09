using System.Runtime.InteropServices;
using System.Reflection;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Advances one actual source head and independently completes the same covered prefix under the new binding.</summary>
internal sealed class DaprLogicalSnapshotRewitnessFixture : IAsyncDisposable
{
    /// <summary>Creates a separate actual current-prefix operation over the same addressed source and codec.</summary>
    internal DaprLogicalSnapshotRewitnessFixture()
    {
        CurrentOwner = new DaprReplayOperationOwner(CurrentStore.Manager, "tenant", "current-prefix", 128 * 1024 * 1024, Actor.Snapshot.Replay.Binding);
    }

    /// <summary>Gets the actual aggregate, source and retained old pair.</summary>
    internal DaprLogicalAnchorFixture Actor { get; } = new();
    /// <summary>Gets the current-prefix participant owner.</summary>
    internal DaprReplayOperationOwner CurrentOwner { get; }
    /// <summary>Gets exact independent durable current-prefix participants.</summary>
    internal DaprReplayTestStore CurrentStore { get; } = new();
    /// <summary>Gets the observed old binding, which confers no historical authority after head advancement.</summary>
    internal DaprLogicalSourceBinding PriorHint { get; private set; } = null!;
    /// <summary>Gets the new actual fixed source targeted at the same coverage.</summary>
    internal DaprLogicalSourceBinding Current { get; private set; } = null!;
    /// <summary>Gets actual current-origin acquisition count.</summary>
    internal int OriginCalls { get; private set; }
    /// <summary>Gets common owner-decision count.</summary>
    internal int OwnerCalls { get; private set; }
    /// <summary>Gets whether the current origin has returned, allowing callbacks to target composed intake independently of capture.</summary>
    internal bool OriginReturned { get; private set; }
    /// <summary>Gets captured private current-origin arrays for clearing controls.</summary>
    internal List<byte[]> OriginArrays { get; } = [];
    /// <summary>Gets or sets a yielding callback after actual origin capture.</summary>
    internal Func<DaprLogicalReplayAnchorOrigin, CancellationToken, Task>? OnOrigin { get; set; }
    /// <summary>Gets the charged pending attempt for direct lifetime and policy controls.</summary>
    internal DaprLogicalSnapshotWrite? Pending => (DaprLogicalSnapshotWrite? )typeof(Hexalith.EventStore.Server.Actors.AggregateActor).GetField("_logicalSnapshotPending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Actor.Actor);

    /// <summary>Completes the old head-one prefix, advances metadata, then reconstructs the full same prefix at actual head three.</summary>
    internal async Task PrepareAsync()
    {
        Actor.Snapshot.Replay.Source.Metadata = new AggregateMetadata(1, DaprLogicalReplayFixture.Timestamp, "old-etag");
        await Actor.Snapshot.PrepareAsync();
        PriorHint = Actor.Snapshot.OriginBinding;
        Actor.Snapshot.Replay.Source.Metadata = new AggregateMetadata(3, DaprLogicalReplayFixture.Timestamp.AddSeconds(1), "new-etag");
        Current = await Actor.Snapshot.SourceAsync(1);
        using (DaprReplayOperationResult begin = await CurrentOwner.BeginAsync(Actor.Snapshot.Replay.Source.Source, Current, Actor.Snapshot.Replay.Source.Trust, "current-owner", null, CancellationToken.None))
        {
            begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        }

        using DaprReplayOperationResult page = await CurrentOwner.ExecutePageAsync(Actor.Snapshot.Replay.Source.Source, Current, Actor.Snapshot.Replay.Source.Trust, Actor.Snapshot.Replay.Source.Key, "current-owner", 1, 1, "current-request", 1, CancellationToken.None);
        page.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
    }

    /// <summary>Captures only the actual full current-prefix history under the caller's exact parent and original token.</summary>
    internal async Task<DaprLogicalReplayAnchorOrigin> AcquireAsync(long target, EventBufferBudget budget, CancellationToken token)
    {
        OriginCalls++;
        OriginReturned = false;
        DaprLogicalReplayAnchorOrigin origin = await CurrentOwner.CaptureCompletedAnchorOriginAsync(Actor.Snapshot.Replay.Source.Source, Current with { TargetSequence = target }, Actor.Snapshot.Replay.Source.Trust, budget, token);
        try
        {
            MemoryMarshal.TryGetArray(origin.State, out ArraySegment<byte> array).ShouldBeTrue();
            OriginArrays.Add(array.Array!);
            if (OnOrigin is not null)
            {
                await OnOrigin(origin, token);
            }

            token.ThrowIfCancellationRequested();
            OriginReturned = true;
            return origin;
        }
        catch
        {
            origin.Dispose();
            token.ThrowIfCancellationRequested();
            throw;
        }
    }

    /// <summary>Invokes the distinct internal policy; defaults use the real earlier/current head pair.</summary>
    internal Task<DaprReplayCommitOutcome> RewitnessAsync(EventBufferBudget budget, CancellationToken token = default, string model = DaprLogicalSnapshotRewitnessPolicy.ModelId, DaprLogicalSourceBinding? prior = null, DaprLogicalSourceBinding? current = null, int maximumStateBytes = 32) => Actor.Actor.RewitnessLogicalSnapshotAsync(model, prior ?? PriorHint, current ?? Current, Actor.Snapshot.Replay.Source.Source, Actor.Snapshot.Replay.Source.Trust, Actor.Snapshot.Replay.Binding, maximumStateBytes, AcquireAsync, SerializeAsync, budget, token);
    /// <summary>Calls initial issuance against the current origin for cross-policy recovery controls.</summary>
    internal Task<DaprReplayCommitOutcome> IssueCurrentAsync(EventBufferBudget budget) => Actor.Actor.IssueLogicalSnapshotAsync(DaprLogicalSnapshotCodec.SnapshotModel, Current, Actor.Snapshot.Replay.Source.Source, Actor.Snapshot.Replay.Source.Trust, Actor.Snapshot.Replay.Binding, 32, AcquireAsync, SerializeAsync, budget, CancellationToken.None);
    /// <summary>Calls explicit replacement against the same current origin for cross-policy recovery controls.</summary>
    internal Task<DaprReplayCommitOutcome> ReplaceCurrentAsync(EventBufferBudget budget) => Actor.Actor.ReplaceLogicalSnapshotAsync(DaprLogicalSnapshotPrior.ModelId, Current, Actor.Snapshot.Replay.Source.Source, Actor.Snapshot.Replay.Source.Trust, Actor.Snapshot.Replay.Binding, 32, AcquireAsync, SerializeAsync, budget, CancellationToken.None);
    private Task SerializeAsync(Func<CancellationToken, Task> decision, CancellationToken token)
    {
        OwnerCalls++;
        return Actor.Snapshot.SerializeOwnerAsync(decision, token);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await CurrentOwner.DisposeAsync();
        await Actor.DisposeAsync();
    }
}
