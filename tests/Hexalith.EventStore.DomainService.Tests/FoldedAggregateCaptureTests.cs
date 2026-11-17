using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.FoldedSnapshotFixture;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Actual convention discovery, dispatch, bounded wire capture and both legacy replay routes.</summary>
public sealed class FoldedAggregateCaptureTests
{
    private const string Domain = "folded-orders";

    [Theory]
    [InlineData(1)]
    [InlineData(51)]
    [InlineData(101)]
    public async Task FullPrefixAndRestoredFoldPlusTailHaveCompleteMutableStateAndCommandParity(int cut)
    {
        using ServiceProvider provider = Provider();
        var commands = new List<FoldedOrderCommand> { new("create", "", "Order 日本語") };
        commands.AddRange(Enumerable.Range(0, 100).Select(index => new FoldedOrderCommand("set", "key-" + index, "value-" + index)));
        commands.AddRange([new("remove", "key-0", ""), new("rename", "", "Renamed"),
            new("close", "", ""), new("reject", "", ""), new("open", "", "")]);
        var prefix = new List<EventEnvelope>();
        JsonElement snapshot = default;
        for (int index = 0; index < commands.Count; index++)
        {
            DomainServiceRequest request = Request(commands[index], new DomainServiceCurrentState(null, prefix.ToArray(), 0, prefix.Count), include: index + 1 == cut);
            DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);
            foreach (DomainServiceWireEvent payload in wire.Events) prefix.Add(Event(payload, prefix.Count + 1));
            if (index + 1 == cut) snapshot = JsonSerializer.Deserialize<JsonElement>(wire.PostCommandStateJson.ShouldNotBeNull());
        }

        var command = new FoldedOrderCommand("set", "final", "persisted");
        DomainServiceWireResult full = await DomainServiceRequestRouter.ProcessAsync(provider,
            Request(command, new DomainServiceCurrentState(null, prefix.ToArray(), 0, prefix.Count)));
        DomainServiceWireResult restored = await DomainServiceRequestRouter.ProcessAsync(provider,
            Request(command, new DomainServiceCurrentState(snapshot, prefix.Skip(cut).ToArray(), cut, prefix.Count)));

        JsonNode.DeepEquals(JsonNode.Parse(full.PostCommandStateJson.ShouldNotBeNull()),
            JsonNode.Parse(restored.PostCommandStateJson.ShouldNotBeNull())).ShouldBeTrue();
        EqualBatch(full, restored);
        FoldedOrderState state = JsonSerializer.Deserialize<FoldedOrderState>(restored.PostCommandStateJson!, JsonSerializerOptions.Web)!;
        state.Name.ShouldBe("Renamed");
        state.Items.Count.ShouldBe(100);
        state.Items.ShouldNotContainKey("key-0");
        state.Items["final"].ShouldBe("persisted");
        state.Tags.Count.ShouldBe(101);
        state.HasItemHistory.ShouldBeTrue();
        state.Closed.ShouldBeFalse();
        state.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        state.ThrowOnApply.ShouldBeFalse();

        prefix.Add(Event(full.Events.ShouldHaveSingleItem(), prefix.Count + 1));
        var replay = new AggregateReconstructionRequest("tenant", Domain, nameof(FoldedOrderAggregate), "order-1", prefix.Count,
            prefix.Select(item => new ReplayEventEnvelope(item.Metadata.SequenceNumber, item.Metadata.EventTypeName,
                item.Payload, "json", 1, item.Metadata.MessageId, "correlation", "cause")).ToArray(), false, "replay");
        AggregateReconstructionResult synchronous = DomainServiceRequestRouter.Replay(provider, replay);
        AggregateReconstructionResult asynchronous = await DomainServiceRequestRouter.ReplayAsync(provider, replay);
        synchronous.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        asynchronous.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        JsonNode.DeepEquals(JsonNode.Parse(synchronous.StateJson!), JsonNode.Parse(restored.PostCommandStateJson!)).ShouldBeTrue();
        asynchronous.StateJson.ShouldBe(synchronous.StateJson);
        asynchronous.LastAppliedSequenceNumber.ShouldBe(prefix.Count);
        provider.GetRequiredKeyedService<IAggregateReplay>(Domain).ShouldBeAssignableTo<FoldedOrderAggregate>();
        provider.GetRequiredKeyedService<IAsyncDomainProcessor>(Domain).ShouldBeAssignableTo<FoldedOrderAggregate>();
    }

    [Fact]
    public async Task HandlerOnlyDictionaryAndListMutationsDoNotEnterSnapshotOrCallerState()
    {
        using ServiceProvider provider = Provider();
        FoldedOrderState source = State();
        string before = JsonSerializer.Serialize(source);
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider,
            Request(new("mutate", "committed", "value"), source));
        FoldedOrderState captured = JsonSerializer.Deserialize<FoldedOrderState>(result.PostCommandStateJson!, JsonSerializerOptions.Web)!;
        captured.Items["committed"].ShouldBe("value");
        captured.Items.ShouldNotContainKey("handler-only");
        captured.Tags.ShouldNotContain("handler-only");
        JsonSerializer.Serialize(source).ShouldBe(before);
        captured.Items["independent"] = "new";
        captured.Tags.Add("new");
        JsonSerializer.Serialize(source).ShouldBe(before);
    }

    [Theory]
    [InlineData("reject")]
    [InlineData("noop")]
    public async Task PersistedRejectionAndNoOpKeepTheirOriginalResultsAndState(string operation)
    {
        using ServiceProvider provider = Provider();
        FoldedOrderState state = State();
        string before = JsonSerializer.Serialize(state);
        DomainServiceWireResult actual = await DomainServiceRequestRouter.ProcessAsync(provider, Request(new(operation, "", ""), state));
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider, Request(new(operation, "", ""), state, include: false));
        EqualBatch(actual, baseline);
        JsonSerializer.Serialize(state).ShouldBe(before);
        if (operation == "noop")
        {
            actual.Events.ShouldBeEmpty();
            actual.PostCommandStateJson.ShouldBeNull();
        }
        else
        {
            actual.IsRejection.ShouldBeTrue();
            JsonNode.DeepEquals(JsonSerializer.SerializeToNode(state, JsonSerializerOptions.Web), JsonNode.Parse(actual.PostCommandStateJson!)).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("domain")]
    [InlineData("state")]
    [InlineData("parity")]
    [InlineData("limits")]
    [InlineData("registry")]
    [InlineData("producer")]
    public async Task MissingOrMismatchedEnrollmentNeverCapturesAndPreservesTheAdmittedBatch(string mismatch)
    {
        using ServiceProvider provider = Provider(mismatch: mismatch);
        DomainServiceRequest request = Request(new("set", "key", "value"), State()) with
        { SnapshotCaptureProfileId = mismatch == "profile" ? "different-profile" : FoldedOrderAggregate.ProfileId };
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider, request with { IncludePostCommandState = false });
        result.PostCommandStateJson.ShouldBeNull();
        EqualBatch(result, baseline);
        result.Events.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("nodes")]
    [InlineData("scratch")]
    public async Task FiniteCaptureRefusalsLeaveValidEventsAndCallerStateUnchanged(string boundary)
    {
        FoldedSnapshotCaptureLimits limits = boundary switch
        {
            "bytes" => FoldedOrderLimits.Create(stateBytes: 64),
            "nodes" => FoldedOrderLimits.Create(nodes: 1),
            _ => FoldedOrderLimits.Create(scratchBytes: 16_384),
        };
        using ServiceProvider provider = Provider(limits: limits);
        FoldedOrderState state = State();
        string before = JsonSerializer.Serialize(state);
        DomainServiceRequest request = Request(new("set", "key", "value"), state);
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider, request with { IncludePostCommandState = false });
        result.PostCommandStateJson.ShouldBeNull();
        EqualBatch(result, baseline);
        JsonSerializer.Serialize(state).ShouldBe(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalCopierOrApplyFailurePreservesTheOrdinarySuccessfulBatch(bool applyFailure)
    {
        using ServiceProvider provider = Provider();
        var aggregate = (FoldedOrderAggregate)provider.GetRequiredKeyedService<IAsyncDomainProcessor>(Domain);
        FoldedOrderState state = State();
        state.ThrowOnApply = applyFailure;
        if (!applyFailure) aggregate.OnCopy = _ => throw new InvalidOperationException("private-copy-sentinel");
        JsonElement input = JsonSerializer.SerializeToElement(state, JsonSerializerOptions.Web);
        DomainServiceRequest request = Request(new("set", "key", "value"), input);
        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider, request with { IncludePostCommandState = false });
        result.PostCommandStateJson.ShouldBeNull();
        EqualBatch(result, baseline);
        result.Events.ShouldHaveSingleItem();
        input.GetProperty("items").EnumerateObject().Count().ShouldBe(1);
    }

    [Fact]
    public async Task CancellationDuringOptionalCopyPropagatesBeforeCommandAndLeavesCallerGraphUntouched()
    {
        using ServiceProvider provider = Provider();
        var aggregate = (FoldedOrderAggregate)provider.GetRequiredKeyedService<IAsyncDomainProcessor>(Domain);
        using var cancellation = new CancellationTokenSource();
        aggregate.OnCopy = _ => { if (aggregate.Copies == 2) cancellation.Cancel(); };
        FoldedOrderState state = State();
        string before = JsonSerializer.Serialize(state);
        OperationCanceledException failure = await Should.ThrowAsync<OperationCanceledException>(() =>
            DomainServiceRequestRouter.ProcessAsync(provider, Request(new("set", "key", "value"), state), cancellation.Token));
        failure.CancellationToken.ShouldBe(cancellation.Token);
        JsonSerializer.Serialize(state).ShouldBe(before);
        aggregate.Copies.ShouldBe(2);
    }

    [Fact]
    public async Task SharedCaptureRejectsExcessEventsAndCancellationWithoutChangingTheValidResult()
    {
        var aggregate = new FoldedOrderAggregate();
        FoldedSnapshotProcessingResult processed = await aggregate.ProcessForSnapshotAsync(Request(new("set", "key", "value"), State()),
            FoldedOrderAggregate.ProfileId, aggregate.CaptureLimits, CancellationToken.None);
        DomainServiceWireEvent wire = DomainServiceWireResult.FromDomainResult(processed.Result).Events.ShouldHaveSingleItem();
        Func<IReadOnlyList<DomainServiceWireEvent>, CancellationToken, string?> capture = processed.Capture.ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => capture(Enumerable.Repeat(wire, 9).ToArray(), CancellationToken.None))
            .Message.ShouldBe("SnapshotCaptureLimitExceeded");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() => capture([wire], cancellation.Token));
        processed.Result.Events.ShouldHaveSingleItem().ShouldBeOfType<FoldedOrderChanged>();
        JsonSerializer.Deserialize<FoldedOrderState>(capture([wire], CancellationToken.None)!, JsonSerializerOptions.Web)!
            .Items["key"].ShouldBe("value");
    }

    private static ServiceProvider Provider(FoldedSnapshotCaptureLimits? limits = null, string? mismatch = null)
    {
        var services = new ServiceCollection();
        services.AddEventStore(typeof(FoldedOrderAggregate).Assembly);
        var aggregate = new FoldedOrderAggregate { CaptureLimits = limits ?? FoldedOrderLimits.Create() };
        services.AddSingleton(aggregate);
        if (mismatch != "registry")
        {
            services.AddSingleton(new FoldedSnapshotQualification(mismatch == "domain" ? "other-domain" : Domain,
                nameof(FoldedOrderAggregate), mismatch == "state" ? typeof(object) : typeof(FoldedOrderState),
                FoldedOrderAggregate.ProfileId, mismatch == "parity" ? "other-parity" : FoldedOrderAggregate.ParityId,
                mismatch == "limits" ? FoldedOrderLimits.Create(nodes: 2047) : aggregate.CaptureLimits));
            services.AddSingleton<FoldedSnapshotQualificationRegistry>();
        }
        if (mismatch != "producer")
        {
            services.AddEventStoreBoundedV1DomainSerialization(Domain, profile => profile
                .Add<FoldedOrderChanged>(typeof(FoldedOrderChanged).FullName!, "json", 16_384,
                    static (payload, sink, token) => JsonSerializer.SerializeAsync(sink, payload, EventStorePayloadSerialization.Options, token))
                .Add<FoldedOrderRejected>(typeof(FoldedOrderRejected).FullName!, "json", 16_384,
                    static (payload, sink, token) => JsonSerializer.SerializeAsync(sink, payload, EventStorePayloadSerialization.Options, token)));
        }
        return services.BuildServiceProvider();
    }

    private static DomainServiceRequest Request(FoldedOrderCommand command, object? state, bool include = true)
        => new(new CommandEnvelope("message", "tenant", Domain, "order-1", nameof(FoldedOrderCommand),
            JsonSerializer.SerializeToUtf8Bytes(command, EventStorePayloadSerialization.Options), "correlation", "cause", "user", null), state)
        { IncludePostCommandState = include, SnapshotCaptureProfileId = FoldedOrderAggregate.ProfileId };

    private static EventEnvelope Event(DomainServiceWireEvent wire, int sequence)
        => new(new EventMetadata("event-" + sequence, "order-1", nameof(FoldedOrderAggregate), "tenant", Domain,
            sequence, sequence, DateTimeOffset.UnixEpoch, "correlation", "cause", "user", "v1", wire.EventTypeName, 1, "json"), wire.Payload.ToArray(), null);

    private static FoldedOrderState State() => new()
    {
        Name = "Original", Items = new() { ["existing"] = "kept" }, Tags = ["original"],
        HasItemHistory = true, CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private static void EqualBatch(DomainServiceWireResult actual, DomainServiceWireResult expected)
    {
        JsonSerializer.SerializeToUtf8Bytes(actual.Events).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(expected.Events));
        actual.ResultPayload.ShouldBe(expected.ResultPayload);
        actual.IsRejection.ShouldBe(expected.IsRejection);
    }
}
