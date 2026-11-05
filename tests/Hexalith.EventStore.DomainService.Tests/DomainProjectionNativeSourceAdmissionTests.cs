using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Http;
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RealRebuildAndReconciliationAdmitUntrustedJsonAndPersistOnlyOriginalProof(bool reconcile, bool selectedReader)
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
        IEventStoreGatewayClient human = selectedReader ? Substitute.For<IEventStoreGatewayClient>() : gateway;
        var registrations = new ServiceCollection().AddSingleton<IAsyncDomainProjectionHandler>(handler).AddSingleton(human)
            .AddSingleton<Hexalith.EventStore.Client.Projections.IReadModelStore>(store)
            .AddSingleton<Hexalith.EventStore.Client.Projections.IReadModelBatchStore>(store);
        if (selectedReader) registrations.AddSingleton(new DomainProjectionNativeSourceReader(
            new AuthoritativeEventStreamReader(gateway, TimeProvider.System, requireNativeProof: true)));
        using var services = registrations.BuildServiceProvider();
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
        if (selectedReader) human.ReceivedCalls().ShouldBeEmpty();
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

    [Theory]
    [InlineData("valid")]
    [InlineData("legacy-after")]
    [InlineData("payload")]
    [InlineData("source-replaced")]
    [InlineData("unavailable")]
    [InlineData("missing-key")]
    [InlineData("malformed-base64")]
    [InlineData("malformed-pkcs8")]
    [InlineData("cancelled-malformed-base64")]
    [InlineData("cancelled-malformed-pkcs8")]
    [InlineData("foreign-domain")]
    public async Task SelectedNamedWorkloadSourceVerifiesOriginalPrefixWithoutUsingHumanGateway(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var (original, source) = Fixture();
        using RSA key = RSA.Create(2048);
        string? privateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        switch (scenario)
        {
            case "missing-key":
                privateKey = null;
                break;
            case "malformed-base64" or "cancelled-malformed-base64":
                privateKey = "PROTECTED_NATIVE_SOURCE_MARKER";
                break;
            case "malformed-pkcs8" or "cancelled-malformed-pkcs8":
                privateKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(4));
                break;
        }
        string daprApiToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500", ["DAPR_API_TOKEN"] = daprApiToken,
            ["EventStore:Gateway:AppId"] = "source-gateway", ["Authentication:WorkloadIssuer:Workload"] = "source-owner",
            ["Authentication:BoundStreamRead:PrivateKey"] = privateKey,
        }).Build();
        var assertions = new List<WorkloadAssertionRequest>();
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns(call => {
            assertions.Add(call.Arg<WorkloadAssertionRequest>());
            return ValueTask.FromResult<string?>("exact-owner-assertion");
        });
        using var transport = new MockHttpMessageHandler(message => {
            message.RequestUri!.AbsoluteUri.ShouldBe("http://localhost:3500/api/v1/streams/read/workload");
            message.Headers.Authorization.ShouldBeNull();
            message.Headers.GetValues("dapr-app-id").Single().ShouldBe("source-gateway");
            message.Headers.GetValues("dapr-api-token").Single().ShouldBe(daprApiToken);
            message.Headers.GetValues(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName).Single().ShouldBe("exact-owner-assertion");
            var query = JsonSerializer.Deserialize<StreamReadRequest>(message.Content!.ReadAsStringAsync().GetAwaiter().GetResult(), JsonSerializerOptions.Web)!;
            BoundStreamReadProof.Verify(message.Headers.GetValues(BoundStreamReadProof.HeaderName).Single(), query,
                "source-owner", Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
            if (scenario == "unavailable") return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            StreamReadPage page = source.ReadStreamAsync(query, Token).GetAwaiter().GetResult();
            if (scenario == "source-replaced") page = page with { Metadata = page.Metadata with {
                NativeProof = NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(Identity,
                    "01ARZ3NDEKTSV4RRFFQ69G5FAW"), Identity, 1, original.Events[0].MessageId!,
                    Convert.ToHexStringLower(SHA256.HashData(original.Events[0].Payload))) } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(page) };
        });
        IEventStoreGatewayClient human = Substitute.For<IEventStoreGatewayClient>();
        var legacy = new AuthoritativeEventStreamReader(source, TimeProvider.System);
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(issuer); services.AddSingleton(human);
        if (scenario != "legacy-after") services.AddSingleton<IAuthoritativeEventStreamReader>(legacy);
        services.AddEventStoreGatewayAuthoritativeEventStreamReader(scenario == "foreign-domain" ? "other" : Identity.Domain)
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        if (scenario == "legacy-after") services.AddSingleton<IAuthoritativeEventStreamReader>(legacy);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAuthoritativeEventStreamReader>().ShouldBeSameAs(legacy);
        ProjectionRequest request = scenario == "payload" ? original with { Events = [original.Events[0] with { Payload = "{}"u8.ToArray() }] } : original;
        if (scenario.StartsWith("cancelled-", StringComparison.Ordinal))
        {
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(Token);
            caller.Cancel();
            (await Should.ThrowAsync<OperationCanceledException>(() =>
                DomainProjectionNativeSourceAdmission.VerifyAsync(provider, request, caller.Token)))
                .CancellationToken.ShouldBe(caller.Token);
        }
        else if (scenario is "payload" or "source-replaced")
            await Should.ThrowAsync<ProjectionDispatchValidationException>(() => DomainProjectionNativeSourceAdmission.VerifyAsync(provider, request, Token));
        else {
            ProjectionRequest admitted = await DomainProjectionNativeSourceAdmission.VerifyAsync(provider, request, Token);
            if (scenario is "valid" or "legacy-after") admitted.VerifiedNativeSourceProof.ShouldBe(original.NativeSourceProof);
            else admitted.VerifiedNativeSourceProof.ShouldBeNull();
        }
        human.ReceivedCalls().ShouldBeEmpty();
        if (scenario is "missing-key" or "foreign-domain" or "malformed-base64" or "malformed-pkcs8"
            or "cancelled-malformed-base64" or "cancelled-malformed-pkcs8") {
            transport.CallCount.ShouldBe(0); assertions.ShouldBeEmpty();
        }
        else {
            transport.CallCount.ShouldBeGreaterThan(0);
            assertions.ShouldAllBe(assertion => assertion.Audience == "eventstore"
                && assertion.Operation == EventStoreWorkloadOperations.GatewayStreamRead && assertion.Bindings != null
                && assertion.Bindings.Count == 2
                && assertion.Bindings[EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] == Identity.TenantId
                && assertion.Bindings[EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] == Identity.Domain);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitReaderRefusalOrErrorNeverFallsBackToHumanGateway(bool error)
    {
        var (request, human) = Fixture();
        IAuthoritativeEventStreamReader selected = Substitute.For<IAuthoritativeEventStreamReader>();
        selected.ReadAsync(Identity, Arg.Any<CancellationToken>()).Returns(_ => error
            ? Task.FromException<AuthoritativeStreamReadResult>(new InvalidOperationException("selected-reader-failure"))
            : Task.FromResult(new AuthoritativeStreamReadResult(null, "selected-reader-unavailable")));
        using var provider = new ServiceCollection().AddSingleton(human)
            .AddSingleton<IAuthoritativeEventStreamReader>(new AuthoritativeEventStreamReader(human, TimeProvider.System))
            .AddSingleton(new DomainProjectionNativeSourceReader(selected)).BuildServiceProvider();
        if (error) await Should.ThrowAsync<InvalidOperationException>(() => DomainProjectionNativeSourceAdmission.VerifyAsync(provider, request, Token));
        else (await DomainProjectionNativeSourceAdmission.VerifyAsync(provider, request, Token)).VerifiedNativeSourceProof.ShouldBeNull();
        human.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealLegacyReaderDoesNotReplaceUnenrolledNativeGatewayFallback(bool gatewayAvailable)
    {
        var (request, gateway) = Fixture();
        var (_, legacyGateway) = Fixture();
        var legacy = new AuthoritativeEventStreamReader(legacyGateway, TimeProvider.System);
        (await legacy.ReadAsync(Identity, Token)).Stream.ShouldNotBeNull().NativeProof.ShouldBeNull();
        legacyGateway.ClearReceivedCalls();
        var registrations = new ServiceCollection().AddSingleton<IAuthoritativeEventStreamReader>(legacy);
        if (gatewayAvailable) registrations.AddSingleton(gateway);
        using var provider = registrations.BuildServiceProvider();

        ProjectionRequest admitted = await DomainProjectionNativeSourceAdmission.VerifyAsync(provider, request, Token);

        provider.GetRequiredService<IAuthoritativeEventStreamReader>().ShouldBeSameAs(legacy);
        legacyGateway.ReceivedCalls().ShouldBeEmpty();
        if (gatewayAvailable)
        {
            admitted.VerifiedNativeSourceProof.ShouldBe(request.NativeSourceProof);
            await gateway.Received().ReadStreamAsync(Arg.Is<StreamReadRequest>(query => query.RequireNativeProof), Arg.Any<CancellationToken>());
            await gateway.DidNotReceive().ReadStreamAsync(Arg.Is<StreamReadRequest>(query => !query.RequireNativeProof), Arg.Any<CancellationToken>());
        }
        else admitted.VerifiedNativeSourceProof.ShouldBeNull();
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
