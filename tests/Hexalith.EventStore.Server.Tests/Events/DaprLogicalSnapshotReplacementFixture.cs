using System.Runtime.InteropServices;
using System.Reflection;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Composes one actual pair actor and two independently completed real replay operations against the same fixed source.</summary>
internal sealed class DaprLogicalSnapshotReplacementFixture : IAsyncDisposable
{
    /// <summary>Creates the new completed owner without substituting history or proof DTOs.</summary>
    internal DaprLogicalSnapshotReplacementFixture()
    {
        NewOwner = new DaprReplayOperationOwner(NewStore.Manager, "tenant", "new-operation", 128 * 1024 * 1024, Actor.Snapshot.Replay.Binding);
    }

    /// <summary>Gets the actual aggregate actor and older completed history.</summary>
    internal DaprLogicalAnchorFixture Actor { get; } = new();
    /// <summary>Gets the independent durable new operation participants.</summary>
    internal DaprReplayTestStore NewStore { get; } = new();
    /// <summary>Gets the actual new completed-operation owner.</summary>
    internal DaprReplayOperationOwner NewOwner { get; }
    /// <summary>Gets the desired exact fixed-source target.</summary>
    internal DaprLogicalSourceBinding Binding { get; private set; } = null!;
    /// <summary>Gets captured private origin arrays for full-capacity clearing assertions.</summary>
    internal List<byte[]> OriginArrays { get; } = [];
    /// <summary>Gets exact retained private prior arrays independently of SDK and origin copies.</summary>
    internal List<byte[]> PriorArrays { get; } = [];
    /// <summary>Gets origin acquisition targets in execution order.</summary>
    internal List<long> OriginTargets { get; } = [];
    /// <summary>Gets supplied common serialized owner calls.</summary>
    internal int OwnerFences { get; private set; }
    /// <summary>Gets or sets an actual asynchronous callback after each origin capture.</summary>
    internal Func<long, DaprLogicalReplayAnchorOrigin, CancellationToken, Task>? OnOrigin { get; set; }
    /// <summary>Gets or sets a missing prior origin.</summary>
    internal bool MissingPrior { get; set; }
    /// <summary>Gets or sets routing to the older completed owner after desired-origin admission, including equal coverage.</summary>
    internal bool PriorOwnerAfterDesired { get; set; }

    /// <summary>Completes actual older and desired histories before retaining the independently encoded older pair.</summary>
    internal async Task PrepareAsync(long covered = 1, long target = 3)
    {
        await Actor.Snapshot.PrepareAsync(covered);
        Binding = await Actor.Snapshot.SourceAsync(target);
        using (DaprReplayOperationResult begin = await NewOwner.BeginAsync(Actor.Snapshot.Replay.Source.Source, Binding, Actor.Snapshot.Replay.Source.Trust, "new-owner", null, CancellationToken.None))
        {
            begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        }

        for (long ordinal = 1; ordinal <= target; ordinal++)
        {
            using DaprReplayOperationResult page = await NewOwner.ExecutePageAsync(Actor.Snapshot.Replay.Source.Source, Binding, Actor.Snapshot.Replay.Source.Trust, Actor.Snapshot.Replay.Source.Key, "new-owner", 1, ordinal, "new-request-" + ordinal, 1, CancellationToken.None);
            page.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        }
    }

    /// <summary>Captures only the corresponding actual completed owner with the caller's exact composed parent.</summary>
    internal async Task<DaprLogicalReplayAnchorOrigin> AcquireOriginAsync(long covered, EventBufferBudget budget, CancellationToken token)
    {
        OriginTargets.Add(covered);
        if (MissingPrior && covered != Binding.TargetSequence)
        {
            return null!;
        }

        DaprLogicalReplayAnchorOrigin origin = covered == Binding.TargetSequence && !(PriorOwnerAfterDesired && OriginTargets.Count > 1) ? await NewOwner.CaptureCompletedAnchorOriginAsync(Actor.Snapshot.Replay.Source.Source, Binding, Actor.Snapshot.Replay.Source.Trust, budget, token) : await Actor.Snapshot.AcquireOriginAsync(covered, budget, token);
        try
        {
            MemoryMarshal.TryGetArray(origin.State, out ArraySegment<byte> bytes).ShouldBeTrue();
            OriginArrays.Add(bytes.Array!);
            if (covered != Binding.TargetSequence || PriorOwnerAfterDesired && OriginTargets.Count > 1)
            {
                DaprLogicalSnapshotWrite? write = (DaprLogicalSnapshotWrite? )Actor.Actor.GetType().GetField("_logicalSnapshotPending", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Actor.Actor);
                if (write is not null)
                {
                    foreach (DaprLogicalResponseOwner owner in new[]
                    {
                        write.PriorState,
                        write.PriorWitness
                    }

                    )
                    {
                        MemoryMarshal.TryGetArray(owner.Bytes, out ArraySegment<byte> prior).ShouldBeTrue();
                        PriorArrays.Add(prior.Array!);
                    }
                }
            }

            if (OnOrigin is not null)
            {
                await OnOrigin(covered, origin, token);
            }

            token.ThrowIfCancellationRequested();
            return origin;
        }
        catch
        {
            origin.Dispose();
            token.ThrowIfCancellationRequested();
            throw;
        }
    }

    /// <summary>Calls the explicit dormant replacement entry with one common serialized decision.</summary>
    internal Task<DaprReplayCommitOutcome> ReplaceAsync(EventBufferBudget budget, CancellationToken token = default, string model = DaprLogicalSnapshotPrior.ModelId, int maximumStateBytes = 32) => Actor.Actor.ReplaceLogicalSnapshotAsync(model, Binding, Actor.Snapshot.Replay.Source.Source, Actor.Snapshot.Replay.Source.Trust, Actor.Snapshot.Replay.Binding, maximumStateBytes, AcquireOriginAsync, SerializeAsync, budget, token);
    private Task SerializeAsync(Func<CancellationToken, Task> decision, CancellationToken token)
    {
        OwnerFences++;
        return Actor.Snapshot.SerializeOwnerAsync(decision, token);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await NewOwner.DisposeAsync();
        await Actor.DisposeAsync();
    }
}
