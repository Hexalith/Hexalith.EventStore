using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Actors;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Controls the separate anchor framing, actual actor pair boundary and private initial ownership without replay authority.</summary>
public sealed class DaprLogicalAnchoredReplayTests
{
    /// <summary>Matches independently generated complete selection/seed images and exact measured capacity.</summary>
    [Fact]
    public void IndependentAnchorVectorsMatchStrictMeasuredImages()
    {
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllText(FindVectors()));
        JsonElement rows = vectors.RootElement.GetProperty("vectors");
        foreach (JsonElement row in rows.EnumerateArray().Take(4))
        {
            byte[] expected = Convert.FromHexString(row.GetProperty("hex").GetString()!);
            DaprLogicalReplayAnchorSelection value = Selection();
            value = row.GetProperty("name").GetString() switch
            {
                "anchor-state-change" => value with
                {
                    CanonicalStateHash = Hash(9)
                },
                "anchor-zero-tail" => value with
                {
                    CoveredSequence = 3
                },
                "anchor-max" => value with
                {
                    CoveredSequence = long.MaxValue,
                    ActorHead = long.MaxValue,
                    TargetSequence = long.MaxValue
                },
                _ => value
            };
            byte[] actual = DaprLogicalReplayAnchorCodec.Encode(value);
            actual.ShouldBe(expected);
            DaprLogicalReplayAnchorCodec.Measure(value).ShouldBe(actual.Length);
            Convert.ToHexString(SHA256.HashData(actual)).ToLowerInvariant().ShouldBe(row.GetProperty("sha256").GetString());
            DaprLogicalReplayAnchorCodec.Encode(DaprLogicalReplayAnchorCodec.Decode(actual)).ShouldBe(expected);
        }

        byte[] selectionHash = SHA256.HashData(DaprLogicalReplayAnchorCodec.Encode(Selection()));
        using var budget = new EventBufferBudget();
        byte[][] seeds = [DaprLogicalReplayAnchorCodec.AccumulatorSeed(selectionHash, Hash(6), budget), DaprLogicalReplayAnchorCodec.EffectiveSeed(selectionHash, Hash(7), budget), DaprLogicalReplayAnchorCodec.TranscriptSeed(selectionHash, Hash(8), budget)];
        for (int i = 0; i < seeds.Length; i++)
        {
            Convert.ToHexString(seeds[i]).ToLowerInvariant().ShouldBe(rows[i + 4].GetProperty("sha256").GetString());
        }

        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses foreign/reordered/duplicate/trailing/invalid scalar framing instead of interpreting v1 data as anchors.</summary>
    [Theory]
    [InlineData("model")]
    [InlineData("below-head-zero")]
    [InlineData("covered-zero")]
    [InlineData("above-target")]
    [InlineData("above-head")]
    [InlineData("hash")]
    [InlineData("text")]
    [InlineData("utf8")]
    [InlineData("tag")]
    [InlineData("trailing")]
    [InlineData("version")]
    public void CanonicalSelectionRefusesUnsupportedShapes(string shape)
    {
        DaprLogicalReplayAnchorSelection value = Selection();
        if (shape is "tag" or "trailing" or "version")
        {
            byte[] bytes = DaprLogicalReplayAnchorCodec.Encode(value);
            if (shape == "tag")
            {
                bytes["HX-EV-DAPR-REPLAY-ANCHOR-1\0"u8.Length + 3] = 2;
            }

            if (shape == "version")
            {
                bytes["HX-EV-DAPR-REPLAY-ANCHOR-1\0"u8.Length] = 0;
            }

            if (shape == "trailing")
            {
                bytes = [..bytes, 0];
            }

            Should.Throw<ArgumentException>(() => DaprLogicalReplayAnchorCodec.Decode(bytes));
            return;
        }

        value = shape switch
        {
            "model" => value with
            {
                ModelId = DaprLogicalSourceBinding.ModelId
            },
            "below-head-zero" => value with
            {
                TargetSequence = 1
            },
            "covered-zero" => value with
            {
                CoveredSequence = 0
            },
            "above-target" => value with
            {
                CoveredSequence = 4
            },
            "above-head" => value with
            {
                ActorHead = 2
            },
            "hash" => value with
            {
                CanonicalStateHash = new byte[31]
            },
            "text" => value with
            {
                TenantId = new string ('a', 513)
            },
            _ => value with
            {
                Domain = "\ud800"
            }
        };
        Should.Throw<ArgumentException>(() => DaprLogicalReplayAnchorCodec.Encode(value));
    }

    /// <summary>Adopts actual canonical coverage into privately pinned seeds while preserving durable bytes and callback limits.</summary>
    [Fact]
    public async Task PrivateInitialAnchorOwnsCanonicalStateAndDistinctSeedsWithoutAdvancingReplay()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        DaprLogicalSourceBinding source = await fixture.SourceAsync();
        DaprLogicalSnapshotCandidate candidate = (await fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, CancellationToken.None))!;
        int applies = fixture.Replay.Applies;
        int saves = fixture.Replay.Store.Saves;
        Dictionary<string, byte[]> durable = fixture.Durable.ToDictionary(x => x.Key, x => x.Value.ToArray());
        using DaprLogicalReplayInitialAnchor anchor = await DaprLogicalReplayInitialAnchor.AdoptAsync(DaprLogicalReplayAnchorCodec.ModelId, candidate, source, fixture.Replay.Binding, budget, CancellationToken.None);
        byte[] canonical = new byte[anchor.CanonicalState.Length];
        anchor.CanonicalState.CopyTo(0, canonical);
        canonical.ShouldBe("{\"value\":1}"u8.ToArray());
        DaprLogicalReplayAnchorSelection selection = DaprLogicalReplayAnchorCodec.Decode(anchor.SelectionImage);
        selection.SourceBindingHash.ToArray().ShouldBe(DaprLogicalClaimCodec.ComputeSourceBindingHash(source));
        selection.CoveredSequence.ShouldBe(1);
        anchor.AccumulatorSeed.ToArray().ShouldNotBe(selection.CoveredAccumulator.ToArray());
        await anchor.RequireCurrentAsync(CancellationToken.None);
        fixture.Replay.Applies.ShouldBe(applies);
        fixture.Replay.Store.Saves.ShouldBe(saves);
        AssertDurable(durable, fixture.Durable);
        byte[][] owned = [Memory(anchor.SelectionImage), Memory(anchor.AccumulatorSeed), Memory(anchor.EffectiveSeed), Memory(anchor.TranscriptSeed), Buffer((object)anchor.CanonicalState, "_owner")];
        budget.LiveBytes.ShouldBeGreaterThan(0);
        ((int)Field(Field(anchor, "_metadataCharge")!, "_capacity")!).ShouldBe(4 * DaprLogicalReplayAnchorCodec.MaximumBytes + 4096);
        anchor.Dispose();
        owned.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
        Field(anchor, "_canonical").ShouldBeNull();
        Field(anchor, "_candidate").ShouldBeNull();
    }

    /// <summary>Proves actual actor save acknowledgement cannot replace fresh complete paired readback.</summary>
    [Theory]
    [InlineData("normal", DaprReplayCommitOutcome.Proven)]
    [InlineData("commit-throw", DaprReplayCommitOutcome.Proven)]
    [InlineData("no-commit", DaprReplayCommitOutcome.NoCommit)]
    [InlineData("mixed", DaprReplayCommitOutcome.Indeterminate)]
    public async Task ActualActorPairSaveUsesFreshExactClassification(string mode, object expected)
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        fixture.SaveMode = mode;
        using var budget = new EventBufferBudget();
        (await fixture.IssueAsync(budget)).ShouldBe((DaprReplayCommitOutcome)expected);
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(2);
        if (mode == "mixed")
        {
            budget.LiveBytes.ShouldBeGreaterThan(0);
            fixture.SdkAliases.ShouldAllBe(x => x.Any(b => b != 0));
            (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
            fixture.Saves.ShouldBe(1);
        }
        else
        {
            fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
            budget.LiveBytes.ShouldBe(0);
        }
    }

    /// <summary>Shows a matching complete pair is idempotent and never saved again.</summary>
    [Fact]
    public async Task ExistingExactPairDoesNotStageOrSaveAgain()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync(true);
        using var budget = new EventBufferBudget();
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Saves.ShouldBe(0);
        fixture.Stages.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Requires the exact distinct model, composed parent and requested candidate source before any codec callback.</summary>
    [Theory]
    [InlineData("model")]
    [InlineData("parent")]
    [InlineData("source")]
    public async Task PrivateInitialSelectionRequiresExactModelParentAndSource(string changed)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        using var foreign = new EventBufferBudget();
        DaprLogicalSourceBinding source = await fixture.SourceAsync();
        DaprLogicalSnapshotCandidate candidate = (await fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, CancellationToken.None))!;
        int reads = fixture.Replay.Reads;
        byte[] canonical = Buffer((object)candidate.CanonicalState, "_owner");
        await Should.ThrowAsync<InvalidOperationException>(() => DaprLogicalReplayInitialAnchor.AdoptAsync(changed == "model" ? DaprLogicalSourceBinding.ModelId : DaprLogicalReplayAnchorCodec.ModelId, candidate, changed == "source" ? source with { TargetSequence = 2 } : source, fixture.Replay.Binding, changed == "parent" ? foreign : budget, CancellationToken.None));
        fixture.Replay.Reads.ShouldBe(reads);
        canonical.ShouldAllBe(b => b == 0);
        budget.LiveBytes.ShouldBe(0);
        foreign.LiveBytes.ShouldBe(0);
    }

    /// <summary>Preserves original cancellation over ordinary and foreign callback exceptions at both canonical codec boundaries.</summary>
    [Theory]
    [InlineData("read", false)]
    [InlineData("read", true)]
    [InlineData("write", false)]
    [InlineData("write", true)]
    public async Task PrivateInitialReadWriteCancellationClearsOwnedCandidate(string point, bool foreign)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        DaprLogicalSourceBinding source = await fixture.SourceAsync(token: cancellation.Token);
        DaprLogicalSnapshotCandidate candidate = (await fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, cancellation.Token))!;
        byte[] canonical = Buffer((object)candidate.CanonicalState, "_owner");
        int applies = fixture.Replay.Applies;
        int saves = fixture.Replay.Store.Saves;
        int stopped = 0;
        int later = 0;
        fixture.Replay.Hook = (callback, token) =>
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

                throw new IOException("application cancellation sentinel");
            }
        };
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => DaprLogicalReplayInitialAnchor.AdoptAsync(DaprLogicalReplayAnchorCodec.ModelId, candidate, source, fixture.Replay.Binding, budget, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        stopped.ShouldBe(1);
        later.ShouldBe(0);
        fixture.Replay.Applies.ShouldBe(applies);
        fixture.Replay.Store.Saves.ShouldBe(saves);
        canonical.ShouldAllBe(b => b == 0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses actual pair or retained private aliases changed during an actual owner await.</summary>
    [Theory]
    [InlineData("pair")]
    [InlineData("canonical")]
    [InlineData("selection")]
    [InlineData("seed")]
    public async Task PrivateInitialYieldingFenceSubstitutionWithholdsOwnership(string changed)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        DaprLogicalSourceBinding source = await fixture.SourceAsync();
        DaprLogicalSnapshotCandidate candidate = (await fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, CancellationToken.None))!;
        using DaprLogicalReplayInitialAnchor anchor = await DaprLogicalReplayInitialAnchor.AdoptAsync(DaprLogicalReplayAnchorCodec.ModelId, candidate, source, fixture.Replay.Binding, budget, CancellationToken.None);
        byte[] canonical = Buffer((object)anchor.CanonicalState, "_owner");
        byte[] selection = Memory(anchor.SelectionImage);
        byte[] seed = Memory(anchor.AccumulatorSeed);
        int reached = 0;
        fixture.OnRead = async (key, token) =>
        {
            if (reached++ == 0)
            {
                budget.LiveBytes.ShouldBeGreaterThan(0);
                await Task.Yield();
                foreach (var lease in fixture.Replay.Borrowed)
                {
                    Should.Throw<ObjectDisposedException>(() => _ = lease.Length);
                }

                foreach (var writer in fixture.Replay.BorrowedWriters)
                {
                    Should.Throw<ObjectDisposedException>(() => writer.Write([]));
                }

                switch (changed)
                {
                    case "pair":
                        fixture.Durable[fixture.Owner.WitnessKey][0] ^= 1;
                        break;
                    case "canonical":
                        canonical[0] ^= 1;
                        break;
                    case "selection":
                        selection[0] ^= 1;
                        break;
                    default:
                        seed[0] ^= 1;
                        break;
                }
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => anchor.RequireCurrentAsync(CancellationToken.None));
        reached.ShouldBeGreaterThan(0);
        anchor.Dispose();
        canonical.ShouldAllBe(b => b == 0);
        selection.ShouldAllBe(b => b == 0);
        seed.ShouldAllBe(b => b == 0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses composed capacity before canonical application intake and clears transferred candidate ownership.</summary>
    [Fact]
    public async Task PrivateInitialCapacityRefusalStopsBeforeCanonicalCallback()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        DaprLogicalSourceBinding source = await fixture.SourceAsync();
        DaprLogicalSnapshotCandidate candidate = (await fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, CancellationToken.None))!;
        using EventBufferReservation held = budget.Reserve(128 * 1024 * 1024 - budget.LiveBytes - 1024);
        int reads = fixture.Replay.Reads;
        await Should.ThrowAsync<InvalidOperationException>(() => DaprLogicalReplayInitialAnchor.AdoptAsync(DaprLogicalReplayAnchorCodec.ModelId, candidate, source, fixture.Replay.Binding, budget, CancellationToken.None));
        fixture.Replay.Reads.ShouldBe(reads);
        held.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Preserves any nonidentical, torn or malformed existing pair without staging, deleting or saving.</summary>
    [Theory]
    [InlineData("state-only")]
    [InlineData("witness-only")]
    [InlineData("malformed")]
    public async Task PriorNonidenticalPairHoldsWithoutOverwrite(string shape)
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync(true);
        if (shape == "state-only")
        {
            fixture.Snapshot.Durable.Remove(fixture.Snapshot.Owner.WitnessKey);
        }
        else if (shape == "witness-only")
        {
            fixture.Snapshot.Durable.Remove(fixture.Snapshot.Owner.StorageKey);
        }
        else
        {
            fixture.Snapshot.Durable[fixture.Snapshot.Owner.WitnessKey] = "malformed"u8.ToArray();
        }

        Dictionary<string, byte[]> durable = fixture.Snapshot.Durable.ToDictionary(x => x.Key, x => x.Value.ToArray());
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.IssueAsync(budget));
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        AssertDurable(durable, fixture.Snapshot.Durable);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Stops before any later SDK stage/save when a staging callback cancels and throws or records capability loss.</summary>
    [Theory]
    [InlineData(1, "cancel")]
    [InlineData(2, "cancel")]
    [InlineData(1, "foreign")]
    [InlineData(2, "foreign")]
    [InlineData(1, "loss")]
    [InlineData(2, "loss")]
    public async Task ActualPairStageLossStopsLaterStagesAndRestoresCharge(int stopAt, string mode)
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        fixture.OnStage = async (key, image, token) =>
        {
            if (fixture.Stages != stopAt)
            {
                return;
            }

            token.ShouldBe(cancellation.Token);
            budget.LiveBytes.ShouldBeGreaterThan(image.Length);
            image.ShouldContain(b => b != 0);
            await Task.Yield();
            if (mode == "loss")
            {
                fixture.Snapshot.Replay.Source.Registry.CapabilityLoss.ObserveViolation();
                return;
            }

            cancellation.Cancel();
            if (mode == "foreign")
            {
                throw new OperationCanceledException(new CancellationToken(true));
            }

            throw new IOException("staging sentinel");
        };
        if (mode == "loss")
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.IssueAsync(budget, cancellation.Token));
        }
        else
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.IssueAsync(budget, cancellation.Token));
            error.CancellationToken.ShouldBe(cancellation.Token);
        }

        fixture.Stages.ShouldBe(stopAt);
        fixture.Saves.ShouldBe(0);
        fixture.Snapshot.Durable.ShouldBeEmpty();
        fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Detects a modified private staging alias immediately after the SDK callback, before a later stage/save.</summary>
    [Fact]
    public async Task ActualPairStagingAliasSubstitutionStopsBeforeLaterStage()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        fixture.OnStage = async (key, image, token) =>
        {
            await Task.Yield();
            image[0] ^= 1;
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.IssueAsync(budget));
        fixture.Stages.ShouldBe(1);
        fixture.Saves.ShouldBe(0);
        fixture.Snapshot.Durable.ShouldBeEmpty();
        fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Reconciles durable truth independently after cancelled/foreign save acknowledgement and withholds originating output.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualPairSaveCancellationPreservesCommitAndClearsPrivateCapacity(bool foreign)
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        fixture.OnSave = async token =>
        {
            await Task.Yield();
            cancellation.Cancel();
            if (foreign)
            {
                throw new OperationCanceledException(new CancellationToken(true));
            }

            throw new IOException("cancelled acknowledgement");
        };
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.IssueAsync(budget, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        fixture.Saves.ShouldBe(1);
        fixture.Snapshot.Durable.Count.ShouldBe(2);
        fixture.Snapshot.Durable[fixture.Snapshot.Owner.StorageKey].ShouldBe("{\"value\":1}"u8.ToArray());
        fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
        fixture.OnSave = null;
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Saves.ShouldBe(1);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Holds all private staging charges through yielding uncertain readback, then reconciles without a repeated save.</summary>
    [Fact]
    public async Task ActualPairYieldingReadbackRetainsUntilExactRecovery()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        byte[]? exactWitness = null;
        int observed = 0;
        fixture.Snapshot.OnRead = async (key, token) =>
        {
            if (fixture.Saves > 0 && observed++ == 0)
            {
                budget.LiveBytes.ShouldBeGreaterThan(400000);
                fixture.SdkAliases.ShouldAllBe(x => x.Any(b => b != 0));
                await Task.Yield();
                exactWitness = fixture.Snapshot.Durable[fixture.Snapshot.Owner.WitnessKey].ToArray();
                fixture.Snapshot.Durable[fixture.Snapshot.Owner.WitnessKey][0] ^= 1;
            }
        };
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        observed.ShouldBeGreaterThan(0);
        budget.LiveBytes.ShouldBeGreaterThan(0);
        fixture.Saves.ShouldBe(1);
        fixture.Snapshot.Durable[fixture.Snapshot.Owner.WitnessKey] = exactWitness!;
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Saves.ShouldBe(1);
        fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses an inadmissible configured maximum before origin, actor materialization, stages or save.</summary>
    [Fact]
    public async Task ActualPairConservativeMaximumRefusesBeforeOriginOrStages()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        int reads = fixture.Snapshot.SnapshotReads;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.IssueAsync(budget, maximumStateBytes: 64 * 1024 * 1024));
        fixture.Snapshot.OriginCalls.ShouldBe(0);
        fixture.Snapshot.SnapshotReads.ShouldBe(reads);
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses a missing completed origin rather than deriving witness authority from a DTO or throwing a null reference.</summary>
    [Fact]
    public async Task ActualPairMissingCompletedOriginStopsBeforeStage()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        fixture.Snapshot.NullOrigin = true;
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.IssueAsync(budget));
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Rejects skipped, repeated, foreign-token and early owner decisions and observes completed cleanup before assertions.</summary>
    [Theory]
    [InlineData("skip")]
    [InlineData("repeat")]
    [InlineData("foreign")]
    [InlineData("early")]
    public async Task ActualSerializedOwnerMustCompleteOneOriginalDecision(string mode)
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        fixture.Snapshot.FenceMode = mode;
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (mode == "early")
        {
            _ = fixture.Snapshot.Replay.Source.SourceState.TryGetStateAsync<AggregateMetadata>(DaprLogicalReplayFixture.Identity.MetadataKey, Arg.Any<CancellationToken>()).Returns(async call =>
            {
                await released.Task;
                return new Dapr.Actors.Runtime.ConditionalValue<AggregateMetadata>(true, fixture.Snapshot.Replay.Source.Metadata!);
            });
        }

        Task<DaprReplayCommitOutcome> invocation = fixture.IssueAsync(budget);
        bool refusedBeforeReadRelease = true;
        if (mode == "early")
        {
            _ = await Task.WhenAny(invocation, Task.Delay(TimeSpan.FromSeconds(2)));
            refusedBeforeReadRelease = invocation.IsCompleted;
            released.SetResult();
        }

        await Should.ThrowAsync<InvalidOperationException>(() => invocation);
        if (mode == "early")
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.Snapshot.LastDecision!);
        }

        fixture.Saves.ShouldBe(mode == "repeat" ? 1 : 0);
        fixture.Stages.ShouldBe(mode == "repeat" ? 2 : 0);
        budget.LiveBytes.ShouldBe(0);
        refusedBeforeReadRelease.ShouldBeTrue();
    }

    /// <summary>Refuses a valid actual origin from the wrong covered target or parent before any actor pair stage.</summary>
    [Theory]
    [InlineData("covered")]
    [InlineData("parent")]
    public async Task ActualIssuerRequiresExactCompletedOriginScope(string changed)
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        using var foreign = new EventBufferBudget();
        DaprLogicalSourceBinding binding = changed == "covered" ? await fixture.Snapshot.SourceAsync() : fixture.Snapshot.OriginBinding;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Actor.IssueLogicalSnapshotAsync(DaprLogicalSnapshotCodec.SnapshotModel, binding, fixture.Snapshot.Replay.Source.Source, fixture.Snapshot.Replay.Source.Trust, fixture.Snapshot.Replay.Binding, 32, (covered, parent, token) => fixture.Snapshot.AcquireOriginAsync(1, changed == "parent" ? foreign : parent, token), fixture.Snapshot.SerializeOwnerAsync, budget, CancellationToken.None));
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        fixture.Snapshot.Durable.ShouldBeEmpty();
        budget.LiveBytes.ShouldBe(0);
        foreign.LiveBytes.ShouldBe(0);
    }

    /// <summary>Classifies and releases conclusive pending storage independently while lost authority prevents outcome release.</summary>
    [Fact]
    public async Task ActualPendingRecoveryClearsWithoutTrustDependentClassification()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        fixture.Snapshot.OnRead = (key, token) => fixture.Saves > 0 ? Task.FromException(new IOException("readback unavailable")) : Task.CompletedTask;
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Saves.ShouldBe(1);
        budget.LiveBytes.ShouldBeGreaterThan(0);
        fixture.Snapshot.OnRead = null;
        fixture.Snapshot.Replay.Source.Registry.CapabilityLoss.ObserveViolation();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.IssueAsync(budget));
        fixture.Saves.ShouldBe(1);
        fixture.Snapshot.Durable.Count.ShouldBe(2);
        fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Stops before the next pair read or stage when an individual actual pre-save getter loses authority.</summary>
    [Fact]
    public async Task ActualInitialPairReadLossStopsBeforeLaterGetter()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        int reads = 0;
        fixture.Snapshot.OnRead = async (key, token) =>
        {
            reads++;
            await Task.Yield();
            fixture.Snapshot.Replay.Source.Registry.CapabilityLoss.ObserveViolation();
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.IssueAsync(budget));
        reads.ShouldBe(1);
        fixture.Stages.ShouldBe(0);
        fixture.Saves.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Reconciles exact absent predecessor after an uncertain no-commit acknowledgement without performing a new save.</summary>
    [Fact]
    public async Task ActualPendingNoCommitRecoveryNeverRepeatsSave()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        fixture.SaveMode = "no-commit";
        using var budget = new EventBufferBudget();
        fixture.Snapshot.OnRead = (key, token) => fixture.Saves > 0 ? Task.FromException(new IOException("readback unavailable")) : Task.CompletedTask;
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Saves.ShouldBe(1);
        budget.LiveBytes.ShouldBeGreaterThan(0);
        fixture.Snapshot.OnRead = null;
        fixture.SaveMode = "normal";
        DaprReplayCommitOutcome recovered = await fixture.IssueAsync(budget);
        fixture.Saves.ShouldBe(1);
        fixture.Stages.ShouldBe(2);
        recovered.ShouldBe(DaprReplayCommitOutcome.NoCommit);
        fixture.Snapshot.Durable.ShouldBeEmpty();
        fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Retains uncertain staging aliases and activates the existing ordinary actor cache barrier before later work.</summary>
    [Fact]
    public async Task ActualFailedCacheReleaseHoldsLegacyActorBoundaryUntilRecovery()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        fixture.OnStage = (key, image, token) => Task.FromException(new IOException("stage failed before save"));
        fixture.CacheClear = token => fixture.Stages > 0 ? Task.FromException(new IOException("cache discard unconfirmed")) : Task.CompletedTask;
        await Should.ThrowAsync<IOException>(() => fixture.IssueAsync(budget));
        fixture.Stages.ShouldBe(1);
        fixture.Saves.ShouldBe(0);
        budget.LiveBytes.ShouldBeGreaterThan(0);
        fixture.SdkAliases.ShouldAllBe(x => x.Any(b => b != 0));
        await Should.ThrowAsync<ActorStateRemediationException>(() => fixture.Actor.GetRetainedFloorAsync());
        Field(fixture.Actor, "_stateCacheUnsafe").ShouldBe(true);
        fixture.Saves.ShouldBe(0);
        fixture.Snapshot.Durable.ShouldBeEmpty();
        fixture.CacheClear = null;
        fixture.OnStage = null;
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.NoCommit);
        fixture.Stages.ShouldBe(1);
        fixture.Saves.ShouldBe(0);
        fixture.SdkAliases.ShouldAllBe(x => x.All(b => b == 0));
        budget.LiveBytes.ShouldBe(0);
        (await fixture.Actor.GetRetainedFloorAsync()).ShouldBe(1);
    }

    /// <summary>Composes the actual actor issuer with fresh candidate intake and separate private adoption without a tail or operation advance.</summary>
    [Fact]
    public async Task ActualIssuedPairFeedsOnlyPrivateInitialAdoption()
    {
        await using var fixture = new DaprLogicalAnchorFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        (await fixture.IssueAsync(budget)).ShouldBe(DaprReplayCommitOutcome.Proven);
        budget.LiveBytes.ShouldBe(0);
        fixture.Saves.ShouldBe(1);
        int applies = fixture.Snapshot.Replay.Applies;
        int operationSaves = fixture.Snapshot.Replay.Store.Saves;
        DaprLogicalSourceBinding requested = await fixture.Snapshot.SourceAsync();
        DaprLogicalSnapshotCandidate candidate = (await fixture.Snapshot.Owner.AcquireAsync(requested, false, fixture.Snapshot.AcquireOriginAsync, budget, CancellationToken.None))!;
        using DaprLogicalReplayInitialAnchor anchor = await DaprLogicalReplayInitialAnchor.AdoptAsync(DaprLogicalReplayAnchorCodec.ModelId, candidate, requested, fixture.Snapshot.Replay.Binding, budget, CancellationToken.None);
        byte[] state = new byte[anchor.CanonicalState.Length];
        anchor.CanonicalState.CopyTo(0, state);
        state.ShouldBe("{\"value\":1}"u8.ToArray());
        candidate.StartSequence.ShouldBe(2);
        fixture.Saves.ShouldBe(1);
        fixture.Snapshot.Replay.Store.Saves.ShouldBe(operationSaves);
        fixture.Snapshot.Replay.Applies.ShouldBe(applies);
        anchor.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    private static DaprLogicalReplayAnchorSelection Selection() => new("tenant-a", "orders", "order-1", "r", Hash(1), Hash(2), Hash(3), Hash(4), Hash(5), Hash(6), Hash(7), Hash(8), 1, 3, 3, DaprLogicalReplayAnchorCodec.ModelId);
    private static byte[] Hash(byte marker) => Enumerable.Repeat(marker, 32).ToArray();
    private static object? Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);
    private static byte[] Buffer(object owner, string name)
    {
        object value = Field(owner, name)!;
        return value is byte[] array ? array : (byte[])value.GetType().GetField("_buffer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
    }

    private static byte[] Memory(ReadOnlyMemory<byte> bytes)
    {
        MemoryMarshal.TryGetArray(bytes, out ArraySegment<byte> segment).ShouldBeTrue();
        return segment.Array!;
    }

    private static void AssertDurable(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        actual.Count.ShouldBe(expected.Count);
        foreach ((string key, byte[] value)in expected)
        {
            actual[key].ShouldBe(value);
        }
    }

    private static string FindVectors()
    {
        for (DirectoryInfo? current = new(Environment.CurrentDirectory); current is not null; current = current.Parent)
        {
            string path = Path.Combine(current.FullName, "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-replay-2026-10-09/vectors.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Independent anchor vectors missing");
    }
}
