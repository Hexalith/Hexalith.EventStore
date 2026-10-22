using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainProjectionNativeSourceAdmissionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly AggregateIdentity Identity = new("tenant-a", "conversation", "conversation-a");

    [Fact]
    public async Task ExactOriginalNativePrefixIsAdmittedBeforeDispatchAndCannotBeCopiedToChangedInput()
    {
        var (request, gateway) = Fixture();
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        ProjectionRequest admitted = await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token);
        admitted.VerifiedNativeSourceProof.ShouldBe(request.NativeSourceProof);
        request.VerifiedNativeSourceProof.ShouldBeNull();
        JsonSerializer.Deserialize<ProjectionRequest>(JsonSerializer.Serialize(admitted))!.VerifiedNativeSourceProof.ShouldBeNull();
        (admitted with { AggregateId = "replacement" }).VerifiedNativeSourceProof.ShouldBeNull();
        (admitted with { Events = [admitted.Events[0] with { MessageId = "different" }] }).VerifiedNativeSourceProof.ShouldBeNull();
        admitted.Events[0].Payload[0] ^= 1;
        admitted.VerifiedNativeSourceProof.ShouldBeNull();
        request.Events[0].Payload[0].ShouldBe((byte)'{');
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("message")]
    [InlineData("timestamp")]
    [InlineData("timestamp-offset")]
    [InlineData("position")]
    [InlineData("user")]
    [InlineData("correlation")]
    [InlineData("type")]
    [InlineData("format")]
    [InlineData("sequence")]
    [InlineData("incarnation")]
    [InlineData("digest")]
    [InlineData("tenant")]
    [InlineData("erased")]
    [InlineData("retained")]
    public async Task TamperedOriginalInputOrNativePrefixIsRejectedBeforeMaterialization(string mutation)
    {
        var (request, gateway) = Fixture();
        ProjectionEventDto item = request.Events[0];
        request = mutation switch
        {
            "payload" => request with { Events = [item with { Payload = "{}"u8.ToArray() }] },
            "message" => request with { Events = [item with { MessageId = "forged" }] },
            "timestamp" => request with { Events = [item with { Timestamp = item.Timestamp.AddSeconds(1) }] },
            "timestamp-offset" => request with { Events = [item with { Timestamp = item.Timestamp.ToOffset(TimeSpan.FromHours(1)) }] },
            "position" => request with { Events = [item with { GlobalPosition = 2 }] },
            "user" => request with { Events = [item with { UserId = "another" }] },
            "correlation" => request with { Events = [item with { CorrelationId = "another" }] },
            "type" => request with { Events = [item with { EventTypeName = "another" }] },
            "format" => request with { Events = [item with { SerializationFormat = "another" }] },
            "sequence" => request with { Events = [item with { SequenceNumber = 2 }] },
            "incarnation" => request with { NativeSourceProof = request.NativeSourceProof! with { SourceIncarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAW" } },
            "digest" => request with { NativeSourceProof = request.NativeSourceProof! with { CumulativeDigest = new('a', 64) } },
            "tenant" => request with { TenantId = "tenant-b" },
            "erased" => request with { NativeSourceProof = request.NativeSourceProof! with { Erased = true } },
            "retained" => request with { NativeSourceProof = request.NativeSourceProof! with { RetainedFloor = 2 } },
            _ => throw new InvalidOperationException(),
        };
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token));
    }

    [Fact]
    public async Task MissingOriginalProofOrSupportedTransportRemainsUnprovenAndDoesNotFetchSource()
    {
        var (request, gateway) = Fixture();
        using var noGateway = new ServiceCollection().BuildServiceProvider();
        (await DomainProjectionNativeSourceAdmission.VerifyAsync(noGateway, request, Token)).VerifiedNativeSourceProof.ShouldBeNull();
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        (await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request with { NativeSourceProof = null }, Token))
            .VerifiedNativeSourceProof.ShouldBeNull();
        await gateway.DidNotReceive().ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
        string legacy = JsonSerializer.Serialize(request with { NativeSourceProof = null }, JsonSerializerOptions.Web);
        legacy.ShouldNotContain("nativeSourceProof");
        legacy.ShouldNotContain("verifiedNativeSourceProof");
    }

    [Fact]
    public async Task AdmittedSourceSurvivesIdenticalRetryButDoesNotCertifyEvolvedBytes()
    {
        var (request, gateway) = Fixture();
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        ProjectionRequest first = await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token);
        ProjectionRequest retry = await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token);
        retry.VerifiedNativeSourceProof.ShouldBe(first.VerifiedNativeSourceProof);
        (first with { Events = [first.Events[0] with { EventTypeName = "effective-renamed-event" }] }).VerifiedNativeSourceProof.ShouldBeNull();
    }

    [Fact]
    public async Task UnenrolledNativeReadAuthorityDoesNotDisableOrdinaryDeliveryOrAdmitProof()
    {
        var (request, gateway) = Fixture();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new HttpRequestException("not-enrolled")));
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        ProjectionRequest unproven = await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token);
        unproven.NativeSourceProof.ShouldBe(request.NativeSourceProof);
        unproven.VerifiedNativeSourceProof.ShouldBeNull();
        unproven.Events[0].Payload.ShouldBe(request.Events[0].Payload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviouslyAdmittedInputLosesPrivateAdmissionWhenVerificationIsUnavailable(bool noGateway)
    {
        var (request, gateway) = Fixture();
        using var available = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        ProjectionRequest admitted = await DomainProjectionNativeSourceAdmission.VerifyAsync(available, request, Token);
        admitted.VerifiedNativeSourceProof.ShouldNotBeNull();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new HttpRequestException("unavailable")));
        using var unavailable = noGateway ? new ServiceCollection().BuildServiceProvider()
            : new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        (await DomainProjectionNativeSourceAdmission.VerifyAsync(unavailable, admitted, Token)).VerifiedNativeSourceProof.ShouldBeNull();
        admitted.VerifiedNativeSourceProof.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalCutSurvivesAppendButNeverSourceReplacement(bool replacement)
    {
        var (request, gateway) = Fixture();
        StreamReadEvent first = (await gateway.ReadStreamAsync(new(Identity.TenantId, Identity.Domain, Identity.AggregateId, 0, 1), Token)).Events[0];
        StreamReadEvent second = first with { SequenceNumber = 2, MessageId = "message-2", NativeEventDigest = new('b', 64), GlobalPosition = 2 };
        NativeSourceProof current = NativeSourceProofCodec.CreateSeed(Identity, replacement ? "01ARZ3NDEKTSV4RRFFQ69G5FAW" : request.NativeSourceProof!.SourceIncarnation);
        foreach (StreamReadEvent item in new[] { first, second })
            current = NativeSourceProofCodec.ExtendDigest(current, Identity, item.SequenceNumber, item.MessageId, item.NativeEventDigest!);
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var query = call.Arg<StreamReadRequest>();
            StreamReadEvent[] events = query.ToSequence == 0 ? [] : [first, second];
            return new StreamReadPage(Identity.TenantId, Identity.Domain, Identity.AggregateId, events,
                new(query.FromSequence, query.ToSequence, events.LastOrDefault()?.SequenceNumber, 2, events.Length, false, null) { NativeProof = current });
        });
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        if (replacement) await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token));
        else (await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token)).VerifiedNativeSourceProof.ShouldBe(request.NativeSourceProof);
    }

    [Fact]
    public async Task SubstantialPrefixMutationChecksDoNotAllocateAnotherEncodedPayload()
    {
        var (request, gateway) = Fixture();
        byte[] payload = new byte[8 * 1024 * 1024];
        request = request with { Events = [request.Events[0] with { Payload = payload }] };
        string digest = Convert.ToHexStringLower(SHA256.HashData(payload));
        NativeSourceProof proof = NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(Identity,
            request.NativeSourceProof!.SourceIncarnation), Identity, 1, "message-1", digest);
        request = request with { NativeSourceProof = proof };
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var query = call.Arg<StreamReadRequest>();
            StreamReadEvent[] events = query.ToSequence == 0 ? [] : [new(1, "ConversationCreated", payload, "json", 1,
                "message-1", "correlation", null, DateTimeOffset.UnixEpoch, "actor") { GlobalPosition = 1, NativeEventDigest = digest }];
            return new StreamReadPage(Identity.TenantId, Identity.Domain, Identity.AggregateId, events,
                new(query.FromSequence, query.ToSequence, events.LastOrDefault()?.SequenceNumber, 1, events.Length, false, null) { NativeProof = proof });
        });
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        ProjectionRequest admitted = await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token);
        admitted.VerifiedNativeSourceProof.ShouldBe(proof);
        long started = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 5; index++) admitted.VerifiedNativeSourceProof.ShouldBe(proof);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - started;
        TestContext.Current.TestOutputHelper!.WriteLine($"payloadBytes={payload.Length}; checks=5; allocatedBytes={allocated}");
        allocated.ShouldBeLessThan(1_048_576);
        admitted.Events[0].Payload[0] = 1;
        admitted.VerifiedNativeSourceProof.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRebuildAndReconciliationAdmitUntrustedJsonAndPersistOnlyOriginalProof(bool reconcile)
    {
        var (request, gateway) = Fixture();
        request = JsonSerializer.Deserialize<ProjectionRequest>(JsonSerializer.Serialize(request))!;
        request.VerifiedNativeSourceProof.ShouldBeNull();
        var handler = Substitute.For<IAsyncDomainProjectionRebuildHandler, IAsyncDomainProjectionReconciliationHandler>();
        handler.Domain.Returns(Identity.Domain); handler.ProjectionType.Returns("native-proof");
        handler.OrdinaryDeliveryEnabled.Returns(true); handler.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        var store = new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
        handler.PrepareRebuildAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            new DomainProjectionRebuildPlan("models", [Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.Write("original-proof",
                call.Arg<ProjectionRequest>().VerifiedNativeSourceProof.ShouldNotBeNull(),
                Hexalith.EventStore.Client.Projections.ReadModelBatchConcurrency.LastWrite)]));
        ((IAsyncDomainProjectionReconciliationHandler)handler).ReconcileAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await store.SaveAsync("models", "original-proof", call.Arg<ProjectionRequest>().VerifiedNativeSourceProof.ShouldNotBeNull(), Token);
                return DomainProjectionHandlerResult.Completed();
            });
        using var services = new ServiceCollection().AddSingleton<IAsyncDomainProjectionHandler>(handler).AddSingleton(gateway)
            .AddSingleton<Hexalith.EventStore.Client.Projections.IReadModelStore>(store)
            .AddSingleton<Hexalith.EventStore.Client.Projections.IReadModelBatchStore>(store).BuildServiceProvider();
        string fingerprint = Hexalith.EventStore.Client.Projections.ProjectionRouteCatalogFingerprint.Compute("native-service", "v1", [new(Identity.Domain, "native-proof")]);
        var dispatch = new ProjectionDispatchRequest(request, ["native-proof"], "original-operation", fingerprint);
        var identity = new DomainProjectionIdentityOptions { AppId = "native-service", ServiceVersion = "v1" };
        async Task<ProjectionDispatchResponse> Invoke(ProjectionDispatchRequest item) => reconcile
            ? await DomainProjectionDispatcher.ReconcileAsync(services, item, new(), identity, Token)
            : await DomainProjectionDispatcher.RebuildAsync(services, item, new(), identity, Token);
        (await Invoke(dispatch)).Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.Snapshot<NativeSourceProof>("models", "original-proof").ShouldBe(request.NativeSourceProof);
        var changed = dispatch with { Request = request with { Events = [request.Events[0] with { Payload = "{}"u8.ToArray() }] } };
        await Should.ThrowAsync<ProjectionDispatchValidationException>(() => Invoke(changed));
        store.Snapshot<NativeSourceProof>("models", "original-proof").ShouldBe(request.NativeSourceProof);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealOrdinaryDispatchPreservesDeliveryButWithholdsUnsupportedProtectedOrEvolvedProvenance(bool evolved)
    {
        var (request, gateway) = evolved ? Fixture("Old.Contracts.CounterRaised", "{\"Amount\":5}"u8.ToArray()) : Fixture(protectedPayload: true);
        request = JsonSerializer.Deserialize<ProjectionRequest>(JsonSerializer.Serialize(request))!;
        var handler = Substitute.For<IAsyncDomainProjectionHandler>();
        handler.Domain.Returns(Identity.Domain); handler.ProjectionType.Returns("native-proof"); handler.OrdinaryDeliveryEnabled.Returns(true);
        ProjectionRequest? received = null;
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        { received = call.Arg<ProjectionRequest>(); return DomainProjectionHandlerResult.Completed(); });
        var registrations = new ServiceCollection().AddSingleton(gateway).AddSingleton(handler);
        if (evolved) registrations.AddSingleton(new Hexalith.EventStore.Client.Events.EventPayloadEvolutionRegistry(
            [typeof(VersionedProjectionDispatchTests.RenamedEvent)], [new VersionedProjectionDispatchTests.RenameStep()]));
        using var services = registrations.BuildServiceProvider();
        var registry = new DomainProjectionCatalogRegistry(); registry.Register("native-proof", [new(Identity.Domain, "native-proof")]);
        (await DomainProjectionDispatcher.DispatchAsync(services, new(request, ["native-proof"], "original", "native-proof"), new(), registry, Token))
            .Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        received.ShouldNotBeNull().VerifiedNativeSourceProof.ShouldBeNull();
        if (evolved) received.Events[0].EventTypeName.ShouldBe(typeof(VersionedProjectionDispatchTests.RenamedEvent).FullName);
        else received.Events[0].Payload.ShouldBe(request.Events[0].Payload);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task ActualGatewayFailuresLeaveOptionalAdmissionUnproven(int status)
    {
        var (request, gateway) = Fixture();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StreamReadPage>(new EventStoreGatewayException(status, "unavailable")));
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        var result = await DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token);
        result.VerifiedNativeSourceProof.ShouldBeNull();
        result.NativeSourceProof.ShouldBe(request.NativeSourceProof);
        result.Events[0].Payload.ShouldBe(request.Events[0].Payload);
    }

    [Theory]
    [InlineData("events")]
    [InlineData("event")]
    [InlineData("payload")]
    [InlineData("identity")]
    [InlineData("proof")]
    public async Task MalformedNativeInputIsRejectedBeforeSourceAccess(string vector)
    {
        var (request, gateway) = Fixture();
        request = vector switch
        {
            "events" => request with { Events = null! },
            "event" => request with { Events = [null!] },
            "payload" => request with { Events = [request.Events[0] with { Payload = null! }] },
            "identity" => request with { AggregateId = "" },
            _ => request with { NativeSourceProof = request.NativeSourceProof! with { HeadSequence = 2 } },
        };
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        await Should.ThrowAsync<ProjectionDispatchValidationException>(() => DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, Token));
        await gateway.DidNotReceive().ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancelledAdmissionWinsBeforeValidationCopyingOrSourceAccess(bool legacy)
    {
        var (request, gateway) = Fixture(payload: new byte[8 * 1024 * 1024]);
        request = request with { Events = null!, NativeSourceProof = legacy ? null : request.NativeSourceProof };
        using var services = new ServiceCollection().AddSingleton(gateway).BuildServiceProvider();
        using var caller = new CancellationTokenSource(); caller.Cancel();
        (await Should.ThrowAsync<OperationCanceledException>(() => DomainProjectionNativeSourceAdmission.VerifyAsync(services, request, caller.Token)))
            .CancellationToken.ShouldBe(caller.Token);
        await gateway.DidNotReceive().ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>());
    }

    private static (ProjectionRequest, IEventStoreGatewayClient) Fixture(string eventType = "ConversationCreated", byte[]? payload = null, bool protectedPayload = false)
    {
        var item = new ProjectionEventDto(eventType, payload ?? "{\"value\":1}"u8.ToArray(), "json", 1,
            DateTimeOffset.UnixEpoch, "correlation", "message-1", "actor", 1);
        string digest = Convert.ToHexStringLower(SHA256.HashData(item.Payload));
        NativeSourceProof proof = NativeSourceProofCodec.ExtendDigest(
            NativeSourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"), Identity, 1, item.MessageId!, digest);
        var native = new StreamReadEvent(1, item.EventTypeName, item.Payload.ToArray(), item.SerializationFormat,
            1, item.MessageId!, item.CorrelationId, null, item.Timestamp, item.UserId, protectedPayload
                ? new(Hexalith.EventStore.Contracts.Security.PayloadProtectionState.Protected, 1, "test", "key", "json", null) : null) { NativeEventDigest = digest, GlobalPosition = 1 };
        IEventStoreGatewayClient gateway = Substitute.For<IEventStoreGatewayClient>();
        gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            StreamReadRequest request = call.Arg<StreamReadRequest>();
            StreamReadEvent[] events = request.ToSequence == 0 ? [] : [native];
            return new StreamReadPage(Identity.TenantId, Identity.Domain, Identity.AggregateId, events,
                new(request.FromSequence, request.ToSequence, events.LastOrDefault()?.SequenceNumber, 1,
                    events.Length, false, null) { NativeProof = proof });
        });
        return (new(Identity.TenantId, Identity.Domain, Identity.AggregateId, [item]) { NativeSourceProof = proof }, gateway);
    }
}
