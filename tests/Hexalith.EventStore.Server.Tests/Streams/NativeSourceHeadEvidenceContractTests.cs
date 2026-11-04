using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Security;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Streams;

/// <summary>Qualifies persisted positive/zero evidence and unavailable absence through the existing authenticated route.</summary>
public sealed class NativeSourceHeadEvidenceContractTests
{
    private static readonly AggregateIdentity Identity = new("test-tenant", "test-domain", "agg-001");
    private const string Caller = "folders-source";
    private const string Channel = "source-contract-test-channel";

    /// <summary>Reads never create proof: existing valid zero differs from missing, legacy, erased and invalid state.</summary>
    [Theory]
    [InlineData("native-positive", 200, true, true, false, 1)]
    [InlineData("native-zero", 200, true, true, false, 0)]
    [InlineData("missing", 503, false, false, false, 0)]
    [InlineData("legacy-empty", 503, false, true, false, 0)]
    [InlineData("legacy-positive", 503, false, true, false, 1)]
    [InlineData("erased-complete", 503, false, false, true, 0)]
    [InlineData("erased-pending", 503, false, false, true, 0)]
    [InlineData("invalid-native", 503, false, true, false, 0)]
    public async Task ExistingWorkloadMetadataRoutePreservesSupportedZeroAndRefusesUnprovedAbsence(
        string scenario, int status, bool available, bool exists, bool erased, long head)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        SourceProof seed = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var storedEvent = new EventEnvelope("original-message", Identity.AggregateId, "Aggregate", Identity.TenantId,
            Identity.Domain, 1, 1, DateTimeOffset.UnixEpoch, "correlation", "cause", "actor", "v1", "Event", 1,
            "json", "{}"u8.ToArray(), null);
        SourceProof proof = head == 0 ? seed : SourceProofCodec.Extend(seed, storedEvent);
        Dictionary<string, object> persisted = [];
        if (exists)
        {
            persisted[Identity.MetadataKey] = new AggregateMetadata(head, DateTimeOffset.UnixEpoch, null)
            {
                SourceProof = scenario.StartsWith("legacy", StringComparison.Ordinal) ? null
                    : scenario == "invalid-native" ? proof with { CumulativeDigest = "invalid" } : proof,
            };
            if (head > 0) persisted[Identity.EventStreamKeyPrefix + "1"] = storedEvent;
        }
        if (erased)
            persisted[TrustedEffectErasureProgress.StateName] = new TrustedEffectErasureProgress(1, 2,
                scenario == "erased-complete", "erasure-capability");
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(persisted);
        AggregateActor actor = AggregateActorTestHelper.CreateActor(stateManager: state).Actor;
        string before = JsonSerializer.Serialize(state.CommittedState);

        SourceProofInspection inspection = await actor.InspectSourceProofAsync();
        inspection.Exists.ShouldBe(exists);
        inspection.Erased.ShouldBe(erased);
        inspection.CurrentSequence.ShouldBe(head);
        inspection.Status.ShouldBe(available ? SourceProofInspectionStatus.Available
            : scenario is "invalid-native" or "erased-pending" ? SourceProofInspectionStatus.Invalid : SourceProofInspectionStatus.Unsupported);
        inspection.Proof.ShouldBe(available ? proof : null);

        using RSA key = RSA.Create(2048);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Is<ActorId>(id => id.GetId() == Identity.ActorId),
            "AggregateActor", Arg.Any<ActorProxyOptions?>()).Returns(actor);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        await using WebApplication app = await StartAsync(proxies, protection, key);
        using HttpClient http = app.GetTestClient();
        http.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, Channel);
        var request = new StreamReadRequest(Identity.TenantId, Identity.Domain, Identity.AggregateId,
            FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/read/workload")
        { Content = JsonContent.Create(request) };
        string assertion = WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience, Caller,
            [EventStoreWorkloadOperations.GatewayStreamRead]);
        message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        message.Headers.Add(BoundStreamReadProof.HeaderName, BoundStreamReadProof.Issue(request, Caller,
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow));
        using HttpResponseMessage response = await http.SendAsync(message, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        if (available)
        {
            StreamReadPage? page = await response.Content.ReadFromJsonAsync<StreamReadPage>(TestContext.Current.CancellationToken);
            page.ShouldNotBeNull().Metadata.NativeProof.ShouldBe(SourceProofCodec.ToNative(proof));
            page.Events.ShouldBeEmpty();
        }

        var assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        assertions.IssueAsync(EventStoreGatewayWorkloadOperations.StreamRead, Identity.TenantId, Identity.Domain,
            Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<string?>(assertion));
        var proofs = Substitute.For<IEventStoreGatewayStreamReadProofSource>();
        proofs.IssueAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            ValueTask.FromResult<string?>(BoundStreamReadProof.Issue(call.Arg<StreamReadRequest>(), Caller,
                Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow)));
        using HttpClient gatewayHttp = app.GetTestClient();
        gatewayHttp.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, Channel);
        var gateway = new EventStoreGatewayClient(gatewayHttp, Options.Create(new EventStoreGatewayClientOptions
            { UseWorkloadTransport = true }), assertions, proofs);
        NativeSourceObservation? observed = await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System)
            .ReadAsync(Identity, TestContext.Current.CancellationToken);
        if (available) observed.ShouldNotBeNull().Proof.ShouldBe(SourceProofCodec.ToNative(proof));
        else observed.ShouldBeNull();

        JsonSerializer.Serialize(state.CommittedState).ShouldBe(before);
        state.CommittedSnapshots.ShouldBeEmpty();
        state.Trace.ShouldNotContain(entry => entry.StartsWith("SaveState", StringComparison.Ordinal)
            || entry.StartsWith("SetState", StringComparison.Ordinal) || entry.StartsWith("AddState", StringComparison.Ordinal)
            || entry.StartsWith("RemoveState", StringComparison.Ordinal));
        state.Trace.ShouldNotContain(entry => entry.StartsWith("TryGetState:" + Identity.EventStreamKeyPrefix, StringComparison.Ordinal));
        protection.ReceivedCalls().ShouldBeEmpty();
        proxies.ReceivedCalls().Count().ShouldBe(2);
    }

    /// <summary>Records the remaining capability boundary: the existing stream grant also admits event pages.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingStreamReadGrantDoesNotEnforceMetadataOnlyAuthority(bool jwtBindings)
    {
        var storedEvent = new EventEnvelope("original-message", Identity.AggregateId, "Aggregate", Identity.TenantId,
            Identity.Domain, 1, 1, DateTimeOffset.UnixEpoch, "correlation", "cause", "actor", "v1", "Event", 1,
            "json", "{}"u8.ToArray(), null);
        SourceProof proof = SourceProofCodec.Extend(SourceProofCodec.CreateSeed(Identity,
            "01ARZ3NDEKTSV4RRFFQ69G5FAV"), storedEvent);
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [Identity.MetadataKey] = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null) { SourceProof = proof },
            [Identity.EventStreamKeyPrefix + "1"] = storedEvent,
        });
        AggregateActor actor = AggregateActorTestHelper.CreateActor(stateManager: state).Actor;
        string before = JsonSerializer.Serialize(state.CommittedState);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Is<ActorId>(id => id.GetId() == Identity.ActorId),
            "AggregateActor", Arg.Any<ActorProxyOptions?>()).Returns(actor);
        using RSA key = RSA.Create(2048);
        await using WebApplication app = await StartAsync(proxies, new NoOpEventPayloadProtectionService(), key);
        using HttpClient http = app.GetTestClient();
        http.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, Channel);
        var metadata = new StreamReadRequest(Identity.TenantId, Identity.Domain, Identity.AggregateId,
            FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true };
        StreamReadRequest eventPage = metadata with { ToSequence = 1 };
        IReadOnlyDictionary<string, string>? bindings = jwtBindings ? new Dictionary<string, string>
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = Identity.TenantId,
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = Identity.Domain,
        } : null;
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/read/workload")
        { Content = JsonContent.Create(eventPage) };
        message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
            WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience, Caller,
                [EventStoreWorkloadOperations.GatewayStreamRead], bindings: bindings));
        if (!jwtBindings)
        {
            string metadataProof = BoundStreamReadProof.Issue(metadata, Caller,
                Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
            BoundStreamReadProof.Verify(metadataProof, eventPage, Caller,
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
            message.Headers.Add(BoundStreamReadProof.HeaderName, metadataProof);
        }

        using HttpResponseMessage response = await http.SendAsync(message, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        StreamReadPage? page = await response.Content.ReadFromJsonAsync<StreamReadPage>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull().Events.Count.ShouldBe(1);
        page.Events[0].MessageId.ShouldBe(storedEvent.MessageId);
        page.Events[0].Payload.ShouldBe(storedEvent.Payload);
        JsonSerializer.Serialize(state.CommittedState).ShouldBe(before);
        state.CommittedSnapshots.ShouldBeEmpty();
        state.Trace.ShouldNotContain(entry => entry.StartsWith("SaveState", StringComparison.Ordinal)
            || entry.StartsWith("SetState", StringComparison.Ordinal) || entry.StartsWith("AddState", StringComparison.Ordinal));
    }

    /// <summary>Receiver-owned enrollment restricts even resource-bound JWTs before actor creation.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("range")]
    [InlineData("from")]
    [InlineData("page-size")]
    [InlineData("native-proof")]
    [InlineData("projection")]
    [InlineData("missing-proof")]
    [InlineData("bad-proof")]
    [InlineData("expired-proof")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("caller")]
    [InlineData("operation")]
    [InlineData("missing-assertion")]
    [InlineData("mixed-human")]
    [InlineData("bound-jwt-no-proof")]
    [InlineData("bound-jwt-range")]
    [InlineData("malformed-enrollment")]
    public async Task SourceOnlyEnrollmentRequiresExactMetadataShapeAndActualProofBeforeObservation(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var state = new FaultInjectingActorStateManager();
        var seed = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [Identity.MetadataKey] = new AggregateMetadata(0, DateTimeOffset.UnixEpoch, null) { SourceProof = seed },
        });
        AggregateActor actor = AggregateActorTestHelper.CreateActor(stateManager: state).Actor;
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>()).Returns(actor);
        using RSA key = RSA.Create(2048);
        await using WebApplication app = await StartAsync(proxies, new NoOpEventPayloadProtectionService(), key,
            scenario == "malformed-enrollment" ? "false" : "true");
        using HttpClient http = app.GetTestClient();
        http.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, Channel);
        var metadata = new StreamReadRequest(Identity.TenantId, Identity.Domain, Identity.AggregateId,
            FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true };
        StreamReadRequest request = scenario switch
        {
            "range" or "bound-jwt-range" => metadata with { ToSequence = 1 },
            "from" => metadata with { FromSequence = 1, ToSequence = 1 },
            "page-size" => metadata with { PageSize = 100 },
            "native-proof" => metadata with { RequireNativeProof = false },
            "projection" => metadata with { ProjectionName = "operational-status" },
            "tenant" => metadata with { Tenant = "other-tenant" },
            "domain" => metadata with { Domain = "other-domain" },
            "aggregate" => metadata with { AggregateId = "other-aggregate" },
            _ => metadata,
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/read/workload")
        { Content = JsonContent.Create(request) };
        IReadOnlyDictionary<string, string>? bindings = scenario.StartsWith("bound-jwt", StringComparison.Ordinal)
            ? new Dictionary<string, string>
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = Identity.TenantId,
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = Identity.Domain,
            } : null;
        string assertion = WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience,
            scenario == "caller" ? "unenrolled" : Caller,
            [scenario == "operation" ? EventStoreWorkloadOperations.GatewayCommandSubmit : EventStoreWorkloadOperations.GatewayStreamRead],
            bindings: bindings);
        if (scenario != "missing-assertion") message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        if (scenario == "mixed-human") message.Headers.Authorization = new("Bearer", assertion);
        if (scenario is not ("missing-proof" or "bound-jwt-no-proof"))
        {
            string boundProof = BoundStreamReadProof.Issue(metadata, Caller, Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
                scenario == "expired-proof" ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow);
            message.Headers.Add(BoundStreamReadProof.HeaderName, scenario == "bad-proof" ? boundProof + "tampered" : boundProof);
        }

        using HttpResponseMessage response = await http.SendAsync(message, TestContext.Current.CancellationToken);

        if (scenario == "valid")
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            proxies.ReceivedCalls().Count().ShouldBe(1);
        }
        else
        {
            response.StatusCode.ShouldBe(scenario is "caller" or "missing-assertion" or "mixed-human"
                ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
            proxies.ReceivedCalls().ShouldBeEmpty();
            state.Trace.ShouldBeEmpty();
        }
        state.CommittedSnapshots.ShouldBeEmpty();
    }

    private static async Task<WebApplication> StartAsync(IActorProxyFactory proxies,
        IEventPayloadProtectionService protection, RSA key, string? metadataOnly = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = Channel,
            ["Authentication:JwtBearer:Authority"] = null,
            ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = DaprInternalAuthenticationOptions.DefaultAudience,
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            [$"Authentication:BoundStreamRead:PublicKeys:{Caller}"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            [$"Authentication:BoundStreamRead:AllowedDomains:{Caller}"] = Identity.Domain,
            [$"Authentication:BoundStreamRead:MetadataOnlyCallers:{Caller}"] = metadataOnly,
        });
        builder.Services.AddAuthentication(DaprInternalAuthenticationOptions.SchemeName)
            .AddEventStoreWorkloadScheme(DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal", (options, _) =>
            { options.Audience = DaprInternalAuthenticationOptions.DefaultAudience; options.AllowedCallers = [Caller]; });
        builder.Services.AddEventStoreWorkloadPolicies(DaprInternalAuthenticationOptions.SchemeName,
            [EventStoreWorkloadOperations.GatewayStreamRead]);
        builder.Services.AddControllers().AddApplicationPart(typeof(StreamsController).Assembly);
        builder.Services.AddSingleton(proxies);
        builder.Services.AddSingleton(Substitute.For<Hexalith.EventStore.Authorization.ITenantValidator>());
        builder.Services.AddSingleton(Substitute.For<IRbacValidator>());
        builder.Services.AddSingleton(protection);
        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
