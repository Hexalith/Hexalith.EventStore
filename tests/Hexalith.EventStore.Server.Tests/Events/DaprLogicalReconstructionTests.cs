using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Dapr.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public sealed class DaprLogicalReconstructionTests
{
    private const string Operation = "logical-replay:operation:v1";
    private const string FinalState = "logical-replay:final-state:v1";

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task MixedHistoryNontrivialUpcastEqualsIndependentCanonicalBaseline(int pageSize)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(mixedHistory: true);
        byte[][] original = fixture.Source.Events.Values.Select(value => value.Payload.ToArray()).ToArray();
        string[] metadata = fixture.Source.Events.Values.Select(value => System.Text.Json.JsonSerializer.Serialize(value)).ToArray();
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        AggregateReconstructionResult result = await DaprAggregateStateReconstructor.ReconstructAddressedAsync(DaprLogicalReplayFixture.Identity,
            "r", fixture.Source.Source, binding, fixture.Source.Trust, fixture.Source.Key, fixture.Owner, "owner", "request", pageSize, CancellationToken.None);
        // V1 units 1 -> delta 13, current V2 delta 14, V1 units 3 -> delta 33.
        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.StateJson.ShouldBe("{\"value\":60}");
        fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":60}"u8.ToArray());
        fixture.Store.Get<DaprReplayOperationRecord>(Operation).CanonicalStateHash.ShouldBe(SHA256.HashData("{\"value\":60}"u8));
        fixture.Applies.ShouldBe(3);
        fixture.Source.DeserializationCalls.ShouldBe(3);
        for (int index = 0; index < original.Length; index++)
        {
            fixture.Source.Events[index + 1].Payload.ShouldBe(original[index]);
            System.Text.Json.JsonSerializer.Serialize(fixture.Source.Events[index + 1]).ShouldBe(metadata[index]);
        }
    }

    [Fact]
    public async Task MultiPageCanonicalStateAndExactRetriesShareCommittedAuthority()
    {
        await using var fixture = new DaprLogicalReconstructionFixture();
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None);
        using var first = await Execute(fixture, binding, 1, 2);
        State(first).ShouldBe("{\"value\":2}");
        first.IsComplete.ShouldBeFalse();
        fixture.Store.Get<byte[]>("logical-replay:state:1").ShouldBe("{\"value\":2}"u8.ToArray());
        using var final = await Execute(fixture, binding, 2, 2);
        State(final).ShouldBe("{\"value\":3}");
        final.IsComplete.ShouldBeTrue();
        fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":3}"u8.ToArray());
        int calls = fixture.Applies;
        using var retryFirst = await Execute(fixture, binding, 1, 2);
        using var retryFinal = await Execute(fixture, binding, 2, 2);
        State(retryFirst).ShouldBe("{\"value\":2}");
        State(retryFinal).ShouldBe("{\"value\":3}");
        fixture.Applies.ShouldBe(calls);
        fixture.Store.Get<DaprReplayOperationRecord>(Operation).CanonicalStateHash.ShouldBe(SHA256.HashData("{\"value\":3}"u8));
    }

    [Fact]
    public async Task StateCallbackBorrowsExpireBeforeAsyncFenceAndLaterApplyOrWrite()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1);
        int fenceChecks = 0;
        int applyChecks = 0;
        int writeChecks = 0;
        fixture.Hook = (point, _) =>
        {
            if (point is "apply" or "write")
            {
                fixture.Borrowed.ForEach(payload => Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[1])));
                if (point == "apply")
                {
                    applyChecks++;
                }
                else if (fixture.Reads > 0)
                {
                    writeChecks++;
                }
            }
        };
        _ = fixture.Source.SourceState.TryGetStateAsync<AggregateMetadata>(DaprLogicalReplayFixture.Identity.MetadataKey,
            Arg.Any<CancellationToken>()).Returns(async call =>
            {
                await Task.Yield();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                fixture.Borrowed.ForEach(payload => Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[1])));
                fixture.BorrowedWriters.ForEach(writer => Should.Throw<ObjectDisposedException>(() => writer.Complete()));
                if (fixture.BorrowedWriters.Count > 0)
                {
                    fenceChecks++;
                }

                return new ConditionalValue<AggregateMetadata>(true, fixture.Source.Metadata!);
            });
        DaprLogicalSourceBinding binding = await Begin(fixture);
        using var result = await Execute(fixture, binding, 1, 1);
        State(result).ShouldBe("{\"value\":1}");
        fenceChecks.ShouldBeGreaterThan(0);
        applyChecks.ShouldBe(1);
        writeChecks.ShouldBeGreaterThan(0);
        fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":1}"u8.ToArray());
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 0)]
    [InlineData(3, 2, 2)]
    [InlineData(3, 3, 3)]
    public async Task AddressedReconstructorReturnsOnlyExactFinalState(int events, long target, int value)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(events);
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", target, CancellationToken.None);
        AggregateReconstructionResult result = await DaprAggregateStateReconstructor.ReconstructAddressedAsync(DaprLogicalReplayFixture.Identity,
            "r", fixture.Source.Source, binding, fixture.Source.Trust, fixture.Source.Key, fixture.Owner, "owner", "request", 1, CancellationToken.None);
        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.LastAppliedSequenceNumber.ShouldBe(target);
        result.StateJson.ShouldBe("{\"value\":" + value + "}");
        fixture.Applies.ShouldBe(value);
        fixture.Store.Get<byte[]>(FinalState).ShouldBe(Encoding.UTF8.GetBytes(result.StateJson!));
        AggregateReconstructionResult retry = await DaprAggregateStateReconstructor.ReconstructAddressedAsync(DaprLogicalReplayFixture.Identity,
            "r", fixture.Source.Source, binding, fixture.Source.Trust, fixture.Source.Key, fixture.Owner, "owner", "request", 1, CancellationToken.None);
        retry.ShouldBe(result);
        fixture.Applies.ShouldBe(value);
    }

    [Fact]
    public async Task MutatingApplyFailureRetainsPrivateWithinPageLastGoodWithoutAnyTransition()
    {
        await using var fixture = new DaprLogicalReconstructionFixture
        {
            ThrowOnApply = 2
        };
        DaprLogicalSourceBinding binding = await Begin(fixture);
        Dictionary<string, byte[]> prior = fixture.Store.Durable.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        await Should.ThrowAsync<InvalidOperationException>(() => Execute(fixture, binding, 1, 3));
        fixture.Applies.ShouldBe(2);
        fixture.Owner.LastGoodDiagnosticSequence.ShouldBe(1);
        using DaprLogicalResponseOwner diagnostic = fixture.Owner.CaptureLastGoodDiagnostic(new EventBufferBudget())!;
        Encoding.UTF8.GetString(diagnostic.Bytes.Span).ShouldBe("{\"value\":1}");
        fixture.Store.Durable.Keys.Order().ShouldBe(prior.Keys.Order());
        foreach ((string key, byte[] bytes) in prior)
        {
            fixture.Store.Durable[key].ShouldBe(bytes);
        }

        fixture.Borrowed.ForEach(payload => Should.Throw<ObjectDisposedException>(() => payload.CopyTo(0, new byte[1])));
    }

    [Theory]
    [InlineData("commit-throw", "Proven")]
    [InlineData("no-commit", "NoCommit")]
    [InlineData("partial", "Indeterminate")]
    public async Task SaveClassificationAdmitsAllCanonicalParticipants(string mode, string expected)
    {
        DaprReplayCommitOutcome outcome = Enum.Parse<DaprReplayCommitOutcome>(expected);
        await using var fixture = new DaprLogicalReconstructionFixture(1);
        DaprLogicalSourceBinding binding = await Begin(fixture);
        fixture.Store.SaveMode = mode;
        using var result = await Execute(fixture, binding, 1, 1);
        result.Outcome.ShouldBe(outcome);
        if (outcome == DaprReplayCommitOutcome.Proven)
        {
            State(result).ShouldBe("{\"value\":1}");
            fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":1}"u8.ToArray());
            using var retry = await Execute(fixture, binding, 1, 1);
            State(retry).ShouldBe(State(result));
            fixture.Applies.ShouldBe(1);
        }
        else
        {
            result.CanonicalState.ShouldBeNull();
            fixture.Store.Durable.ContainsKey(FinalState).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("source")]
    [InlineData("trust")]
    [InlineData("loss")]
    public async Task PostSaveLossPreservesProvenCanonicalTruthAndWithholdsResponse(string loss)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1);
        using var cancellation = new CancellationTokenSource();
        DaprLogicalSourceBinding binding = await Begin(fixture);
        fixture.Store.AfterSave = () => Lose(fixture, cancellation, loss);
        using var result = await Execute(fixture, binding, 1, 1, cancellation.Token);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        result.CanonicalState.ShouldBeNull();
        result.Response.ShouldBeNull();
        result.OriginatingCancellationObserved.ShouldBe(loss == "cancel");
        result.ResponseUnavailable.ShouldBe(loss != "cancel");
        fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":1}"u8.ToArray());
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("source")]
    [InlineData("trust")]
    [InlineData("loss")]
    public async Task BeginPostSaveLossPreservesExactInitialParticipants(string loss)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1);
        using var cancellation = new CancellationTokenSource();
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, cancellation.Token);
        fixture.Store.AfterSave = () => Lose(fixture, cancellation, loss);
        using var result = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, cancellation.Token);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        result.OriginatingCancellationObserved.ShouldBe(loss == "cancel");
        result.ResponseUnavailable.ShouldBe(loss != "cancel");
        fixture.Store.Get<byte[]>("logical-replay:state:0").ShouldBe("{\"value\":0}"u8.ToArray());
        fixture.Applies.ShouldBe(0);
    }

    [Theory]
    [InlineData("read", "cancel")]
    [InlineData("read", "source")]
    [InlineData("read", "trust")]
    [InlineData("apply", "cancel")]
    [InlineData("apply", "source")]
    [InlineData("apply", "trust")]
    [InlineData("apply", "loss")]
    [InlineData("write", "source")]
    public async Task CallbackLossStopsBeforeAnyLaterCallbackOrTransition(string boundary, string loss)
    {
        await using var fixture = new DaprLogicalReconstructionFixture();
        using var cancellation = new CancellationTokenSource();
        DaprLogicalSourceBinding binding = await Begin(fixture);
        bool lost = false;
        int callsAtLoss = 0;
        fixture.Hook = (point, token) =>
        {
            token.ShouldBe(cancellation.Token);
            if (!lost && point == boundary)
            {
                lost = true;
                callsAtLoss = fixture.Tokens.Count;
                Lose(fixture, cancellation, loss);
            }
        };
        if (loss == "cancel")
        {
            await Should.ThrowAsync<OperationCanceledException>(() => Execute(fixture, binding, 1, 3, cancellation.Token));
        }
        else
        {
            await Should.ThrowAsync<Exception>(() => Execute(fixture, binding, 1, 3, cancellation.Token));
        }

        lost.ShouldBeTrue();
        fixture.Tokens.Count.ShouldBe(callsAtLoss);
        fixture.Store.Saves.ShouldBe(1);
        fixture.Store.Durable.ContainsKey(FinalState).ShouldBeFalse();
        fixture.Store.Get<byte[]>("logical-replay:state:0").ShouldBe("{\"value\":0}"u8.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidCanonicalCodecOutputRefusesBeforeAnySave(bool malformed)
    {
        await using var fixture = new DaprLogicalReconstructionFixture();
        fixture.Malformed = malformed;
        fixture.Noncanonical = !malformed;
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        await Should.ThrowAsync<Exception>(() => fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None));
        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Durable.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidUtf8StateRefusesBeforeSuppliedSchemaReadOrSave()
    {
        await using var fixture = new DaprLogicalReconstructionFixture
        {
            InvalidUtf8 = true
        };
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        await Should.ThrowAsync<DecoderFallbackException>(() => fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None));
        fixture.Reads.ShouldBe(0);
        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Durable.ShouldBeEmpty();
    }

    [Fact]
    public async Task TamperedLaterLogicalResponseRefusesEntirePageBeforeCurrentCallbacksAndReleasesCharges()
    {
        await using var fixture = new DaprLogicalReconstructionFixture();
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        var budget = new EventBufferBudget();
        using var page = await fixture.Source.Source.ReadFirstPageAsync(binding, 3, fixture.Source.Trust, fixture.Source.Key, budget, CancellationToken.None);
        using var response = page.EncodeResponse(budget, CancellationToken.None);
        byte[] image = response.Bytes.ToArray();
        image[^1] ^= 1;
        int live = budget.LiveBytes;
        byte[] genesis = DaprLogicalClaimCodec.ComputeGenesis(DaprLogicalClaimCodec.ComputeSourceBindingHash(binding), fixture.Source.Trust.RegistryFingerprint);
        Should.Throw<InvalidOperationException>(() => PrivateLogicalReplayPage.Capture(image, binding, fixture.Source.Trust, genesis,
            fixture.Source.Service, budget, CancellationToken.None));
        budget.LiveBytes.ShouldBe(live);
        fixture.Source.DeserializationCalls.ShouldBe(0);
        fixture.Applies.ShouldBe(0);
        fixture.Tokens.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("logical-replay:state:0")]
    [InlineData("logical-replay:state:1")]
    [InlineData("logical-replay:final-state:v1")]
    public async Task OrphanCanonicalParticipantsBlockBegin(string key)
    {
        await using var fixture = new DaprLogicalReconstructionFixture();
        fixture.Store.Put<byte[]?>(key, null);
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var result = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Store.Saves.ShouldBe(0);
        fixture.Tokens.ShouldBeEmpty();
    }

    [Fact]
    public async Task TakeoverRetainsCurrentCanonicalStateAfterCommittedPage()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(2);
        DaprLogicalSourceBinding binding = await Begin(fixture);
        using var first = await Execute(fixture, binding, 1, 1);
        using var takeover = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "next", 1, CancellationToken.None);
        takeover.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        takeover.Generation.ShouldBe(2);
        using var final = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, binding, fixture.Source.Trust, fixture.Source.Key,
            "next", 2, 2, "request", 1, CancellationToken.None);
        State(final).ShouldBe("{\"value\":2}");
        fixture.Applies.ShouldBe(2);
    }

    [Fact]
    public async Task CompletePageRefusalRunsZeroStateApplyOrEventDeserializers()
    {
        await using var fixture = new DaprLogicalReconstructionFixture();
        DaprLogicalSourceBinding binding = await Begin(fixture);
        int reads = fixture.Reads;
        fixture.Source.Events[3] = fixture.Source.Events[3] with
        {
            AggregateType = "foreign"
        };
        await Should.ThrowAsync<InvalidOperationException>(() => Execute(fixture, binding, 1, 3));
        fixture.Reads.ShouldBe(reads);
        fixture.Applies.ShouldBe(0);
        fixture.Source.DeserializationCalls.ShouldBe(0);
        fixture.Source.ValidationCalls.ShouldBe(0);
        fixture.Store.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task MalformedSuccessorLedgerBlocksTakeoverBeforeCallbacksOrSave()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(2);
        DaprLogicalSourceBinding binding = await Begin(fixture);
        using var first = await Execute(fixture, binding, 1, 1);
        DaprReplayPageLedger ledger = fixture.Store.Get<DaprReplayPageLedger>("logical-replay:ledger:1");
        fixture.Store.Put("logical-replay:ledger:2", ledger with
        {
            PageOrdinal = 2,
            RequestHash = new byte[1024 * 1024]
        });
        int callbacks = fixture.Tokens.Count;
        using var result = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "next", 1, CancellationToken.None);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Store.Saves.ShouldBe(2);
        fixture.Tokens.Count.ShouldBe(callbacks);
    }

    [Fact]
    public async Task CombinedWorkingCapacityRefusesBeforeStateCallbacks()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, 128 * 1024 * 1024);
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None));
        fixture.Tokens.ShouldBeEmpty();
        fixture.Store.Durable.ShouldBeEmpty();
        fixture.Store.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task BeginUncertainReadbackRetainsInitialBytesAndRetriesWithoutCallbacks()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1);
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        fixture.Store.AfterSave = () => fixture.Store.FailReads = true;
        using var unknown = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None);
        unknown.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        int callbacks = fixture.Tokens.Count;
        var pending = (DaprReplayBeginTransition)typeof(DaprReplayOperationOwner).GetField("_beginPending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        pending.ShouldNotBeNull();
        MemoryMarshal.TryGetArray(pending.InitialState!.Bytes, out ArraySegment<byte> image).ShouldBeTrue();
        pending.Budget.LiveBytes.ShouldBeGreaterThan(0);
        await Should.ThrowAsync<IOException>(() => fixture.Owner.DisposeAsync().AsTask());
        pending.Budget.LiveBytes.ShouldBeGreaterThan(0);
        Encoding.UTF8.GetString(image.Array!).ShouldBe("{\"value\":0}");
        fixture.Store.FailReads = false;
        fixture.Store.AfterSave = null;
        using var resolved = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None);
        resolved.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Tokens.Count.ShouldBe(callbacks);
        fixture.Store.Saves.ShouldBe(1);
        image.Array!.ShouldAllBe(value => value == 0);
        pending.Budget.LiveBytes.ShouldBe(0);
        using var final = await Execute(fixture, binding, 1, 1);
        State(final).ShouldBe("{\"value\":1}");
    }

    [Fact]
    public async Task CompleteReconstructionCapacityShrinksBeforeAnyApplyAndSingleEventFits()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(maximumBufferBytes: 3500000, payloadBytes: 65536);
        DaprLogicalSourceBinding binding = await Begin(fixture);
        using var first = await Execute(fixture, binding, 1, 3);
        first.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Store.Get<DaprReplayPageLedger>("logical-replay:ledger:1").Count.ShouldBe(1);
        State(first).ShouldBe("{\"value\":1}");
        fixture.Applies.ShouldBe(1);
        fixture.Source.DeserializationCalls.ShouldBe(1);
        first.Dispose();
        using var second = await Execute(fixture, binding, 2, 3);
        if (second.IsComplete)
        {
            State(second).ShouldBe("{\"value\":3}");
        }
        else
        {
            State(second).ShouldBe("{\"value\":2}");
            second.Dispose();
            using var final = await Execute(fixture, binding, 3, 3);
            State(final).ShouldBe("{\"value\":3}");
            final.IsComplete.ShouldBeTrue();
        }
        fixture.Applies.ShouldBe(3);
    }

    [Fact]
    public async Task ProofHeavySingleEventFitsConservativeIntakePartitionBeforeCallbacks()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, maximumBufferBytes: 4000000);
        fixture.Source.Events[1] = fixture.Source.Events[1] with
        {
            MessageId = new string('m', 60000)
        };
        DaprLogicalSourceBinding binding = await Begin(fixture);
        using var result = await Execute(fixture, binding, 1, 1);
        State(result).ShouldBe("{\"value\":1}");
        result.IsComplete.ShouldBeTrue();
        fixture.Applies.ShouldBe(1);
        fixture.Source.DeserializationCalls.ShouldBe(1);
        fixture.Store.Saves.ShouldBe(2);
    }

    [Theory]
    [InlineData(false, "source")]
    [InlineData(false, "trust")]
    [InlineData(false, "loss")]
    [InlineData(true, "source")]
    [InlineData(true, "trust")]
    [InlineData(true, "loss")]
    public async Task EventCallbackLossStopsBeforeAnyLaterCandidateApplyOrTransition(bool deserialize, string loss)
    {
        await using var fixture = new DaprLogicalReconstructionFixture();
        using var cancellation = new CancellationTokenSource();
        DaprLogicalSourceBinding binding = await Begin(fixture);
        int priorReads = fixture.Reads;
        int calls = 0;
        Action<CancellationToken> hook = token =>
        {
            // Source preparation also uses version validation; this control loses authority inside the fold's current event callback.
            if (fixture.Reads == priorReads)
            {
                return;
            }
            token.ShouldBe(cancellation.Token);
            calls++;
            Lose(fixture, cancellation, loss);
        };
        if (deserialize)
        {
            fixture.Source.OnDeserialization = hook;
        }
        else
        {
            fixture.Source.OnValidation = hook;
        }
        await Should.ThrowAsync<Exception>(() => Execute(fixture, binding, 1, 3, cancellation.Token));
        calls.ShouldBe(1);
        fixture.Source.DeserializationCalls.ShouldBe(deserialize ? 1 : 0);
        fixture.Applies.ShouldBe(0);
        fixture.Store.Saves.ShouldBe(1);
        fixture.Store.Durable.ContainsKey(FinalState).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task ForeignAggregateRouteRefusesEmptyTargetBeforeAnyStateCallback(int events)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(events);
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("foreign", 0, CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust,
            "owner", null, CancellationToken.None));
        fixture.Tokens.ShouldBeEmpty();
        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Durable.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => new RegisteredLogicalReplayBinding(typeof(DaprLogicalReconstructionTestState),
            fixture.Source.Service, "foreign", "test-state-json", "{}\n"u8.ToArray(), _ => new DaprLogicalReconstructionTestState(),
            (_, _) => new DaprLogicalReconstructionTestState(), (_, _, _) =>
            {
            }, (_, _, _) => new DaprLogicalReconstructionTestState(), 32, 16384));
    }

    [Fact]
    public async Task PageUncertainReadbackRetainsCandidateAndRetriesWithoutApply()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1);
        DaprLogicalSourceBinding binding = await Begin(fixture);
        fixture.Store.AfterSave = () => fixture.Store.FailReads = true;
        using var unknown = await Execute(fixture, binding, 1, 1);
        unknown.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        fixture.Store.FailReads = false;
        fixture.Store.AfterSave = null;
        using var resolved = await Execute(fixture, binding, 1, 1);
        State(resolved).ShouldBe("{\"value\":1}");
        fixture.Applies.ShouldBe(1);
    }

    [Fact]
    public async Task AddressedApplyFailureReturnsExactSignedDiagnosticIdentityWithoutState()
    {
        await using var fixture = new DaprLogicalReconstructionFixture
        {
            ThrowOnApply = 2
        };
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        AggregateReconstructionResult result = await DaprAggregateStateReconstructor.ReconstructAddressedAsync(DaprLogicalReplayFixture.Identity,
            "r", fixture.Source.Source, binding, fixture.Source.Trust, fixture.Source.Key, fixture.Owner, "owner", "request", 3, CancellationToken.None);
        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.ApplyFailed);
        result.FailedSequenceNumber.ShouldBe(2);
        result.FailedEventType.ShouldBe("evt");
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
        fixture.Store.Get<byte[]>("logical-replay:state:0").ShouldBe("{\"value\":0}"u8.ToArray());
    }

    [Fact]
    public async Task PrivateResponseAndCanonicalBuffersClearAfterDisposal()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1);
        DaprLogicalSourceBinding binding = await Begin(fixture);
        var result = await Execute(fixture, binding, 1, 1);
        MemoryMarshal.TryGetArray(result.CanonicalState!.Bytes, out ArraySegment<byte> state).ShouldBeTrue();
        MemoryMarshal.TryGetArray(result.Response!.Bytes, out ArraySegment<byte> response).ShouldBeTrue();
        result.Dispose();
        state.Array!.ShouldAllBe(value => value == 0);
        response.Array!.ShouldAllBe(value => value == 0);
        fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":1}"u8.ToArray());
    }

    private static string State(DaprReplayOperationResult result)
    {
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        return Encoding.UTF8.GetString(result.CanonicalState!.Bytes.Span);
    }

    private static async Task<DaprLogicalSourceBinding> Begin(DaprLogicalReconstructionFixture fixture)
    {
        DaprLogicalSourceBinding binding = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var result = await fixture.Owner.BeginAsync(fixture.Source.Source, binding, fixture.Source.Trust, "owner", null, CancellationToken.None);
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        return binding;
    }

    private static Task<DaprReplayOperationResult> Execute(DaprLogicalReconstructionFixture fixture, DaprLogicalSourceBinding binding,
        long page, int size, CancellationToken token = default)
        => fixture.Owner.ExecutePageAsync(fixture.Source.Source, binding, fixture.Source.Trust, fixture.Source.Key, "owner", 1, page, "request", size, token);

    private static void Lose(DaprLogicalReconstructionFixture fixture, CancellationTokenSource cancellation, string loss)
    {
        switch (loss)
        {
            case "cancel":
                cancellation.Cancel();
                break;
            case "source":
                fixture.Source.Metadata = fixture.Source.Metadata! with
                {
                    ETag = "changed"
                };
                break;
            case "trust":
                fixture.Source.Trust.Dispose();
                break;
            case "loss":
                fixture.Source.Registry.CapabilityLoss.ObserveViolation();
                break;
        }
    }
}
