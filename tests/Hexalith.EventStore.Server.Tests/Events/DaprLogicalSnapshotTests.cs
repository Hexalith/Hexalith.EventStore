using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Exercises distinct candidate framing, actual complete-origin admission and private refusal cleanup.</summary>
public sealed class DaprLogicalSnapshotTests
{
    /// <summary>Verifies eligible tail candidate owns private canonical bytes and fresh complete origin.</summary>
    [Fact]
    public async Task EligibleTailCandidateOwnsPrivateCanonicalBytesAndFreshCompleteOrigin()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        DaprLogicalSourceBinding source = await fixture.SourceAsync();
        int applies = fixture.Replay.Applies;
        int saves = fixture.Replay.Store.Saves;
        using var budget = new EventBufferBudget();
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldNotBeNull();
        candidate.CoveredSequence.ShouldBe(1);
        candidate.StartSequence.ShouldBe(2);
        candidate.TargetSequence.ShouldBe(3);
        candidate.HasZeroTail.ShouldBeFalse();
        byte[] state = new byte[candidate.CanonicalState.Length];
        candidate.CanonicalState.CopyTo(0, state);
        state.ShouldBe("{\"value\":1}"u8.ToArray());
        budget.LiveBytes.ShouldBeGreaterThan(0);
        object decoding = typeof(DaprLogicalSnapshotCandidate).GetField("_decoding", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(candidate)!;
        ((int)typeof(EventBufferReservation).GetField("_capacity", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(decoding)!).ShouldBe(4 * fixture.Durable[fixture.Owner.WitnessKey].Length + 4096);
        await candidate.RequireCurrentAsync(CancellationToken.None);
        fixture.Replay.Applies.ShouldBe(applies);
        fixture.Replay.Store.Saves.ShouldBe(saves);
        _ = fixture.Replay.Source.SourceState.DidNotReceive().RemoveStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ = fixture.Replay.Source.SourceState.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
        _ = fixture.Replay.Source.SourceState.DidNotReceive().SetStateAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        byte[] owned = Buffer((object)candidate.CanonicalState, "_owner");
        byte[] sourcePin = Buffer(candidate, "_sourcePin");
        byte[] witnessPin = Buffer(candidate, "_witnessPin");
        byte[][] stored = fixture.Durable.Values.Select(x => x.ToArray()).ToArray();
        candidate.Dispose();
        owned.ShouldAllBe(x => x == 0);
        sourcePin.ShouldAllBe(x => x == 0);
        witnessPin.ShouldAllBe(x => x == 0);
        budget.LiveBytes.ShouldBe(0);
        typeof(DaprLogicalSnapshotCandidate).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(candidate).ShouldBeNull();
        typeof(DaprLogicalSnapshotCandidate).GetField("_origin", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(candidate).ShouldBeNull();
        fixture.Durable.Values.Select(x => Convert.ToHexString(x)).ShouldBe(stored.Select(x => Convert.ToHexString(x)));
        fixture.Borrowed.ShouldAllBe(x => x.Any(b => b != 0));
        Should.Throw<ObjectDisposedException>(() => candidate.CanonicalState.CopyTo(0, []));
    }

    /// <summary>Verifies below head zero tail above target and timeline restart at one.</summary>
    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(3, 2, false)]
    [InlineData(1, 3, true)]
    public async Task BelowHeadZeroTailAboveTargetAndTimelineRestartAtOne(long covered, long target, bool timeline)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync(covered);
        DaprLogicalSourceBinding source = await fixture.SourceAsync(target);
        using var budget = new EventBufferBudget();
        int reads = fixture.Replay.Reads;
        int applies = fixture.Replay.Applies;
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(source, timeline, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldBeNull();
        fixture.OriginCalls.ShouldBe(0);
        fixture.Replay.Reads.ShouldBe(reads);
        fixture.Replay.Applies.ShouldBe(applies);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies exact head candidate proposes zero tail without apply.</summary>
    [Fact]
    public async Task ExactHeadCandidateProposesZeroTailWithoutApply()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync(3);
        using var budget = new EventBufferBudget();
        DaprLogicalSourceBinding source = await fixture.SourceAsync();
        int calls = fixture.Replay.Applies;
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldNotBeNull();
        candidate.HasZeroTail.ShouldBeTrue();
        candidate.StartSequence.ShouldBe(4);
        candidate.CoveredSequence.ShouldBe(3);
        fixture.Replay.Applies.ShouldBe(calls);
    }

    /// <summary>Verifies invalid pair discards entire candidate and preserves stored bytes.</summary>
    [Theory]
    [InlineData("storage")]
    [InlineData("witness")]
    [InlineData("malformed")]
    [InlineData("checkpoint")]
    [InlineData("rebase")]
    [InlineData("protected")]
    [InlineData("wrong-key")]
    [InlineData("wrong-source")]
    [InlineData("wrong-binding")]
    [InlineData("wrong-operation")]
    [InlineData("null-origin")]
    public async Task InvalidPairDiscardsEntireCandidateAndPreservesStoredBytes(string corruption)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        DaprLogicalSnapshotWitness fields = DaprLogicalSnapshotCodec.DecodeSnapshot(fixture.Durable[fixture.Owner.WitnessKey]);
        switch (corruption)
        {
            case "storage":
                fixture.Durable.Remove(fixture.Owner.StorageKey);
                break;
            case "witness":
                fixture.Durable.Remove(fixture.Owner.WitnessKey);
                break;
            case "malformed":
                fixture.Durable[fixture.Owner.WitnessKey] = [1, 2, 3];
                break;
            case "checkpoint":
                fixture.Durable[fixture.Owner.WitnessKey] = Vector("checkpoint");
                break;
            case "rebase":
                fixture.Durable[fixture.Owner.WitnessKey] = Vector("rebase");
                break;
            case "protected":
                fixture.Durable[fixture.Owner.StorageKey][0] ^= 1;
                break;
            case "wrong-key":
                fixture.Durable[fixture.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(fields with { WitnessKey = "wrong:key" });
                break;
            case "wrong-source":
                fixture.Durable[fixture.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(fields with { SourceBindingHash = new byte[32] });
                break;
            case "wrong-binding":
                fixture.Durable[fixture.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(fields with { ReconstructionBindingHash = new byte[32] });
                break;
            case "wrong-operation":
                fixture.Durable[fixture.Owner.WitnessKey] = DaprLogicalSnapshotCodec.EncodeSnapshot(fields with { OperationId = "other" });
                break;
            case "null-origin":
                fixture.NullOrigin = true;
                break;
        }

        string[] durable = fixture.Durable.Values.Select(Convert.ToHexString).ToArray();
        using var budget = new EventBufferBudget();
        int reads = fixture.Replay.Reads;
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(await fixture.SourceAsync(), false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldBeNull();
        fixture.Replay.Reads.ShouldBe(reads);
        if (corruption is not ("wrong-operation" or "null-origin"))
        {
            fixture.OriginCalls.ShouldBe(0);
        }

        budget.LiveBytes.ShouldBe(0);
        fixture.Durable.Values.Select(Convert.ToHexString).ShouldBe(durable);
    }

    /// <summary>Verifies noncanonical roundtrip falls back without apply or save.</summary>
    [Fact]
    public async Task NoncanonicalRoundtripFallsBackWithoutApplyOrSave()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        fixture.Replay.Noncanonical = true;
        using var budget = new EventBufferBudget();
        int applies = fixture.Replay.Applies;
        int saves = fixture.Replay.Store.Saves;
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(await fixture.SourceAsync(), false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldBeNull();
        fixture.Replay.Applies.ShouldBe(applies);
        fixture.Replay.Store.Saves.ShouldBe(saves);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies application failure propagates instead of claiming absent snapshot.</summary>
    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("json")]
    public async Task ApplicationFailurePropagatesInsteadOfClaimingAbsentSnapshot(string point)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        fixture.Replay.Hook = (callback, _) =>
        {
            if (point == "json")
            {
                throw new JsonException("application JSON sentinel");
            }

            if (callback == point)
            {
                throw new IOException("application sentinel");
            }
        };
        if (point == "json")
        {
            await Should.ThrowAsync<JsonException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, CancellationToken.None));
        }
        else
        {
            await Should.ThrowAsync<IOException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, CancellationToken.None));
        }

        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies complete flag cannot substitute for full history.</summary>
    [Theory]
    [InlineData("ledger")]
    [InlineData("response")]
    [InlineData("state")]
    [InlineData("final")]
    [InlineData("next")]
    public async Task CompleteFlagCannotSubstituteForFullHistory(string participant)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        string key = participant switch
        {
            "ledger" => "logical-replay:ledger:1",
            "response" => "logical-replay:response:1",
            "state" => "logical-replay:state:0",
            "final" => "logical-replay:final-state:v1",
            _ => "logical-replay:state:2"
        };
        if (participant == "next")
        {
            fixture.Replay.Store.Put(key, "{}"u8.ToArray());
        }
        else
        {
            fixture.Replay.Store.Durable.Remove(key);
        }

        using var budget = new EventBufferBudget();
        int reads = fixture.Replay.Reads;
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(await fixture.SourceAsync(), false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldBeNull();
        fixture.Replay.Reads.ShouldBe(reads);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies yielding final read substitution withholds candidate.</summary>
    [Theory]
    [InlineData("pair")]
    [InlineData("origin")]
    public async Task YieldingFinalReadSubstitutionWithholdsCandidate(string changed)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        int stopped = 0;
        fixture.OnRead = async (key, _) =>
        {
            await Task.Yield();
            if (fixture.SnapshotReads == 3 && key == fixture.Owner.StorageKey)
            {
                stopped++;
                if (changed == "pair")
                {
                    fixture.Durable[fixture.Owner.WitnessKey][^1] ^= 1;
                }
                else
                {
                    fixture.Replay.Store.Durable.Remove("logical-replay:ledger:1");
                }
            }

            fixture.Replay.Borrowed.ForEach(view => Should.Throw<ObjectDisposedException>(() => view.CopyTo(0, [])));
            fixture.Replay.BorrowedWriters.ForEach(writer => Should.Throw<ObjectDisposedException>(() => writer.Complete()));
            budget.LiveBytes.ShouldBeGreaterThan(0);
        };
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(await fixture.SourceAsync(), false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        stopped.ShouldBe(1);
        candidate.ShouldBeNull();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies post decision private origin substitution withholds candidate.</summary>
    [Fact]
    public async Task PostDecisionPrivateOriginSubstitutionWithholdsCandidate()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        byte[]? captured = null;
        fixture.OnOrigin = origin => captured = MemoryMarshal.TryGetArray(origin.State, out ArraySegment<byte> segment) ? segment.Array : throw new InvalidOperationException();
        fixture.AfterDecision = () => captured![0] ^= 1;
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, CancellationToken.None));
        captured.ShouldNotBeNull();
        captured.ShouldAllBe(x => x == 0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies serialized owner must complete exactly one original decision.</summary>
    [Theory]
    [InlineData("skip")]
    [InlineData("repeat")]
    [InlineData("foreign")]
    [InlineData("early")]
    public async Task SerializedOwnerMustCompleteExactlyOneOriginalDecision(string mode)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        fixture.FenceMode = mode;
        if (mode == "early")
        {
            fixture.OnRead = async (_, _) => await Task.Delay(1);
        }

        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, CancellationToken.None));
        fixture.Replay.Applies.ShouldBe(1);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies callback cancellation and loss refuse before later state callbacks.</summary>
    [Theory]
    [InlineData("read", "cancel")]
    [InlineData("write", "cancel")]
    [InlineData("read", "throw")]
    [InlineData("write", "throw")]
    [InlineData("read", "foreign")]
    [InlineData("write", "foreign")]
    [InlineData("read", "loss")]
    [InlineData("write", "loss")]
    public async Task CallbackCancellationAndLossRefuseBeforeLaterStateCallbacks(string point, string mode)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        int stopped = 0;
        int later = 0;
        fixture.Replay.Hook = (callback, token) =>
        {
            if (stopped != 0)
            {
                later++;
            }

            if (callback == point)
            {
                stopped++;
                if (mode == "loss")
                {
                    fixture.Replay.Source.Registry.CapabilityLoss.ObserveViolation();
                }
                else
                {
                    cancellation.Cancel();
                }

                if (mode == "throw")
                {
                    throw new IOException("cancel sentinel");
                }

                if (mode == "foreign")
                {
                    throw new OperationCanceledException(new CancellationToken(true));
                }
            }
        };
        DaprLogicalSourceBinding source = fixture.OriginBinding with
        {
            TargetSequence = 3
        };
        if (mode == "loss")
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, cancellation.Token));
        }
        else
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.Owner.AcquireAsync(source, false, fixture.AcquireOriginAsync, budget, cancellation.Token));
            error.CancellationToken.ShouldBe(cancellation.Token);
        }

        stopped.ShouldBe(1);
        later.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
        fixture.Replay.Applies.ShouldBe(1);
    }

    /// <summary>Verifies infrastructure read failure propagates without candidate or mutation.</summary>
    [Fact]
    public async Task InfrastructureReadFailurePropagatesWithoutCandidateOrMutation()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        fixture.OnRead = (_, _) => throw new IOException("source infrastructure unavailable");
        using var budget = new EventBufferBudget();
        await Should.ThrowAsync<IOException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, CancellationToken.None));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies tight combined capacity refuses before candidate reads.</summary>
    [Theory]
    [InlineData(64 * 1024 * 1024)]
    [InlineData(32)]
    public async Task TightCombinedCapacityRefusesBeforeCandidateReads(int maximumState)
    {
        await using var fixture = new DaprLogicalSnapshotFixture(maximumState);
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget(maximumState == 32 ? 128 * 1024 : 128 * 1024 * 1024);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, CancellationToken.None));
        fixture.SnapshotReads.ShouldBe(0);
        fixture.OriginCalls.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies retained candidate rechecks actual pair and history.</summary>
    [Fact]
    public async Task RetainedCandidateRechecksActualPairAndHistory()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(await fixture.SourceAsync(), false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldNotBeNull();
        fixture.Durable.Remove(fixture.Owner.WitnessKey);
        await Should.ThrowAsync<InvalidOperationException>(() => candidate.RequireCurrentAsync(CancellationToken.None));
        candidate.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies model and actual manager selection are explicit.</summary>
    [Theory]
    [InlineData("model")]
    [InlineData("manager")]
    public async Task ModelAndActualManagerSelectionAreExplicit(string wrong)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        int forbiddenReads = 0;
        IActorStateManager foreign = Substitute.For<IActorStateManager>();
        _ = foreign.TryGetStateAsync<byte[]>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            forbiddenReads++;
            return new ConditionalValue<byte[]>(true, fixture.Durable[call.ArgAt<string>(0)].ToArray());
        });
        using var budget = new EventBufferBudget();
        DaprLogicalSnapshotCandidate? candidate = null;
        try
        {
            var owner = new DaprLogicalSnapshotOwner(wrong == "model" ? DaprLogicalSourceBinding.ModelId : DaprLogicalSnapshotCodec.SnapshotModel, wrong == "manager" ? foreign : fixture.Replay.Source.SourceState, DaprLogicalReplayFixture.Identity, fixture.Replay.Source.Source, fixture.Replay.Source.Trust, fixture.Replay.Binding, 32, (decision, token) => decision(token));
            candidate = await owner.AcquireAsync(await fixture.SourceAsync(), false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        }
        catch (ArgumentException)
        {
        }

        try
        {
            candidate.ShouldBeNull();
            forbiddenReads.ShouldBe(0);
            fixture.OriginCalls.ShouldBe(0);
        }
        finally
        {
            candidate?.Dispose();
        }

        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies different origin budget refuses before canonical callbacks.</summary>
    [Fact]
    public async Task DifferentOriginBudgetRefusesBeforeCanonicalCallbacks()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        using var foreign = new EventBufferBudget();
        int reads = fixture.Replay.Reads;
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(await fixture.SourceAsync(), false, (sequence, _, token) => fixture.AcquireOriginAsync(sequence, foreign, token), budget, CancellationToken.None);
        candidate.ShouldBeNull();
        fixture.Replay.Reads.ShouldBe(reads);
        budget.LiveBytes.ShouldBe(0);
        foreign.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies origin disposal clears state pins and drops owned references.</summary>
    [Fact]
    public async Task OriginDisposalClearsStatePinsAndDropsOwnedReferences()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var budget = new EventBufferBudget();
        DaprLogicalReplayAnchorOrigin origin = await fixture.AcquireOriginAsync(1, budget, CancellationToken.None);
        MemoryMarshal.TryGetArray(origin.State, out ArraySegment<byte> state).ShouldBeTrue();
        DaprReplayOperationRecord record = (DaprReplayOperationRecord)typeof(DaprLogicalReplayAnchorOrigin).GetField("_record", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(origin)!;
        byte[] pin = record.CanonicalStateHash!;
        budget.LiveBytes.ShouldBeGreaterThan(state.Count + 4096);
        origin.Dispose();
        state.Array.ShouldNotBeNull();
        state.Array.ShouldAllBe(x => x == 0);
        pin.ShouldAllBe(x => x == 0);
        typeof(DaprLogicalReplayAnchorOrigin).GetField("_record", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(origin).ShouldBeNull();
        typeof(DaprLogicalReplayAnchorOrigin).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(origin).ShouldBeNull();
        typeof(DaprLogicalReplayAnchorOrigin).GetField("_fence", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(origin).ShouldBeNull();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies origin factory cancellation wins throw and prevents state callbacks.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginFactoryCancellationWinsThrowAndPreventsStateCallbacks(bool foreign)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        int reads = fixture.Replay.Reads;
        fixture.OnOrigin = _ =>
        {
            cancellation.Cancel();
            if (foreign)
            {
                throw new OperationCanceledException(new CancellationToken(true));
            }

            throw new IOException("factory cancel sentinel");
        };
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        fixture.Replay.Reads.ShouldBe(reads);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses cancellation at the private pair-return boundary without leaking either copied owner.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAtPrivatePairReturnRestoresChargesAndPreservesActorBytes(bool foreign)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        int observed = 0;
        fixture.OnCacheClear = async _ =>
        {
            await Task.Yield();
            if (fixture.SnapshotReads == 2)
            {
                observed++;
                budget.LiveBytes.ShouldBeGreaterThan(0);
                cancellation.Cancel();
                if (foreign)
                {
                    throw new OperationCanceledException(new CancellationToken(true));
                }
            }
        };
        string[] durable = fixture.Durable.Values.Select(Convert.ToHexString).ToArray();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        observed.ShouldBe(1);
        fixture.OriginCalls.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
        fixture.Durable.Values.Select(Convert.ToHexString).ShouldBe(durable);
        fixture.Borrowed.Select(Convert.ToHexString).ShouldBe(durable);
    }

    /// <summary>Checks final owner-return cancellation clears private candidates and preserves actor bytes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAtFinalOwnerReturnWithholdsAndClearsCandidate(bool foreign)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        byte[]? originBytes = null;
        byte[]? canonicalBytes = null;
        fixture.OnOrigin = origin =>
        {
            MemoryMarshal.TryGetArray(origin.State, out ArraySegment<byte> array).ShouldBeTrue();
            originBytes = array.Array;
        };
        fixture.Replay.Hook = (point, _) =>
        {
            if (point == "read" && canonicalBytes is null)
            {
                object view = fixture.Replay.Borrowed[^1];
                FieldInfo payload = view.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(field => typeof(Hexalith.EventStore.Contracts.Events.IReadOnlyPayload).IsAssignableFrom(field.FieldType));
                canonicalBytes = Buffer(payload.GetValue(view)!, "_owner");
            }
        };
        fixture.AfterDecision = () =>
        {
            cancellation.Cancel();
            if (foreign)
            {
                throw new OperationCanceledException(new CancellationToken(true));
            }
        };
        string[] durable = fixture.Durable.Values.Select(Convert.ToHexString).ToArray();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        originBytes.ShouldNotBeNull();
        canonicalBytes.ShouldNotBeNull();
        originBytes.ShouldAllBe(x => x == 0);
        canonicalBytes.ShouldAllBe(x => x == 0);
        budget.LiveBytes.ShouldBe(0);
        fixture.Durable.Values.Select(Convert.ToHexString).ShouldBe(durable);
    }

    /// <summary>Refuses originating cancellation at the final actual source fence with owned origin capacity live.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAtCompletedOriginFinalFenceRestoresCharge(bool foreign)
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        int observed = 0;
        _ = fixture.Replay.Source.SourceState.TryGetStateAsync<AggregateMetadata>(DaprLogicalReplayFixture.Identity.MetadataKey, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Yield();
            if (budget.LiveBytes >= 4096)
            {
                observed++;
                // Two charged observations precede capture; the third is the constructed origin's final actual fence.
                if (observed == 3)
                {
                    cancellation.Cancel();
                    if (foreign)
                    {
                        throw new OperationCanceledException(new CancellationToken(true));
                    }
                }
            }

            return new ConditionalValue<AggregateMetadata>(true, fixture.Replay.Source.Metadata!);
        });
        string[] durable = fixture.Replay.Store.Durable.Values.Select(Convert.ToHexString).ToArray();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => fixture.Replay.Owner.CaptureCompletedAnchorOriginAsync(fixture.Replay.Source.Source, fixture.OriginBinding, fixture.Replay.Source.Trust, budget, cancellation.Token));
        error.CancellationToken.ShouldBe(cancellation.Token);
        observed.ShouldBe(3);
        budget.LiveBytes.ShouldBe(0);
        fixture.Replay.Store.Durable.Values.Select(Convert.ToHexString).ShouldBe(durable);
    }

    /// <summary>Retains original cancellation precedence after a privately admitted candidate is disposed.</summary>
    [Fact]
    public async Task OriginalCancellationPrecedesDisposedCandidateAndForeignToken()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        await fixture.PrepareAsync();
        using var cancellation = new CancellationTokenSource();
        using var budget = new EventBufferBudget();
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(fixture.OriginBinding with { TargetSequence = 3 }, false, fixture.AcquireOriginAsync, budget, cancellation.Token);
        candidate.ShouldNotBeNull();
        candidate.Dispose();
        cancellation.Cancel();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => candidate.RequireCurrentAsync(new CancellationToken(true)));
        error.CancellationToken.ShouldBe(cancellation.Token);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Leaves even malformed legacy snapshot storage intact while refusing unsupported legacy authority.</summary>
    [Fact]
    public async Task LegacyMalformedSnapshotRemainsIntactAndSuppliesNoCandidate()
    {
        await using var fixture = new DaprLogicalSnapshotFixture();
        byte[] legacy = "malformed snapshot"u8.ToArray();
        fixture.Durable[DaprLogicalReplayFixture.Identity.SnapshotKey] = legacy;
        using var budget = new EventBufferBudget();
        using DaprLogicalSnapshotCandidate? candidate = await fixture.Owner.AcquireAsync(await fixture.SourceAsync(), false, fixture.AcquireOriginAsync, budget, CancellationToken.None);
        candidate.ShouldBeNull();
        fixture.OriginCalls.ShouldBe(0);
        fixture.Durable[DaprLogicalReplayFixture.Identity.SnapshotKey].ShouldBe(legacy);
        _ = fixture.Replay.Source.SourceState.DidNotReceive().RemoveStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies independent vectors decode and encode exactly including max and distinct schemas.</summary>
    [Fact]
    public void IndependentVectorsDecodeAndEncodeExactlyIncludingMaxAndDistinctSchemas()
    {
        foreach (string name in new[]
        {
            "snapshot",
            "snapshot-max",
            "snapshot-folded-change",
            "snapshot-generation"
        }

        )
        {
            byte[] bytes = Vector(name);
            DaprLogicalSnapshotWitness fields = DaprLogicalSnapshotCodec.DecodeSnapshot(bytes);
            DaprLogicalSnapshotCodec.EncodeSnapshot(fields).ShouldBe(bytes);
            DaprLogicalSnapshotCodec.MeasureSnapshot(fields).ShouldBe(bytes.Length);
            using var writer = new EventEvolutionBinaryWriter(bytes.Length);
            writer.WriteRaw(bytes);
            writer.Length.ShouldBe(bytes.Length);
            using var shortWriter = new EventEvolutionBinaryWriter(bytes.Length - 1);
            Should.Throw<InvalidOperationException>(() => shortWriter.WriteRaw(bytes));
        }

        DaprLogicalSnapshotCodec.TailStart(DaprLogicalSnapshotCodec.DecodeSnapshot(Vector("snapshot-max")).CoveredSequence).ShouldBe(long.MaxValue);
        DaprLogicalSnapshotCodec.TailStart(7).ShouldBe(8);
        Should.Throw<ArgumentOutOfRangeException>(() => DaprLogicalSnapshotCodec.TailStart(0));
        byte[] checkpoint = Vector("checkpoint");
        DaprLogicalSnapshotCodec.EncodeCheckpoint(DaprLogicalSnapshotCodec.DecodeCheckpoint(checkpoint)).ShouldBe(checkpoint);
        byte[] rebase = Vector("rebase");
        DaprLogicalSnapshotCodec.EncodeRebase(DaprLogicalSnapshotCodec.DecodeRebase(rebase)).ShouldBe(rebase);
        Should.Throw<ArgumentException>(() => DaprLogicalSnapshotCodec.DecodeSnapshot(checkpoint));
        Should.Throw<ArgumentException>(() => DaprLogicalSnapshotCodec.DecodeSnapshot(rebase));
        Should.Throw<ArgumentException>(() => DaprLogicalClaimCodec.DecodePrefix(Vector("snapshot")));
    }

    /// <summary>Bounds exact UTF-8 witness text before writer allocation at its encoded boundary.</summary>
    [Fact]
    public void EncodedTextBoundaryUsesUtf8Bytes()
    {
        DaprLogicalSnapshotWitness original = DaprLogicalSnapshotCodec.DecodeSnapshot(Vector("snapshot"));
        DaprLogicalSnapshotWitness boundary = original with
        {
            SerializerId = new string ('a', 512)
        };
        byte[] image = DaprLogicalSnapshotCodec.EncodeSnapshot(boundary);
        DaprLogicalSnapshotCodec.MeasureSnapshot(boundary).ShouldBe(image.Length);
        DaprLogicalSnapshotCodec.DecodeSnapshot(image).SerializerId.ShouldBe(boundary.SerializerId);
        Should.Throw<ArgumentException>(() => DaprLogicalSnapshotCodec.EncodeSnapshot(original with { SerializerId = new string ('a', 513) }));
        Should.Throw<ArgumentException>(() => DaprLogicalSnapshotCodec.EncodeSnapshot(original with { SerializerId = new string ('é', 257) }));
    }

    /// <summary>Verifies malformed snapshot record cannot reach authority.</summary>
    [Theory]
    [InlineData("trailing")]
    [InlineData("truncated")]
    [InlineData("tag")]
    [InlineData("version")]
    [InlineData("negative")]
    [InlineData("length")]
    public void MalformedSnapshotRecordCannotReachAuthority(string mutation)
    {
        byte[] bytes = Vector("snapshot");
        if (mutation == "trailing")
        {
            bytes = [..bytes, 0];
        }

        if (mutation == "truncated")
        {
            bytes = bytes[..^1];
        }

        int offset = "HX-EV-DAPR-SNAPSHOT-WITNESS-1\0"u8.Length;
        if (mutation == "version")
        {
            bytes[offset] = 2;
        }

        if (mutation == "tag")
        {
            bytes[offset + 3] = 2;
        }

        if (mutation == "length")
        {
            bytes.AsSpan(offset + 4, 4).Fill(255);
        }

        if (mutation == "negative")
        {
            var reader = new EventEvolutionBinaryReader(bytes);
            reader.ReadRaw(offset + 3);
            for (int i = 0; i < 4; i++)
            {
                reader.ReadByte();
                reader.ReadString(512);
            }

            reader.ReadByte();
            reader.ReadHash();
            reader.ReadByte();
            bytes[reader.Position] = 128;
        }

        Should.Throw<ArgumentException>(() => DaprLogicalSnapshotCodec.DecodeSnapshot(bytes));
    }

    private static byte[] Buffer(object owner, string field) => (byte[])owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static byte[] Vector(string name)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }

        using JsonDocument values = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root!.FullName, "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-snapshot-2026-10-09/vectors.json")));
        return Convert.FromHexString(values.RootElement.GetProperty(name).GetProperty("hex").GetString()!);
    }
}
