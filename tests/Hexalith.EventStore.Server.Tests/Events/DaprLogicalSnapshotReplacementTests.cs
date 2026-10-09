using System.Reflection;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Exercises explicit older-pair admission and same-save replacement through actual owners without production registration.</summary>
public sealed class DaprLogicalSnapshotReplacementTests
{
    /// <summary>Replaces one exact older pair, reads both images back and supplies a proof-qualified current candidate without a nested fence.</summary>
    [Fact]
    public async Task ExactOlderPairReplacesAndAdmitsCurrentCandidate()
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        var snapshot = fixture.Actor.Snapshot;
        int reads = snapshot.Replay.Reads;
        int applies = snapshot.Replay.Applies;
        using var budget = new EventBufferBudget();
        byte[] legacy = "legacy-owned"u8.ToArray();
        snapshot.Durable[DaprLogicalReplayFixture.Identity.SnapshotKey] = legacy;
        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.OwnerFences.ShouldBe(1);
        fixture.OriginTargets.ShouldBe(new long[] { 3, 1 });
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        snapshot.Replay.Reads.ShouldBeGreaterThan(reads);
        snapshot.Replay.Applies.ShouldBe(applies);
        snapshot.Durable[snapshot.Owner.StorageKey].ShouldBe("{\"value\":3}"u8.ToArray());
        DaprLogicalSnapshotCodec.DecodeSnapshot(snapshot.Durable[snapshot.Owner.WitnessKey]).CoveredSequence.ShouldBe(3);
        snapshot.Durable[DaprLogicalReplayFixture.Identity.SnapshotKey].ShouldBeSameAs(legacy);
        legacy.ShouldBe("legacy-owned"u8.ToArray());
        AssertCleared(fixture, budget);
        using DaprLogicalSnapshotCandidate candidate = (await snapshot.Owner.AcquireAsync(fixture.Binding, false, fixture.AcquireOriginAsync, budget, CancellationToken.None))!;
        candidate.ShouldNotBeNull();
        candidate.CoveredSequence.ShouldBe(3);
        candidate.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses torn, absent, malformed, mixed and nonmonotonic pairs before staging and preserves their actual bytes.</summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("state-only")]
    [InlineData("witness-only")]
    [InlineData("malformed")]
    [InlineData("state-mismatch")]
    [InlineData("equal")]
    [InlineData("newer")]
    [InlineData("scope")]
    [InlineData("source")]
    [InlineData("registry")]
    [InlineData("reconstruction")]
    [InlineData("storage-key")]
    [InlineData("serializer")]
    public async Task IneligiblePriorPairNeverStagesOrSaves(string shape)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync(shape == "equal" ? 2 : shape == "newer" ? 3 : 1, shape is "equal" or "newer" ? 2 : 3);
        fixture.PriorOwnerAfterDesired = true;
        var snapshot = fixture.Actor.Snapshot;
        if (shape == "absent")
        {
            snapshot.Durable.Clear();
        }
        else if (shape == "state-only")
        {
            snapshot.Durable.Remove(snapshot.Owner.WitnessKey);
        }
        else if (shape == "witness-only")
        {
            snapshot.Durable.Remove(snapshot.Owner.StorageKey);
        }
        else if (shape == "malformed")
        {
            snapshot.Durable[snapshot.Owner.WitnessKey] = "malformed"u8.ToArray();
        }
        else if (shape == "state-mismatch")
        {
            snapshot.Durable[snapshot.Owner.StorageKey][0] ^= 1;
        }
        else if (shape is not "equal" and not "newer")
        {
            DaprLogicalSnapshotWitness witness = DaprLogicalSnapshotCodec.DecodeSnapshot(snapshot.Durable[snapshot.Owner.WitnessKey]);
            witness = shape switch
            {
                "scope" => witness with
                {
                    AggregateId = "other"
                },
                "source" => witness with
                {
                    SourceBindingHash = new byte[32]
                },
                "registry" => witness with
                {
                    RegistryFingerprint = new byte[32]
                },
                "reconstruction" => witness with
                {
                    ReconstructionBindingHash = new byte[32]
                },
                "storage-key" => witness with
                {
                    StorageKey = "other-key"
                },
                _ => witness with
                {
                    SerializerId = "other-codec"
                },
            };
            snapshot.Durable[snapshot.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(witness);
        }

        Dictionary<string, byte[]> durable = Capture(fixture);
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ReplaceAsync(budget));
        AssertNoWrite(fixture, durable, budget);
    }

    /// <summary>Requires the explicit replacement policy before the supplied serialized decision or origin acquisition.</summary>
    [Fact]
    public async Task ReplacementRequiresDistinctExplicitPolicy()
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ReplaceAsync(budget, model: DaprLogicalSnapshotCodec.SnapshotModel));
        fixture.OwnerFences.ShouldBe(0);
        fixture.OriginTargets.ShouldBeEmpty();
        fixture.Actor.Stages.ShouldBe(0);
        fixture.Actor.Saves.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Requires the actual prior completed history and canonical codec rather than accepting a locally valid witness DTO.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("history")]
    [InlineData("noncanonical")]
    public async Task PriorActualHistoryAndCanonicalRoundtripAreRequired(string changed)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        if (changed == "missing")
        {
            fixture.MissingPrior = true;
        }
        else if (changed == "history")
        {
            string key = fixture.Actor.Snapshot.Replay.Store.Durable.Keys.Single(x => x.Contains(":ledger:1", StringComparison.Ordinal));
            fixture.Actor.Snapshot.Replay.Store.Durable[key][0] ^= 1;
        }
        else
        {
            fixture.Actor.Snapshot.Replay.AlwaysNoncanonical = true;
        }

        Dictionary<string, byte[]> durable = Capture(fixture);
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<Exception>(() => fixture.ReplaceAsync(budget));
        fixture.OriginTargets.ShouldContain(1);
        AssertNoWrite(fixture, durable, budget);
    }

    /// <summary>Freezes private and actual prior bytes before the codec callback and refuses callback substitution before any stage.</summary>
    [Theory]
    [InlineData("actual-pair")]
    [InlineData("private-pair")]
    [InlineData("private-witness")]
    [InlineData("prior-history")]
    [InlineData("new-history")]
    public async Task CodecCallbackCannotSubstitutePriorOrEitherHistory(string changed)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        int stopped = 0;
        int later = 0;
        var snapshot = fixture.Actor.Snapshot;
        snapshot.Replay.Hook = (point, token) =>
        {
            if (stopped > 0)
            {
                later++;
            }

            if (point == "read" && stopped++ == 0)
            {
                if (changed == "actual-pair")
                {
                    snapshot.Durable[snapshot.Owner.StorageKey][0] ^= 1;
                }
                else if (changed is "private-pair" or "private-witness")
                {
                    DaprLogicalSnapshotWrite write = (DaprLogicalSnapshotWrite)Field(fixture.Actor.Actor, "_logicalSnapshotPending")!;
                    DaprLogicalResponseOwner state = (DaprLogicalResponseOwner)Field(write, changed == "private-pair" ? "_priorState" : "_priorWitness")!;
                    System.Runtime.InteropServices.MemoryMarshal.TryGetArray(state.Bytes, out ArraySegment<byte> bytes).ShouldBeTrue();
                    bytes.Array![0] ^= 1;
                }
                else
                {
                    DaprReplayTestStore store = changed == "prior-history" ? snapshot.Replay.Store : fixture.NewStore;
                    string key = store.Durable.Keys.Single(x => x.Contains(":ledger:1", StringComparison.Ordinal));
                    store.Durable[key][0] ^= 1;
                }
            }
        };
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<Exception>(() => fixture.ReplaceAsync(budget));
        stopped.ShouldBeGreaterThan(0);
        if (changed != "actual-pair")
        {
            later.ShouldBe(0);
        }

        fixture.Actor.Stages.ShouldBe(0);
        fixture.Actor.Saves.ShouldBe(0);
        AssertCleared(fixture, budget);
    }

    /// <summary>Preserves the originating token and stops later codec/actor callbacks when canonical read or write cancels and throws.</summary>
    [Theory]
    [InlineData("read", false)]
    [InlineData("read", true)]
    [InlineData("write", false)]
    [InlineData("write", true)]
    public async Task PriorCodecCancellationStopsLaterCallbacksAndClears(string point, bool foreign)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        using var cancellation = new CancellationTokenSource();
        int stopped = 0;
        int later = 0;
        fixture.Actor.Snapshot.Replay.Hook = (callback, token) =>
        {
            if (stopped > 0)
            {
                later++;
            }

            if (callback == point)
            {
                stopped++;
                token.ShouldBe(cancellation.Token);
                cancellation.Cancel();
                if (foreign)
                {
                    throw new OperationCanceledException(new CancellationToken(true));
                }

                throw new IOException("cancelled canonical callback");
            }
        };
        using var budget = new EventBufferBudget();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.ReplaceAsync(budget, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        stopped.ShouldBe(1);
        later.ShouldBe(0);
        AssertNoWrite(fixture, durable, budget);
    }

    /// <summary>Uses exact paired independent readback for every acknowledgement mode and never repeats an uncertain save.</summary>
    [Theory]
    [InlineData("normal", 0)]
    [InlineData("commit-throw", 0)]
    [InlineData("no-commit", 1)]
    [InlineData("mixed", 2)]
    public async Task ReplacementReadbackClassifiesExactDesiredPriorOrHold(string mode, int expected)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        fixture.Actor.SaveMode = mode;
        using var budget = new EventBufferBudget();
        DaprReplayCommitOutcome outcome = await fixture.ReplaceAsync(budget);
        outcome.ShouldBe((DaprReplayCommitOutcome)expected);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        if (mode == "no-commit")
        {
            AssertDurable(fixture, durable);
        }

        if (mode == "mixed")
        {
            budget.LiveBytes.ShouldBeGreaterThan(0);
            fixture.Actor.SdkAliases.ShouldAllBe(x => x.Any(b => b != 0));
            (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
            fixture.Actor.Saves.ShouldBe(1);
            foreach ((string key, byte[] value)in durable)
            {
                fixture.Actor.Snapshot.Durable[key] = value;
            }

            (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.NoCommit);
            fixture.Actor.Saves.ShouldBe(1);
        }

        AssertCleared(fixture, budget);
    }

    /// <summary>Refuses composed capacity before any origin, read, stage or save.</summary>
    [Fact]
    public async Task ReplacementCapacityRefusesBeforeOriginAndStage()
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        using var budget = new EventBufferBudget();
        int reads = fixture.Actor.Snapshot.SnapshotReads;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ReplaceAsync(budget, maximumStateBytes: 64 * 1024 * 1024));
        fixture.OriginTargets.ShouldBeEmpty();
        fixture.Actor.Snapshot.SnapshotReads.ShouldBe(reads);
        AssertNoWrite(fixture, durable, budget);
    }

    /// <summary>Rechecks the desired actual completed origin on explicit retry without additional stages, saves or prior acquisition.</summary>
    [Fact]
    public async Task ProvenReplacementRetryIsFreshAndIdempotent()
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        Dictionary<string, byte[]> durable = Capture(fixture);
        fixture.OriginTargets.Clear();
        int reads = fixture.Actor.Snapshot.SnapshotReads;
        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.OriginTargets.ShouldBe(new long[] { 3 });
        fixture.Actor.Snapshot.SnapshotReads.ShouldBeGreaterThan(reads);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        fixture.OwnerFences.ShouldBe(2);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    /// <summary>Retains the full decoding reservation and expires codec facades before a yielding later actor callback.</summary>
    [Fact]
    public async Task PriorDecodingAndOriginOwnershipRemainChargedThroughStage()
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        int held = 0;
        fixture.OnOrigin = async (covered, origin, token) =>
        {
            if (covered == 1)
            {
                await Task.Yield();
                int desiredLength = fixture.OriginArrays[0].Length;
                DaprLogicalSnapshotWrite write = (DaprLogicalSnapshotWrite)Field(fixture.Actor.Actor, "_logicalSnapshotPending")!;
                int priorWitnessLength = fixture.Actor.Snapshot.Durable[fixture.Actor.Snapshot.Owner.WitnessKey].Length;
                int expected = 512 + 4 * 32 + 6 * 64 * 1024 + 4096 + 2 * desiredLength + 256 + 2 * write.WitnessArray.Length + 256 + 2 * origin.State.Length + 256 + 2 * priorWitnessLength + 256 + 4096 + 2 * desiredLength + 256 + 4096 + 2 * origin.State.Length + 256 + 4 * priorWitnessLength + 4096;
                budget.LiveBytes.ShouldBe(expected);
                held++;
            }
        };
        fixture.Actor.OnStage = async (key, image, token) =>
        {
            await Task.Yield();
            foreach (var payload in fixture.Actor.Snapshot.Replay.Borrowed)
            {
                Should.Throw<ObjectDisposedException>(() => _ = payload.Length);
            }

            foreach (var writer in fixture.Actor.Snapshot.Replay.BorrowedWriters)
            {
                Should.Throw<ObjectDisposedException>(() => writer.Write([]));
            }

            fixture.OriginArrays.ShouldAllBe(x => x.Any(b => b != 0));
            budget.LiveBytes.ShouldBeGreaterThan(400000);
        };
        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        held.ShouldBe(1);
        AssertCleared(fixture, budget);
    }

    /// <summary>Checks both actual histories after each stage so substitution prevents the next stage or save.</summary>
    [Theory]
    [InlineData(1, "prior")]
    [InlineData(1, "desired")]
    [InlineData(2, "prior")]
    [InlineData(2, "desired")]
    public async Task StageCallbackHistorySubstitutionStopsLaterStageAndSave(int stage, string history)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        fixture.Actor.OnStage = async (key, image, token) =>
        {
            if (fixture.Actor.Stages == stage)
            {
                await Task.Yield();
                DaprReplayTestStore store = history == "prior" ? fixture.Actor.Snapshot.Replay.Store : fixture.NewStore;
                string ledger = store.Durable.Keys.Single(x => x.Contains(":ledger:1", StringComparison.Ordinal));
                store.Durable[ledger][0] ^= 1;
            }
        };
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<Exception>(() => fixture.ReplaceAsync(budget));
        fixture.Actor.Stages.ShouldBe(stage);
        fixture.Actor.Saves.ShouldBe(0);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    /// <summary>Re-admits retained prior canonical/history evidence before releasing Proven after an uncertain replacement save.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingReplacementRevalidatesPriorBeforeProven(bool changed)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        fixture.Actor.Snapshot.OnRead = (key, token) => fixture.Actor.Saves > 0 ? Task.FromException(new IOException("independent readback unavailable")) : Task.CompletedTask;
        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        budget.LiveBytes.ShouldBeGreaterThan(0);
        fixture.Actor.Snapshot.OnRead = null;
        Dictionary<string, byte[]> durable = Capture(fixture);
        fixture.OriginTargets.Clear();
        int reads = fixture.Actor.Snapshot.Replay.Reads;
        if (changed)
        {
            string ledger = fixture.Actor.Snapshot.Replay.Store.Durable.Keys.Single(x => x.Contains(":ledger:1", StringComparison.Ordinal));
            fixture.Actor.Snapshot.Replay.Store.Durable[ledger][0] ^= 1;
            await Should.ThrowAsync<Exception>(() => fixture.ReplaceAsync(budget));
        }
        else
        {
            (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
            fixture.Actor.Snapshot.Replay.Reads.ShouldBeGreaterThan(reads);
        }

        fixture.OriginTargets.ShouldBe(new long[] { 3, 1 });
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    /// <summary>Rejects foreign boundary tokens and original cancellation before any actual source callback or owner readback.</summary>
    [Theory]
    [InlineData("foreign-live")]
    [InlineData("foreign-canceled")]
    [InlineData("both-canceled")]
    [InlineData("same-canceled")]
    public async Task PriorBoundaryTokenRefusesBeforeAddressedCallback(string mode)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        using var cancellation = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        var snapshot = fixture.Actor.Snapshot;
        using var write = new DaprLogicalSnapshotWrite(budget, 32, snapshot.Owner.StorageKey, snapshot.Owner.WitnessKey);
        DaprLogicalResponseOwner state = write.CaptureRead(snapshot.Durable[snapshot.Owner.StorageKey], false);
        DaprLogicalResponseOwner witness = write.CaptureRead(snapshot.Durable[snapshot.Owner.WitnessKey], true);
        write.SetPrior(state, witness);
        int callbacks = 0;
        async Task SourceFence(CancellationToken token)
        {
            callbacks++;
            await snapshot.Replay.Source.Source.RequireCurrentAsync(fixture.Binding, snapshot.Replay.Source.Trust, token);
        }

        using DaprLogicalSnapshotPrior prior = await DaprLogicalSnapshotPrior.AcquireAsync(state, witness, write, fixture.Binding, snapshot.Replay.Source.Trust, snapshot.Replay.Binding, fixture.AcquireOriginAsync, SourceFence, write.RequireDesiredPins, cancellation.Token);
        int before = callbacks;
        int sourceCalls = snapshot.Replay.Source.SourceState.ReceivedCalls().Count();
        if (mode is "foreign-canceled" or "both-canceled")
        {
            foreign.Cancel();
        }

        if (mode is "both-canceled" or "same-canceled")
        {
            cancellation.Cancel();
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => prior.RequireSourceAndOriginAsync(SourceFence, mode == "same-canceled" ? cancellation.Token : foreign.Token));
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => prior.RequireSourceAndOriginAsync(SourceFence, foreign.Token));
        }

        callbacks.ShouldBe(before);
        snapshot.Replay.Source.SourceState.ReceivedCalls().Count().ShouldBe(sourceCalls);
        fixture.Actor.Stages.ShouldBe(0);
        fixture.Actor.Saves.ShouldBe(0);
        AssertDurable(fixture, durable);
        prior.Dispose();
        write.Dispose();
        AssertCleared(fixture, budget);
    }

    /// <summary>Allows a fresh independent exact-desired admission after conclusive cleanup only when its full new history and canonical state prove current.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshExactDesiredAdmissionAfterFailedPriorProofRequiresCanonicalState(bool noncanonical)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        fixture.Actor.Snapshot.OnRead = (key, token) => fixture.Actor.Saves > 0 ? Task.FromException(new IOException("independent readback unavailable")) : Task.CompletedTask;
        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Actor.Snapshot.OnRead = null;
        Dictionary<string, byte[]> durable = Capture(fixture);
        string ledger = fixture.Actor.Snapshot.Replay.Store.Durable.Keys.Single(x => x.Contains(":ledger:1", StringComparison.Ordinal));
        fixture.Actor.Snapshot.Replay.Store.Durable[ledger][0] ^= 1;
        await Should.ThrowAsync<Exception>(() => fixture.ReplaceAsync(budget));
        AssertCleared(fixture, budget);
        fixture.OriginTargets.Clear();
        int reads = fixture.Actor.Snapshot.Replay.Reads;
        int applies = fixture.Actor.Snapshot.Replay.Applies;
        fixture.Actor.Snapshot.Replay.AlwaysNoncanonical = noncanonical;
        if (noncanonical)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.ReplaceAsync(budget));
        }
        else
        {
            (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        }

        fixture.OriginTargets.ShouldBe(new long[] { 3 });
        fixture.Actor.Snapshot.Replay.Reads.ShouldBeGreaterThan(reads);
        fixture.Actor.Snapshot.Replay.Applies.ShouldBe(applies);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    /// <summary>Rechecks actual completed histories after the last pair readback before any Proven result leaves the common serialized decision.</summary>
    [Theory]
    [InlineData("fresh", "desired")]
    [InlineData("pending", "desired")]
    [InlineData("pending", "prior")]
    [InlineData("save", "desired")]
    [InlineData("save", "prior")]
    public async Task LastPairReadbackCannotSubstituteCompletedHistory(string route, string history)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        if (route == "pending")
        {
            fixture.Actor.Snapshot.OnRead = (key, token) => fixture.Actor.Saves > 0 ? Task.FromException(new IOException("independent readback unavailable")) : Task.CompletedTask;
            (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
            fixture.Actor.Snapshot.OnRead = null;
        }
        else if (route == "fresh")
        {
            (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        }

        bool canonical = false;
        int afterCanonicalWitnessReads = 0;
        int stopped = 0;
        fixture.Actor.Snapshot.Replay.Hook = (point, token) => canonical |= point == "read";
        fixture.Actor.Snapshot.OnRead = async (key, token) =>
        {
            if (canonical && fixture.Actor.Saves > 0 && key == fixture.Actor.Snapshot.Owner.WitnessKey && ++afterCanonicalWitnessReads == (route == "fresh" ? 1 : 2))
            {
                await Task.Yield();
                DaprReplayTestStore store = history == "prior" ? fixture.Actor.Snapshot.Replay.Store : fixture.NewStore;
                string ledger = store.Durable.Keys.Single(x => x.Contains(":ledger:1", StringComparison.Ordinal));
                store.Put(ledger, store.Get<DaprReplayPageLedger>(ledger)with { Accumulator = new byte[32] });
                stopped++;
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ReplaceAsync(budget));
        stopped.ShouldBe(1);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        fixture.Actor.Snapshot.Durable[fixture.Actor.Snapshot.Owner.StorageKey].ShouldBe("{\"value\":3}"u8.ToArray());
        AssertCleared(fixture, budget);
    }

    private static Dictionary<string, byte[]> Capture(DaprLogicalSnapshotReplacementFixture fixture) => fixture.Actor.Snapshot.Durable.ToDictionary(x => x.Key, x => x.Value.ToArray());
    private static void AssertDurable(DaprLogicalSnapshotReplacementFixture fixture, Dictionary<string, byte[]> durable)
    {
        fixture.Actor.Snapshot.Durable.Keys.Order().ShouldBe(durable.Keys.Order());
        foreach ((string key, byte[] value)in durable)
        {
            fixture.Actor.Snapshot.Durable[key].ShouldBe(value);
        }
    }

    private static void AssertNoWrite(DaprLogicalSnapshotReplacementFixture fixture, Dictionary<string, byte[]> durable, EventBufferBudget budget)
    {
        fixture.Actor.Stages.ShouldBe(0);
        fixture.Actor.Saves.ShouldBe(0);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    private static void AssertCleared(DaprLogicalSnapshotReplacementFixture fixture, EventBufferBudget budget)
    {
        fixture.OriginArrays.ShouldAllBe(x => x.All(b => b == 0));
        fixture.Actor.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        fixture.PriorArrays.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
    }

    private static object? Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);
}
