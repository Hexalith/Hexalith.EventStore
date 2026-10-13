using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Snapshots;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exercises the qualified request-to-wire folded-state capture seam.</summary>
public sealed class FoldedSnapshotCaptureTests
{
    [Fact]
    public async Task PreCommandCopyPrecedesHandlerOnlyMutationAndPreservesAdmittedResult()
    {
        // HX-FS-V1 §8.2, V4/V6/V7: only persisted events advance the captured state.
        var processor = new SnapshotCaptureTestProcessor { HandlerMutation = 100, IncludeResultPayload = true };
        using ServiceProvider provider = BuildProvider(processor);
        var state = new SnapshotCaptureTestState { Count = 41 };
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with { CurrentState = state };

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
            request with { IncludePostCommandState = false, CurrentState = new SnapshotCaptureTestState { Count = 41 } });

        state.Count.ShouldBe(141);
        processor.CopiedCount.ShouldBe(41);
        processor.CapturedState.ShouldNotBeNull().Count.ShouldBe(42);
        using JsonDocument captured = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        captured.RootElement.GetProperty("count").GetInt32().ShouldBe(42);
        DomainServiceWireEvent admitted = wire.Events.ShouldHaveSingleItem();
        admitted.Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        JsonSerializer.SerializeToUtf8Bytes(admitted).ShouldBe(
            JsonSerializer.SerializeToUtf8Bytes(baseline.Events.ShouldHaveSingleItem()));
        wire.ResultPayload.ShouldBe("{\"result\":7}");
        wire.ResultPayload.ShouldBe(baseline.ResultPayload);
        wire.IsRejection.ShouldBe(baseline.IsRejection);
        processor.ReturnedEvent.ShouldNotBeNull().Amount.ShouldBe(99);
    }

    [Fact]
    public async Task OversizedEscapableInputRefusesScratchBeforeCopyOrApplyWithoutChangingResult()
    {
        // HX-FS-V1 §8.6, V6/V16: output rejection cannot admit the encoder's source-string scratch.
        FoldedSnapshotCaptureLimits limits = SnapshotCaptureTestLimits.For(
            maximumStateGraphBytes: 132_096, maximumCaptureWorkspaceBytes: 1_048_576);
        var processor = new SnapshotCaptureTestProcessor(limits) { IncludeResultPayload = true };
        using ServiceProvider provider = BuildProvider(processor, limits);
        var state = new SnapshotCaptureTestState { Count = 41, Text = new string('\u0001', 65_536) };
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with { CurrentState = state };

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
            request with { IncludePostCommandState = false });

        processor.CopiedCount.ShouldBeNull();
        processor.CapturedState.ShouldBeNull();
        state.Count.ShouldBe(41);
        wire.PostCommandStateJson.ShouldBeNull();
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        JsonSerializer.SerializeToUtf8Bytes(wire.Events[0]).ShouldBe(
            JsonSerializer.SerializeToUtf8Bytes(baseline.Events[0]));
        wire.ResultPayload.ShouldBe("{\"result\":7}");
        wire.ResultPayload.ShouldBe(baseline.ResultPayload);
        wire.IsRejection.ShouldBe(baseline.IsRejection);
    }

    [Fact]
    public async Task QualifiedDomainFoldForwardsCollisionEvidenceForActorClassification()
    {
        // HX-FS-V1 §§5.3/8.3, V8: the actor emits SnapshotStateShapeCollision instead of absent evidence.
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", new SnapshotCollisionTestProcessor());
        services.AddEventStoreFoldedSnapshotQualification<SnapshotCollisionTestState>(
            "d", "aggregate", "collision-profile", "collision-parity", SnapshotCaptureTestLimits.Value);
        services.AddEventStoreBoundedV1DomainSerialization("d", profile => profile.Add<SnapshotCaptureTestEvent>(
            typeof(SnapshotCaptureTestEvent).FullName!, "json", 64,
            static (_, sink, _) => { sink.Write("{\"amount\":1}"u8); return Task.CompletedTask; }));
        using ServiceProvider provider = services.BuildServiceProvider();

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: true, "collision-profile"));

        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        using JsonDocument evidence = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        evidence.RootElement.GetProperty("currentSequence").GetInt32().ShouldBe(1);
        evidence.RootElement.GetProperty("events").GetArrayLength().ShouldBe(0);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(
            provider, CreateRequest(include: false, "collision-profile"));
        wire.Events[0].Payload.ShouldBe(baseline.Events[0].Payload);
        wire.ResultPayload.ShouldBe(baseline.ResultPayload);
        wire.IsRejection.ShouldBe(baseline.IsRejection);
    }

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

    [Fact]
    public async Task RequestedCaptureFoldsJsonSnapshotAndTailBeforeAdmittedCommandEvent()
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor);
        var tailMetadata = new EventMetadata(
            "01HZX6FKA8B3YTX6MBQ3AYT3PX", "aggregate-id", "aggregate", "tenant", "d",
            2, 2, DateTimeOffset.UnixEpoch, "01HZX6FKA8B3YTX6MBQ3AYT3PT",
            "01HZX6FKA8B3YTX6MBQ3AYT3PT", "user", "v1",
            typeof(SnapshotCaptureTestEvent).FullName!, 1, "json");
        var current = new DomainServiceCurrentState(
            JsonSerializer.Deserialize<JsonElement>("""{"count":41,"throwOnApply":false}"""),
            [new EventEnvelope(tailMetadata, "{\"amount\":2}"u8.ToArray(), null)], 1, 2);
        JsonElement jsonCurrent = JsonSerializer.SerializeToElement(current, JsonSerializerOptions.Web);
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with
        {
            CurrentState = jsonCurrent,
        };

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(provider, request);

        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":1}"u8.ToArray());
        using JsonDocument document = JsonDocument.Parse(wire.PostCommandStateJson.ShouldNotBeNull());
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(44);
        jsonCurrent.GetProperty("snapshotState").GetProperty("count").GetInt32().ShouldBe(41);
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

    [Theory]
    [InlineData("state")]
    [InlineData("parity")]
    [InlineData("limits")]
    public async Task QualificationMismatchOmitsOnlyOptionalEvidence(string mismatch)
    {
        var processor = new SnapshotCaptureTestProcessor();
        using ServiceProvider provider = BuildProvider(processor, mismatch: mismatch);
        DomainServiceRequest request = CreateRequest(include: true, "test-profile");

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
            request with { IncludePostCommandState = false });

        result.PostCommandStateJson.ShouldBeNull();
        result.Events.ShouldHaveSingleItem().Payload.ShouldBe(baseline.Events.ShouldHaveSingleItem().Payload);
        result.IsRejection.ShouldBe(baseline.IsRejection);
        result.ResultPayload.ShouldBe(baseline.ResultPayload);
    }

    [Fact]
    public async Task RouterAdmitsExactEventCountAndByteLimitThenSkipsExcess()
    {
        FoldedSnapshotCaptureLimits countLimits = SnapshotCaptureTestLimits.For(maximumEventCount: 2);
        var countProcessor = new MutatingCaptureProcessor(false)
        {
            ExpectedLimits = countLimits,
            EventAmounts = [99, 99],
        };
        using (ServiceProvider provider = BuildProvider(countProcessor, countLimits))
        {
            DomainServiceWireResult atLimit = await DomainServiceRequestRouter.ProcessAsync(provider,
                CreateRequest(include: true, "test-profile"));
            atLimit.PostCommandStateJson.ShouldBe("{}");
            countProcessor.CaptureCalls.ShouldBe(1);
            countProcessor.EventAmounts = [99, 99, 99];
            DomainServiceWireResult overLimit = await DomainServiceRequestRouter.ProcessAsync(provider,
                CreateRequest(include: true, "test-profile"));
            overLimit.Events.Count.ShouldBe(3);
            overLimit.PostCommandStateJson.ShouldBeNull();
            countProcessor.CaptureCalls.ShouldBe(1);
        }

        FoldedSnapshotCaptureLimits byteLimits = SnapshotCaptureTestLimits.For(maximumEventBytes: 12);
        var byteProcessor = new MutatingCaptureProcessor(false) { ExpectedLimits = byteLimits };
        using ServiceProvider byteProvider = BuildProvider(byteProcessor, byteLimits);
        DomainServiceWireResult exactBytes = await DomainServiceRequestRouter.ProcessAsync(byteProvider,
            CreateRequest(include: true, "test-profile"));
        exactBytes.Events.ShouldHaveSingleItem().Payload.Length.ShouldBe(12);
        exactBytes.PostCommandStateJson.ShouldBe("{}");
        byteProcessor.EventAmounts = [98];
        DomainServiceWireResult excessBytes = await DomainServiceRequestRouter.ProcessAsync(byteProvider,
            CreateRequest(include: true, "test-profile"));
        excessBytes.Events.ShouldHaveSingleItem().Payload.Length.ShouldBe(13);
        excessBytes.PostCommandStateJson.ShouldBeNull();
        byteProcessor.CaptureCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DomainProcessorEnforcesEventCountBytesAndSerializedStateLimits()
    {
        FoldedSnapshotCaptureLimits eventLimits = SnapshotCaptureTestLimits.For(maximumEventCount: 2, maximumEventBytes: 24);
        var processor = new SnapshotCaptureTestProcessor(eventLimits);
        FoldedSnapshotProcessingResult processing = await processor.ProcessForSnapshotAsync(
            CreateRequest(include: true, "test-profile"), "test-profile", eventLimits, CancellationToken.None);
        Func<IReadOnlyList<DomainServiceWireEvent>, CancellationToken, string?> capture = processing.Capture.ShouldNotBeNull();
        var eventBytes = new DomainServiceWireEvent(typeof(SnapshotCaptureTestEvent).FullName!, "{\"amount\":1}"u8.ToArray(), "json");
        string exact = capture([eventBytes, eventBytes], CancellationToken.None).ShouldNotBeNull();
        exact.ShouldContain("\"count\":2");
        Should.Throw<InvalidOperationException>(() => capture([eventBytes, eventBytes, eventBytes], CancellationToken.None))
            .Message.ShouldBe("SnapshotCaptureLimitExceeded");
        var excessBytes = eventBytes with { Payload = "{\"amount\":10}"u8.ToArray() };
        Should.Throw<InvalidOperationException>(() => capture([eventBytes, excessBytes], CancellationToken.None))
            .Message.ShouldBe("SnapshotCaptureLimitExceeded");

        int exactStateBytes = Encoding.UTF8.GetByteCount(exact);
        FoldedSnapshotCaptureLimits stateLimits = SnapshotCaptureTestLimits.For(maximumStateJsonBytes: exactStateBytes);
        var stateProcessor = new SnapshotCaptureTestProcessor(stateLimits);
        FoldedSnapshotProcessingResult stateProcessing = await stateProcessor.ProcessForSnapshotAsync(
            CreateRequest(include: true, "test-profile"), "test-profile", stateLimits, CancellationToken.None);
        stateProcessing.Capture.ShouldNotBeNull()([eventBytes, eventBytes], CancellationToken.None).ShouldBe(exact);
        FoldedSnapshotCaptureLimits shortStateLimits = SnapshotCaptureTestLimits.For(maximumStateJsonBytes: exactStateBytes - 1);
        var shortProcessor = new SnapshotCaptureTestProcessor(shortStateLimits);
        FoldedSnapshotProcessingResult shortProcessing = await shortProcessor.ProcessForSnapshotAsync(
            CreateRequest(include: true, "test-profile"), "test-profile", shortStateLimits, CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => shortProcessing.Capture.ShouldNotBeNull()(
            [eventBytes, eventBytes], CancellationToken.None)).Message.ShouldBe("SnapshotCaptureLimitExceeded");

        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(exact));
        int nodes = 0;
        while (reader.Read()) { nodes++; }
        long graphBytes = checked(16L * exactStateBytes + 64L * nodes);
        FoldedSnapshotCaptureLimits exactShapeLimits = SnapshotCaptureTestLimits.For(
            maximumNodeCount: nodes, maximumDomainParsedStateGraphBytes: graphBytes);
        var shapeProcessor = new SnapshotCaptureTestProcessor(exactShapeLimits);
        FoldedSnapshotProcessingResult shapeProcessing = await shapeProcessor.ProcessForSnapshotAsync(
            CreateRequest(include: true, "test-profile"), "test-profile", exactShapeLimits, CancellationToken.None);
        shapeProcessing.Capture.ShouldNotBeNull()([eventBytes, eventBytes], CancellationToken.None).ShouldBe(exact);

        FoldedSnapshotCaptureLimits shortNodeLimits = SnapshotCaptureTestLimits.For(maximumNodeCount: nodes - 1);
        var shortNodeProcessor = new SnapshotCaptureTestProcessor(shortNodeLimits);
        FoldedSnapshotProcessingResult shortNodeProcessing = await shortNodeProcessor.ProcessForSnapshotAsync(
            CreateRequest(include: true, "test-profile"), "test-profile", shortNodeLimits, CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => shortNodeProcessing.Capture.ShouldNotBeNull()(
            [eventBytes, eventBytes], CancellationToken.None)).Message.ShouldBe("SnapshotCaptureLimitExceeded");

        FoldedSnapshotCaptureLimits shortGraphLimits = SnapshotCaptureTestLimits.For(
            maximumDomainParsedStateGraphBytes: graphBytes - 1);
        var shortGraphProcessor = new SnapshotCaptureTestProcessor(shortGraphLimits);
        FoldedSnapshotProcessingResult shortGraphProcessing = await shortGraphProcessor.ProcessForSnapshotAsync(
            CreateRequest(include: true, "test-profile"), "test-profile", shortGraphLimits, CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => shortGraphProcessing.Capture.ShouldNotBeNull()(
            [eventBytes, eventBytes], CancellationToken.None)).Message.ShouldBe("SnapshotCaptureLimitExceeded");
    }

    [Theory]
    [InlineData(122_880, true)]
    [InlineData(122_879, false)]
    [InlineData(16_384, false)]
    public async Task SerializerScratchIsAdmittedBeforeOptionalStateCopy(long scratchBytes, bool admitted)
    {
        // HX-FS-V1 §8.6, V6/V16: fixed buffer + encoder and depth scratch precede the copier.
        FoldedSnapshotCaptureLimits limits = SnapshotCaptureTestLimits.For(
            maximumSerializerScratchBytes: scratchBytes, maximumStateGraphBytes: 4096);
        var processor = new SnapshotCaptureTestProcessor(limits);
        using ServiceProvider provider = BuildProvider(processor, limits);
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with
        {
            CurrentState = new SnapshotCaptureTestState { Count = 41 },
        };

        DomainServiceWireResult result = await DomainServiceRequestRouter.ProcessAsync(provider, request);
        DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
            request with { IncludePostCommandState = false });

        result.Events.ShouldHaveSingleItem().Payload.ShouldBe(baseline.Events.ShouldHaveSingleItem().Payload);
        result.ResultPayload.ShouldBe(baseline.ResultPayload);
        result.IsRejection.ShouldBe(baseline.IsRejection);
        if (admitted)
        {
            processor.CapturedState.ShouldNotBeNull().Count.ShouldBe(42);
            result.PostCommandStateJson.ShouldNotBeNull();
        }
        else
        {
            processor.CapturedState.ShouldBeNull();
            result.PostCommandStateJson.ShouldBeNull();
        }
    }

    [Fact]
    public async Task TokenDenseIgnoredFieldRefusesBeforeApplyAndCapturesAtTheAdmittedBoundary()
    {
        // HX-FS-V1 §§8.2/8.6, V6/V16: payload length alone does not bound retained token tables.
        byte[] payload = Encoding.UTF8.GetBytes("{\"amount\":1,\"ignored\":[" + string.Join(',', Enumerable.Repeat("0", 128)) + "]}");
        int tokens = CountTokens(payload);
        // Outer envelopes, wrapper, captured references/prepared inputs, then 128 bytes/token.
        long exactGraphBytes = 128 + 256 + 256 + 128 + 2 * IntPtr.Size + 256 + 128L * tokens;
        FoldedSnapshotCaptureLimits exactLimits = SnapshotCaptureTestLimits.For(
            maximumDetachedEventGraphBytes: exactGraphBytes, maximumCaptureWorkspaceBytes: 512_000);
        FoldedSnapshotCaptureLimits shortLimits = SnapshotCaptureTestLimits.For(
            maximumDetachedEventGraphBytes: exactGraphBytes - 1, maximumCaptureWorkspaceBytes: 512_000);
        var refusedProcessor = new SnapshotCaptureTestProcessor(shortLimits);
        DomainServiceRequest request = CreateRequest(include: true, "test-profile") with
        {
            CurrentState = new SnapshotCaptureTestState { Count = 41 },
        };
        FoldedSnapshotProcessingResult processing = await refusedProcessor.ProcessForSnapshotAsync(
            request, "test-profile", shortLimits, CancellationToken.None);
        var admittedEvent = new DomainServiceWireEvent(typeof(SnapshotCaptureTestEvent).FullName!, payload, "json");

        Should.Throw<InvalidOperationException>(() => processing.Capture.ShouldNotBeNull()(
            [admittedEvent], CancellationToken.None)).Message.ShouldBe("SnapshotCaptureLimitExceeded");
        refusedProcessor.CapturedState.ShouldNotBeNull().Count.ShouldBe(41);
        admittedEvent.Payload.ShouldBe(payload);

        using (ServiceProvider provider = BuildProvider(refusedProcessor, shortLimits, customPayload: payload))
        {
            DomainServiceWireResult refused = await DomainServiceRequestRouter.ProcessAsync(provider, request);
            DomainServiceWireResult baseline = await DomainServiceRequestRouter.ProcessAsync(provider,
                request with { IncludePostCommandState = false });
            refused.PostCommandStateJson.ShouldBeNull();
            refusedProcessor.CapturedState.ShouldNotBeNull().Count.ShouldBe(41);
            DomainServiceWireEvent baselineEvent = baseline.Events.ShouldHaveSingleItem();
            (refused.Events.ShouldHaveSingleItem() with { Payload = baselineEvent.Payload }).ShouldBe(baselineEvent);
            refused.Events[0].Payload.ShouldBe(payload);
            refused.IsRejection.ShouldBe(baseline.IsRejection);
            refused.ResultPayload.ShouldBe(baseline.ResultPayload);
            refusedProcessor.ReturnedEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        }

        var acceptedProcessor = new SnapshotCaptureTestProcessor(exactLimits);
        using ServiceProvider acceptedProvider = BuildProvider(acceptedProcessor, exactLimits, customPayload: payload);
        DomainServiceWireResult accepted = await DomainServiceRequestRouter.ProcessAsync(acceptedProvider, request);
        accepted.Events.ShouldHaveSingleItem().Payload.ShouldBe(payload);
        acceptedProcessor.CapturedState.ShouldNotBeNull().Count.ShouldBe(42);
        using JsonDocument document = JsonDocument.Parse(accepted.PostCommandStateJson.ShouldNotBeNull());
        document.RootElement.GetProperty("count").GetInt32().ShouldBe(42);
    }

    private static int CountTokens(byte[] payload)
    {
        var reader = new Utf8JsonReader(payload);
        int count = 0;
        while (reader.Read()) { count++; }
        return count;
    }

    private static ServiceProvider BuildProvider(IAsyncDomainProcessor processor,
        FoldedSnapshotCaptureLimits? limits = null, string? mismatch = null, byte[]? customPayload = null)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAsyncDomainProcessor>("d", processor);
        services.AddEventStoreBoundedV1DomainSerialization("d", profile => profile.Add<SnapshotCaptureTestEvent>(
            typeof(SnapshotCaptureTestEvent).FullName!, "json", Math.Max(64, customPayload?.Length ?? 0),
            (value, sink, _) =>
            {
                if (customPayload is not null) { sink.Write(customPayload); }
                else { sink.Write(value.Amount == 98 ? "{\"amount\":10}"u8 : "{\"amount\":1}"u8); }
                return Task.CompletedTask;
            })
            .Add<SnapshotCaptureTestRejectionEvent>(typeof(SnapshotCaptureTestRejectionEvent).FullName!, "json", 64,
                static (_, sink, _) => { sink.Write("{\"amount\":3}"u8); return Task.CompletedTask; }));
        FoldedSnapshotCaptureLimits declaration = limits ?? SnapshotCaptureTestLimits.Value;
        if (mismatch == "state")
        {
            services.AddEventStoreFoldedSnapshotQualification<object>(
                "d", "aggregate", "test-profile", "test-parity-evidence", declaration);
        }
        else
        {
            services.AddEventStoreFoldedSnapshotQualification<SnapshotCaptureTestState>(
                "d", "aggregate", "test-profile", mismatch == "parity" ? "other-parity" : "test-parity-evidence",
                mismatch == "limits" ? SnapshotCaptureTestLimits.For(maximumNodeCount: 99) : declaration);
        }
        return services.BuildServiceProvider();
    }

    private sealed class MutatingCaptureProcessor(bool throwAfterMutation)
        : IAsyncDomainProcessor, IAsyncFoldedSnapshotProcessor
    {
        public bool CancelCapture { get; init; }

        public Action? OnQualify { get; init; }

        public FoldedSnapshotCaptureLimits ExpectedLimits { get; init; } = SnapshotCaptureTestLimits.Value;

        public int[] EventAmounts { get; set; } = [99];

        public int CaptureCalls { get; private set; }

        public bool IsQualified(Type stateType, string profileId, string parityEvidenceId,
            FoldedSnapshotCaptureLimits limits)
        {
            OnQualify?.Invoke();
            return stateType == typeof(SnapshotCaptureTestState)
                && profileId == "test-profile" && parityEvidenceId == "test-parity-evidence"
                && Equals(limits, ExpectedLimits);
        }

        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState,
            CancellationToken cancellationToken)
            => Task.FromResult(DomainResult.Success(EventAmounts.Select(amount =>
                (IEventPayload)new SnapshotCaptureTestEvent { Amount = amount }).ToArray()));

        public Task<FoldedSnapshotProcessingResult> ProcessForSnapshotAsync(DomainServiceRequest request,
            string qualifiedProfileId, FoldedSnapshotCaptureLimits limits, CancellationToken cancellationToken)
            => Task.FromResult(new FoldedSnapshotProcessingResult(
                DomainResult.Success(EventAmounts.Select(amount =>
                    (IEventPayload)new SnapshotCaptureTestEvent { Amount = amount }).ToArray()),
                (events, _) =>
                {
                    CaptureCalls++;
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
