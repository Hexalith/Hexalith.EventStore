using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Snapshots;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exercises the qualified request-to-wire folded-state capture seam.</summary>
public sealed class FoldedSnapshotCaptureTests
{
    [Fact]
    public async Task RequestedQualifiedCaptureUsesOnceAdmittedBytesAndDetachedApplyInput()
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"));

        wire.Events.ShouldHaveSingleItem();
        Encoding.UTF8.GetString(wire.Events[0].Payload).ShouldBe("{\"amount\":1}");
        processor.ReturnedEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        using JsonDocument document = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(1);
        document.RootElement.TryGetProperty("events", out _).ShouldBeFalse();

        using var output = new MemoryStream();
        await BoundedV1WireResultResponse.WriteAsync(output, wire, CancellationToken.None);
        DomainServiceWireResult? parsed = JsonSerializer.Deserialize<DomainServiceWireResult>(
            output.ToArray(), JsonSerializerOptions.Web);
        parsed.ShouldNotBeNull().PostCommandStateJson.ShouldBe(wire.PostCommandStateJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestedCaptureAppliesAdmittedEventToNonEmptyPreCommandState(bool jsonState)
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        object state = jsonState
            ? JsonSerializer.Deserialize<JsonElement>("""{"count":41,"throwOnApply":false}""")
            : new SnapshotCaptureTestState { Count = 41 };
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with { CurrentState = state };

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        using JsonDocument document = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(42);
        if (!jsonState) { ((SnapshotCaptureTestState)state).Count.ShouldBe(41); }
    }

    [Theory]
    [InlineData(false, "test-profile")]
    [InlineData(true, "wrong-profile")]
    public async Task MissingOrMismatchedQualificationOmitsStateWithoutChangingEvents(bool include, string profileId)
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include, profileId));

        wire.PostCommandStateJson.ShouldBeNull();
        Encoding.UTF8.GetString(wire.Events[0].Payload).ShouldBe("{\"amount\":1}");
    }

    [Fact]
    public async Task CaptureApplyFailureLeavesTheOnceAdmittedEventAndReturnedGraphUnchanged()
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with
        {
            CurrentState = new SnapshotCaptureTestState { Count = 4, ThrowOnApply = true },
        };

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        wire.PostCommandStateJson.ShouldBeNull();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        processor.ReturnedEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
            request with { IncludePostCommandState = false });
        wire.Events[0].Payload.ShouldBe(baseline.Events[0].Payload);
        wire.IsRejection.ShouldBe(baseline.IsRejection);
        wire.ResultPayload.ShouldBe(baseline.ResultPayload);
    }

    [Fact]
    public async Task PersistedRejectionUsesItsOnceAdmittedEventBytesForTheFold()
    {
        var processor = new SnapshotCaptureTestProcessor { Reject = true };
        using ServiceProvider provider = BuildProvider(processor);

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"));

        wire.IsRejection.ShouldBeTrue();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":3}"u8.ToArray());
        processor.ReturnedRejectionEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        using JsonDocument document = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaptureCannotMutateOnceAdmittedWirePayload(bool throwAfterMutation)
    {
        var processor = new MutatingCaptureProcessor(throwAfterMutation);
        using ServiceProvider provider = BuildProvider(processor);

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"));

        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        wire.PostCommandStateJson.ShouldBe(throwAfterMutation ? null : "{}");
    }

    [Fact]
    public async Task CaptureCallbackCancellationWithoutCallerCancellationKeepsValidEvents()
    {
        var processor = new MutatingCaptureProcessor(false) { CancelCapture = true };
        using ServiceProvider provider = BuildProvider(processor);

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"));

        wire.PostCommandStateJson.ShouldBeNull();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCommandCopyFailureKeepsValidEventsWithoutEvidence(bool cancelCopy)
    {
        var processor = new CopyFailingProcessor(cancelCopy);
        using ServiceProvider provider = BuildProvider(processor);
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with
        {
            CurrentState = new SnapshotCaptureTestState { Count = 4 },
        };

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
            request with { IncludePostCommandState = false });

        wire.PostCommandStateJson.ShouldBeNull();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe(baseline.Events.ShouldHaveSingleItem().Payload);
        wire.IsRejection.ShouldBe(baseline.IsRejection);
        wire.ResultPayload.ShouldBe(baseline.ResultPayload);
    }

    [Fact]
    public async Task QualificationInternalCancellationSkipsCaptureAndKeepsValidEvents()
    {
        var processor = new MutatingCaptureProcessor(false)
        {
            OnQualify = () => throw new OperationCanceledException("internal qualification cancellation"),
        };
        using ServiceProvider provider = BuildProvider(processor);

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"));

        wire.PostCommandStateJson.ShouldBeNull();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
    }

    [Fact]
    public async Task QualificationCallerCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var processor = new MutatingCaptureProcessor(false)
        {
            OnQualify = () =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            },
        };
        using ServiceProvider provider = BuildProvider(processor);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "test-profile"), cancellation.Token));
    }

    private static ServiceProvider BuildProvider(IAsyncDomainProcessor processor)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        services.AddEventStoreBoundedV1DomainSerialization("d", profile => profile.Add<SnapshotCaptureTestEvent>(
            typeof(SnapshotCaptureTestEvent).FullName!, "json", 64,
            static (_, sink, _) => { sink.Write("{\"amount\":1}"u8); return Task.CompletedTask; })
            .Add<SnapshotCaptureTestRejectionEvent>(typeof(SnapshotCaptureTestRejectionEvent).FullName!, "json", 64,
                static (_, sink, _) => { sink.Write("{\"amount\":3}"u8); return Task.CompletedTask; }));
        services.AddEventStoreFoldedSnapshotQualification<SnapshotCaptureTestState>(
            "d", "aggregate", "test-profile", "test-parity-evidence", SnapshotCaptureTestLimits.Value);
        return services.BuildServiceProvider();
    }

    private sealed class MutatingCaptureProcessor(bool throwAfterMutation)
        : IAsyncDomainProcessor, IAsyncFoldedSnapshotProcessor
    {
        public bool CancelCapture { get; init; }

        public Action? OnQualify { get; init; }

        public bool IsQualified(Type stateType, string profileId, string parityEvidenceId,
            FoldedSnapshotCaptureLimits limits)
        {
            OnQualify?.Invoke();
            return stateType == typeof(SnapshotCaptureTestState)
                && profileId == "test-profile" && parityEvidenceId == "test-parity-evidence"
                && Equals(limits, SnapshotCaptureTestLimits.Value);
        }

        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState,
            CancellationToken cancellationToken)
            => Task.FromResult(DomainResult.Success([new SnapshotCaptureTestEvent { Amount = 99 }]));

        public Task<FoldedSnapshotProcessingResult> ProcessForSnapshotAsync(DomainServiceRequest request,
            string qualifiedProfileId, FoldedSnapshotCaptureLimits limits, CancellationToken cancellationToken)
            => Task.FromResult(new FoldedSnapshotProcessingResult(
                DomainResult.Success([new SnapshotCaptureTestEvent { Amount = 99 }]),
                (events, _) =>
                {
                    events[0].Payload[0] = (byte)'x';
                    if (CancelCapture) { throw new OperationCanceledException("optional callback canceled"); }
                    if (throwAfterMutation) { throw new InvalidOperationException("capture failed"); }
                    return "{}";
                }));
    }

    private sealed class CopyFailingProcessor(bool cancelCopy) : DomainProcessorBase<SnapshotCaptureTestState>
    {
        protected override FoldedSnapshotCaptureProfile<SnapshotCaptureTestState> FoldedSnapshotCapture
            => new("test-profile", "test-parity-evidence",
                new DetachedStateCapture<SnapshotCaptureTestState>(1024,
                    (_, _) =>
                    {
                        if (cancelCopy) { throw new OperationCanceledException("internal copier cancellation"); }
                        throw new InvalidOperationException("copier failed");
                    }),
                SnapshotCaptureTestLimits.Value);

        protected override Task<DomainResult> HandleAsync(CommandEnvelope command, SnapshotCaptureTestState? currentState)
            => Task.FromResult(DomainResult.Success([new SnapshotCaptureTestEvent { Amount = 99 }]));
    }

    private static DomainServiceRequest CreateRequest(bool include, string profileId)
        => new(new CommandEnvelope("01HZX6FKA8B3YTX6MBQ3AYT3PT", "tenant", "d", "aggregate-id",
            "Create", [], "01HZX6FKA8B3YTX6MBQ3AYT3PT", null, "user", null), null)
        {
            IncludePostCommandState = include,
            SnapshotCaptureProfileId = profileId,
        };
}
