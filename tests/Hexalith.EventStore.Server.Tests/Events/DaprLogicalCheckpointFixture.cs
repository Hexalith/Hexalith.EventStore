using Dapr.Actors;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Composes actual completed pure fold history with a separate actual projection actor and durable three-row store.</summary>
internal sealed class DaprLogicalCheckpointFixture : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, byte[]> _staged = [];
    private readonly Dictionary<string, byte[]> _cache = [];
    /// <summary>Creates separate source/replay/projection managers and a locally declared exact pure fold.</summary>
    internal DaprLogicalCheckpointFixture(int maximumBudget = 128 * 1024 * 1024)
    {
        Budget = new EventBufferBudget(maximumBudget);
        Actor = new DaprLogicalCheckpointActor(ActorHost.CreateForTest<DaprLogicalCheckpointActor>(new ActorTestOptions { ActorId = new ActorId("projection:tenant:id") }));
        ActorStateManagerTestHelper.SetStateManager(Actor, Manager);
        _ = Manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            if (FailReads)
            {
                throw new IOException("projection readback unavailable");
            }

            _staged.Clear();
            _cache.Clear();
            if (OnClear is not null)
            {
                await OnClear(call.Arg<CancellationToken>());
            }
        });
        _ = Manager.TryGetStateAsync<byte[]>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            string key = call.ArgAt<string>(0);
            CancellationToken token = call.Arg<CancellationToken>();
            token.ThrowIfCancellationRequested();
            Reads++;
            byte[]? bytes = Durable.TryGetValue(key, out byte[]? image) ? image.ToArray() : null;
            if (bytes is not null)
            {
                _cache[key] = bytes;
                Borrowed.Add(bytes);
            }

            if (OnRead is not null)
            {
                await OnRead(key, token);
            }

            return new ConditionalValue<byte[]>(bytes is not null, bytes!);
        });
        _ = Manager.SetStateAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Stages++;
            string key = call.ArgAt<string>(0);
            byte[] image = call.ArgAt<byte[]>(1);
            _staged[key] = image;
            Aliases.Add(image);
            StageKeys.Add(key);
            if (OnStage is not null)
            {
                await OnStage(key, image, call.Arg<CancellationToken>());
            }
        });
        _ = Manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Saves++;
            call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            if (SaveMode != "no-commit")
            {
                foreach ((string key, byte[] value)in _staged)
                {
                    if (SaveMode != "mixed" || key.EndsWith(":current", StringComparison.Ordinal))
                    {
                        Durable[key] = value.ToArray();
                    }
                }
            }

            _staged.Clear();
            if (OnSave is not null)
            {
                await OnSave(call.Arg<CancellationToken>());
            }

            if (SaveMode != "normal")
            {
                throw new IOException("acknowledgement unavailable");
            }
        });
    }

    /// <summary>Gets actual ordinary reconstruction, used here explicitly as the declared pure projection fold.</summary>
    internal DaprLogicalReconstructionFixture Replay { get; } = new(2);
    /// <summary>Gets the separately owned projection manager.</summary>
    internal IActorStateManager Manager { get; } = Substitute.For<IActorStateManager>();
    /// <summary>Gets the actual actor issuing save calls.</summary>
    internal DaprLogicalCheckpointActor Actor { get; }
    /// <summary>Gets the fixed source at completed coverage two.</summary>
    internal DaprLogicalSourceBinding Binding { get; private set; } = null!;
    /// <summary>Gets the exact local pure projection declaration.</summary>
    internal DaprLogicalProjectionFold Fold { get; private set; } = null!;
    /// <summary>Gets the shared retained parent.</summary>
    internal EventBufferBudget Budget { get; }
    /// <summary>Gets independently persisted exact byte images.</summary>
    internal Dictionary<string, byte[]> Durable { get; } = [];
    /// <summary>Gets SDK-retained private staging aliases.</summary>
    internal List<byte[]> Aliases { get; } = [];
    /// <summary>Gets borrowed SDK materialization arrays.</summary>
    internal List<byte[]> Borrowed { get; } = [];
    /// <summary>Gets staged exact keys.</summary>
    internal List<string> StageKeys { get; } = [];
    /// <summary>Gets stage count.</summary>
    internal int Stages { get; private set; }
    /// <summary>Gets save count.</summary>
    internal int Saves { get; private set; }
    /// <summary>Gets actual projection reads.</summary>
    internal int Reads { get; private set; }
    /// <summary>Gets or sets acknowledgement/durable save behavior.</summary>
    internal string SaveMode { get; set; } = "normal";
    /// <summary>Gets or sets unavailable SDK cache/readback.</summary>
    internal bool FailReads { get; set; }
    /// <summary>Gets or sets an actual asynchronous stage boundary.</summary>
    internal Func<string, byte[], CancellationToken, Task>? OnStage { get; set; }
    /// <summary>Gets or sets an actual asynchronous pair readback boundary.</summary>
    internal Func<string, CancellationToken, Task>? OnRead { get; set; }
    /// <summary>Gets or sets an actual asynchronous save boundary.</summary>
    internal Func<CancellationToken, Task>? OnSave { get; set; }
    /// <summary>Gets or sets cache-release callback.</summary>
    internal Func<CancellationToken, Task>? OnClear { get; set; }
    /// <summary>Gets or sets serialized decision behavior.</summary>
    internal string FenceMode { get; set; } = "normal";
    /// <summary>Gets actual read count before a repeated decision attempt.</summary>
    internal int BeforeRepeatReads { get; private set; }
    /// <summary>Gets a started decision for bounded early-return cleanup controls.</summary>
    internal Task? LastDecision { get; private set; }

    /// <summary>Completes a real full-prefix operation using this exact pure fold before checkpoint admission.</summary>
    internal async Task PrepareAsync()
    {
        Binding = await Replay.Source.Source.CaptureBindingAsync("r", 2, CancellationToken.None);
        using (DaprReplayOperationResult begin = await Replay.Owner.BeginAsync(Replay.Source.Source, Binding, Replay.Source.Trust, "owner", null, CancellationToken.None))
        {
            begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        }

        using DaprReplayOperationResult page = await Replay.Owner.ExecutePageAsync(Replay.Source.Source, Binding, Replay.Source.Trust, Replay.Source.Key, "owner", 1, 1, "request", 2, CancellationToken.None);
        page.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        Fold = new DaprLogicalProjectionFold(Binding, "projection", "projection:tenant:id", "projection-store", new byte[32], Replay.Binding, Budget);
    }

    /// <summary>Calls first issuance or exact actual read-only verification.</summary>
    internal Task<DaprReplayCommitOutcome> CallAsync(bool readOnly = false, CancellationToken token = default, DaprLogicalProjectionFold? fold = null, DaprLogicalSourceBinding? binding = null, int maximumStateBytes = 32) => readOnly ? Actor.ReadLogicalCheckpointAsync(DaprLogicalCheckpointCodec.ModelId, fold ?? Fold, binding ?? Binding, Replay.Source.Source, Replay.Source.Trust, Replay.Owner, maximumStateBytes, SerializeAsync, token) : Actor.IssueLogicalCheckpointAsync(DaprLogicalCheckpointCodec.ModelId, fold ?? Fold, binding ?? Binding, Replay.Source.Source, Replay.Source.Trust, Replay.Owner, maximumStateBytes, SerializeAsync, token);
    private async Task SerializeAsync(Func<CancellationToken, Task> decision, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (FenceMode == "skip")
            {
                return;
            }

            using var foreign = new CancellationTokenSource();
            LastDecision = decision(FenceMode == "foreign" ? foreign.Token : token);
            if (FenceMode == "early")
            {
                return;
            }

            await LastDecision;
            if (FenceMode == "repeat")
            {
                BeforeRepeatReads = Reads;
                await decision(token);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Fold?.Dispose();
        Budget.Dispose();
        await Replay.DisposeAsync();
        _gate.Dispose();
    }
}
