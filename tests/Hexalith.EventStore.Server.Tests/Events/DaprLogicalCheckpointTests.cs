using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Proves actual first checkpoint issuance/readback, private ownership and durable recovery without serving activation.</summary>
public sealed class DaprLogicalCheckpointTests
{
    /// <summary>Exercises actual pure fold issues three rows and readback retry does not save.</summary>
    [Fact]
    public async Task ActualPureFoldIssuesThreeRowsAndReadbackRetryDoesNotSave()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        fixture.Durable[EventReplayProjectionActor.ProjectionStateKey] = [11, 12];
        fixture.Durable[EventReplayProjectionActor.ProjectionRebuildCandidateKey] = [21, 22];
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Stages.ShouldBe(3);
        fixture.Saves.ShouldBe(1);
        fixture.Durable.Count.ShouldBe(5);
        int applies = fixture.Replay.Applies;
        int sourceReads = fixture.Replay.Source.EventReads;
        int participantReads = 0;
        fixture.Replay.Store.OnRead = _ => participantReads++;
        string versionKey = fixture.StageKeys[0];
        string rootKey = fixture.StageKeys[1];
        string witnessKey = fixture.StageKeys[2];
        fixture.Durable[versionKey].ShouldBe("{\"value\":2}"u8.ToArray());
        DaprLogicalCheckpointWitness witness = DaprLogicalSnapshotCodec.DecodeCheckpoint(fixture.Durable[witnessKey]);
        witness.CoveredSequence.ShouldBe(2L);
        witness.Projection.ShouldBe("projection");
        witness.RootHash.ToArray().ShouldBe(System.Security.Cryptography.SHA256.HashData(fixture.Durable[rootKey]));
        var expected = fixture.Durable.ToDictionary(row => row.Key, row => row.Value.ToArray());
        (await fixture.CallAsync(true)).ShouldBe(DaprReplayCommitOutcome.Proven);
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(3);
        fixture.Replay.Applies.ShouldBe(applies);
        // Exact current-head readback visits actual ledger/response/state participants without rereading event keys.
        fixture.Replay.Source.EventReads.ShouldBe(sourceReads);
        participantReads.ShouldBeGreaterThan(0);
        foreach ((string key, byte[] value)in expected)
        {
            fixture.Durable[key].ShouldBe(value);
        }

        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Aliases.ShouldAllBe(image => image.All(value => value == 0));
    }

    /// <summary>Exercises absent readback stages nothing.</summary>
    [Fact]
    public async Task AbsentReadbackStagesNothing()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        (await fixture.CallAsync(true)).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        fixture.Durable.ShouldBeEmpty();
        fixture.Budget.LiveBytes.ShouldBe(before);
    }

    /// <summary>Exercises independent actual readback classifies save and never repeats pending save.</summary>
    [Theory]
    [InlineData("normal", 0)]
    [InlineData("commit-throw", 0)]
    [InlineData("no-commit", 1)]
    [InlineData("mixed", 2)]
    public async Task IndependentActualReadbackClassifiesSaveAndNeverRepeatsPendingSave(string mode, int result)
    {
        ArgumentNullException.ThrowIfNull(mode);
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        fixture.SaveMode = mode;
        DaprReplayCommitOutcome expected = result == 0 ? DaprReplayCommitOutcome.Proven : result == 1 ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Indeterminate;
        (await fixture.CallAsync()).ShouldBe(expected);
        fixture.Saves.ShouldBe(1);
        if (result == 2)
        {
            fixture.Budget.LiveBytes.ShouldBeGreaterThan(before);
            (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
            fixture.Saves.ShouldBe(1);
            foreach (string key in fixture.StageKeys)
            {
                fixture.Durable.Remove(key);
            }

            (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        }

        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Aliases.ShouldAllBe(image => image.All(value => value == 0));
    }

    /// <summary>Exercises nonidentical existing participant holds without overwrite.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NonidenticalExistingParticipantHoldsWithoutOverwrite(int index)
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Durable[fixture.StageKeys[index]][0] ^= 1;
        var prior = fixture.Durable.ToDictionary(row => row.Key, row => row.Value.ToArray());
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync());
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(3);
        foreach ((string key, byte[] value)in prior)
        {
            fixture.Durable[key].ShouldBe(value);
        }
    }

    /// <summary>Exercises callback substitution stops later stages and restores budget.</summary>
    [Theory]
    [InlineData("source")]
    [InlineData("history")]
    [InlineData("binding")]
    [InlineData("backend")]
    [InlineData("private")]
    [InlineData("keys")]
    public async Task CallbackSubstitutionStopsLaterStagesAndRestoresBudget(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        fixture.OnStage = async (_, bytes, _) =>
        {
            await Task.Yield();
            if (point == "source")
            {
                fixture.Replay.Source.Metadata = fixture.Replay.Source.Metadata with
                {
                    ETag = "changed"
                };
            }
            else if (point == "history")
            {
                fixture.Replay.Store.Put("logical-replay:state:0", new byte[] { 91 });
            }
            else if (point == "binding")
            {
                fixture.Replay.Source.Registry.Dispose();
            }
            else if (point == "backend")
            {
                MemoryMarshal.TryGetArray(fixture.Fold.Backend, out ArraySegment<byte> backend).ShouldBeTrue();
                backend.Array![0] ^= 1;
            }
            else if (point == "keys")
            {
                object pending = typeof(DaprLogicalCheckpointActor).GetField("_logicalCheckpointPending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Actor)!;
                string[] keys = (string[])pending.GetType().GetField("_keys", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pending)!;
                keys[1] = keys[1] + "-substituted";
            }
            else
            {
                bytes[0] ^= 1;
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync());
        fixture.Stages.ShouldBe(1);
        fixture.Saves.ShouldBe(0);
        fixture.Durable.ShouldBeEmpty();
        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Aliases.ShouldAllBe(image => image.All(value => value == 0));
    }

    /// <summary>Exercises original cancellation at each stage wins foreign callback exception.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task OriginalCancellationAtEachStageWinsForeignCallbackException(int stage, bool foreign)
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        using var cancellation = new CancellationTokenSource();
        using var other = new CancellationTokenSource();
        fixture.OnStage = async (_, _, _) =>
        {
            await Task.Yield();
            if (fixture.Stages == stage)
            {
                cancellation.Cancel();
                if (foreign)
                {
                    throw new OperationCanceledException(other.Token);
                }

                throw new IOException("cancel plus throw");
            }
        };
        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => fixture.CallAsync(token: cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        fixture.Stages.ShouldBe(stage);
        fixture.Saves.ShouldBe(0);
        fixture.Durable.ShouldBeEmpty();
        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Aliases.ShouldAllBe(image => image.All(value => value == 0));
    }

    /// <summary>Exercises serialized owner requires exactly one original decision.</summary>
    [Theory]
    [InlineData("skip")]
    [InlineData("foreign")]
    [InlineData("repeat")]
    public async Task SerializedOwnerRequiresExactlyOneOriginalDecision(string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        fixture.FenceMode = mode;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync());
        if (mode == "repeat")
        {
            fixture.Reads.ShouldBe(fixture.BeforeRepeatReads);
        }

        if (mode != "repeat")
        {
            fixture.Stages.ShouldBe(0);
            fixture.Saves.ShouldBe(0);
            fixture.Reads.ShouldBe(0);
        }
    }

    /// <summary>Exercises fresh readback requires actual origin even when root bytes are exact.</summary>
    [Fact]
    public async Task FreshReadbackRequiresActualOriginEvenWhenRootBytesAreExact()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Proven);
        var prior = fixture.Durable.ToDictionary(row => row.Key, row => row.Value.ToArray());
        fixture.Replay.Store.Put("logical-replay:state:0", new byte[] { 88 });
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync(true));
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(3);
        foreach ((string key, byte[] bytes)in prior)
        {
            fixture.Durable[key].ShouldBe(bytes);
        }
    }

    /// <summary>Exercises final readback callback history loss withholds proven.</summary>
    [Fact]
    public async Task FinalReadbackCallbackHistoryLossWithholdsProven()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        fixture.OnRead = async (_, _) =>
        {
            await Task.Yield();
            if (fixture.Saves == 1)
            {
                fixture.Replay.Store.Put("logical-replay:state:0", new byte[] { 88 });
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync());
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(3);
        fixture.Durable.Count.ShouldBe(3);
        fixture.Budget.LiveBytes.ShouldBe(8192);
    }

    /// <summary>Exercises conservative capacity refuses before origin or projection effects.</summary>
    [Fact]
    public async Task ConservativeCapacityRefusesBeforeOriginOrProjectionEffects()
    {
        await using var fixture = new DaprLogicalCheckpointFixture(16384);
        await fixture.PrepareAsync();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync());
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        fixture.Reads.ShouldBe(0);
        fixture.Budget.LiveBytes.ShouldBe(8192);
    }

    /// <summary>Exercises pending proven requires fresh current canonical codec and does not repeat save.</summary>
    [Fact]
    public async Task PendingProvenRequiresFreshCurrentCanonicalCodecAndDoesNotRepeatSave()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        fixture.OnSave = _ =>
        {
            fixture.FailReads = true;
            return Task.CompletedTask;
        };
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Budget.LiveBytes.ShouldBeGreaterThan(before);
        fixture.FailReads = false;
        fixture.Replay.AlwaysNoncanonical = true;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync());
        fixture.Stages.ShouldBe(3);
        fixture.Saves.ShouldBe(1);
        fixture.Durable.Count.ShouldBe(3);
        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Replay.AlwaysNoncanonical = false;
        (await fixture.CallAsync(true)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Saves.ShouldBe(1);
    }

    /// <summary>Exercises pending admission refuses different declaration before actual readback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingAdmissionRefusesDifferentDeclarationBeforeActualReadback(bool proven)
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        fixture.SaveMode = proven ? "commit-throw" : "mixed";
        fixture.OnSave = _ =>
        {
            fixture.FailReads = true;
            return Task.CompletedTask;
        };
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.FailReads = false;
        int reads = fixture.Reads;
        int before = fixture.Budget.LiveBytes;
        using var other = new DaprLogicalProjectionFold(fixture.Binding, "projection", "projection:tenant:id", "projection-store", new byte[32], fixture.Replay.Binding, fixture.Budget);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync(fold: other));
        fixture.Reads.ShouldBe(reads);
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(3);
        foreach (string key in fixture.StageKeys)
        {
            fixture.Durable.Remove(key);
        }

        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        fixture.Budget.LiveBytes.ShouldBe(16384);
        before.ShouldBeGreaterThan(8192);
    }

    /// <summary>Exercises actually yielding early owner refuses save and eventually clears staging.</summary>
    [Fact]
    public async Task ActuallyYieldingEarlyOwnerRefusesSaveAndEventuallyClearsStaging()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.OnStage = async (_, _, _) => await release.Task;
        fixture.FenceMode = "early";
        Task<DaprReplayCommitOutcome> call = fixture.CallAsync();
        try
        {
            await Task.WhenAny(call, Task.Delay(500));
            call.IsCompleted.ShouldBeTrue();
            await Should.ThrowAsync<InvalidOperationException>(() => call);
            fixture.Saves.ShouldBe(0);
            fixture.Stages.ShouldBe(1);
            fixture.Budget.LiveBytes.ShouldBeGreaterThan(before);
        }
        finally
        {
            release.SetResult();
        }

        await Should.ThrowAsync<InvalidOperationException>(() => fixture.LastDecision!.WaitAsync(TimeSpan.FromSeconds(10)));
        fixture.Stages.ShouldBe(1);
        fixture.Saves.ShouldBe(0);
        fixture.Durable.ShouldBeEmpty();
        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Aliases.ShouldAllBe(image => image.All(value => value == 0));
    }

    /// <summary>Exercises save cancellation preserves actual durable rows and clears released private images.</summary>
    [Fact]
    public async Task SaveCancellationPreservesActualDurableRowsAndClearsReleasedPrivateImages()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        using var cancellation = new CancellationTokenSource();
        fixture.OnSave = _ =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        };
        OperationCanceledException exception = await Should.ThrowAsync<OperationCanceledException>(() => fixture.CallAsync(token: cancellation.Token));
        exception.CancellationToken.ShouldBe(cancellation.Token);
        fixture.Saves.ShouldBe(1);
        fixture.Durable.Count.ShouldBe(3);
        fixture.Budget.LiveBytes.ShouldBe(before);
        fixture.Aliases.ShouldAllBe(image => image.All(value => value == 0));
        (await fixture.CallAsync(true)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Saves.ShouldBe(1);
    }

    /// <summary>Exercises retained private keys and exact source hash are bounded before allocation.</summary>
    [Fact]
    public async Task RetainedPrivateKeysAndExactSourceHashAreBoundedBeforeAllocation()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int before = fixture.Budget.LiveBytes;
        Should.Throw<ArgumentException>(() => new DaprLogicalCheckpointWrite(fixture.Fold, new byte[33], 32));
        Should.Throw<ArgumentException>(() => DaprLogicalCheckpointCodec.TextSize(new string ('é', 257)));
        DaprLogicalCheckpointCodec.TextSize(new string ('é', 256)).ShouldBe(516);
        using (var write = new DaprLogicalCheckpointWrite(fixture.Fold, new byte[32], 32))
        {
            Should.Throw<InvalidOperationException>(() => write.SetDesired(["logical-checkpoint-v1:a", "logical-checkpoint-v1:a", "logical-checkpoint-v1:b"], [], [], []));
        }

        fixture.Budget.LiveBytes.ShouldBe(before);
    }

    /// <summary>Exercises source or completed manager cannot own checkpoint staging.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOrCompletedManagerCannotOwnCheckpointStaging(bool source)
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        Hexalith.EventStore.Server.Tests.TestUtilities.ActorStateManagerTestHelper.SetStateManager(fixture.Actor, source ? fixture.Replay.Source.SourceState : fixture.Replay.Store.Manager);
        int calls = fixture.Replay.Source.EventReads;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync());
        fixture.Replay.Source.EventReads.ShouldBe(calls);
        fixture.Saves.ShouldBe(0);
        fixture.Stages.ShouldBe(0);
    }

    /// <summary>Exercises declared projection fold must be the actual completed binding object.</summary>
    [Fact]
    public async Task DeclaredProjectionFoldMustBeTheActualCompletedBindingObject()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await using var another = new DaprLogicalReconstructionFixture(2);
        await fixture.PrepareAsync();
        using var other = new DaprLogicalProjectionFold(fixture.Binding, "projection", "projection:tenant:id", "projection-store", new byte[32], another.Binding, fixture.Budget);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync(fold: other));
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        fixture.Reads.ShouldBe(0);
    }

    /// <summary>Exercises workspace reservation survives actually yielding stage and clears on refusal.</summary>
    [Fact]
    public async Task WorkspaceReservationSurvivesActuallyYieldingStageAndClearsOnRefusal()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        int held = 0;
        fixture.OnStage = async (_, _, _) =>
        {
            await Task.Yield();
            held = fixture.Budget.LiveBytes;
            throw new IOException("stage refuses");
        };
        await Should.ThrowAsync<IOException>(() => fixture.CallAsync());
        held.ShouldBeGreaterThan(8 * 64 * 1024 + 8192);
        fixture.Stages.ShouldBe(1);
        fixture.Saves.ShouldBe(0);
        fixture.Durable.ShouldBeEmpty();
        fixture.Budget.LiveBytes.ShouldBe(8192);
        fixture.Aliases.ShouldAllBe(image => image.All(value => value == 0));
    }

    /// <summary>Exercises uncertain pending attempt cannot enter another save boundary.</summary>
    [Fact]
    public async Task UncertainPendingAttemptCannotEnterAnotherSaveBoundary()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        fixture.OnSave = _ =>
        {
            fixture.FailReads = true;
            return Task.CompletedTask;
        };
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.FailReads = false;
        fixture.OnSave = null;
        fixture.Durable[fixture.StageKeys[2]][0] ^= 1;
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(3);
        foreach (string key in fixture.StageKeys)
        {
            fixture.Durable.Remove(key);
        }

        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        fixture.Saves.ShouldBe(1);
        fixture.Budget.LiveBytes.ShouldBe(8192);
    }

    /// <summary>Exercises pending fixed source refuses before readback.</summary>
    [Fact]
    public async Task PendingFixedSourceRefusesBeforeReadback()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        fixture.SaveMode = "mixed";
        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        int reads = fixture.Reads;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.CallAsync(binding: fixture.Binding with { MetadataETag = "changed" }));
        fixture.Reads.ShouldBe(reads);
        fixture.Stages.ShouldBe(3);
        fixture.Saves.ShouldBe(1);
        foreach (string key in fixture.StageKeys)
        {
            fixture.Durable.Remove(key);
        }

        (await fixture.CallAsync()).ShouldBe(DaprReplayCommitOutcome.NoCommit);
    }

    /// <summary>Exercises independent checkpoint owner vectors match runtime framing and measured bytes.</summary>
    [Fact]
    public async Task IndependentCheckpointOwnerVectorsMatchRuntimeFramingAndMeasuredBytes()
    {
        await using var fixture = new DaprLogicalCheckpointFixture();
        await fixture.PrepareAsync();
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }

        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory!.FullName, "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-checkpoint-owner-2026-10-09/vectors.json")));
        byte[] hash = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        byte[] ns = DaprLogicalCheckpointCodec.Namespace(fixture.Binding, "projection", "projection:tenant:id", "projection-store", hash);
        byte[] fold = DaprLogicalCheckpointCodec.Fold(ns, hash);
        byte[] scope = DaprLogicalCheckpointCodec.Scope(ns, hash, fold);
        byte[] version = DaprLogicalCheckpointCodec.Version("operation", 1);
        Check("namespace", ns);
        Check("fold", fold);
        Check("scope", scope);
        Check("version", version);
        string prefix = "logical-checkpoint-v1:" + Convert.ToHexStringLower(ns);
        string key = prefix + ":state:" + Convert.ToHexStringLower(version);
        foreach (long sequence in new[]
        {
            2L,
            long.MaxValue
        }

        )
        {
            var fields = new DaprLogicalSnapshotWitness("tenant", "d", "aggregate", "r", hash, sequence, key, prefix + ":witness", hash, hash, hash, hash, hash, "operation", 1, hash, hash, "plaintext-canonical-v1", DaprLogicalSnapshotCodec.SnapshotModel, "test-state-json");
            byte[] origin = DaprLogicalSnapshotCodec.EncodeSnapshot(fields);
            byte[] root = DaprLogicalCheckpointCodec.Root(fixture.Binding, "projection", "projection:tenant:id", "projection-store", hash, scope, fold, key, origin);
            Check(sequence == 2 ? "root" : "root-max-encoding-only", root);
            root.Length.ShouldBe("HX-EV-DAPR-CHECKPOINT-ROOT-1\0"u8.Length + 3 + 10 + 96 + 4 + origin.Length + new[] { "tenant", "d", "projection", "projection:tenant:id", "projection-store", key }.Sum(DaprLogicalCheckpointCodec.TextSize));
        }

        void Check(string name, byte[] actual)
        {
            JsonElement expected = vectors.RootElement.GetProperty(name);
            actual.ShouldBe(Convert.FromHexString(expected.GetProperty("hex").GetString()!));
            actual.Length.ShouldBe(expected.GetProperty("bytes").GetInt32());
            Convert.ToHexStringLower(SHA256.HashData(actual)).ShouldBe(expected.GetProperty("sha256").GetString());
        }
    }
}
