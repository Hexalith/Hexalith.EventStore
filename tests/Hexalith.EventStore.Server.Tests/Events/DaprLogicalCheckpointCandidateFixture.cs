using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Composes the existing actual checkpoint fixture with counted candidate owner decisions.</summary>
internal sealed class DaprLogicalCheckpointCandidateFixture : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>Gets the actual source/completed-operation/checkpoint composition.</summary>
    internal DaprLogicalCheckpointFixture Checkpoint { get; } = new();
    /// <summary>Gets completed candidate capture decisions.</summary>
    internal int Decisions { get; private set; }
    /// <summary>Gets or sets a boundary before actual capture for yielding/cancellation controls.</summary>
    internal Func<int, CancellationToken, Task>? Before { get; set; }
    /// <summary>Gets or sets an independently serialized advancement between completed capture decisions.</summary>
    internal Func<int, CancellationToken, Task>? After { get; set; }
    /// <summary>Gets or sets missing/repeated/foreign decision behavior.</summary>
    internal string Mode { get; set; } = "normal";
    /// <summary>Gets an early-return decision for bounded late cleanup controls.</summary>
    internal Task? LastDecision { get; private set; }

    /// <summary>Produces the actual full prefix and three persisted checkpoint rows.</summary>
    internal async Task PrepareAsync()
    {
        await Checkpoint.PrepareAsync();
        (await Checkpoint.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Proven);
    }

    /// <summary>Calls only the distinct internal private candidate entry.</summary>
    internal Task<DaprLogicalCheckpointCandidate> AcquireAsync(CancellationToken token = default, string model = DaprLogicalCheckpointCandidate.ModelId, int maximumStateBytes = 32) => Checkpoint.Actor.AcquireLogicalCheckpointCandidateAsync(model, Checkpoint.Fold, Checkpoint.Binding, Checkpoint.Replay.Source.Source, Checkpoint.Replay.Source.Trust, Checkpoint.Replay.Owner, maximumStateBytes, SerializeAsync, token);
    private async Task SerializeAsync(Func<CancellationToken, Task> decision, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        int ordinal = ++Decisions;
        try
        {
            if (Before is not null)
            {
                await Before(ordinal, token);
            }

            if (Mode == "skip")
            {
                return;
            }

            using var foreign = new CancellationTokenSource();
            LastDecision = decision(Mode == "foreign" ? foreign.Token : token);
            if (Mode == "early")
            {
                return;
            }

            await LastDecision;
            if (Mode == "repeat")
            {
                await decision(token);
            }
        }
        finally
        {
            _gate.Release();
        }

        if (After is not null)
        {
            await After(ordinal, token);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Checkpoint.DisposeAsync();
        _gate.Dispose();
    }
}
