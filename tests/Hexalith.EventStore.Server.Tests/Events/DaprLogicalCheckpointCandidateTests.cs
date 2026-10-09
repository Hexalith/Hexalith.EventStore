using System.Reflection;
using System.Runtime.InteropServices;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Exercises actual private checkpoint capture, retained currentness and ownership without serving authority.</summary>
public sealed class DaprLogicalCheckpointCandidateTests
{
    /// <summary>Retains charged detached copies and checks actual participants without events, effects or durable changes.</summary>
    [Fact]
    public async Task ActualCandidateRetainsThreeDetachedImagesAndRevalidatesWithoutEffects()
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        var checkpoint = fixture.Checkpoint;
        int budget = checkpoint.Budget.LiveBytes;
        int events = checkpoint.Replay.Source.EventReads;
        int applies = checkpoint.Replay.Applies;
        int stages = checkpoint.Stages;
        int saves = checkpoint.Saves;
        int history = 0;
        checkpoint.Replay.Store.OnRead = _ => history++;
        var durable = checkpoint.Durable.ToDictionary(row => row.Key, row => row.Value.ToArray());
        var candidate = await fixture.AcquireAsync();
        fixture.Decisions.ShouldBe(2);
        checkpoint.Budget.LiveBytes.ShouldBeGreaterThan(budget + candidate.State.Length + candidate.Root.Length + candidate.Witness.Length);
        checkpoint.Budget.LiveBytes.ShouldBe(budget + 8192 + 3 * 256 + 2 * (candidate.State.Length + candidate.Root.Length + candidate.Witness.Length));
        candidate.State.ToArray().ShouldBe(durable[checkpoint.StageKeys[0]]);
        candidate.Root.ToArray().ShouldBe(durable[checkpoint.StageKeys[1]]);
        candidate.Witness.ToArray().ShouldBe(durable[checkpoint.StageKeys[2]]);
        candidate.CoveredSequence.ShouldBe(2L);
        byte[][] captured = CaptureArrays(candidate).Concat(CapturePins(candidate)).ToArray();
        await candidate.RequireCurrentAsync(CancellationToken.None);
        fixture.Decisions.ShouldBe(3);
        history.ShouldBeGreaterThan(0);
        checkpoint.Replay.Source.EventReads.ShouldBe(events);
        checkpoint.Replay.Applies.ShouldBe(applies);
        checkpoint.Stages.ShouldBe(stages);
        checkpoint.Saves.ShouldBe(saves);
        foreach ((string key, byte[] value)in durable)
        {
            checkpoint.Durable[key].ShouldBe(value);
        }

        candidate.Dispose();
        captured.ShouldAllBe(image => image.All(value => value == 0));
        checkpoint.Budget.LiveBytes.ShouldBe(budget);
        typeof(DaprLogicalCheckpointCandidate).GetField("_images", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(candidate).ShouldBeNull();
        typeof(DaprLogicalCheckpointCandidate).GetField("_refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(candidate).ShouldBeNull();
        Should.Throw<ObjectDisposedException>(() => candidate.State);
    }

    /// <summary>Requires exact images even when the next actual completed operation and new issuance independently prove Proven.</summary>
    [Fact]
    public async Task DifferentExactProvenIssuanceBetweenDecisionsRefusesOriginalCandidate()
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int budget = fixture.Checkpoint.Budget.LiveBytes;
        byte[] priorRoot = fixture.Checkpoint.Durable[fixture.Checkpoint.StageKeys[1]].ToArray();
        fixture.After = async (ordinal, _) =>
        {
            if (ordinal != 1)
            {
                return;
            }

            // A fresh current-generation participant set is independently admissible, not the old candidate image.
            DaprReplayOperationRecord record = fixture.Checkpoint.Replay.Store.Get<DaprReplayOperationRecord>("logical-replay:operation:v1");
            fixture.Checkpoint.Replay.Store.Put("logical-replay:operation:v1", record with { Generation = record.Generation + 1 });
            fixture.Checkpoint.Durable.Clear();
            (await fixture.Checkpoint.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Proven);
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.AcquireAsync());
        fixture.Decisions.ShouldBe(2);
        fixture.Checkpoint.Saves.ShouldBe(2);
        fixture.Checkpoint.Stages.ShouldBe(6);
        fixture.Checkpoint.Durable[fixture.Checkpoint.StageKeys[1]].ShouldNotBe(priorRoot);
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(budget);
    }

    /// <summary>Refuses every changed participant or actual origin before returning candidate bytes.</summary>
    [Theory]
    [InlineData("state")]
    [InlineData("root")]
    [InlineData("witness")]
    [InlineData("absent")]
    [InlineData("history")]
    [InlineData("source")]
    public async Task ChangedActualEvidenceRefusesWithoutStageOrSave(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        switch (point)
        {
            case "state":
                fixture.Checkpoint.Durable[fixture.Checkpoint.StageKeys[0]][0] ^= 1;
                break;
            case "root":
                fixture.Checkpoint.Durable[fixture.Checkpoint.StageKeys[1]][0] ^= 1;
                break;
            case "witness":
                fixture.Checkpoint.Durable[fixture.Checkpoint.StageKeys[2]][0] ^= 1;
                break;
            case "absent":
                fixture.Checkpoint.Durable.Clear();
                break;
            case "history":
                fixture.Checkpoint.Replay.Store.Put("logical-replay:state:0", new byte[] { 99 });
                break;
            case "source":
                fixture.Checkpoint.Replay.Source.Metadata = fixture.Checkpoint.Replay.Source.Metadata!with
                {
                    ETag = "changed"
                };
                break;
        }

        var durable = fixture.Checkpoint.Durable.ToDictionary(row => row.Key, row => row.Value.ToArray());
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.AcquireAsync());
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
        foreach ((string key, byte[] value)in durable)
        {
            fixture.Checkpoint.Durable[key].ShouldBe(value);
        }
    }

    /// <summary>Refuses private image substitution before another actual decision or after an actually yielding refresh.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task PrivateImageSubstitutionRefusesBeforeLaterWork(int index, bool duringAwait)
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        using var candidate = await fixture.AcquireAsync();
        byte[][] arrays = CaptureArrays(candidate);
        int reads = fixture.Checkpoint.Reads;
        if (duringAwait)
        {
            fixture.Before = async (_, _) =>
            {
                await Task.Yield();
                fixture.Checkpoint.Budget.LiveBytes.ShouldBeGreaterThan(before);
                arrays[index][0] ^= 1;
            };
        }
        else
        {
            arrays[index][0] ^= 1;
        }

        await Should.ThrowAsync<InvalidOperationException>(() => candidate.RequireCurrentAsync(CancellationToken.None));
        if (!duringAwait)
        {
            fixture.Checkpoint.Reads.ShouldBe(reads);
        }

        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
        candidate.Dispose();
        arrays.ShouldAllBe(image => image.All(value => value == 0));
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
    }

    /// <summary>Preserves original cancellation and private cleanup across completed decisions and throwing callbacks.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task OriginalCancellationAtDecisionReturnClearsProvisionalCopies(int ordinal, bool foreignException)
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        using var original = new CancellationTokenSource();
        fixture.After = async (current, _) =>
        {
            if (current != ordinal)
            {
                return;
            }

            await Task.Yield();
            fixture.Checkpoint.Budget.LiveBytes.ShouldBeGreaterThan(before);
            original.Cancel();
            if (foreignException)
            {
                throw new OperationCanceledException(new CancellationToken(true));
            }
        };
        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => fixture.AcquireAsync(original.Token));
        exception.CancellationToken.ShouldBe(original.Token);
        fixture.Decisions.ShouldBe(ordinal);
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Disposes provisional copies when either actor cleanup turns Proven into Indeterminate.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CleanupFailureNeverReturnsChargedCandidate(int ordinal)
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        int clear = 0;
        fixture.Before = (current, _) =>
        {
            clear = 0;
            fixture.Checkpoint.OnClear = _ =>
            {
                if (current == ordinal && ++clear == 5)
                {
                    throw new IOException("cleanup unavailable after private capture");
                }

                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.AcquireAsync());
        fixture.Decisions.ShouldBe(ordinal);
        object? pending = typeof(Hexalith.EventStore.Server.Actors.DaprLogicalCheckpointActor).GetField("_logicalCheckpointPending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Checkpoint.Actor);
        pending.ShouldNotBeNull();
        fixture.Checkpoint.Budget.LiveBytes.ShouldBeGreaterThan(before);
        fixture.Checkpoint.OnClear = null;
        (await fixture.Checkpoint.CallAsync(true)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Refuses missing/repeated/foreign decisions while preserving all durable checkpoint rows.</summary>
    [Theory]
    [InlineData("skip")]
    [InlineData("repeat")]
    [InlineData("foreign")]
    public async Task CandidateRequiresExactlyOneOriginalCompletedOwnerDecision(string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        fixture.Mode = mode;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.AcquireAsync());
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Refuses foreign tokens before reads, with originating cancellation winning when both cancel.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RetainedOriginalTokenPrecedesForeignTokenAndActualReads(bool originalCanceled, bool foreignCanceled)
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        using var original = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        using var candidate = await fixture.AcquireAsync(original.Token);
        if (originalCanceled)
            original.Cancel();
        if (foreignCanceled)
            foreign.Cancel();
        int reads = fixture.Checkpoint.Reads;
        if (originalCanceled)
        {
            OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => candidate.RequireCurrentAsync(foreign.Token));
            exception.CancellationToken.ShouldBe(original.Token);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => candidate.RequireCurrentAsync(foreign.Token));
        }

        fixture.Checkpoint.Reads.ShouldBe(reads);
        fixture.Decisions.ShouldBe(2);
    }

    /// <summary>Refuses unsupported model or conservative capacity without events/effects.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnsupportedModelOrCapacityRefusesWithoutEffects(bool wrongModel)
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        int reads = fixture.Checkpoint.Reads;
        using var reserved = wrongModel ? null : fixture.Checkpoint.Budget.Reserve(128 * 1024 * 1024 - before - 17000);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.AcquireAsync(model: wrongModel ? DaprLogicalCheckpointCodec.ModelId : DaprLogicalCheckpointCandidate.ModelId));
        fixture.Checkpoint.Reads.ShouldBe(reads);
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Retains no late candidate when an actual read yields beyond an early owner return.</summary>
    [Fact]
    public async Task EarlyYieldingOwnerRefusesLateCaptureAndReleasesAfterDecisionEnds()
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Checkpoint.OnRead = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
        };
        fixture.Mode = "early";
        Task<DaprLogicalCheckpointCandidate> acquisition = fixture.AcquireAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            acquisition.IsCompleted.ShouldBeTrue();
            await Should.ThrowAsync<InvalidOperationException>(() => acquisition);
        }
        finally
        {
            release.TrySetResult();
            if (fixture.LastDecision is not null)
            {
                await Should.ThrowAsync<InvalidOperationException>(async () => await fixture.LastDecision.WaitAsync(TimeSpan.FromSeconds(1)));
            }
        }

        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Owns detached copies after actual uncertain save reconciliation, and never transfers pending arrays.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSaveRecoveryCopiesPrivatelyAndNeverRepeatsSave(bool indeterminate)
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        fixture.Checkpoint.Durable.Clear();
        fixture.Checkpoint.SaveMode = "commit-throw";
        fixture.Checkpoint.OnSave = _ =>
        {
            fixture.Checkpoint.FailReads = true;
            return Task.CompletedTask;
        };
        (await fixture.Checkpoint.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Checkpoint.FailReads = false;
        fixture.Checkpoint.OnSave = null;
        byte[][] pendingArrays = fixture.Checkpoint.Aliases.Skip(3).ToArray();
        string rootKey = fixture.Checkpoint.StageKeys[1];
        byte[] root = fixture.Checkpoint.Durable[rootKey].ToArray();
        if (indeterminate)
        {
            fixture.Checkpoint.Durable[rootKey][0] ^= 1;
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.AcquireAsync());
            fixture.Checkpoint.Budget.LiveBytes.ShouldBeGreaterThan(before);
            fixture.Checkpoint.Durable[rootKey] = root;
        }

        using var candidate = await fixture.AcquireAsync();
        byte[][] privateArrays = CaptureArrays(candidate);
        pendingArrays.ShouldAllBe(image => image.All(value => value == 0));
        privateArrays.ShouldAllBe(image => !pendingArrays.Any(pending => ReferenceEquals(image, pending)));
        candidate.Root.ToArray().ShouldBe(root);
        fixture.Checkpoint.Stages.ShouldBe(6);
        fixture.Checkpoint.Saves.ShouldBe(2);
        candidate.Dispose();
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
    }

    /// <summary>Checks complete private source/fold pins before any subsequent owner callback.</summary>
    [Theory]
    [InlineData("_sourcePin")]
    [InlineData("_foldPin")]
    public async Task PrivateDeclarationPinSubstitutionRefusesBeforeActualDecision(string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        using var candidate = await fixture.AcquireAsync();
        int reads = fixture.Checkpoint.Reads;
        byte[] pin = (byte[])typeof(DaprLogicalCheckpointCandidate).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(candidate)!;
        pin[0] ^= 1;
        await Should.ThrowAsync<InvalidOperationException>(() => candidate.RequireCurrentAsync(CancellationToken.None));
        fixture.Decisions.ShouldBe(2);
        fixture.Checkpoint.Reads.ShouldBe(reads);
    }

    /// <summary>Refuses registry loss, actual advanced-head drift and canonical codec failures without effects.</summary>
    [Theory]
    [InlineData("registry")]
    [InlineData("head")]
    [InlineData("codec")]
    public async Task RetainedCandidateRefusesLostSourceOrCanonicalAuthority(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        using var candidate = await fixture.AcquireAsync();
        if (point == "registry")
        {
            fixture.Checkpoint.Replay.Source.Registry.Dispose();
        }
        else if (point == "head")
        {
            fixture.Checkpoint.Replay.Source.Metadata = fixture.Checkpoint.Replay.Source.Metadata!with
            {
                CurrentSequence = 3
            };
        }
        else
        {
            fixture.Checkpoint.Replay.AlwaysNoncanonical = true;
        }

        await Should.ThrowAsync<InvalidOperationException>(() => candidate.RequireCurrentAsync(CancellationToken.None));
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Refuses an equivalent declaration object before executing its callbacks or actual owner reads.</summary>
    [Fact]
    public async Task EquivalentDeclarationInstanceCannotReplaceCapturedFold()
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        using var candidate = await fixture.AcquireAsync();
        using var equivalent = new DaprLogicalProjectionFold(fixture.Checkpoint.Binding, "projection", "projection:tenant:id", "projection-store", new byte[32], fixture.Checkpoint.Replay.Binding, fixture.Checkpoint.Budget);
        equivalent.Fingerprint.ToArray().ShouldBe(fixture.Checkpoint.Fold.Fingerprint.ToArray());
        typeof(DaprLogicalCheckpointCandidate).GetField("_fold", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(candidate, equivalent);
        int reads = fixture.Checkpoint.Reads;
        await Should.ThrowAsync<InvalidOperationException>(() => candidate.RequireCurrentAsync(CancellationToken.None));
        fixture.Decisions.ShouldBe(2);
        fixture.Checkpoint.Reads.ShouldBe(reads);
    }

    /// <summary>Retains only the exact covered binding below head, without adopting a requested tail target or returning progress.</summary>
    [Fact]
    public async Task BelowHeadPrivateCandidateKeepsOriginalFixedTargetWithoutTailReads()
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        fixture.Checkpoint.Replay.Source.Metadata = fixture.Checkpoint.Replay.Source.Metadata!with
        {
            CurrentSequence = 3
        };
        await fixture.PrepareAsync();
        int events = fixture.Checkpoint.Replay.Source.EventReads;
        using var candidate = await fixture.AcquireAsync();
        candidate.CoveredSequence.ShouldBe(2L);
        fixture.Checkpoint.Binding.ActorHead.ShouldBe(3L);
        fixture.Checkpoint.Binding.TargetSequence.ShouldBe(2L);
        fixture.Checkpoint.Replay.Source.EventReads.ShouldBe(events);
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    /// <summary>Originating cancellation in canonical read prevents later callbacks and copy release.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelingCanonicalCallbackPreventsLaterWriteAndCandidateRelease(bool foreignException)
    {
        await using var fixture = new DaprLogicalCheckpointCandidateFixture();
        await fixture.PrepareAsync();
        using var original = new CancellationTokenSource();
        int before = fixture.Checkpoint.Budget.LiveBytes;
        int writes = fixture.Checkpoint.Replay.Writes;
        fixture.Checkpoint.Replay.Hook = (point, _) =>
        {
            if (point == "read")
            {
                original.Cancel();
                if (foreignException)
                {
                    throw new OperationCanceledException(new CancellationToken(true));
                }

                throw new IOException("canonical read callback failed");
            }
        };
        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => fixture.AcquireAsync(original.Token));
        exception.CancellationToken.ShouldBe(original.Token);
        fixture.Checkpoint.Replay.Writes.ShouldBe(writes);
        fixture.Checkpoint.Budget.LiveBytes.ShouldBe(before);
        fixture.Checkpoint.Stages.ShouldBe(3);
        fixture.Checkpoint.Saves.ShouldBe(1);
    }

    private static byte[][] CapturePins(DaprLogicalCheckpointCandidate candidate)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        byte[][] images = (byte[][])typeof(DaprLogicalCheckpointCandidate).GetField("_imagePins", flags)!.GetValue(candidate)!;
        return images.Concat(new[] { (byte[])typeof(DaprLogicalCheckpointCandidate).GetField("_sourcePin", flags)!.GetValue(candidate)!, (byte[])typeof(DaprLogicalCheckpointCandidate).GetField("_foldPin", flags)!.GetValue(candidate)! }).ToArray();
    }

    private static byte[][] CaptureArrays(DaprLogicalCheckpointCandidate candidate)
    {
        return new[]
        {
            candidate.State,
            candidate.Root,
            candidate.Witness
        }.Select(memory =>
        {
            MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> array).ShouldBeTrue();
            return array.Array!;
        }).ToArray();
    }
}
