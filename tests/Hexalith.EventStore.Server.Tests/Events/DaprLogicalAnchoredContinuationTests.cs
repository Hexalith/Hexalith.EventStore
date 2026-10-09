using System.Security.Cryptography;
using System.Text;
using NSubstitute;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Controls the distinct dormant anchored operation through actual Begin, page commits and readback.</summary>
public sealed class DaprLogicalAnchoredContinuationTests
{
    private const string Operation = "logical-replay:anchored:operation:v1";
    private const string FinalState = "logical-replay:anchored:final-state:v1";
    /// <summary>Commits only the uncovered tail and retries exact bytes without repeating Apply.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ActualAnchoredTailBeginPageReadbackAndRetry(int count)
    {
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync();
        int initialApplies = fixture.Snapshot.Replay.Applies;
        using var begin = await fixture.BeginAsync();
        begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        DaprReplayOperationRecord genesis = fixture.Store.Get<DaprReplayOperationRecord>(Operation);
        genesis.CompletedSequence.ShouldBe(1);
        genesis.PageOrdinal.ShouldBe(0);
        genesis.LogicalEvidenceModelId.ShouldBe(DaprLogicalReplayAnchorCodec.ModelId);
        genesis.AnchorSelectionHash.ShouldNotBeNull();
        long ordinal = 1;
        using var first = await fixture.ExecuteAsync(ordinal, count);
        first.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        State(first).ShouldBe("{\"value\":" + (count + 1) + "}");
        DaprReplayPageLedger ledger = fixture.Store.Get<DaprReplayPageLedger>("logical-replay:anchored:ledger:1");
        ledger.RequestHash.ShouldBe(DaprLogicalReplayCommitmentCodec.AnchoredRequestHash("tenant", "anchored-operation", "owner", 1, 1, "request", count, genesis.AnchorSelectionHash, genesis.SourceBindingHash, genesis.RegistryFingerprint, fixture.Budget));
        var entry = new DaprLogicalPageTranscriptEntry(1, 1, ledger.RequestHash, ledger.PreviousAccumulator, ledger.Accumulator, ledger.StartSequence, ledger.EndSequence, ledger.Count, ledger.ResponseHash, ledger.IsFinal, ledger.PriorStateHash, ledger.CanonicalStateHash, ledger.PreviousEffectiveChainHash!, ledger.EffectiveChainHash!);
        ledger.TranscriptHash.ShouldBe(DaprLogicalReplayCommitmentCodec.AnchoredTranscriptStep("tenant", "anchored-operation", genesis.AnchorSelectionHash, genesis.TranscriptHash!, entry, fixture.Budget));
        DaprLogicalReplayAnchorSelection selection = DaprLogicalReplayAnchorCodec.Decode(fixture.Store.Get<byte[]>("logical-replay:anchored:selection:v1"));
        var intake = new DaprLogicalAnchoredIntake(fixture.Trust, selection, genesis.AnchorSelectionHash);
        ledger.EffectiveChainHash.ShouldBe(DaprLogicalReplayCommitmentCodec.EffectiveSuccessor(first.Response!.Bytes.Span, fixture.Source, fixture.Snapshot.Replay.Source.Trust, genesis.Accumulator, genesis.EffectiveChainHash!, genesis.ReconstructionBindingHash, fixture.Budget, fixture.Token, intake));
        Should.Throw<ArgumentException>(() => DaprLogicalReplayResponseVerifier.Verify(first.Response!.Bytes.Span, fixture.Source, fixture.Snapshot.Replay.Source.Trust, genesis.Accumulator, fixture.Budget, fixture.Token));
        if (!first.IsComplete)
        {
            ordinal++;
            using var final = await fixture.ExecuteAsync(ordinal, count);
            final.IsComplete.ShouldBeTrue();
            State(final).ShouldBe("{\"value\":3}");
        }

        fixture.Snapshot.Replay.Applies.ShouldBe(initialApplies + 2);
        int calls = fixture.Snapshot.Replay.Applies;
        using var retry = await fixture.ExecuteAsync(1, count);
        retry.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        retry.Response!.Bytes.ToArray().ShouldBe(first.Response!.Bytes.ToArray());
        fixture.Snapshot.Replay.Applies.ShouldBe(calls);
        fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":3}"u8.ToArray());
        fixture.Store.Get<DaprReplayOperationRecord>(Operation).CanonicalStateHash.ShouldBe(SHA256.HashData("{\"value\":3}"u8));
    }

    /// <summary>Commits a distinct empty terminal page at the head without source event reads or Apply.</summary>
    [Fact]
    public async Task ActualHeadSnapshotZeroTailCommitsTerminalReadbackAndExactRetry()
    {
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync(3);
        int applies = fixture.Snapshot.Replay.Applies;
        int reads = fixture.Snapshot.Replay.Source.EventReads;
        using var begin = await fixture.BeginAsync();
        begin.IsComplete.ShouldBeFalse();
        using var final = await fixture.ExecuteAsync();
        final.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        final.IsComplete.ShouldBeTrue();
        State(final).ShouldBe("{\"value\":3}");
        DaprReplayPageLedger ledger = fixture.Store.Get<DaprReplayPageLedger>("logical-replay:anchored:ledger:1");
        ledger.StartSequence.ShouldBe(4);
        ledger.EndSequence.ShouldBe(3);
        ledger.Count.ShouldBe(0);
        using var retry = await fixture.ExecuteAsync();
        retry.Response!.Bytes.ToArray().ShouldBe(final.Response!.Bytes.ToArray());
        fixture.Snapshot.Replay.Applies.ShouldBe(applies);
        fixture.Snapshot.Replay.Source.EventReads.ShouldBe(reads);
    }

    /// <summary>Takeover retains current anchored state and preserves every ordinary operation-key byte.</summary>
    [Fact]
    public async Task ActualTakeoverUsesSameOperationTokenAndSeparateOrdinaryKeys()
    {
        using var cancellation = new CancellationTokenSource();
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync(token: cancellation.Token);
        string[] ordinary = ["logical-replay:operation:v1", "logical-replay:state:0", "logical-replay:ledger:1", "logical-replay:response:1", "logical-replay:final:v1", "logical-replay:final-state:v1", "logical-replay:command-proof:v1"];
        foreach (string key in ordinary)
        {
            fixture.Store.Put(key, "ordinary sentinel");
        }

        Dictionary<string, byte[]> saved = ordinary.ToDictionary(key => key, key => fixture.Store.Durable[key].ToArray());
        using var begin = await fixture.BeginAsync();
        using var first = await fixture.ExecuteAsync();
        using var takeover = await fixture.BeginAsync("next", 1);
        takeover.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        takeover.Generation.ShouldBe(2);
        using var final = await fixture.ExecuteAsync(2, 1, "next", 2);
        State(final).ShouldBe("{\"value\":3}");
        int calls = fixture.Snapshot.Replay.Applies;
        using var retry = await fixture.ExecuteAsync(2, 1, "next", 2);
        fixture.Snapshot.Replay.Applies.ShouldBe(calls);
        foreach ((string key, byte[] bytes)in saved)
        {
            fixture.Store.Durable[key].ShouldBe(bytes);
        }
    }

    private static string State(DaprReplayOperationResult value) => Encoding.UTF8.GetString(value.CanonicalState!.Bytes.Span);
    /// <summary>Refuses an independently canceled or uncanceled request token before any anchored save or Apply.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentRequestTokenCannotContinueRetainedOperation(bool canceled)
    {
        using var originating = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync(token: originating.Token);
        if (canceled)
        {
            foreign.Cancel();
        }

        int calls = fixture.Snapshot.Replay.Applies;
        int live = fixture.Budget.LiveBytes;
        fixture.Store.Manager.ClearReceivedCalls();
        var gate = (SemaphoreSlim)typeof(DaprReplayOperationOwner).GetField("_gate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(fixture.Owner)!;
        Func<Task<DaprReplayOperationResult>>[] attempts = [() => fixture.Owner.BeginAsync(fixture.Snapshot.Replay.Source.Source, fixture.Source, fixture.Snapshot.Replay.Source.Trust, "owner", null, foreign.Token), () => fixture.Owner.ExecutePageAsync(fixture.Snapshot.Replay.Source.Source, fixture.Source, fixture.Snapshot.Replay.Source.Trust, fixture.Snapshot.Replay.Source.Key, "owner", 1, 1, "request", 1, foreign.Token)];
        foreach (Func<Task<DaprReplayOperationResult>> attempt in attempts)
        {
            await gate.WaitAsync();
            Task<DaprReplayOperationResult> pending;
            bool refusedBeforeGate;
            try
            {
                pending = attempt();
                refusedBeforeGate = pending.IsCompleted;
            }
            finally
            {
                gate.Release();
            }

            InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => pending);
            refusedBeforeGate.ShouldBeTrue();
            fixture.Store.Manager.ReceivedCalls().ShouldBeEmpty();
            error.Message.ShouldContain("AnchorCapabilityHold");
        }

        fixture.Store.Manager.ReceivedCalls().ShouldBeEmpty();
        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Durable.ShouldBeEmpty();
        fixture.Snapshot.Replay.Applies.ShouldBe(calls);
        fixture.Budget.LiveBytes.ShouldBe(live);
    }

    /// <summary>The originating cancellation wins before foreign gates, argument failures, callbacks or unsupported capture.</summary>
    [Theory]
    [InlineData("begin")]
    [InlineData("page")]
    [InlineData("invalid-page")]
    [InlineData("anchor-capture")]
    [InlineData("command-capture")]
    public async Task DualCanceledTokensRefuseBeforeGateOrSideEffects(string entry)
    {
        using var originating = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync(token: originating.Token);
        int applies = fixture.Snapshot.Replay.Applies;
        int reads = fixture.Snapshot.Replay.Reads;
        int live = fixture.Budget.LiveBytes;
        fixture.Store.Manager.ClearReceivedCalls();
        originating.Cancel();
        foreign.Cancel();
        OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            switch (entry)
            {
                case "begin":
                    using (await fixture.Owner.BeginAsync(fixture.Snapshot.Replay.Source.Source, fixture.Source, fixture.Snapshot.Replay.Source.Trust, "owner", null, foreign.Token))
                    {
                    }

                    break;
                case "page":
                case "invalid-page":
                    using (await fixture.Owner.ExecutePageAsync(fixture.Snapshot.Replay.Source.Source, fixture.Source, fixture.Snapshot.Replay.Source.Trust, fixture.Snapshot.Replay.Source.Key, "owner", 1, entry == "page" ? 1 : 0, "request", 1, foreign.Token))
                    {
                    }

                    break;
                case "anchor-capture":
                    using (await fixture.Owner.CaptureCompletedAnchorOriginAsync(fixture.Snapshot.Replay.Source.Source, fixture.Source, fixture.Snapshot.Replay.Source.Trust, fixture.Budget, foreign.Token))
                    {
                    }

                    break;
                case "command-capture":
                    using (await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Snapshot.Replay.Source.Source, fixture.Source, fixture.Snapshot.Replay.Source.Trust, foreign.Token))
                    {
                    }

                    break;
            }
        });
        error.CancellationToken.ShouldBe(originating.Token);
        fixture.Store.Manager.ReceivedCalls().ShouldBeEmpty();
        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Durable.ShouldBeEmpty();
        fixture.Snapshot.Replay.Applies.ShouldBe(applies);
        fixture.Snapshot.Replay.Reads.ShouldBe(reads);
        fixture.Budget.LiveBytes.ShouldBe(live);
    }

    /// <summary>Actual snapshot substitution during a state callback stops before later Apply or any durable transition.</summary>
    [Theory]
    [InlineData("pair")]
    [InlineData("origin")]
    [InlineData("source")]
    [InlineData("cancel-throw")]
    public async Task CallbackLossPreservesPriorAndReleasesTemporaryCapacity(string point)
    {
        using var cancellation = new CancellationTokenSource();
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync(token: cancellation.Token);
        using (var begin = await fixture.BeginAsync())
        {
        }

        Dictionary<string, byte[]> prior = Copy(fixture.Store.Durable);
        int live = fixture.Budget.LiveBytes;
        int applies = fixture.Snapshot.Replay.Applies;
        int calls = 0;
        fixture.Snapshot.Replay.Hook = (boundary, _) =>
        {
            if (boundary != "read" || calls++ != 0)
            {
                return;
            }

            if (point == "pair")
            {
                fixture.Snapshot.Durable[fixture.Snapshot.Owner.StorageKey][0] ^= 1;
            }
            else if (point == "origin")
            {
                fixture.Snapshot.Replay.Store.Put("logical-replay:state:0", "{\"value\":99}"u8.ToArray());
            }
            else if (point == "source")
            {
                fixture.Snapshot.Replay.Source.Metadata = fixture.Snapshot.Replay.Source.Metadata!with
                {
                    ETag = "changed"
                };
            }
            else
            {
                cancellation.Cancel();
                throw new OperationCanceledException(new CancellationToken(true));
            }
        };
        Exception error = await Should.ThrowAsync<Exception>(() => fixture.ExecuteAsync());
        if (point == "cancel-throw")
        {
            ((OperationCanceledException)error).CancellationToken.ShouldBe(cancellation.Token);
        }

        calls.ShouldBe(1);
        fixture.Snapshot.Replay.Applies.ShouldBe(applies);
        fixture.Store.Saves.ShouldBe(1);
        AssertDurable(prior, fixture.Store.Durable);
        fixture.Budget.LiveBytes.ShouldBe(live);
    }

    /// <summary>A changed actual selection or committed earlier state cannot be treated as a later predecessor.</summary>
    [Theory]
    [InlineData("selection")]
    [InlineData("state")]
    [InlineData("response")]
    [InlineData("next-ordinal")]
    public async Task ActualCommittedHistorySubstitutionRefusesBeforeCallbacks(string point)
    {
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync();
        using (var begin = await fixture.BeginAsync())
        {
        }

        using (var first = await fixture.ExecuteAsync())
        {
        }

        string key = point switch
        {
            "selection" => "logical-replay:anchored:selection:v1",
            "state" => "logical-replay:anchored:state:1",
            "response" => "logical-replay:anchored:response:1",
            _ => "logical-replay:anchored:state:2"
        };
        fixture.Store.Put(key, "changed"u8.ToArray());
        int saves = fixture.Store.Saves;
        int calls = fixture.Snapshot.Replay.Applies;
        Dictionary<string, byte[]> prior = Copy(fixture.Store.Durable);
        int live = fixture.Budget.LiveBytes;
        using var refusal = await fixture.ExecuteAsync(2);
        refusal.Outcome.ShouldBe(DaprReplayCommitOutcome.Indeterminate);
        refusal.Response.ShouldBeNull();
        fixture.Snapshot.Replay.Applies.ShouldBe(calls);
        fixture.Store.Saves.ShouldBe(saves);
        AssertDurable(prior, fixture.Store.Durable);
        fixture.Budget.LiveBytes.ShouldBe(live);
    }

    /// <summary>Independent acknowledgement-loss readback alone decides response disclosure; no-commit retries never repeat a save automatically.</summary>
    [Theory]
    [InlineData("commit-throw", DaprReplayCommitOutcome.Proven)]
    [InlineData("no-commit", DaprReplayCommitOutcome.NoCommit)]
    [InlineData("partial", DaprReplayCommitOutcome.Indeterminate)]
    public async Task ActualAnchoredSaveModesUseIndependentReadback(string mode, object expectedValue)
    {
        var expected = (DaprReplayCommitOutcome)expectedValue;
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync(3);
        using (var begin = await fixture.BeginAsync())
        {
        }

        fixture.Store.SaveMode = mode;
        int calls = fixture.Snapshot.Replay.Applies;
        using var result = await fixture.ExecuteAsync();
        result.Outcome.ShouldBe(expected);
        if (expected == DaprReplayCommitOutcome.Proven)
        {
            State(result).ShouldBe("{\"value\":3}");
            using var retry = await fixture.ExecuteAsync();
            retry.Response!.Bytes.ToArray().ShouldBe(result.Response!.Bytes.ToArray());
            fixture.Store.Saves.ShouldBe(2);
        }
        else
        {
            result.Response.ShouldBeNull();
        }

        fixture.Snapshot.Replay.Applies.ShouldBe(calls);
    }

    private static Dictionary<string, byte[]> Copy(Dictionary<string, byte[]> values) => values.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    /// <summary>Constructor refuses a full parent budget before operation callbacks/save and clears every transferred initial buffer.</summary>
    [Fact]
    public async Task ConstructorCapacityRefusalClearsTransferredInitialOwnership()
    {
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.PrepareAsync(exhaustConstructorCapacity: true));
        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Durable.ShouldBeEmpty();
        fixture.CapturedInitialArrays.Count.ShouldBe(6);
        fixture.CapturedInitialArrays.ShouldAllBe(array => array.All(value => value == 0));
        fixture.Budget.LiveBytes.ShouldBe(0);
        fixture.Trust.RequireCurrent(fixture.Snapshot.Replay.Source.Trust, fixture.Token);
    }

    /// <summary>The actual final await may observe lost pair authority or original cancellation, withholding a committed response without repeating Apply.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedZeroTailWithholdsDisclosureAfterFinalAuthorityLoss(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync(3, token: cancellation.Token);
        using (var begin = await fixture.BeginAsync())
        {
        }

        int live = fixture.Budget.LiveBytes;
        int calls = fixture.Snapshot.Replay.Applies;
        fixture.Store.AfterSave = () =>
        {
            if (cancel)
            {
                cancellation.Cancel();
            }
            else
            {
                fixture.Snapshot.Durable[fixture.Snapshot.Owner.StorageKey][0] ^= 1;
            }
        };
        using var result = await fixture.ExecuteAsync();
        result.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        result.Response.ShouldBeNull();
        result.CanonicalState.ShouldBeNull();
        result.OriginatingCancellationObserved.ShouldBe(cancel);
        result.ResponseUnavailable.ShouldBe(!cancel);
        fixture.Store.Get<byte[]>(FinalState).ShouldBe("{\"value\":3}"u8.ToArray());
        fixture.Store.Saves.ShouldBe(2);
        fixture.Snapshot.Replay.Applies.ShouldBe(calls);
        fixture.Budget.LiveBytes.ShouldBe(live);
    }

    /// <summary>Private initial substitution during a yielding pair read refuses before save and clears owned full capacities on disposal.</summary>
    [Fact]
    public async Task YieldingInitialAliasSubstitutionRefusesAndDropsOwnedCapacity()
    {
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync();
        object initial = Field(fixture.Owner, "_initialAnchor")!;
        var canonical = (Hexalith.EventStore.Contracts.Events.IReadOnlyPayload)Field(initial, "_canonical")!;
        byte[] bytes = (byte[])Field(canonical, "_owner")!;
        var image = (DaprLogicalResponseOwner)Field(fixture.Owner, "_anchorImage")!;
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(image.Bytes, out ArraySegment<byte> segment).ShouldBeTrue();
        DaprLogicalReplayAnchorSelection fields = ((DaprLogicalAnchoredIntake)Field(fixture.Owner, "_anchoredIntake")!).Selection;
        byte[][] decoded = new[]
        {
            fields.SourceBindingHash,
            fields.RegistryFingerprint,
            fields.ReconstructionBindingHash,
            fields.SnapshotWitnessHash,
            fields.CanonicalStateHash,
            fields.CoveredAccumulator,
            fields.CoveredEffectiveChain,
            fields.CoveredTranscript
        }.Select(OwnedArray).ToArray();
        decoded.ShouldAllBe(array => ReferenceEquals(array, OwnedArray(((DaprLogicalReplayInitialAnchor)initial).SelectionImage)));
        int calls = 0;
        fixture.Snapshot.OnRead = async (_, _) =>
        {
            await Task.Yield();
            if (calls++ == 0)
            {
                bytes[0] ^= 1;
            }
        };
        int applies = fixture.Snapshot.Replay.Applies;
        Dictionary<string, byte[]> durable = Copy(fixture.Snapshot.Durable);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.BeginAsync());
        calls.ShouldBeGreaterThan(0);
        fixture.Store.Saves.ShouldBe(0);
        fixture.Snapshot.Replay.Applies.ShouldBe(applies);
        AssertDurable(durable, fixture.Snapshot.Durable);
        await fixture.Owner.DisposeAsync();
        bytes.ShouldAllBe(value => value == 0);
        segment.Array!.ShouldAllBe(value => value == 0);
        decoded.ShouldAllBe(array => array.All(value => value == 0));
        Field(fixture.Owner, "_initialAnchor").ShouldBeNull();
        Field(fixture.Owner, "_anchorImage").ShouldBeNull();
        Field(fixture.Owner, "_anchoredIntake").ShouldBeNull();
        fixture.Budget.LiveBytes.ShouldBe(0);
        fixture.Trust.RequireCurrent(fixture.Snapshot.Replay.Source.Trust, fixture.Token);
    }

    /// <summary>Missing snapshot evidence falls back before anchored Begin and reconstructs sequence one through the ordinary owner.</summary>
    [Fact]
    public async Task MalformedCandidateFallsBackToActualFullReplayWithoutDeletion()
    {
        await using var snapshot = new DaprLogicalSnapshotFixture();
        await snapshot.PrepareAsync();
        snapshot.Durable[snapshot.Owner.WitnessKey] = [1];
        Dictionary<string, byte[]> stored = Copy(snapshot.Durable);
        using var budget = new EventBufferBudget();
        DaprLogicalSourceBinding source = await snapshot.SourceAsync();
        (await snapshot.Owner.AcquireAsync(source, false, snapshot.AcquireOriginAsync, budget, CancellationToken.None)).ShouldBeNull();
        budget.LiveBytes.ShouldBe(0);
        var store = new DaprReplayTestStore();
        await using var owner = new DaprReplayOperationOwner(store.Manager, "tenant", "full-replay", reconstruction: snapshot.Replay.Binding);
        int applies = snapshot.Replay.Applies;
        using (var begin = await owner.BeginAsync(snapshot.Replay.Source.Source, source, snapshot.Replay.Source.Trust, "owner", null, CancellationToken.None))
        {
        }

        using var final = await owner.ExecutePageAsync(snapshot.Replay.Source.Source, source, snapshot.Replay.Source.Trust, snapshot.Replay.Source.Key, "owner", 1, 1, "request", 3, CancellationToken.None);
        State(final).ShouldBe("{\"value\":3}");
        snapshot.Replay.Applies.ShouldBe(applies + 3);
        store.Get<DaprReplayPageLedger>("logical-replay:ledger:1").StartSequence.ShouldBe(1);
        AssertDurable(stored, snapshot.Durable);
    }

    private static object? Field(object value, string name) => value.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(value);
    private static byte[] OwnedArray(ReadOnlyMemory<byte> value)
    {
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(value, out ArraySegment<byte> segment).ShouldBeTrue();
        return segment.Array!;
    }

    /// <summary>A later-page application callback cannot replace an admitted earlier participant and trigger a save.</summary>
    [Fact]
    public async Task LaterCallbackCannotReplaceActualAnchoredPredecessor()
    {
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync();
        using (var begin = await fixture.BeginAsync())
        {
        }

        using (var first = await fixture.ExecuteAsync())
        {
        }

        int saves = fixture.Store.Saves;
        int calls = 0;
        int live = fixture.Budget.LiveBytes;
        fixture.Snapshot.Replay.Hook = (boundary, _) =>
        {
            if (boundary == "apply")
            {
                calls++;
                fixture.Store.Put("logical-replay:anchored:state:1", "{\"value\":99}"u8.ToArray());
            }
        };
        try
        {
            using var result = await fixture.ExecuteAsync(2);
            result.Response.ShouldBeNull();
        }
        catch (InvalidOperationException)
        {
        }

        calls.ShouldBe(1);
        fixture.Store.Saves.ShouldBe(saves);
        fixture.Store.Durable.ContainsKey("logical-replay:anchored:ledger:2").ShouldBeFalse();
        fixture.Budget.LiveBytes.ShouldBe(live);
    }

    /// <summary>An equivalent caller trust object cannot replace the exact ordinary route trust captured by anchored admission.</summary>
    [Fact]
    public async Task EquivalentRouteTrustCannotGrantAnchoredBegin()
    {
        await using var fixture = new DaprLogicalAnchoredContinuationFixture();
        await fixture.PrepareAsync();
        using DaprLogicalClaimTrust alternative = fixture.Snapshot.Replay.Source.NewTrust();
        bool refused = false;
        try
        {
            using var begin = await fixture.Owner.BeginAsync(fixture.Snapshot.Replay.Source.Source, fixture.Source, alternative, "owner", null, fixture.Token);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Durable.ShouldBeEmpty();
        refused.ShouldBeTrue();
    }

    private static void AssertDurable(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        actual.Keys.Order().ShouldBe(expected.Keys.Order());
        foreach ((string key, byte[] bytes)in expected)
        {
            actual[key].ShouldBe(bytes);
        }
    }
}
