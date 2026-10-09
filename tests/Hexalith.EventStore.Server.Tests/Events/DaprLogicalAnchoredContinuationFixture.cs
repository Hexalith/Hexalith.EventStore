using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using System.Reflection;
using System.Runtime.InteropServices;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Composes actual completed origin, paired snapshot intake and a separately addressed anchored replay owner.</summary>
internal sealed class DaprLogicalAnchoredContinuationFixture : IAsyncDisposable
{
    private DaprReplayOperationOwner? _owner;
    /// <summary>Bounds substitute diagnostic history while retaining configured callbacks, real state bytes and explicit read/save counters.</summary>
    internal DaprLogicalAnchoredContinuationFixture()
    {
        Snapshot.OnCacheClear = _ =>
        {
            Snapshot.Replay.Source.SourceState.ClearReceivedCalls();
            Snapshot.Replay.Store.Manager.ClearReceivedCalls();
            Store.Manager.ClearReceivedCalls();
            return Task.CompletedTask;
        };
    }

    /// <summary>Gets the actual snapshot/origin fixture without replacing its owner checks.</summary>
    internal DaprLogicalSnapshotFixture Snapshot { get; } = new();
    /// <summary>Gets the separate anchored operation's durable actor-state seam.</summary>
    internal DaprReplayTestStore Store { get; } = new();
    /// <summary>Gets the single shared capacity owner retained across admission and operation callbacks.</summary>
    internal EventBufferBudget Budget { get; } = new();
    /// <summary>Gets the explicitly selected new-model trust using the exact ordinary route key.</summary>
    internal DaprLogicalAnchoredClaimTrust Trust { get; private set; } = null!;
    /// <summary>Gets the requested actual source binding.</summary>
    internal DaprLogicalSourceBinding Source { get; private set; } = null!;
    /// <summary>Gets the actual anchored operation after successful private initial transfer.</summary>
    internal DaprReplayOperationOwner Owner => _owner!;
    /// <summary>Gets the originating operation token, retained unchanged through Begin, page, retry and takeover.</summary>
    internal CancellationToken Token { get; private set; }
    /// <summary>Gets actual private initial arrays captured solely to observe refusal clearing.</summary>
    internal List<byte[]> CapturedInitialArrays { get; } = [];

    /// <summary>Admits real completed-origin snapshot bytes before privately transferring the initial state.</summary>
    internal async Task PrepareAsync(long covered = 1, long target = 3, CancellationToken token = default, bool exhaustConstructorCapacity = false)
    {
        await Snapshot.PrepareAsync(covered).ConfigureAwait(false);
        Token = token;
        Source = await Snapshot.SourceAsync(target, token).ConfigureAwait(false);
        DaprLogicalSnapshotCandidate candidate = (await Snapshot.Owner.AcquireAsync(Source, false, Snapshot.AcquireOriginAsync, Budget, token).ConfigureAwait(false))!;
        DaprLogicalReplayInitialAnchor initial = await DaprLogicalReplayInitialAnchor.AdoptAsync(DaprLogicalReplayAnchorCodec.ModelId, candidate, Source, Snapshot.Replay.Binding, Budget, token).ConfigureAwait(false);
        foreach (ReadOnlyMemory<byte> bytes in new[]
        {
            initial.SelectionImage,
            initial.SelectionHash,
            initial.AccumulatorSeed,
            initial.EffectiveSeed,
            initial.TranscriptSeed
        }

        )
        {
            MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment);
            CapturedInitialArrays.Add(segment.Array!);
        }

        CapturedInitialArrays.Add((byte[])initial.CanonicalState.GetType().GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(initial.CanonicalState)!);
        Trust = new DaprLogicalAnchoredClaimTrust(DaprLogicalReplayAnchorCodec.ModelId, Snapshot.Replay.Source.Trust, token);
        EventBufferReservation? exhausted = exhaustConstructorCapacity ? Budget.Reserve(128 * 1024 * 1024 - Budget.LiveBytes) : null;
        try
        {
            _owner = new DaprReplayOperationOwner(Store.Manager, "tenant", "anchored-operation", Snapshot.Replay.Binding, initial, Trust, token);
        }
        finally
        {
            exhausted?.Dispose();
        }
    }

    /// <summary>Invokes the actual owner Begin or takeover protocol with the retained originating token.</summary>
    internal Task<DaprReplayOperationResult> BeginAsync(string owner = "owner", long? generation = null) => Owner.BeginAsync(Snapshot.Replay.Source.Source, Source, Snapshot.Replay.Source.Trust, owner, generation, Token);
    /// <summary>Invokes actual source preparation, reconstruction, staging and independent save readback.</summary>
    internal Task<DaprReplayOperationResult> ExecuteAsync(long ordinal = 1, int count = 1, string owner = "owner", long generation = 1) => Owner.ExecutePageAsync(Snapshot.Replay.Source.Source, Source, Snapshot.Replay.Source.Trust, Snapshot.Replay.Source.Key, owner, generation, ordinal, "request", count, Token);
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_owner is not null)
        {
            await _owner.DisposeAsync().ConfigureAwait(false);
        }

        Trust?.Dispose();
        await Snapshot.DisposeAsync().ConfigureAwait(false);
        Budget.Dispose();
    }
}
