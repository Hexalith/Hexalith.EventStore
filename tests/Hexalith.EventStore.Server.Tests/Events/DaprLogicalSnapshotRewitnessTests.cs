using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Contracts.Events;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Certifies only fresh complete current prefixes while preserving the historical-source hold.</summary>
public sealed class DaprLogicalSnapshotRewitnessTests
{
    /// <summary>Changes the actual head and re-witnesses identical state from a separate full-prefix completed owner.</summary>
    [Fact]
    public async Task ActualCurrentPrefixRewitnessesWithoutHistoricalAuthority()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        var snapshot = fixture.Actor.Snapshot;
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => snapshot.Replay.Source.Source.RequireCurrentAsync(fixture.PriorHint, snapshot.Replay.Source.Trust, CancellationToken.None));
        byte[] legacy = "legacy-untouched"u8.ToArray();
        snapshot.Durable[DaprLogicalReplayFixture.Identity.SnapshotKey] = legacy;
        byte[] oldWitness = snapshot.Durable[snapshot.Owner.WitnessKey].ToArray();
        int applies = snapshot.Replay.Applies;
        (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        fixture.OriginCalls.ShouldBe(1);
        fixture.OwnerCalls.ShouldBe(1);
        snapshot.Replay.Applies.ShouldBe(applies);
        snapshot.Durable[snapshot.Owner.StorageKey].ShouldBe("{\"value\":1}"u8.ToArray());
        DaprLogicalSnapshotWitness witness = DaprLogicalSnapshotCodec.DecodeSnapshot(snapshot.Durable[snapshot.Owner.WitnessKey]);
        witness.OperationId.ShouldBe("current-prefix");
        witness.SourceBindingHash.ToArray().ShouldBe(DaprLogicalClaimCodec.ComputeSourceBindingHash(fixture.Current));
        witness.SourceBindingHash.ToArray().ShouldNotBe(DaprLogicalSnapshotCodec.DecodeSnapshot(oldWitness).SourceBindingHash.ToArray());
        snapshot.Durable[DaprLogicalReplayFixture.Identity.SnapshotKey].ShouldBeSameAs(legacy);
        legacy.ShouldBe("legacy-untouched"u8.ToArray());
        AssertCleared(fixture, budget);
        (await snapshot.Owner.AcquireAsync(fixture.Current, false, fixture.AcquireAsync, budget, CancellationToken.None)).ShouldBeNull();
        using DaprLogicalSnapshotCandidate candidate = (await snapshot.Owner.AcquireAsync(fixture.Current with { TargetSequence = 3 }, false, fixture.AcquireAsync, budget, CancellationToken.None))!;
        candidate.ShouldNotBeNull();
        candidate.CoveredSequence.ShouldBe(1);
        candidate.Dispose();
        (await snapshot.Owner.AcquireAsync(fixture.Current with { TargetSequence = 3 }, true, fixture.AcquireAsync, budget, CancellationToken.None)).ShouldBeNull();
        AssertCleared(fixture, budget);
    }

    /// <summary>Re-admits a proven pair from fresh actual current history without staging or saving again.</summary>
    [Fact]
    public async Task ExactDesiredRetryIsCanonicalAndIdempotent()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        Dictionary<string, byte[]> durable = Capture(fixture);
        int reads = fixture.Actor.Snapshot.Replay.Reads;
        int writes = fixture.Actor.Snapshot.Replay.Writes;
        (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.OriginCalls.ShouldBe(2);
        fixture.Actor.Snapshot.Replay.Reads.ShouldBeGreaterThan(reads);
        fixture.Actor.Snapshot.Replay.Writes.ShouldBeGreaterThan(writes);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    /// <summary>Bounds the declared hint and current source before any addressed owner callback.</summary>
    [Theory]
    [InlineData("policy")]
    [InlineData("same-head")]
    [InlineData("newer-head")]
    [InlineData("zero")]
    [InlineData("target")]
    [InlineData("prior-floor")]
    [InlineData("current-floor")]
    [InlineData("metadata")]
    [InlineData("namespace")]
    [InlineData("configuration")]
    public async Task UnsupportedBindingRefusesBeforeOwner(string changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        DaprLogicalSourceBinding prior = fixture.PriorHint;
        DaprLogicalSourceBinding current = fixture.Current;
        switch (changed)
        {
            case "same-head":
                prior = prior with
                {
                    ActorHead = current.ActorHead
                };
                break;
            case "newer-head":
                prior = prior with
                {
                    ActorHead = current.ActorHead + 1
                };
                break;
            case "zero":
                prior = prior with
                {
                    TargetSequence = 0
                };
                current = current with
                {
                    TargetSequence = 0
                };
                break;
            case "target":
                prior = prior with
                {
                    TargetSequence = 0
                };
                break;
            case "prior-floor":
                prior = prior with
                {
                    RetainedFloor = 2
                };
                break;
            case "current-floor":
                current = current with
                {
                    RetainedFloor = 2
                };
                break;
            case "metadata":
                prior = prior with
                {
                    MetadataPresent = false
                };
                break;
            case "namespace":
                prior = prior with
                {
                    Namespace = "other"
                };
                break;
            case "configuration":
                prior = prior with
                {
                    SourceConfigurationHash = new byte[32]
                };
                break;
        }

        using var budget = new EventBufferBudget();
        int calls = fixture.Actor.Snapshot.Replay.Source.SourceState.ReceivedCalls().Count();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.RewitnessAsync(budget, model: changed == "policy" ? DaprLogicalSnapshotCodec.RebaseModel : DaprLogicalSnapshotRewitnessPolicy.ModelId, prior: prior, current: current));
        fixture.OwnerCalls.ShouldBe(0);
        fixture.OriginCalls.ShouldBe(0);
        fixture.Actor.Snapshot.Replay.Source.SourceState.ReceivedCalls().Count().ShouldBe(calls);
        fixture.Actor.Stages.ShouldBe(0);
        fixture.Actor.Saves.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses torn and incompatible hint pairs without overwriting stored bytes.</summary>
    [Theory]
    [InlineData("absent")]
    [InlineData("state-only")]
    [InlineData("witness-only")]
    [InlineData("malformed")]
    [InlineData("scope")]
    [InlineData("source")]
    [InlineData("registry")]
    [InlineData("reconstruction")]
    [InlineData("serializer")]
    [InlineData("state-hash")]
    public async Task IncompatibleHintNeverStagesOrSaves(string shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
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
        else if (shape == "state-hash")
        {
            snapshot.Durable[snapshot.Owner.StorageKey][0] ^= 1;
        }
        else
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
                _ => witness with
                {
                    SerializerId = "other"
                },
            };
            snapshot.Durable[snapshot.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(witness);
        }

        Dictionary<string, byte[]> durable = Capture(fixture);
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.RewitnessAsync(budget));
        AssertNoWrite(fixture, durable, budget);
    }

    /// <summary>Allows forged historical identity only as an untrusted hint after independent full current-prefix equivalence.</summary>
    [Fact]
    public async Task ForgedHistoricalOperationIsOnlyAHint()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        var snapshot = fixture.Actor.Snapshot;
        DaprLogicalSnapshotWitness hint = DaprLogicalSnapshotCodec.DecodeSnapshot(snapshot.Durable[snapshot.Owner.WitnessKey])with
        {
            OperationId = "not-a-real-operation",
            Generation = 999,
            Accumulator = new byte[32]
        };
        snapshot.Durable[snapshot.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(hint);
        using var budget = new EventBufferBudget();
        (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        DaprLogicalSnapshotWitness actual = DaprLogicalSnapshotCodec.DecodeSnapshot(snapshot.Durable[snapshot.Owner.WitnessKey]);
        actual.OperationId.ShouldBe("current-prefix");
        actual.Generation.ShouldNotBe(999L);
        fixture.OriginCalls.ShouldBe(1);
        actual.Accumulator.ToArray().ShouldNotBe(new byte[32]);
        AssertCleared(fixture, budget);
    }

    /// <summary>Requires exact canonical equality even when the declared old state and its hashes are internally consistent.</summary>
    [Fact]
    public async Task CurrentPrefixStateMustEqualObservedHint()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        var snapshot = fixture.Actor.Snapshot;
        byte[] different = "{\"value\":9}"u8.ToArray();
        DaprLogicalSnapshotWitness hint = DaprLogicalSnapshotCodec.DecodeSnapshot(snapshot.Durable[snapshot.Owner.WitnessKey])with
        {
            StorageHash = SHA256.HashData(different),
            FoldedHash = SHA256.HashData(different)
        };
        snapshot.Durable[snapshot.Owner.StorageKey] = different;
        snapshot.Durable[snapshot.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(hint);
        Dictionary<string, byte[]> durable = Capture(fixture);
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.RewitnessAsync(budget));
        fixture.OriginCalls.ShouldBe(1);
        AssertNoWrite(fixture, durable, budget);
    }

    /// <summary>Refuses callback changes to private/actual pairs and current completed history before any save.</summary>
    [Theory]
    [InlineData("actual-pair")]
    [InlineData("private-witness")]
    [InlineData("current-history")]
    [InlineData("origin-state")]
    public async Task CodecCallbackCannotSubstitutePinsOrCurrentHistory(string changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        int stopped = 0;
        int later = 0;
        fixture.Actor.Snapshot.Replay.Hook = (point, token) =>
        {
            if (!fixture.OriginReturned)
            {
                return;
            }

            if (stopped > 0)
            {
                later++;
            }

            if (point != "read" || stopped != 0)
            {
                return;
            }

            stopped++;
            if (changed == "actual-pair")
            {
                fixture.Actor.Snapshot.Durable[fixture.Actor.Snapshot.Owner.WitnessKey][0] ^= 1;
            }
            else if (changed == "private-witness")
            {
                MemoryMarshal.TryGetArray(fixture.Pending!.PriorWitness.Bytes, out ArraySegment<byte> image).ShouldBeTrue();
                image.Array![0] ^= 1;
            }
            else if (changed == "origin-state")
            {
                fixture.OriginArrays[^1][0] ^= 1;
            }
            else
            {
                MutateHistory(fixture);
            }
        };
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<Exception>(() => fixture.RewitnessAsync(budget));
        stopped.ShouldBe(1);
        if (changed != "actual-pair")
        {
            later.ShouldBe(0);
        }

        fixture.Actor.Stages.ShouldBe(0);
        fixture.Actor.Saves.ShouldBe(0);
        AssertCleared(fixture, budget);
    }

    /// <summary>Preserves original cancellation and expires all callback facades before later asynchronous work.</summary>
    [Theory]
    [InlineData("read", false)]
    [InlineData("read", true)]
    [InlineData("write", false)]
    [InlineData("write", true)]
    public async Task CanonicalCancellationStopsLaterCallbacks(string point, bool foreign)
    {
        ArgumentNullException.ThrowIfNull(point);
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        using var cancel = new CancellationTokenSource();
        int stopped = 0;
        int later = 0;
        fixture.Actor.Snapshot.Replay.Hook = (callback, token) =>
        {
            if (!fixture.OriginReturned)
            {
                return;
            }

            if (stopped > 0)
            {
                later++;
            }

            if (callback != point || stopped != 0)
            {
                return;
            }

            stopped++;
            cancel.Cancel();
            if (foreign)
            {
                throw new OperationCanceledException(new CancellationToken(true));
            }

            throw new IOException("callback failed after original cancellation");
        };
        using var budget = new EventBufferBudget();
        (await Should.ThrowAsync<OperationCanceledException>(() => fixture.RewitnessAsync(budget, cancel.Token))).CancellationToken.ShouldBe(cancel.Token);
        stopped.ShouldBe(1);
        later.ShouldBe(0);
        AssertNoWrite(fixture, durable, budget);
    }

    /// <summary>Classifies actual paired bytes independently of acknowledgement without repeating an uncertain save.</summary>
    [Theory]
    [InlineData("normal")]
    [InlineData("commit-throw")]
    [InlineData("no-commit")]
    [InlineData("mixed")]
    [InlineData("state-only")]
    public async Task IndependentReadbackClassifiesAndPendingNeverRepeatsSave(string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        fixture.Actor.SaveMode = mode == "state-only" ? "mixed" : mode;
        if (mode == "mixed")
        {
            fixture.Actor.OnSave = token =>
            {
                fixture.Actor.Snapshot.Durable[fixture.Actor.Snapshot.Owner.WitnessKey] = "third-witness"u8.ToArray();
                return Task.CompletedTask;
            };
        }

        using var budget = new EventBufferBudget();
        (await fixture.RewitnessAsync(budget)).ShouldBe(mode == "mixed" ? DaprReplayCommitOutcome.Indeterminate : mode is "no-commit" or "state-only" ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Proven);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        if (mode == "mixed")
        {
            fixture.Pending!.PolicyId.ShouldBe(DaprLogicalSnapshotRewitnessPolicy.ModelId);
            budget.LiveBytes.ShouldBeGreaterThan(0);
            (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
            Restore(fixture, durable);
            (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.NoCommit);
            fixture.Actor.Stages.ShouldBe(2);
            fixture.Actor.Saves.ShouldBe(1);
        }

        AssertCleared(fixture, budget);
    }

    /// <summary>Refuses older policy entries before any readback, origin, stage or save, for both actual desired and mixed pending pairs.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RewitnessPendingCannotCrossIntoOlderPolicy(bool actualDesired, bool replacement)
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> prior = Capture(fixture);
        using var budget = new EventBufferBudget();
        await MakePendingAsync(fixture, budget, actualDesired);
        int calls = fixture.Actor.Snapshot.Replay.Source.SourceState.ReceivedCalls().Count();
        int origins = fixture.OriginCalls;
        int reads = fixture.Actor.Snapshot.SnapshotReads;
        int charged = budget.LiveBytes;
        Dictionary<string, byte[]> actual = Capture(fixture);
        await Should.ThrowAsync<InvalidOperationException>(() => replacement ? fixture.ReplaceCurrentAsync(budget) : fixture.IssueCurrentAsync(budget));
        fixture.Actor.Snapshot.Replay.Source.SourceState.ReceivedCalls().Count().ShouldBe(calls);
        fixture.OriginCalls.ShouldBe(origins);
        fixture.Actor.Snapshot.SnapshotReads.ShouldBe(reads);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        budget.LiveBytes.ShouldBe(charged);
        AssertDurable(fixture, actual);
        fixture.Pending!.PolicyId.ShouldBe(DaprLogicalSnapshotRewitnessPolicy.ModelId);
        Restore(fixture, prior);
        (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        AssertCleared(fixture, budget);
    }

    /// <summary>Requires a fresh canonical current origin before releasing a physically desired uncertain attempt as Proven.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingProvenRecoveryRequiresFreshCurrentCanonicalProof(bool noncanonical)
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        await MakePendingAsync(fixture, budget, true);
        int reads = fixture.Actor.Snapshot.Replay.Reads;
        fixture.Actor.Snapshot.Replay.AlwaysNoncanonical = noncanonical;
        if (noncanonical)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.RewitnessAsync(budget));
        }
        else
        {
            (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        }

        fixture.OriginCalls.ShouldBe(2);
        fixture.Actor.Snapshot.Replay.Reads.ShouldBeGreaterThan(reads);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        AssertCleared(fixture, budget);
    }

    /// <summary>Rejects an initial issuance pending obligation before any re-witness callback or classification.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialPendingCannotCrossIntoRewitness(bool actualDesired)
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        fixture.Actor.Snapshot.Durable.Clear();
        using var budget = new EventBufferBudget();
        ConfigurePending(fixture, actualDesired);
        (await fixture.IssueCurrentAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Actor.Snapshot.OnRead = null;
        int calls = fixture.Actor.Snapshot.Replay.Source.SourceState.ReceivedCalls().Count();
        int origins = fixture.OriginCalls;
        int charged = budget.LiveBytes;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.RewitnessAsync(budget));
        fixture.Actor.Snapshot.Replay.Source.SourceState.ReceivedCalls().Count().ShouldBe(calls);
        fixture.OriginCalls.ShouldBe(origins);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        budget.LiveBytes.ShouldBe(charged);
        fixture.Actor.Snapshot.Durable.Clear();
        (await fixture.IssueCurrentAsync(budget)).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        AssertCleared(fixture, budget);
    }

    /// <summary>Rejects replacement pending ownership before current-prefix readback for actual desired and mixed pairs.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementPendingCannotCrossIntoRewitness(bool actualDesired)
    {
        await using var fixture = new DaprLogicalSnapshotReplacementFixture();
        await fixture.PrepareAsync(1, 2);
        var snapshot = fixture.Actor.Snapshot;
        Dictionary<string, byte[]> prior = snapshot.Durable.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);
        fixture.Actor.SaveMode = actualDesired ? "normal" : "mixed";
        snapshot.OnRead = (key, token) =>
        {
            if (actualDesired && fixture.Actor.Saves == 1)
            {
                throw new IOException("independent readback unavailable");
            }

            return Task.CompletedTask;
        };
        using var budget = new EventBufferBudget();
        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        snapshot.OnRead = null;
        int calls = snapshot.Replay.Source.SourceState.ReceivedCalls().Count();
        int origins = fixture.OriginTargets.Count;
        int charged = budget.LiveBytes;
        Dictionary<string, byte[]> actual = snapshot.Durable.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Actor.Actor.RewitnessLogicalSnapshotAsync(DaprLogicalSnapshotRewitnessPolicy.ModelId, fixture.Binding with { ActorHead = 2 }, fixture.Binding, snapshot.Replay.Source.Source, snapshot.Replay.Source.Trust, snapshot.Replay.Binding, 32, fixture.AcquireOriginAsync, snapshot.SerializeOwnerAsync, budget, CancellationToken.None));
        snapshot.Replay.Source.SourceState.ReceivedCalls().Count().ShouldBe(calls);
        fixture.OriginTargets.Count.ShouldBe(origins);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        budget.LiveBytes.ShouldBe(charged);
        foreach ((string key, byte[] value)in actual)
        {
            snapshot.Durable[key].ShouldBe(value);
        }

        snapshot.Durable.Clear();
        foreach ((string key, byte[] value)in prior)
        {
            snapshot.Durable[key] = value;
        }

        (await fixture.ReplaceAsync(budget)).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        budget.LiveBytes.ShouldBe(0);
        fixture.OriginArrays.ShouldAllBe(x => x.All(b => b == 0));
        fixture.Actor.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
    }

    /// <summary>Keeps copied state charged through an actual asynchronous stage and clears it on refusal.</summary>
    [Fact]
    public async Task YieldingStageKeepsCapacityAndExpiresFacadesBeforeRefusal()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        using var budget = new EventBufferBudget();
        int observed = 0;
        fixture.Actor.OnStage = async (key, image, token) =>
        {
            await Task.Yield();
            budget.LiveBytes.ShouldBeGreaterThan(400000);
            fixture.OriginArrays.ShouldAllBe(x => x.Any(b => b != 0));
            foreach (IReadOnlyPayload payload in fixture.Actor.Snapshot.Replay.Borrowed)
            {
                Should.Throw<ObjectDisposedException>(() => _ = payload.Length);
            }

            foreach (IBoundedPayloadWriter writer in fixture.Actor.Snapshot.Replay.BorrowedWriters)
            {
                Should.Throw<ObjectDisposedException>(() => writer.Write([]));
            }

            observed++;
            MutateHistory(fixture);
        };
        await Should.ThrowAsync<Exception>(() => fixture.RewitnessAsync(budget));
        observed.ShouldBe(1);
        fixture.Actor.Stages.ShouldBe(1);
        fixture.Actor.Saves.ShouldBe(0);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    /// <summary>Checks the actual current history after the final paired readback and refuses release on callback substitution.</summary>
    [Fact]
    public async Task LastReadbackCannotSubstituteCurrentCompletedHistory()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        int changed = 0;
        fixture.Actor.Snapshot.OnRead = (key, token) =>
        {
            if (fixture.Actor.Saves == 1 && key == fixture.Actor.Snapshot.Owner.WitnessKey && changed++ == 0)
            {
                MutateHistory(fixture);
            }

            return Task.CompletedTask;
        };
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<Exception>(() => fixture.RewitnessAsync(budget));
        changed.ShouldBe(1);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        AssertCleared(fixture, budget);
    }

    /// <summary>Requires a fresh current origin after the last readback on an exact desired retry.</summary>
    [Fact]
    public async Task ExactDesiredLastReadbackCannotSubstituteCurrentHistory()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        int reads = fixture.Actor.Snapshot.SnapshotReads;
        int changed = 0;
        fixture.Actor.Snapshot.OnRead = (key, token) =>
        {
            if (fixture.Actor.Snapshot.SnapshotReads == reads + 4 && key == fixture.Actor.Snapshot.Owner.WitnessKey)
            {
                changed++;
                MutateHistory(fixture);
            }

            return Task.CompletedTask;
        };
        await Should.ThrowAsync<Exception>(() => fixture.RewitnessAsync(budget));
        changed.ShouldBe(1);
        fixture.Actor.Stages.ShouldBe(2);
        fixture.Actor.Saves.ShouldBe(1);
        AssertCleared(fixture, budget);
    }

    /// <summary>Requires a complete original-token decision and bounds the early-return liveness control.</summary>
    [Theory]
    [InlineData("skip")]
    [InlineData("foreign")]
    [InlineData("repeat")]
    [InlineData("early")]
    public async Task CommonOwnerMustCompleteExactlyOneOriginalDecision(string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        fixture.Actor.Snapshot.FenceMode = mode;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (mode == "early")
        {
            fixture.OnOrigin = (origin, token) => release.Task;
        }

        using var budget = new EventBufferBudget();
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.RewitnessAsync(budget).WaitAsync(TimeSpan.FromSeconds(3)));
            if (mode is "skip" or "foreign" or "early")
            {
                fixture.Actor.Stages.ShouldBe(0);
                fixture.Actor.Saves.ShouldBe(0);
            }
        }
        finally
        {
            release.TrySetResult();
            if (fixture.Actor.Snapshot.LastDecision is not null)
            {
                try
                {
                    await fixture.Actor.Snapshot.LastDecision.WaitAsync(TimeSpan.FromSeconds(3));
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        AssertCleared(fixture, budget);
    }

    /// <summary>Admits all typed materialization before any origin callback when a conservative configured ceiling cannot fit.</summary>
    [Fact]
    public async Task CapacityRefusesBeforeOriginAndStage()
    {
        await using var fixture = new DaprLogicalSnapshotRewitnessFixture();
        await fixture.PrepareAsync();
        Dictionary<string, byte[]> durable = Capture(fixture);
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.RewitnessAsync(budget, maximumStateBytes: 64 * 1024 * 1024));
        fixture.OriginCalls.ShouldBe(0);
        AssertNoWrite(fixture, durable, budget);
    }

    private static async Task MakePendingAsync(DaprLogicalSnapshotRewitnessFixture fixture, EventBufferBudget budget, bool actualDesired)
    {
        ConfigurePending(fixture, actualDesired);
        (await fixture.RewitnessAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Actor.Snapshot.OnRead = null;
        fixture.Pending.ShouldNotBeNull();
    }

    private static void ConfigurePending(DaprLogicalSnapshotRewitnessFixture fixture, bool actualDesired)
    {
        fixture.Actor.SaveMode = actualDesired ? "normal" : "mixed";
        fixture.Actor.OnSave = token =>
        {
            if (!actualDesired)
            {
                fixture.Actor.Snapshot.Durable[fixture.Actor.Snapshot.Owner.WitnessKey] = "third-witness"u8.ToArray();
            }

            return Task.CompletedTask;
        };
        fixture.Actor.Snapshot.OnRead = (key, token) =>
        {
            if (actualDesired && fixture.Actor.Saves == 1)
            {
                throw new IOException("independent readback unavailable");
            }

            return Task.CompletedTask;
        };
    }

    private static void MutateHistory(DaprLogicalSnapshotRewitnessFixture fixture)
    {
        string key = fixture.CurrentStore.Durable.Keys.Single(x => x.Contains(":ledger:1", StringComparison.Ordinal));
        fixture.CurrentStore.Durable[key][0] ^= 1;
    }

    private static Dictionary<string, byte[]> Capture(DaprLogicalSnapshotRewitnessFixture fixture) => fixture.Actor.Snapshot.Durable.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal);
    private static void Restore(DaprLogicalSnapshotRewitnessFixture fixture, Dictionary<string, byte[]> durable)
    {
        fixture.Actor.Snapshot.Durable.Clear();
        foreach ((string key, byte[] value)in durable)
        {
            fixture.Actor.Snapshot.Durable[key] = value.ToArray();
        }
    }

    private static void AssertDurable(DaprLogicalSnapshotRewitnessFixture fixture, Dictionary<string, byte[]> durable)
    {
        fixture.Actor.Snapshot.Durable.Keys.Order().ShouldBe(durable.Keys.Order());
        foreach ((string key, byte[] value)in durable)
        {
            fixture.Actor.Snapshot.Durable[key].ShouldBe(value);
        }
    }

    private static void AssertNoWrite(DaprLogicalSnapshotRewitnessFixture fixture, Dictionary<string, byte[]> durable, EventBufferBudget budget)
    {
        fixture.Actor.Stages.ShouldBe(0);
        fixture.Actor.Saves.ShouldBe(0);
        AssertDurable(fixture, durable);
        AssertCleared(fixture, budget);
    }

    private static void AssertCleared(DaprLogicalSnapshotRewitnessFixture fixture, EventBufferBudget budget)
    {
        budget.LiveBytes.ShouldBe(0);
        fixture.Pending.ShouldBeNull();
        fixture.OriginArrays.ShouldAllBe(x => x.All(b => b == 0));
        fixture.Actor.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        foreach (IReadOnlyPayload payload in fixture.Actor.Snapshot.Replay.Borrowed)
        {
            Should.Throw<ObjectDisposedException>(() => _ = payload.Length);
        }

        foreach (IBoundedPayloadWriter writer in fixture.Actor.Snapshot.Replay.BorrowedWriters)
        {
            Should.Throw<ObjectDisposedException>(() => writer.Write([]));
        }
    }
}
