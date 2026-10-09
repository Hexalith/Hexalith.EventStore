using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Exercises actual owner/proof/router composition against independent durable participants.</summary>
public sealed class DaprLogicalCommandStateTests
{
    private const string Operation = "logical-replay:operation:v1";
    private const string Proof = "logical-replay:command-proof:v1";
    private static CommandEnvelope Command => new("command-1", "tenant", "d", "aggregate", "Change", "{}"u8.ToArray(), "correlation", null, "user", null);

    /// <summary>Admits the maximum command payload explicitly and keeps both private copies charged until their owners release.</summary>
    [Theory]
    [InlineData(40, false)]
    [InlineData(96, true)]
    public async Task MaximumCommandPayloadUsesActualPartitionCapacity(int maximumMiB, bool fits)
    {
        CommandEnvelope command = Command with
        {
            Payload = new byte[16 * 1024 * 1024]
        };
        command.Payload[0] = 17;
        command.Payload[^1] = 29;
        await using var fixture = new DaprLogicalReconstructionFixture(0, maximumBufferBytes: maximumMiB * 1024 * 1024, command: command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 0);
        EventBufferBudget budget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        int initial = budget.LiveBytes;
        initial.ShouldBeGreaterThanOrEqualTo(command.Payload.Length + 4 * 1024 * 1024);
        int reads = fixture.Reads;
        byte[] durableProof = fixture.Store.Get<byte[]>(Proof);
        if (!fits)
        {
            (await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None))).Message.ShouldContain("ScratchLimit");
            fixture.Reads.ShouldBe(reads);
        }
        else
        {
            using (PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None))
            {
                budget.LiveBytes.ShouldBeGreaterThanOrEqualTo(initial + command.Payload.Length + 12 * 1024 * 1024);
                completed.Budget.LiveBytes.ShouldBeGreaterThanOrEqualTo(command.Payload.Length + 4 * 1024 * 1024);
                completed.Command.Payload.ShouldBe(command.Payload);
                completed.Command.Payload.ShouldNotBeSameAs(command.Payload);
                var processor = new DaprLogicalCommandStateProcessor();
                using ServiceProvider provider = Provider(processor, true);
                _ = await DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(command, null), completed);
                processor.Calls.ShouldBe(1);
            }
        }
        budget.LiveBytes.ShouldBe(initial);
        fixture.Store.Get<byte[]>(Proof).ShouldBe(durableProof);
        command.Payload[0].ShouldBe((byte)17);
        command.Payload[^1].ShouldBe((byte)29);
        await fixture.Owner.DisposeAsync();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Admits the exact metadata ceiling before private dictionary capture and refuses the next byte without retained charges.</summary>
    [Fact]
    public async Task CommandExtensionCeilingKeepsConservativeDictionaryCharge()
    {
        CommandEnvelope command = Command;
        int TextBytes(string value) => 4 + System.Text.Encoding.UTF8.GetByteCount(value);
        int overhead = "HX-EV-DAPR-COMMAND-ROUTE-1\0"u8.Length + 1
            + new[] { command.MessageId, command.TenantId, command.Domain, command.AggregateId, command.CommandType }.Sum(TextBytes)
            + 8 + 32 + TextBytes(command.CorrelationId) + 1 + TextBytes(command.UserId) + 1 + 4 + 8 + 1;
        string value = new('x', 512 * 1024 - overhead);
        command = command with
        {
            Extensions = new Dictionary<string, string> { ["k"] = value }
        };
        using var hashBudget = new EventBufferBudget(4 * 1024 * 1024);
        byte[] hash = DaprLogicalCommandStateCodec.CommandHash(command, hashBudget, CancellationToken.None);
        hash.Length.ShouldBe(32);
        hashBudget.LiveBytes.ShouldBe(0);
        CommandEnvelope oversized = command with
        {
            Extensions = new Dictionary<string, string> { ["k"] = value + "x" }
        };
        Should.Throw<InvalidOperationException>(() => DaprLogicalCommandStateCodec.CommandHash(oversized, hashBudget, CancellationToken.None)).Message.ShouldContain("MetadataLimit");
        hashBudget.LiveBytes.ShouldBe(0);
        await using var fixture = new DaprLogicalReconstructionFixture(0, command: command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 0);
        EventBufferBudget ownerBudget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        int initial = ownerBudget.LiveBytes;
        initial.ShouldBeGreaterThanOrEqualTo(command.Payload.Length + 4 * 1024 * 1024);
        using (PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None))
        {
            completed.Command.Extensions.ShouldNotBeSameAs(command.Extensions);
            completed.Command.Extensions!["k"].ShouldBe(value);
            completed.Budget.LiveBytes.ShouldBeGreaterThanOrEqualTo(command.Payload.Length + 4 * 1024 * 1024);
        }
        ownerBudget.LiveBytes.ShouldBe(initial);
        command.Extensions!["k"].ShouldBe(value);
    }

    /// <summary>Clears captured full payload capacities and drops dictionaries before either command ownership reservation releases.</summary>
    [Fact]
    public async Task DisposedCommandOwnersClearAndDropEveryPrivateCommandReference()
    {
        CommandEnvelope command = Command with
        {
            Extensions = new Dictionary<string, string> { ["trace"] = "retained" }
        };
        await using var fixture = new DaprLogicalReconstructionFixture(0, command: command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 0);
        EventBufferBudget ownerBudget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        int initial = ownerBudget.LiveBytes;
        PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        CommandEnvelope invocation = completed.Command;
        CommandEnvelope replay = (CommandEnvelope)typeof(DaprReplayOperationOwner).GetField("_command", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        invocation.ShouldNotBeSameAs(replay);
        invocation.Payload.ShouldNotBeSameAs(replay.Payload);
        invocation.Extensions.ShouldNotBeSameAs(replay.Extensions);
        completed.Dispose();
        invocation.Payload.ShouldAllBe(static value => value == 0);
        invocation.Extensions.ShouldBeEmpty();
        typeof(PrivateLogicalCommandState).GetField("_command", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(completed).ShouldBeNull();
        Should.Throw<ObjectDisposedException>(() => _ = completed.Command);
        completed.Budget.LiveBytes.ShouldBe(0);
        ownerBudget.LiveBytes.ShouldBe(initial);
        await fixture.Owner.DisposeAsync();
        replay.Payload.ShouldAllBe(static value => value == 0);
        replay.Extensions.ShouldBeEmpty();
        typeof(DaprReplayOperationOwner).GetField("_command", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner).ShouldBeNull();
        ownerBudget.LiveBytes.ShouldBe(0);
        command.Payload.ShouldBe("{}"u8.ToArray());
        command.Extensions!["trace"].ShouldBe("retained");
    }

    /// <summary>Restores initial payload and staging charges when transcript-genesis admission refuses before save.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BeginGenesisCapacityRefusalRestoresEveryInitialCharge(bool cancelled)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(0, workingBytes: 1, maximumBufferBytes: 8192);
        DaprLogicalSourceBinding source = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        EventBufferBudget budget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        using var token = new CancellationTokenSource();
        if (cancelled)
        {
            fixture.Hook = (point, _) =>
            {
                if (point == "write")
                {
                    token.Cancel();
                    throw new InvalidOperationException("cancelled initial writer sentinel");
                }
            };
        }
        Exception error = await Should.ThrowAsync<Exception>(() => fixture.Owner.BeginAsync(fixture.Source.Source, source, fixture.Source.Trust, "owner", null, token.Token));
        if (cancelled)
        {
            error.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(token.Token);
        }
        else
        {
            error.Message.ShouldContain("ScratchLimit");
        }
        fixture.Writes.ShouldBeGreaterThan(0);
        fixture.Store.Saves.ShouldBe(0);
        fixture.Store.Manager.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "SetStateAsync").ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Preserves original cancellation from throwing create/read/write/Apply callbacks without later callbacks or durable staging.</summary>
    [Theory]
    [InlineData("create", false, false)]
    [InlineData("read", false, false)]
    [InlineData("write", false, false)]
    [InlineData("read", true, false)]
    [InlineData("write", true, false)]
    [InlineData("apply", true, false)]
    [InlineData("apply", true, true)]
    public async Task SynchronousReconstructionCallbacksPreserveOriginalCancellationAndDurableBytes(string point, bool page, bool foreignCancellation)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        if (page)
        {
            using DaprReplayOperationResult begin = await fixture.Owner.BeginAsync(fixture.Source.Source, source, fixture.Source.Trust, "owner", null, CancellationToken.None);
            begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        }
        EventBufferBudget budget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        int initial = budget.LiveBytes;
        int saves = fixture.Store.Saves;
        int stages = fixture.Store.Manager.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "SetStateAsync");
        Dictionary<string, byte[]> durable = fixture.Store.Durable.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToArray());
        var callbacks = new List<string>();
        using var token = new CancellationTokenSource();
        fixture.Hook = (boundary, _) =>
        {
            callbacks.Add(boundary);
            if (boundary == point)
            {
                token.Cancel();
                if (foreignCancellation)
                {
                    throw new OperationCanceledException(new CancellationToken(true));
                }
                throw new InvalidOperationException("cancelled reconstruction callback sentinel");
            }
        };
        Exception error = await Should.ThrowAsync<Exception>(async () =>
        {
            using DaprReplayOperationResult result = page
                ? await fixture.Owner.ExecutePageAsync(fixture.Source.Source, source, fixture.Source.Trust, fixture.Source.Key, "owner", 1, 1, "page", 1, token.Token)
                : await fixture.Owner.BeginAsync(fixture.Source.Source, source, fixture.Source.Trust, "owner", null, token.Token);
        });
        error.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(token.Token);
        callbacks.Count.ShouldBeGreaterThan(0);
        callbacks.Last().ShouldBe(point);
        callbacks.Count(callback => callback == point).ShouldBe(1);
        fixture.Store.Saves.ShouldBe(saves);
        fixture.Store.Manager.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "SetStateAsync").ShouldBe(stages);
        fixture.Store.Durable.Keys.ShouldBe(durable.Keys);
        foreach ((string key, byte[] bytes) in durable)
        {
            fixture.Store.Durable[key].ShouldBe(bytes);
        }
        budget.LiveBytes.ShouldBe(initial);
        foreach (IReadOnlyPayload lease in fixture.Borrowed)
        {
            Should.Throw<ObjectDisposedException>(() => lease.CopyTo(0, Span<byte>.Empty));
        }
        foreach (IBoundedPayloadWriter writer in fixture.BorrowedWriters)
        {
            Should.Throw<ObjectDisposedException>(() => writer.Write([]));
        }
    }

    /// <summary>Compares all production commitments, terminal claims, framing and ledger bytes with 14 independent preimages.</summary>
    [Fact]
    public void EveryCommandCommitmentMatchesIndependentPythonVectors()
    {
        byte[] source = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
        byte[] registry = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();
        byte[] binding = Enumerable.Range(64, 32).Select(static value => (byte)value).ToArray();
        byte[] Hash(string value) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        var command = new CommandEnvelope("01K00000000000000000000001", "tenant-a", "counter", "aggregate-1", "Fixtures.Increment",
            "{\"amount\":2}"u8.ToArray(), "correlation-1", "cause-1", "user-1", new Dictionary<string, string> { ["\uE000"] = "bmp", ["\U00010000"] = "astral", ["trace"] = "exact" });
        var budget = new EventBufferBudget();
        byte[] commandHash = DaprLogicalCommandStateCodec.CommandHash(command, budget, CancellationToken.None);
        commandHash.ShouldBe(Vector("command-route", "sha256Hex"));
        DaprLogicalCommandStateCodec.CommandHash(command with
        {
            Extensions = null
        }, budget, CancellationToken.None).ShouldBe(Vector("command-route-null-extensions", "sha256Hex"));
        DaprLogicalCommandStateCodec.CommandHash(command with
        {
            Extensions = []
        }, budget, CancellationToken.None).ShouldBe(Vector("command-route-empty-extensions", "sha256Hex"));
        byte[] c0 = DaprLogicalReplayCommitmentCodec.EffectiveGenesis(source, registry, binding, budget);
        c0.ShouldBe(Vector("effective-genesis", "sha256Hex"));
        byte[] c1 = DaprLogicalReplayCommitmentCodec.EffectiveStep(source, registry, binding, c0, 1, Hash("route-one"), "increment", 2, "json", Hash("{\"delta\":13}"), budget);
        c1.ShouldBe(Vector("effective-step-one", "sha256Hex"));
        byte[] c2 = DaprLogicalReplayCommitmentCodec.EffectiveStep(source, registry, binding, c1, 2, Hash("route-two"), "increment", 2, "json", Hash("{\"delta\":7}"), budget);
        c2.ShouldBe(Vector("effective-step-two", "sha256Hex"));
        byte[] state0 = Hash("{\"value\":0}"), state2 = Hash("{\"value\":20}"), request = Hash("page-request"), a0 = Hash("logical-genesis"), a2 = Hash("logical-two"), response = Hash("pinned-response");
        byte[] t0 = DaprLogicalReplayCommitmentCodec.TranscriptGenesis("tenant-a", "operation-1", source, registry, binding, state0, commandHash, budget);
        t0.ShouldBe(Vector("transcript-genesis", "sha256Hex"));
        var entry = new DaprLogicalPageTranscriptEntry(1, 1, request, a0, a2, 1, 2, 2, response, true, state0, state2, c0, c2);
        DaprLogicalReplayCommitmentCodec.EncodeTranscriptEntry(entry).ShouldBe(Vector("page-transcript-entry", "preimageHex"));
        byte[] terminal = DaprLogicalReplayCommitmentCodec.TranscriptStep("tenant-a", "operation-1", source, registry, binding, t0, entry, budget);
        terminal.ShouldBe(Vector("transcript-step", "sha256Hex"));
        var issued = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        var claim = new DaprLogicalCommandStateClaim("tenant-a", "counter", "aggregate-1", "Counter", "Fixtures.Increment", command.MessageId, commandHash,
            source, 4, 2, registry, binding, "operation-1", "owner-1", 1, 1, 2, a2, Hash("prefix-claim"), Hash("source-proof"), response, state2, c2, terminal, issued, issued.AddMinutes(5));
        byte[] encoded = DaprLogicalCommandStateCodec.Encode(claim);
        encoded.ShouldBe(Vector("completed-command-state-claim", "preimageHex"));
        DaprLogicalCommandStateCodec.Encode(DaprLogicalCommandStateCodec.Decode(encoded)).ShouldBe(encoded);
        using var signed = new DaprLogicalSignedClaim(encoded.ToArray(), "logical-key-1", Enumerable.Range(0, 64).Select(static value => (byte)value).ToArray(), budget.Reserve(encoded.Length + 256));
        byte[] proof = DaprLogicalCommandStateProofCodec.Encode(signed);
        proof.ShouldBe(Vector("command-proof-framing", "preimageHex"));
        var ledger = new DaprReplayPageLedger(1, 1, request, a0, a2, 1, 2, 2, response, true)
        {
            PriorStateHash = state0,
            CanonicalStateHash = state2,
            PreviousEffectiveChainHash = c0,
            EffectiveChainHash = c2,
            PreviousTranscriptHash = t0,
            TranscriptHash = terminal,
            CommandProofHash = SHA256.HashData(proof)
        };
        DaprReplayLedgerCodec.Encode(ledger).ShouldBe(Vector("committed-ledger-two", "preimageHex"));
        var empty = entry with
        {
            Accumulator = a0,
            EndSequence = 0,
            Count = 0,
            CanonicalStateHash = state0,
            EffectiveChain = c0
        };
        DaprLogicalReplayCommitmentCodec.EncodeTranscriptEntry(empty).ShouldBe(Vector("empty-page-transcript-entry", "preimageHex"));
        DaprLogicalReplayCommitmentCodec.TranscriptStep("tenant-a", "operation-1", source, registry, binding, t0, empty, budget).ShouldBe(Vector("empty-page-transcript-step", "sha256Hex"));
    }

    /// <summary>Preserves empty and multi-page genesis, exact retry proof bytes, and privately decoded processor state.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task CompletedOwnerRoutesOnlyPrivateCanonicalStateAndRetainsExactRetry(int events, bool asynchronous)
    {
        CommandEnvelope command = Command;
        await using var fixture = new DaprLogicalReconstructionFixture(events, command: command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, events);
        byte[] proof = fixture.Store.Get<byte[]>(Proof);
        DaprReplayOperationRecord operation = fixture.Store.Get<DaprReplayOperationRecord>(Operation);
        operation.EffectiveChainHash!.Length.ShouldBe(32);
        operation.TranscriptHash!.Length.ShouldBe(32);
        operation.CommandProofHash.ShouldBe(SHA256.HashData(proof));
        int saves = fixture.Store.Saves;
        int applies = fixture.Applies;
        using DaprReplayOperationResult retry = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, source, fixture.Source.Trust,
            fixture.Source.Key, "owner", 1, Math.Max(1, events), "page-" + Math.Max(1, events), 1, CancellationToken.None);
        retry.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        fixture.Store.Saves.ShouldBe(saves);
        fixture.Applies.ShouldBe(applies);
        fixture.Store.Get<byte[]>(Proof).ShouldBe(proof);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        var processor = new DaprLogicalCommandStateProcessor();
        using ServiceProvider provider = Provider(processor, asynchronous);
        _ = await DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(command, null), completed);
        processor.Calls.ShouldBe(1);
        processor.Value.ShouldBe(events);
        fixture.Applies.ShouldBe(applies);
        foreach (var lease in fixture.Borrowed)
        {
            Should.Throw<ObjectDisposedException>(() => lease.CopyTo(0, Span<byte>.Empty));
        }
        await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(command, null), completed));
        processor.Calls.ShouldBe(1);
    }

    /// <summary>Refuses caller command/state/wire substitutions and expired private authority before processor invocation.</summary>
    [Theory]
    [InlineData("command")]
    [InlineData("state")]
    [InlineData("wire")]
    [InlineData("disposed")]
    [InlineData("owner")]
    [InlineData("registry")]
    [InlineData("source")]
    [InlineData("proof")]
    [InlineData("old-ledger")]
    [InlineData("old-response")]
    [InlineData("old-state")]
    [InlineData("final-state")]
    [InlineData("next-ledger")]
    [InlineData("next-response")]
    [InlineData("next-state")]
    public async Task ActualParticipantAndCallerSubstitutionsRefuseBeforeProcessor(string kind)
    {
        CommandEnvelope command = Command;
        await using var fixture = new DaprLogicalReconstructionFixture(2, command: command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 2);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        var request = new DomainServiceRequest(command, null);
        if (kind == "command")
        {
            request = request with
            {
                Command = command with
                {
                    Payload = "{\"changed\":true}"u8.ToArray()
                }
            };
        }
        if (kind == "state")
        {
            request = request with
            {
                CurrentState = new DaprLogicalReconstructionTestState()
            };
        }
        if (kind == "wire")
        {
            request = request with
            {
                CommandStateProof = fixture.Store.Get<byte[]>(Proof)
            };
        }
        if (kind == "disposed")
        {
            completed.Dispose();
        }
        if (kind == "owner")
        {
            await fixture.Owner.DisposeAsync();
        }
        if (kind == "registry")
        {
            fixture.Source.Registry.Dispose();
        }
        if (kind == "source")
        {
            fixture.Source.Metadata = fixture.Source.Metadata! with
            {
                CurrentSequence = 3
            };
        }
        if (kind == "proof")
        {
            byte[] bytes = fixture.Store.Get<byte[]>(Proof);
            bytes[^1] ^= 1;
            fixture.Store.Put(Proof, bytes);
        }
        if (kind == "old-ledger")
        {
            DaprReplayPageLedger ledger = fixture.Store.Get<DaprReplayPageLedger>("logical-replay:ledger:1");
            fixture.Store.Put("logical-replay:ledger:1", ledger with
            {
                RequestHash = new byte[32]
            });
        }
        if (kind == "old-response")
        {
            byte[] bytes = fixture.Store.Get<byte[]>("logical-replay:response:1");
            bytes[^1] ^= 1;
            fixture.Store.Put("logical-replay:response:1", bytes);
        }
        if (kind == "old-state")
        {
            fixture.Store.Put("logical-replay:state:1", "{\"value\":99}"u8.ToArray());
        }
        if (kind == "final-state")
        {
            fixture.Store.Put("logical-replay:final-state:v1", "{\"value\":99}"u8.ToArray());
        }
        if (kind == "next-ledger")
        {
            fixture.Store.Put("logical-replay:ledger:3", fixture.Store.Get<DaprReplayPageLedger>("logical-replay:ledger:2"));
        }
        if (kind == "next-response")
        {
            fixture.Store.Put("logical-replay:response:3", "orphan"u8.ToArray());
        }
        if (kind == "next-state")
        {
            fixture.Store.Put("logical-replay:state:3", "{\"value\":2}"u8.ToArray());
        }
        var processor = new DaprLogicalCommandStateProcessor();
        using ServiceProvider provider = Provider(processor, true);
        await Should.ThrowAsync<Exception>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, request, completed));
        processor.Calls.ShouldBe(0);
    }

    /// <summary>Independently corrupts an earlier participant during final save; recovery retains uncertainty and refuses command authority.</summary>
    [Theory]
    [InlineData("commit-throw", true)]
    [InlineData("normal", false)]
    [InlineData("no-commit", false)]
    [InlineData("partial", false)]
    public async Task TerminalSaveRequiresEveryPriorParticipantAndExactAtomicProof(string mode, bool expectedProven)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(2, command: Command);
        DaprLogicalSourceBinding source = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await fixture.Owner.BeginAsync(fixture.Source.Source, source, fixture.Source.Trust, "owner", null, CancellationToken.None);
        using var first = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, source, fixture.Source.Trust, fixture.Source.Key, "owner", 1, 1, "page-1", 1, CancellationToken.None);
        fixture.Store.SaveMode = mode;
        if (mode == "normal")
        {
            fixture.Store.AfterSave = () => fixture.Store.Put("logical-replay:state:1", "{\"value\":99}"u8.ToArray());
        }
        using var result = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, source, fixture.Source.Trust, fixture.Source.Key, "owner", 1, 2, "page-2", 1, CancellationToken.None);
        result.Outcome.ShouldBe(expectedProven ? DaprReplayCommitOutcome.Proven : mode == "no-commit" ? DaprReplayCommitOutcome.NoCommit : DaprReplayCommitOutcome.Indeterminate);
        if (expectedProven)
        {
            using var completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None));
        }
    }

    /// <summary>Freezes prior history before callbacks and rejects callback mutation before any successor save.</summary>
    [Theory]
    [InlineData("read")]
    [InlineData("apply")]
    [InlineData("write")]
    public async Task LaterPageCallbackCannotReplaceAdmittedPredecessorHistory(string boundary)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(2, command: Command);
        DaprLogicalSourceBinding source = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        using var begin = await fixture.Owner.BeginAsync(fixture.Source.Source, source, fixture.Source.Trust, "owner", null, CancellationToken.None);
        using var first = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, source, fixture.Source.Trust, fixture.Source.Key, "owner", 1, 1, "page-1", 1, CancellationToken.None);
        int saves = fixture.Store.Saves;
        byte[] pointer = fixture.Store.Durable[Operation].ToArray();
        fixture.Hook = (point, _) =>
        {
            if (point == boundary)
            {
                fixture.Store.Put("logical-replay:state:1", "{\"value\":99}"u8.ToArray());
            }
        };
        (await Should.ThrowAsync<InvalidOperationException>(() => fixture.Owner.ExecutePageAsync(fixture.Source.Source, source, fixture.Source.Trust,
            fixture.Source.Key, "owner", 1, 2, "page-2", 1, CancellationToken.None))).Message.ShouldContain("predecessor changed");
        fixture.Store.Saves.ShouldBe(saves);
        fixture.Store.Durable[Operation].ShouldBe(pointer);
        fixture.Store.Durable.ContainsKey(Proof).ShouldBeFalse();
        fixture.Store.Durable.ContainsKey("logical-replay:ledger:2").ShouldBeFalse();
    }

    /// <summary>Preserves original cancellation after a throwing state callback and clears private captured state before releasing its budget.</summary>
    [Fact]
    public async Task ThrowingCancelledDecoderRefusesBeforeProcessorAndClearsCapturedBytes()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        EventBufferBudget budget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        int initial = budget.LiveBytes;
        PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        ImmutablePayload state = (ImmutablePayload)typeof(PrivateLogicalCommandState).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(completed)!;
        byte[] image = (byte[])typeof(ImmutablePayload).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state)!;
        using var cancellation = new CancellationTokenSource();
        fixture.Hook = (point, _) =>
        {
            if (point == "read")
            {
                cancellation.Cancel();
                throw new InvalidOperationException("decoder sentinel");
            }
        };
        var processor = new DaprLogicalCommandStateProcessor();
        using ServiceProvider provider = Provider(processor, true);
        (await Should.ThrowAsync<OperationCanceledException>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed, cancellation.Token))).CancellationToken.ShouldBe(cancellation.Token);
        processor.Calls.ShouldBe(0);
        completed.Dispose();
        image.ShouldAllBe(static item => item == 0);
        budget.LiveBytes.ShouldBe(initial);
    }

    /// <summary>Stops every later actual router callback after loss or original cancellation, even when the callback throws.</summary>
    [Theory]
    [InlineData("write", "loss", true)]
    [InlineData("write", "cancel", true)]
    [InlineData("stage", "loss", false)]
    [InlineData("stage", "cancel", true)]
    [InlineData("sync", "loss", true)]
    [InlineData("sync", "cancel", true)]
    [InlineData("async", "loss", false)]
    [InlineData("async", "cancel", true)]
    [InlineData("producer", "loss", false)]
    [InlineData("producer", "cancel", true)]
    public async Task ActualRouterCallbackFencesPreventLaterCallbacksAndResultRelease(string boundary, string refusal, bool throws)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        EventBufferBudget budget = (EventBufferBudget)typeof(DaprReplayOperationOwner).GetField("_bufferBudget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Owner)!;
        int initial = budget.LiveBytes;
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        var durable = fixture.Store.Durable.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToArray());
        using var cancellation = new CancellationTokenSource();
        void Refuse()
        {
            if (refusal == "cancel")
            {
                cancellation.Cancel();
            }
            else
            {
                fixture.Source.Registry.CapabilityLoss.ObserveViolation();
            }
            if (throws)
            {
                throw new InvalidOperationException("throwing callback sentinel");
            }
        }
        if (boundary == "write")
        {
            fixture.Hook = (point, _) =>
            {
                if (point == "write")
                {
                    Refuse();
                }
            };
        }
        var stage = new DaprLogicalCommandStateAdmissionStage { Hook = boundary == "stage" ? Refuse : null };
        var processor = new DaprLogicalCommandStateProcessor
        {
            Hook = boundary is "sync" or "async" ? Refuse : null,
            Result = new DomainResult([new DaprLogicalCommandStateTestEvent(), new DaprLogicalCommandStateTestEvent()])
        };
        int serializers = 0;
        Stream? borrowed = null;
        var producer = new BoundedV1DomainResultProducer([new BoundedV1EventSerialization(typeof(DaprLogicalCommandStateTestEvent), "result", "json", 32,
            (_, stream, _) =>
            {
                serializers++;
                borrowed = stream;
                stream.Write("{}"u8);
                if (boundary == "producer")
                {
                    Refuse();
                }
                return Task.CompletedTask;
            })]);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainServiceAdmissionStage>(stage);
        if (boundary == "sync")
        {
            services.AddKeyedSingleton<IDomainProcessor>("d", processor);
        }
        else
        {
            services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        }
        services.AddKeyedSingleton("d", producer);
        using ServiceProvider provider = services.BuildServiceProvider();
        Exception error = await Should.ThrowAsync<Exception>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed, cancellation.Token));
        if (refusal == "cancel")
        {
            error.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(cancellation.Token);
        }
        stage.Calls.ShouldBe(boundary == "write" ? 0 : 1);
        processor.Calls.ShouldBe(boundary is "write" or "stage" ? 0 : 1);
        serializers.ShouldBe(boundary == "producer" ? 1 : 0);
        if (borrowed is not null)
        {
            Should.Throw<Exception>(() => borrowed.WriteByte(0));
        }
        foreach (string key in durable.Keys)
        {
            fixture.Store.Durable[key].ShouldBe(durable[key]);
        }
        fixture.Store.Durable.Keys.ShouldBe(durable.Keys);
        completed.Dispose();
        budget.LiveBytes.ShouldBe(initial);
    }

    /// <summary>Checks exact expiry at intake and rejects historical, malformed, reordered or trailing purpose-07 claim bytes.</summary>
    [Fact]
    public async Task ExpiryAndStrictPurposeSevenSchemaRefuseWithoutCommandAuthority()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        var time = new DaprLogicalReplayTimeProvider { Now = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero) };
        using DaprLogicalClaimTrust trust = fixture.Source.NewTrust(time: time);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1, trust);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, trust, CancellationToken.None);
        byte[] proof = fixture.Store.Get<byte[]>(Proof);
        var budget = new EventBufferBudget();
        using (DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim> verified = DaprLogicalCommandStateProofCodec.Verify(proof, trust, budget, CancellationToken.None))
        {
            byte[] encoded = DaprLogicalCommandStateCodec.Encode(verified.Value);
            budget.LiveBytes.ShouldBeGreaterThanOrEqualTo(encoded.Length * 4 + 4096);
            byte[] tag = encoded.ToArray();
            tag["HX-EV-DAPR-COMMAND-STATE-1\0"u8.Length + 3] = 2;
            byte[] historical = encoded.ToArray();
            historical[6] = (byte)'X';
            foreach (byte[] malformed in new[] { tag, historical, encoded[..^1], [.. encoded, (byte)0] })
            {
                Should.Throw<ArgumentException>(() => DaprLogicalCommandStateCodec.Decode(malformed));
            }
        }
        time.Now = time.Now.AddMinutes(5);
        var processor = new DaprLogicalCommandStateProcessor();
        using ServiceProvider provider = Provider(processor, true);
        (await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed))).Message.ShouldContain("validity");
        processor.Calls.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Retains actual produced wire-array charges across a yielding final actual-owner fence and clears before releasing refused capacity.</summary>
    [Fact]
    public async Task ProducedResultRemainsChargedThroughYieldingFinalOwnerFenceAndClearsOnRefusal()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        EventBufferBudget budget = completed.Budget;
        int initial = budget.LiveBytes;
        var producer = new BoundedV1DomainResultProducer([new BoundedV1EventSerialization(typeof(DaprLogicalCommandStateTestEvent), "result", "json", 4096,
            (_, stream, _) => { stream.Write("{}"u8); return Task.CompletedTask; })]);
        PrivateProducedDomainResult produced = await producer.ProduceOwnedAsync(new DomainResult([new DaprLogicalCommandStateTestEvent()]), CancellationToken.None, completed.RequireCurrentAsync, budget);
        byte[] bytes = produced.Wire.Events.Single().Payload;
        budget.LiveBytes.ShouldBeGreaterThan(initial + bytes.Length);
        await Task.Yield();
        budget.LiveBytes.ShouldBeGreaterThan(initial + bytes.Length);
        fixture.Store.Put("logical-replay:final-state:v1", "{\"value\":99}"u8.ToArray());
        await Should.ThrowAsync<InvalidOperationException>(() => completed.RequireCurrentAsync(CancellationToken.None));
        bytes.ShouldBe("{}"u8.ToArray());
        produced.Dispose();
        bytes.ShouldAllBe(static value => value == 0);
        budget.LiveBytes.ShouldBe(initial);
    }

    /// <summary>Uses an actually yielding addressed owner fence to observe expired state and producer facades during the router invocation.</summary>
    [Fact]
    public async Task YieldingActualOwnerFenceSeesExpiredCommandAndProducerBorrowedFacades()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        int reads = fixture.Borrowed.Count, writes = fixture.BorrowedWriters.Count;
        Stream? borrowed = null;
        int observed = 0;
        _ = fixture.Source.SourceState.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Yield();
            call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            foreach (IReadOnlyPayload lease in fixture.Borrowed.Skip(reads))
            {
                observed++;
                Should.Throw<ObjectDisposedException>(() => lease.CopyTo(0, Span<byte>.Empty));
            }
            foreach (IBoundedPayloadWriter writer in fixture.BorrowedWriters.Skip(writes))
            {
                Should.Throw<ObjectDisposedException>(() => writer.Write([]));
            }
            if (borrowed is not null)
            {
                Should.Throw<ObjectDisposedException>(() => borrowed.WriteByte(0));
            }
            return new ConditionalValue<AggregateMetadata>(true, fixture.Source.Metadata!);
        });
        var processor = new DaprLogicalCommandStateProcessor { Result = new DomainResult([new DaprLogicalCommandStateTestEvent()]) };
        var producer = new BoundedV1DomainResultProducer([new BoundedV1EventSerialization(typeof(DaprLogicalCommandStateTestEvent), "result", "json", 32,
            (_, stream, _) => { borrowed = stream; stream.Write("{}"u8); return Task.CompletedTask; })]);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        services.AddKeyedSingleton("d", producer);
        using ServiceProvider provider = services.BuildServiceProvider();
        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed);
        wire.Events.Single().Payload.ShouldBe("{}"u8.ToArray());
        processor.Calls.ShouldBe(1);
        observed.ShouldBeGreaterThan(0);
    }

    /// <summary>Refuses an admission stage's state mutation before the next stage or either processor observes it.</summary>
    [Fact]
    public async Task StageStateMutationRefusesBeforeLaterStageOrProcessor()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        byte[] canonical = fixture.Store.Get<byte[]>("logical-replay:final-state:v1");
        var first = new DaprLogicalCommandStateAdmissionStage { ContextHook = context => ((DaprLogicalReconstructionTestState)context.CurrentState!).Value = 99 };
        var later = new DaprLogicalCommandStateAdmissionStage();
        var processor = new DaprLogicalCommandStateProcessor();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainServiceAdmissionStage>(first);
        services.AddSingleton<IDomainServiceAdmissionStage>(later);
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        services.AddKeyedSingleton("d", new BoundedV1DomainResultProducer([]));
        using ServiceProvider provider = services.BuildServiceProvider();
        (await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed))).Message.ShouldContain("admission stage changed");
        first.Calls.ShouldBe(1);
        later.Calls.ShouldBe(0);
        processor.Calls.ShouldBe(0);
        fixture.Store.Get<byte[]>("logical-replay:final-state:v1").ShouldBe(canonical);
    }

    /// <summary>Rechecks processor-visible private command bytes after an actual owner await before dispatch.</summary>
    [Fact]
    public async Task YieldingOwnerFenceCommandSubstitutionRefusesBeforeProcessor()
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        CommandEnvelope? retained = null;
        bool armed = false;
        bool changed = false;
        var stage = new DaprLogicalCommandStateAdmissionStage { ContextHook = context => retained = context.Command };
        _ = fixture.Source.SourceState.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Yield();
            call.Arg<CancellationToken>().ThrowIfCancellationRequested();
            if (armed && !changed)
            {
                retained!.Payload[0] ^= 1;
                changed = true;
            }
            return new ConditionalValue<AggregateMetadata>(true, fixture.Source.Metadata!);
        });
        var processor = new DaprLogicalCommandStateProcessor();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainServiceAdmissionStage>(stage);
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", (_, _) => { armed = true; return processor; });
        services.AddKeyedSingleton("d", new BoundedV1DomainResultProducer([]));
        using ServiceProvider provider = services.BuildServiceProvider();
        await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed));
        changed.ShouldBeTrue();
        stage.Calls.ShouldBe(1);
        processor.Calls.ShouldBe(0);
    }

    /// <summary>Fences each actual result list, virtual payload and serialized metadata getter before any later getter.</summary>
    [Theory]
    [InlineData("count", false)]
    [InlineData("count", true)]
    [InlineData("indexer", false)]
    [InlineData("indexer", true)]
    [InlineData("result", false)]
    [InlineData("result", true)]
    [InlineData("alias", false)]
    [InlineData("alias", true)]
    [InlineData("format", false)]
    [InlineData("format", true)]
    [InlineData("metadata", false)]
    [InlineData("metadata", true)]
    [InlineData("contract", false)]
    [InlineData("contract", true)]
    [InlineData("version", false)]
    [InlineData("version", true)]
    [InlineData("bytes", false)]
    [InlineData("bytes", true)]
    public async Task ResultGetterLossStopsBeforeEveryLaterGetter(string point, bool cancellation)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        var payload = new DaprLogicalCommandStateSerializedEvent();
        var inputs = new DaprLogicalCommandStateEventList(payload);
        var result = new DaprLogicalCommandStateResult(inputs);
        var calls = new List<string>();
        using var token = new CancellationTokenSource();
        void Getter(string callback)
        {
            calls.Add(callback);
            if (callback == point)
            {
                if (cancellation)
                {
                    token.Cancel();
                    throw new InvalidOperationException("throwing getter sentinel");
                }
                fixture.Source.Registry.CapabilityLoss.ObserveViolation();
            }
        }
        inputs.Hook = Getter;
        payload.Hook = Getter;
        result.Getter = () => { Getter("result"); return null; };
        var processor = new DaprLogicalCommandStateProcessor { Result = result };
        int serializers = 0;
        var producer = new BoundedV1DomainResultProducer([new BoundedV1EventSerialization(typeof(DaprLogicalCommandStateSerializedEvent), "result", "json", 32,
            (_, _, _) => { serializers++; return Task.CompletedTask; })]);
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        services.AddKeyedSingleton("d", producer);
        using ServiceProvider provider = services.BuildServiceProvider();
        Exception error = await Should.ThrowAsync<Exception>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed, token.Token));
        if (cancellation)
        {
            error.ShouldBeAssignableTo<OperationCanceledException>().CancellationToken.ShouldBe(token.Token);
        }
        calls.Last().ShouldBe(point);
        calls.ShouldBe(new[] { "count", "indexer", "result", "alias", "format", "metadata", "contract", "version", "bytes" }.TakeWhile(item => item != point).Append(point));
        processor.Calls.ShouldBe(1);
        serializers.ShouldBe(0);
    }

    /// <summary>Requires bounded production even for no-op and admits its virtual payload against the same composed budget.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoOpCannotBypassBoundedResultPayloadAdmission(bool declaredProducer)
    {
        await using var fixture = new DaprLogicalReconstructionFixture(1, command: Command);
        DaprLogicalSourceBinding source = await CompleteAsync(fixture, 1);
        using PrivateLogicalCommandState completed = await fixture.Owner.CaptureCompletedCommandStateAsync(fixture.Source.Source, source, fixture.Source.Trust, CancellationToken.None);
        int getters = 0;
        var result = new DaprLogicalCommandStateResult([]) { Getter = () => { getters++; return new string('x', 4 * 1024 * 1024); } };
        var processor = new DaprLogicalCommandStateProcessor { Result = result };
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        if (declaredProducer)
        {
            services.AddKeyedSingleton("d", new BoundedV1DomainResultProducer([]));
        }
        using ServiceProvider provider = services.BuildServiceProvider();
        await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessCompletedLogicalAsync(provider, new(Command, null), completed));
        processor.Calls.ShouldBe(1);
        getters.ShouldBe(declaredProducer ? 1 : 0);
    }

    private static async Task<DaprLogicalSourceBinding> CompleteAsync(DaprLogicalReconstructionFixture fixture, int events, DaprLogicalClaimTrust? selectedTrust = null)
    {
        DaprLogicalSourceBinding source = await fixture.Source.Source.CaptureBindingAsync("r", null, CancellationToken.None);
        DaprLogicalClaimTrust trust = selectedTrust ?? fixture.Source.Trust;
        using var begin = await fixture.Owner.BeginAsync(fixture.Source.Source, source, trust, "owner", null, CancellationToken.None);
        begin.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
        for (int ordinal = 1; ordinal <= Math.Max(1, events); ordinal++)
        {
            using var page = await fixture.Owner.ExecutePageAsync(fixture.Source.Source, source, trust, fixture.Source.Key, "owner", 1, ordinal, "page-" + ordinal, 1, CancellationToken.None);
            page.Outcome.ShouldBe(DaprReplayCommitOutcome.Proven);
            page.IsComplete.ShouldBe(ordinal == Math.Max(1, events));
        }
        return source;
    }

    private static ServiceProvider Provider(DaprLogicalCommandStateProcessor processor, bool asynchronous)
    {
        var services = new ServiceCollection();
        if (asynchronous)
        {
            services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        }
        else
        {
            services.AddKeyedSingleton<IDomainProcessor>("d", processor);
        }
        services.AddKeyedSingleton("d", new BoundedV1DomainResultProducer([]));
        return services.BuildServiceProvider();
    }

    private static byte[] Vector(string name, string field)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory!.FullName,
            "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-command-state-2026-10-08/vectors.json")));
        return Convert.FromHexString(vectors.RootElement.GetProperty("vectors").EnumerateArray().Single(item => item.GetProperty("name").GetString() == name).GetProperty(field).GetString()!);
    }

}
