using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Composes actual full-history replay origins with a separate pair on the same addressed source actor.</summary>
internal sealed class DaprLogicalSnapshotFixture : IAsyncDisposable
{
    private readonly Dictionary<string, byte[]> _cache = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>Creates trusted local declarations and a serial decision fixture.</summary>
    internal DaprLogicalSnapshotFixture(int maximumStateBytes = 32)
    {
        Replay = new DaprLogicalReconstructionFixture(3);
        Owner = new DaprLogicalSnapshotOwner(DaprLogicalSnapshotCodec.SnapshotModel, Replay.Source.SourceState, DaprLogicalReplayFixture.Identity, Replay.Source.Source, Replay.Source.Trust, Replay.Binding, maximumStateBytes, SerializeAsync);
        _ = Replay.Source.SourceState.TryGetStateAsync<byte[]>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            CancellationToken token = call.Arg<CancellationToken>();
            token.ThrowIfCancellationRequested();
            string key = call.ArgAt<string>(0);
            SnapshotReads++;
            byte[]? value = Durable.TryGetValue(key, out byte[]? stored) ? stored.ToArray() : null;
            if (value is not null)
            {
                _cache[key] = value;
                Borrowed.Add(value);
            }

            if (OnRead is not null)
            {
                await OnRead(key, token);
            }

            token.ThrowIfCancellationRequested();
            return new ConditionalValue<byte[]>(value is not null, value!);
        });
        _ = Replay.Source.SourceState.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            _cache.Clear();
            if (OnCacheClear is not null)
            {
                await OnCacheClear(call.Arg<CancellationToken>());
            }
        });
    }

    /// <summary>Gets the real shared replay and reconstruction path.</summary>
    internal DaprLogicalReconstructionFixture Replay { get; }
    /// <summary>Gets the read-only logical snapshot owner.</summary>
    internal DaprLogicalSnapshotOwner Owner { get; }
    /// <summary>Gets the actual source actor's independent persisted pair.</summary>
    internal Dictionary<string, byte[]> Durable { get; } = [];
    /// <summary>Gets typed source-cache arrays for preservation assertions.</summary>
    internal List<byte[]> Borrowed { get; } = [];
    /// <summary>Gets or sets an actually yielding source pair read callback.</summary>
    internal Func<string, CancellationToken, Task>? OnRead { get; set; }
    /// <summary>Gets or sets an actually yielding cache-clear callback after borrowed actor references are dropped.</summary>
    internal Func<CancellationToken, Task>? OnCacheClear { get; set; }
    /// <summary>Gets or sets a callback after the serialized decision completes.</summary>
    internal Action? AfterDecision { get; set; }
    /// <summary>Gets or sets normal, skip, repeat, foreign or early owner behavior.</summary>
    internal string FenceMode { get; set; } = "normal";
    /// <summary>Gets the last started serialized decision so negative early-return controls can observe complete cleanup.</summary>
    internal Task? LastDecision { get; private set; }
    /// <summary>Gets actual byte-pair reads.</summary>
    internal int SnapshotReads { get; private set; }
    /// <summary>Gets completed-origin acquisition calls.</summary>
    internal int OriginCalls { get; private set; }
    /// <summary>Gets or sets a hook after a completed origin has been privately captured.</summary>
    internal Action<DaprLogicalReplayAnchorOrigin>? OnOrigin { get; set; }
    /// <summary>Gets or sets a deliberately missing origin callback response.</summary>
    internal bool NullOrigin { get; set; }
    /// <summary>Gets the pinned source targeted at snapshot coverage.</summary>
    internal DaprLogicalSourceBinding OriginBinding { get; private set; } = null!;

    /// <summary>Produces real completed history, then persists a candidate pair for readback controls.</summary>
    internal async Task PrepareAsync(long covered = 1)
    {
        OriginBinding = await Replay.Source.Source.CaptureBindingAsync("r", covered, CancellationToken.None);
        using (DaprReplayOperationResult begin = await Replay.Owner.BeginAsync(Replay.Source.Source, OriginBinding, Replay.Source.Trust, "owner", null, CancellationToken.None))
        {
        }

        for (long ordinal = 1; ordinal <= covered; ordinal++)
        {
            using DaprReplayOperationResult page = await Replay.Owner.ExecutePageAsync(Replay.Source.Source, OriginBinding, Replay.Source.Trust, Replay.Source.Key, "owner", 1, ordinal, "request-" + ordinal, 1, CancellationToken.None);
            if (page.Outcome != DaprReplayCommitOutcome.Proven)
            {
                throw new InvalidOperationException("fixture completion failed");
            }
        }

        using var budget = new EventBufferBudget();
        using DaprLogicalReplayAnchorOrigin origin = await Replay.Owner.CaptureCompletedAnchorOriginAsync(Replay.Source.Source, OriginBinding, Replay.Source.Trust, budget, CancellationToken.None);
        Durable[Owner.StorageKey] = origin.State.ToArray();
        Durable[Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(origin.CreateWitness(Owner.StorageKey, Owner.WitnessKey));
    }

    /// <summary>Acquires only actual owner-verified origin state under the same composed parent budget.</summary>
    internal async Task<DaprLogicalReplayAnchorOrigin> AcquireOriginAsync(long covered, EventBufferBudget budget, CancellationToken token)
    {
        OriginCalls++;
        if (NullOrigin)
        {
            return null!;
        }

        DaprLogicalReplayAnchorOrigin origin = await Replay.Owner.CaptureCompletedAnchorOriginAsync(Replay.Source.Source, OriginBinding with { TargetSequence = covered }, Replay.Source.Trust, budget, token);
        try
        {
            OnOrigin?.Invoke(origin);
            token.ThrowIfCancellationRequested();
            return origin;
        }
        catch
        {
            origin.Dispose();
            throw;
        }
    }

    /// <summary>Reads a requested fixed target from actual source metadata.</summary>
    internal Task<DaprLogicalSourceBinding> SourceAsync(long target = 3, CancellationToken token = default) => Replay.Source.Source.CaptureBindingAsync("r", target, token);
    /// <summary>Runs the local common serialized decision fixture for the actual source actor issuer.</summary>
    internal Task SerializeOwnerAsync(Func<CancellationToken, Task> decision, CancellationToken token) => SerializeAsync(decision, token);
    private async Task SerializeAsync(Func<CancellationToken, Task> decision, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (FenceMode == "skip")
            {
                return;
            }

            if (FenceMode == "foreign")
            {
                LastDecision = decision(new CancellationToken(true));
                await LastDecision;
                return;
            }

            if (FenceMode == "early")
            {
                LastDecision = decision(token);
                return;
            }

            LastDecision = decision(token);
            await LastDecision;
            if (FenceMode == "repeat")
            {
                LastDecision = decision(token);
                await LastDecision;
            }

            AfterDecision?.Invoke();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Replay.DisposeAsync();
        _gate.Dispose();
    }
}
