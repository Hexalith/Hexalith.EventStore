using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

[Collection("NativeCaptureAdmission")]
public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Fact]
    public async Task TimedOutNativeCapturesKeepBoundedSlotsUntilTheirActorsActuallyComplete()
    {
        EventEnvelope[] events = [Envelope(1)];
        SourceProof proof = SourceProofCodec.Extend(SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"), events[0]);
        var inspection = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 1, false, proof);
        var release = new TaskCompletionSource<SourceProofInspection>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = System.Threading.Channels.Channel.CreateUnbounded<bool>();
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        int calls = 0;
        actor.InspectSourceProofAsync().Returns(_ => { Interlocked.Increment(ref calls); started.Writer.TryWrite(true); return release.Task; });
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), nameof(AggregateActor)).Returns(actor);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var http = new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(new ProjectionDispatchResponse(2,
            [new("widget-detail", ProjectionDispatchStatus.Retryable, null, "partial_retry")])));
        var coordinator = CreateCoordinator(Snapshot("bounded-saturated", "widget-detail"), http, out _, out _, timeProvider: clock, actorProxyFactory: proxies);
        try
        {
            for (int index = 0; index < 16; index++)
            {
                var pending = coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1)], TestContext.Current.CancellationToken);
                await started.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                clock.Advance(TimeSpan.FromSeconds(2));
                (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeTrue();
            }
            (await coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1)], TestContext.Current.CancellationToken)).ShouldBeTrue();
            Volatile.Read(ref calls).ShouldBe(16);
            JsonSerializer.Deserialize<ProjectionDispatchRequest>(http.RequestJson!, JsonSerializerOptions.Web)!.Request.NativeSourceProof.ShouldBeNull();
        }
        finally
        {
            release.TrySetResult(inspection);
            // A fresh admission probes actual worker completion, not just caller timeout.
            using var budget = new Hexalith.EventStore.Client.Streams.AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(5),
                TimeProvider.System, TestContext.Current.CancellationToken, TimeProvider.System.GetTimestamp());
            int available = 0;
            for (int attempt = 0; attempt < 1000 && available == 0; attempt++)
            {
                try { available = await budget.ReadWithAdmissionAsync(_ => Task.FromResult(1)); }
                catch (Hexalith.EventStore.Client.Streams.AuthoritativeStreamReadAdmissionException) { await Task.Delay(1, TestContext.Current.CancellationToken); }
            }
            available.ShouldBe(1);
        }
        actor.InspectSourceProofAsync().Returns(inspection);
        (await coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1)], TestContext.Current.CancellationToken)).ShouldBeTrue();
        JsonSerializer.Deserialize<ProjectionDispatchRequest>(http.RequestJson!, JsonSerializerOptions.Web)!.Request.NativeSourceProof.ShouldBe(SourceProofCodec.ToNative(proof));
    }
    [Theory]
    [InlineData("protected")]
    [InlineData("exact")]
    [InlineData("missing")]
    [InlineData("unsupported")]
    [InlineData("fault")]
    [InlineData("tampered-prefix")]
    [InlineData("replacement")]
    public async Task OriginalNativePrefixIsCarriedOnlyWhenExactAndLegacyDeliverySurvivesUnavailableProof(string scenario)
    {
        EventEnvelope[] events = [Envelope(1), Envelope(2)];
        if (scenario == "protected") events[0] = events[0] with { Extensions = new Dictionary<string, string>(
            Hexalith.EventStore.Contracts.Security.EventStorePayloadProtectionMetadataCarrier.Write((IReadOnlyDictionary<string, string>?)null,
                new(Hexalith.EventStore.Contracts.Security.PayloadProtectionState.Protected, 1, "test-protection", "test-key", "application/json", null))) };
        SourceProof proof = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        foreach (EventEnvelope item in events) proof = SourceProofCodec.Extend(proof, item);
        if (scenario == "tampered-prefix") proof = proof with { CumulativeDigest = new('a', 64) };
        var before = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 2, false, proof);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        if (scenario == "fault") actor.InspectSourceProofAsync().Returns(Task.FromException<SourceProofInspection>(new IOException("unavailable")));
        else if (scenario == "missing") actor.InspectSourceProofAsync().Returns(Task.FromResult<SourceProofInspection>(null!));
        else if (scenario == "unsupported") actor.InspectSourceProofAsync().Returns(before with { Status = SourceProofInspectionStatus.Unsupported });
        else actor.InspectSourceProofAsync().Returns(before, scenario == "replacement"
            ? before with { Proof = proof with { SourceIncarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAW" } } : before);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), nameof(AggregateActor)).Returns(actor);
        var http = new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version, [new("widget-detail", ProjectionDispatchStatus.Completed, null, null)])));
        var coordinator = CreateCoordinator(Snapshot("native-fixture", "widget-detail"), http, out var checkpoints,
            out _, actorProxyFactory: proxies);
        checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 2, Arg.Any<CancellationToken>()).Returns(true);
        (await coordinator.TryDispatchAsync(Identity, Registration(), events,
            [ProjectionEvent(1), ProjectionEvent(2)], TestContext.Current.CancellationToken)).ShouldBeTrue();
        ProjectionRequest delivered = JsonSerializer.Deserialize<ProjectionDispatchRequest>(http.RequestJson!, JsonSerializerOptions.Web)!.Request;
        if (scenario == "exact") delivered.NativeSourceProof.ShouldBe(SourceProofCodec.ToNative(proof));
        else delivered.NativeSourceProof.ShouldBeNull();
        delivered.VerifiedNativeSourceProof.ShouldBeNull();
        await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task OptionalCaptureDeadlineBoundsBothInspectionsAndPreservesCallerCancellation(bool afterFirst, bool cancel)
    {
        EventEnvelope[] events = [Envelope(1)];
        SourceProof proof = SourceProofCodec.Extend(SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"), events[0]);
        var inspection = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 1, false, proof);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalled = new TaskCompletionSource<SourceProofInspection>(TaskCreationOptions.RunContinuationsAsynchronously);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        int reads = 0;
        actor.InspectSourceProofAsync().Returns(_ =>
        {
            if (++reads == (afterFirst ? 2 : 1)) { entered.TrySetResult(); return stalled.Task; }
            return Task.FromResult(inspection);
        });
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), nameof(AggregateActor)).Returns(actor);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var http = new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(new ProjectionDispatchResponse(2,
            [new("widget-detail", ProjectionDispatchStatus.Retryable, null, "partial_retry")])));
        var coordinator = CreateCoordinator(Snapshot("bounded-native", "widget-detail"), http, out _, out _, timeProvider: clock, actorProxyFactory: proxies);
        using var caller = new CancellationTokenSource();
        Task<bool> delivery = coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1)], caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            if (cancel)
            {
                caller.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
                http.Requests.ShouldBeEmpty();
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(2));
                (await delivery.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeTrue();
                JsonSerializer.Deserialize<ProjectionDispatchRequest>(http.RequestJson!, JsonSerializerOptions.Web)!.Request.NativeSourceProof.ShouldBeNull();
            }
        }
        finally { stalled.TrySetResult(inspection); }
    }

    [Theory]
    [InlineData("append", true)]
    [InlineData("failure", true)]
    [InlineData("later-native", false)]
    public async Task OrdinaryAndCompatibleRetriesKeepSchedulerFrozenOriginalProofOrAbsence(string change, bool proven)
    {
        EventEnvelope[] events = [Envelope(1), Envelope(2)];
        SourceProof proof = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        foreach (EventEnvelope item in events) proof = SourceProofCodec.Extend(proof, item);
        var inspection = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 2, false, proof);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.InspectSourceProofAsync().Returns(proven ? inspection : inspection with { Status = SourceProofInspectionStatus.Unsupported });
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), nameof(AggregateActor)).Returns(actor);
        var http = new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(new ProjectionDispatchResponse(2,
            [new("widget-detail", ProjectionDispatchStatus.Retryable, null, "partial_retry")])));
        var scheduler = new MemoryRetryScheduler();
        var snapshot = Snapshot("frozen-native", "widget-detail");
        var coordinator = CreateCoordinator(snapshot, http, out _, out _, scheduler: scheduler, actorProxyFactory: proxies);
        await coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1), ProjectionEvent(2)], TestContext.Current.CancellationToken);
        var frozen = scheduler.Work!.NativeSourceProof;
        frozen.ShouldBe(proven ? SourceProofCodec.ToNative(proof) : null);
        if (change == "failure") actor.InspectSourceProofAsync().Returns(Task.FromException<SourceProofInspection>(new IOException()));
        else if (change == "append") actor.InspectSourceProofAsync().Returns(inspection with { CurrentSequence = 3, Proof = SourceProofCodec.Extend(proof, Envelope(3)) });
        else actor.InspectSourceProofAsync().Returns(inspection);
        await coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1), ProjectionEvent(2)], TestContext.Current.CancellationToken);
        JsonSerializer.Deserialize<ProjectionDispatchRequest>(http.RequestJson!, JsonSerializerOptions.Web)!.Request.NativeSourceProof.ShouldBe(frozen);
        await coordinator.TryDispatchCompatibleAsync(Identity, snapshot.Entries.Single(), 2, events[^1].MessageId,
            [ProjectionEvent(1), ProjectionEvent(2)], ["widget-detail"], SourceProofCodec.ToNative(proof), TestContext.Current.CancellationToken);
        JsonSerializer.Deserialize<ProjectionDispatchRequest>(http.RequestJson!, JsonSerializerOptions.Web)!.Request.NativeSourceProof.ShouldBe(frozen);
        scheduler.Work!.NativeSourceProof.ShouldBe(frozen);
    }

    [Fact]
    public async Task ActualRawEnvelopeCodecInteroperatesWithProducerJsonNativeReaderAndPersistedSdkDispatch()
    {
        EventEnvelope[] raw = [Envelope(1) with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV", CorrelationId = new('a', 32), CausationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV" },
            Envelope(2) with { MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAW", CorrelationId = new('a', 32), CausationId = "01ARZ3NDEKTSV4RRFFQ69G5FAV" }];
        SourceProof proof = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        foreach (EventEnvelope item in raw) proof = SourceProofCodec.Extend(proof, item);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.InspectSourceProofAsync().Returns(new SourceProofInspection(SourceProofInspectionStatus.Available, true, 2, false, proof));
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), nameof(AggregateActor)).Returns(actor);
        var wire = (await Hexalith.EventStore.Server.Projections.ProjectionEventWireBuilder.BuildAsync(
            Substitute.For<Hexalith.EventStore.Contracts.Security.IEventPayloadProtectionService>(), Identity, raw, TestContext.Current.CancellationToken)).Events!;
        var http = new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(new ProjectionDispatchResponse(2,
            [new("widget-detail", ProjectionDispatchStatus.Completed, null, null)])));
        var coordinator = CreateCoordinator(Snapshot("raw-native", "widget-detail"), http, out var checkpoints, out _, actorProxyFactory: proxies);
        checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 2, Arg.Any<CancellationToken>()).Returns(true);
        await coordinator.TryDispatchAsync(Identity, Registration(), raw, wire, TestContext.Current.CancellationToken);
        var transported = JsonSerializer.Deserialize<ProjectionDispatchRequest>(http.RequestJson!, JsonSerializerOptions.Web)!;
        transported.Request.VerifiedNativeSourceProof.ShouldBeNull();
        var gateway = Substitute.For<Hexalith.EventStore.Client.Gateway.IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<Hexalith.EventStore.Contracts.Streams.StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var query = call.Arg<Hexalith.EventStore.Contracts.Streams.StreamReadRequest>();
            Hexalith.EventStore.Contracts.Streams.StreamReadEvent[] values = query.ToSequence == 0 ? [] : raw.Select(item =>
                new Hexalith.EventStore.Contracts.Streams.StreamReadEvent(item.SequenceNumber, item.EventTypeName, item.Payload,
                    item.SerializationFormat, item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId, item.Timestamp, item.UserId)
                { GlobalPosition = item.GlobalPosition, PayloadVersion = item.PayloadVersion, NativeEventDigest = SourceProofCodec.ComputeEventHash(item) }).ToArray();
            return new Hexalith.EventStore.Contracts.Streams.StreamReadPage(Identity.TenantId, Identity.Domain, Identity.AggregateId, values,
                new(query.FromSequence, query.ToSequence, values.LastOrDefault()?.SequenceNumber, 2, values.Length, false, null) { NativeProof = SourceProofCodec.ToNative(proof) });
        });
        var store = new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
        var handler = Substitute.For<Hexalith.EventStore.DomainService.IAsyncDomainProjectionHandler>();
        handler.Domain.Returns(Identity.Domain); handler.ProjectionType.Returns("widget-detail"); handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var admitted = call.Arg<ProjectionRequest>().VerifiedNativeSourceProof.ShouldNotBeNull();
            await store.SaveAsync("models", "original-native-proof", admitted, TestContext.Current.CancellationToken);
            return Hexalith.EventStore.DomainService.DomainProjectionHandlerResult.Completed();
        });
        using var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton(gateway).AddSingleton(handler).BuildServiceProvider();
        var catalog = new Hexalith.EventStore.DomainService.DomainProjectionCatalogRegistry(); catalog.Register("raw-native", [new(Identity.Domain, "widget-detail")]);
        var response = await Hexalith.EventStore.DomainService.DomainProjectionDispatcher.DispatchAsync(services, transported,
            new(), catalog, TestContext.Current.CancellationToken);
        response.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Snapshot<Hexalith.EventStore.Contracts.Streams.NativeSourceProof>("models", "original-native-proof").ShouldBe(SourceProofCodec.ToNative(proof));
    }

    [Fact]
    public async Task TerminalReconciliationUsesOriginalSchedulerProofAfterInspectionFailure()
    {
        EventEnvelope[] events = [Envelope(1)];
        SourceProof proof = SourceProofCodec.Extend(SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"), events[0]);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.InspectSourceProofAsync().Returns(new SourceProofInspection(SourceProofInspectionStatus.Available, true, 1, false, proof));
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), nameof(AggregateActor)).Returns(actor);
        string failed = JsonSerializer.Serialize(new ProjectionDispatchResponse(2, [new("widget-detail", ProjectionDispatchStatus.Failed, null, "handler_failure")]));
        var http = new ProjectionDispatchHttpMessageHandler(failed);
        var scheduler = new MemoryRetryScheduler();
        var coordinator = CreateCoordinator(Snapshot("terminal-native", "widget-detail"), http, out _, out _, scheduler: scheduler, actorProxyFactory: proxies);
        await coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1)], TestContext.Current.CancellationToken);
        actor.InspectSourceProofAsync().Returns(Task.FromException<SourceProofInspection>(new IOException()));
        await coordinator.TryDispatchAsync(Identity, Registration(), events, [ProjectionEvent(1)], TestContext.Current.CancellationToken);
        http.Requests.ShouldContain(item => item.Path.EndsWith("project/v2/reconcile", StringComparison.Ordinal));
        http.Requests.ShouldAllBe(item => JsonSerializer.Deserialize<ProjectionDispatchRequest>(item.Body!, JsonSerializerOptions.Web)!.Request.NativeSourceProof == SourceProofCodec.ToNative(proof));
        scheduler.Work!.NativeSourceProof.ShouldBe(SourceProofCodec.ToNative(proof));
    }

    [Fact]
    public async Task RecoveryRequestsReuseFrozenProofAcrossInterruptedPreparationAndChangedProposal()
    {
        using var fixture = new ReplayRecoveryCoordinatorFixture();
        await fixture.ActivateFixtureWriterAsync();
        var proof = Hexalith.EventStore.Client.Streams.NativeSourceProofCodec.CreateSeed(fixture.Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        foreach (var item in fixture.Host.Request.Request.Events)
            proof = Hexalith.EventStore.Client.Streams.NativeSourceProofCodec.ExtendDigest(proof, fixture.Identity, item.SequenceNumber, item.MessageId!,
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(item.Payload)));
        fixture.Http.LoseAfterAction = "stage";
        await fixture.DispatchNativeAsync(proof);
        fixture.Work!.NativeSourceProof.ShouldBe(proof);
        await fixture.DispatchNativeAsync(proof with { SourceIncarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAW" });
        fixture.Http.Inputs.ShouldNotBeEmpty();
        fixture.Http.Inputs.ShouldAllBe(item => item.Request.NativeSourceProof == proof);
        fixture.Work.ShouldBeNull();
    }

}
