using System.Security.Claims;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Server.Tests.Fakes;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Testing.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class StreamsControllerTests {
    private const string Tenant = "tenant-a";
    private const string Domain = "party";
    private const string AggregateId = "party-1";

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(5, false)]
    public async Task NativeHeadGatewayCrossesRealWorkloadMiddlewareWithJwtBindingsOrExactSignedProof(int head, bool jwtBindings)
    {
        using RSA key = RSA.Create(2048);
        const string channelToken = "native-head-test-channel";
        SourceProofInspection inspection = NativeInspection(head, head + 1);
        IAggregateActor actor = HeadOnlyActor(inspection);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        proxies.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>()).Returns(actor);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        var tenantValidator = new FakeTenantValidator { ConfiguredResult = TenantValidationResult.Denied("denied") };
        var rbacValidator = new FakeRbacValidator { ConfiguredResult = RbacValidationResult.Denied("denied") };
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = channelToken,
            ["Authentication:JwtBearer:Authority"] = null,
            ["Authentication:JwtBearer:Issuer"] = WorkloadAssertionTestTokens.Issuer,
            ["Authentication:JwtBearer:Audience"] = DaprInternalAuthenticationOptions.DefaultAudience,
            ["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey,
            ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
            ["Authentication:BoundStreamRead:PublicKeys:projects"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
            ["Authentication:BoundStreamRead:AllowedDomains:projects"] = Domain,
        });
        builder.Services.AddAuthentication(DaprInternalAuthenticationOptions.SchemeName)
            .AddEventStoreWorkloadScheme(DaprInternalAuthenticationOptions.SchemeName, "Authentication:DaprInternal", (options, _) =>
            { options.Audience = DaprInternalAuthenticationOptions.DefaultAudience; options.AllowedCallers = ["projects"]; });
        builder.Services.AddEventStoreWorkloadPolicies(DaprInternalAuthenticationOptions.SchemeName,
            [EventStoreWorkloadOperations.GatewayStreamRead]);
        builder.Services.AddControllers().AddApplicationPart(typeof(StreamsController).Assembly);
        builder.Services.AddSingleton(proxies);
        builder.Services.AddSingleton<Hexalith.EventStore.Authorization.ITenantValidator>(tenantValidator);
        builder.Services.AddSingleton<IRbacValidator>(rbacValidator);
        builder.Services.AddSingleton(protection);
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient http = app.GetTestClient();
        http.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, channelToken);
        IReadOnlyDictionary<string, string>? bindings = jwtBindings ? new Dictionary<string, string>
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = Tenant,
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = Domain,
        } : null;
        string assertion = WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience, "projects",
            [EventStoreWorkloadOperations.GatewayStreamRead], bindings: bindings);
        var assertions = Substitute.For<IEventStoreGatewayWorkloadAssertionSource>();
        assertions.IssueAsync(EventStoreGatewayWorkloadOperations.StreamRead, Tenant, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(assertion));
        var proofs = Substitute.For<IEventStoreGatewayStreamReadProofSource>();
        proofs.IssueAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call => ValueTask.FromResult<string?>(
            BoundStreamReadProof.Issue(call.Arg<StreamReadRequest>(), "projects", Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow)));
        var gateway = new EventStoreGatewayClient(http, Options.Create(new EventStoreGatewayClientOptions { UseWorkloadTransport = true }),
            assertions, jwtBindings ? null : proofs);

        NativeSourceObservation? observation = await new GatewayNativeSourceMetadataReader(gateway, TimeProvider.System)
            .ReadAsync(new(Tenant, Domain, AggregateId), TestContext.Current.CancellationToken);

        observation.ShouldNotBeNull().Proof.ShouldBe(SourceProofCodec.ToNative(inspection.Proof!));
        actor.ReceivedCalls().Select(call => call.GetMethodInfo().Name)
            .ShouldBe([nameof(IAggregateActor.InspectSourceProofAsync), nameof(IAggregateActor.InspectSourceProofAsync)]);
        protection.ReceivedCalls().ShouldBeEmpty();
        tenantValidator.ReceivedRequests.ShouldBeEmpty();
        rbacValidator.ReceivedRequests.ShouldBeEmpty();

        // Exercise the same middleware and exact proof with denied enrollment/operation/target variants.
        foreach (string denial in new[] { "operation", "workload", "channel", "target", "missing-binding" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/read/workload")
            { Content = JsonContent.Create(denial == "target" ? HeadRequest() with { AggregateId = "foreign-target" } : HeadRequest()) };
            request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName,
                WorkloadAssertionTestTokens.Create(DaprInternalAuthenticationOptions.DefaultAudience,
                    denial == "workload" ? "unenrolled" : "projects",
                    [denial == "operation" ? EventStoreWorkloadOperations.TrustedEffect : EventStoreWorkloadOperations.GatewayStreamRead]));
            if (denial is not ("channel" or "missing-binding"))
                request.Headers.Add(BoundStreamReadProof.HeaderName,
                    BoundStreamReadProof.Issue(HeadRequest(), "projects", Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow));
            if (denial == "channel") http.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            using HttpResponseMessage response = await http.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(denial is "workload" or "channel" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
            if (denial == "channel") http.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, channelToken);
        }
        actor.ReceivedCalls().Count().ShouldBe(2);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(5, 3)]
    [InlineData(5, 6)]
    public async Task NativeHeadInspectionReturnsOnlyStableProofWithoutRangeOrPayloadCalls(int head, int retainedFloor)
    {
        SourceProofInspection inspection = NativeInspection(head, retainedFloor);
        IAggregateActor actor = HeadOnlyActor(inspection);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        (StreamsController controller, IActorProxyFactory proxies, _, _) = CreateController(actor, protection);
        BindWorkload(controller);

        StreamReadPage page = (await controller.ReadWorkloadStreamAsync(HeadRequest()))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<StreamReadPage>();

        page.Tenant.ShouldBe(Tenant);
        page.Domain.ShouldBe(Domain);
        page.AggregateId.ShouldBe(AggregateId);
        page.Events.ShouldBeEmpty();
        page.Metadata.ShouldBe(new StreamReadMetadata(0, 0, null, head, 0, false, null)
            { NativeProof = SourceProofCodec.ToNative(inspection.Proof!) });
        actor.ReceivedCalls().Select(call => call.GetMethodInfo().Name)
            .ShouldBe([nameof(IAggregateActor.InspectSourceProofAsync), nameof(IAggregateActor.InspectSourceProofAsync)]);
        protection.ReceivedCalls().ShouldBeEmpty();
        proxies.Received(1).CreateActorProxy<IAggregateActor>(new ActorId($"{Tenant}:{Domain}:{AggregateId}"), "AggregateActor", Arg.Any<ActorProxyOptions?>());
    }

    [Theory]
    [InlineData("replaced")]
    [InlineData("regressed")]
    [InlineData("revoked")]
    [InlineData("erased")]
    [InlineData("missing")]
    [InlineData("digest")]
    [InlineData("retention")]
    [InlineData("head-message")]
    public async Task NativeHeadInspectionRejectsAnyChangedSecondObservation(string change)
    {
        SourceProofInspection initial = NativeInspection(5);
        SourceProofInspection changed = change switch
        {
            "replaced" => initial with { Proof = initial.Proof! with { SourceIncarnation = "01ARZ3NDEKTSV4RRFFQ69G5FAW" } },
            "regressed" => NativeInspection(4),
            "revoked" => initial with { Status = SourceProofInspectionStatus.Unsupported, Proof = null },
            "erased" => initial with { Erased = true, Proof = initial.Proof! with { Erased = true } },
            "missing" => initial with { Exists = false },
            "digest" => initial with { Proof = initial.Proof! with { CumulativeDigest = new string('a', 64) } },
            "retention" => initial with { Proof = initial.Proof! with { RetainedFloor = 2 } },
            _ => initial with { Proof = initial.Proof! with { HeadMessageId = "other-message" } },
        };
        IAggregateActor actor = HeadOnlyActor(initial);
        actor.InspectSourceProofAsync().Returns(initial, changed);
        (StreamsController controller, _, _, _) = CreateController(actor);
        BindWorkload(controller);

        (await controller.ReadWorkloadStreamAsync(HeadRequest())).ShouldBeOfType<StatusCodeResult>()
            .StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        actor.ReceivedCalls().Select(call => call.GetMethodInfo().Name)
            .ShouldBe([nameof(IAggregateActor.InspectSourceProofAsync), nameof(IAggregateActor.InspectSourceProofAsync)]);
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("invalid")]
    [InlineData("erased")]
    [InlineData("missing")]
    [InlineData("foreign-tenant")]
    [InlineData("foreign-domain")]
    [InlineData("foreign-aggregate")]
    [InlineData("bad-empty-seed")]
    [InlineData("bad-digest")]
    public async Task NativeHeadInspectionRefusesUnavailableOrInvalidSourceWithoutReadingEvents(string change)
    {
        SourceProofInspection initial = NativeInspection(0);
        initial = change switch
        {
            "unsupported" => initial with { Status = SourceProofInspectionStatus.Unsupported, Proof = null },
            "invalid" => initial with { Status = SourceProofInspectionStatus.Invalid },
            "erased" => initial with { Erased = true },
            "missing" => initial with { Exists = false },
            "foreign-tenant" => initial with { Proof = initial.Proof! with { TenantId = "other-tenant" } },
            "foreign-domain" => initial with { Proof = initial.Proof! with { Domain = "other-domain" } },
            "foreign-aggregate" => initial with { Proof = initial.Proof! with { AggregateId = "other-aggregate" } },
            "bad-empty-seed" => initial with { Proof = initial.Proof! with { CumulativeDigest = new string('a', 64) } },
            _ => initial with { Proof = initial.Proof! with { CumulativeDigest = "bad" } },
        };
        IAggregateActor actor = HeadOnlyActor(initial);
        (StreamsController controller, _, _, _) = CreateController(actor);
        BindWorkload(controller);

        IActionResult result = await controller.ReadWorkloadStreamAsync(HeadRequest());
        if (change == "missing") AssertProblem(result, StatusCodes.Status404NotFound);
        else result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        actor.ReceivedCalls().Select(call => call.GetMethodInfo().Name).ShouldBe([nameof(IAggregateActor.InspectSourceProofAsync)]);
    }

    [Theory]
    [InlineData("workload")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    public async Task NativeHeadInspectionRejectsChangedSignedTargetOrWorkloadBeforeActor(string change)
    {
        using RSA key = RSA.Create(2048);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:BoundStreamRead:AllowedDomains:projects"] = Domain,
            ["Authentication:BoundStreamRead:PublicKeys:projects"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
        }).Build();
        (StreamsController controller, IActorProxyFactory proxies, _, _) = CreateController(configuration: configuration);
        BindWorkload(controller, change == "workload" ? "foreign-workload" : "projects");
        StreamReadRequest original = HeadRequest();
        controller.Request.Headers[BoundStreamReadProof.HeaderName] = BoundStreamReadProof.Issue(original, "projects",
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()), DateTimeOffset.UtcNow);
        StreamReadRequest request = change switch
        {
            "tenant" => original with { Tenant = "other-tenant" },
            "domain" => original with { Domain = "other-domain" },
            "aggregate" => original with { AggregateId = "other-aggregate" },
            _ => original,
        };

        (await controller.ReadWorkloadStreamAsync(request)).ShouldBeOfType<ForbidResult>();
        proxies.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task NativeHeadInspectionPreservesPublicDenialAndNativePageSizeBound()
    {
        IAggregateActor actor = HeadOnlyActor(NativeInspection(5));
        (StreamsController controller, _, _, _) = CreateController(actor);
        (await controller.ReadStreamAsync(HeadRequest())).ShouldBeOfType<ForbidResult>();
        actor.ReceivedCalls().ShouldBeEmpty();
        BindWorkload(controller);
        (await controller.ReadWorkloadStreamAsync(HeadRequest() with { PageSize = 256 })).ShouldBeOfType<BadRequestResult>();
        actor.ReceivedCalls().Select(call => call.GetMethodInfo().Name).ShouldBe([nameof(IAggregateActor.InspectSourceProofAsync)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeHeadInspectionPropagatesCancellationDuringEitherObservation(bool second)
    {
        using var canceled = new CancellationTokenSource();
        IAggregateActor actor = HeadOnlyActor(NativeInspection(5));
        int calls = 0;
        actor.InspectSourceProofAsync().Returns(_ =>
        {
            if (++calls == (second ? 2 : 1))
            {
                canceled.Cancel();
                return new TaskCompletionSource<SourceProofInspection>().Task;
            }
            return Task.FromResult(NativeInspection(5));
        });
        (StreamsController controller, _, _, _) = CreateController(actor);
        BindWorkload(controller);
        await Should.ThrowAsync<OperationCanceledException>(() => controller.ReadWorkloadStreamAsync(HeadRequest(), canceled.Token));
        calls.ShouldBe(second ? 2 : 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeHeadInspectionNormalizesTransportFailureDuringEitherObservation(bool second)
    {
        IAggregateActor actor = HeadOnlyActor(NativeInspection(5));
        int calls = 0;
        actor.InspectSourceProofAsync().Returns(_ => ++calls == (second ? 2 : 1)
            ? Task.FromException<SourceProofInspection>(new HttpRequestException("private transport diagnostics"))
            : Task.FromResult(NativeInspection(5)));
        (StreamsController controller, _, _, _) = CreateController(actor);
        BindWorkload(controller);
        ProblemDetails problem = AssertProblem(await controller.ReadWorkloadStreamAsync(HeadRequest()), StatusCodes.Status503ServiceUnavailable);
        JsonSerializer.Serialize(problem).ShouldNotContain("private transport diagnostics");
        actor.ReceivedCalls().All(call => call.GetMethodInfo().Name == nameof(IAggregateActor.InspectSourceProofAsync)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("range")]
    [InlineData("continuation")]
    [InlineData("page-size")]
    [InlineData("identity")]
    public async Task NativeHeadInspectionKeepsOrdinaryInputValidationBeforeActorAccess(string invalid)
    {
        (StreamsController controller, IActorProxyFactory proxies, _, _) = CreateController();
        BindWorkload(controller);
        StreamReadRequest request = invalid switch
        {
            "range" => HeadRequest() with { FromSequence = -1 },
            "continuation" => HeadRequest() with { ContinuationToken = new("unexpected") },
            "page-size" => HeadRequest() with { PageSize = 0 },
            _ => HeadRequest() with { AggregateId = "invalid/identity" },
        };
        AssertProblem(await controller.ReadWorkloadStreamAsync(request), StatusCodes.Status400BadRequest);
        proxies.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task OrdinaryNativeEventPageStillReadsRangeAndRechecksProof()
    {
        SourceProofInspection inspection = NativeInspection(1);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.InspectSourceProofAsync().Returns(inspection);
        actor.ReadNativeEventsBoundedRangeAsync(0, 1, 101, 2_097_152)
            .Returns(new SourceEventRange([BuildEnvelope(1)], 1));
        (StreamsController controller, _, _, _) = CreateController(actor);
        BindWorkload(controller);

        StreamReadPage page = (await controller.ReadWorkloadStreamAsync(HeadRequest() with { ToSequence = null }))
            .ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<StreamReadPage>();
        page.Events.Single().SequenceNumber.ShouldBe(1);
        page.Events.Single().NativeEventDigest.ShouldBe(SourceProofCodec.ComputeEventHash(BuildEnvelope(1)));
        page.Metadata.EventCount.ShouldBe(1);
        page.Metadata.NativeProof.ShouldBe(SourceProofCodec.ToNative(inspection.Proof!));
        await actor.Received(2).InspectSourceProofAsync();
        await actor.Received(1).ReadNativeEventsBoundedRangeAsync(0, 1, 101, 2_097_152);
    }

    private static StreamReadRequest HeadRequest()
        => new(Tenant, Domain, AggregateId, FromSequence: 0, ToSequence: 0) { RequireNativeProof = true };

    private static SourceProofInspection NativeInspection(int head, int retainedFloor = 1)
    {
        var identity = new AggregateIdentity(Tenant, Domain, AggregateId);
        SourceProof proof = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        for (int sequence = 1; sequence <= head; sequence++) proof = SourceProofCodec.Extend(proof, BuildEnvelope(sequence));
        proof = proof with { RetainedFloor = retainedFloor };
        return new(SourceProofInspectionStatus.Available, true, head, false, proof);
    }

    private static IAggregateActor HeadOnlyActor(SourceProofInspection inspection)
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.InspectSourceProofAsync().Returns(inspection);
        actor.GetStreamMetadataAsync().ThrowsAsync(new InvalidOperationException("Metadata inspection used legacy stream read."));
        actor.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long?>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("Metadata inspection fetched events."));
        actor.ReadNativeEventsBoundedRangeAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("Metadata inspection fetched native events."));
        return actor;
    }

    private static void BindWorkload(StreamsController controller, string workload = "projects")
        => controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, workload),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, Tenant),
            new Claim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, Domain),
        ], "test-workload"));

    [Fact]
    public async Task ReadStreamAsyncWithInvalidRangeRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: 10,
            ToSequence: 5));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidRange);
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithContinuationRejectsBeforeActorProxyUntilTokenSupportExists() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            ContinuationToken: new ReplayContinuationToken("opaque")));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidContinuation);
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithDeniedTenantRejectsBeforeRbacAndActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController();
        tenantValidator.ConfiguredResult = TenantValidationResult.Denied("No tenant access.");

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status403Forbidden);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.UnauthorizedTenant);
        rbacValidator.ReceivedRequests.ShouldBeEmpty();
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithDeniedReplayScopeRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController();
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Denied("Missing replay permission.");

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status403Forbidden);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.ForbiddenReplayScope);
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncReturnsOrderedBoundedAggregatePage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 4));
        _ = actor.ReadEventsRangeAsync(1, null, 3).Returns([
            BuildEnvelope(2) with { GlobalPosition = 102 },
            BuildEnvelope(3) with { GlobalPosition = 109 },
            BuildEnvelope(4),
        ]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: 1,
            PageSize: 2));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Events.Select(e => e.SequenceNumber).ShouldBe([2, 3]);
        page.Events.Select(e => e.GlobalPosition).ShouldBe([102, 109]);
        page.Metadata.LastSequenceReturned.ShouldBe(3);
        page.Metadata.LatestSequence.ShouldBe(4);
        page.Metadata.IsTruncated.ShouldBeTrue();
        // P-D3: continuation tokens are deferred until request-binding is implemented.
        // Server returns null and callers paginate via FromSequence = lastSequenceReturned.
        page.Metadata.NextContinuationToken.ShouldBeNull();
        _ = await actor.Received(1).ReadEventsRangeAsync(1, null, 3);
    }

    [Fact]
    public async Task ReadStreamAsyncPreservesStoredPayloadVersion() {
        byte[] payload = "{\"name\":\"version-two\"}"u8.ToArray();
        ServerEventEnvelope source = BuildEnvelope(1, payload, metadata: null) with
        {
            MetadataVersion = 2,
            PayloadVersion = 2,
        };
        source = source with { ApplicationPayloadDigest = EventLogicalDigest.Compute(source, "json", EventLogicalDigest.HashPayload(payload)) };
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 1));
        _ = actor.ReadEventsRangeAsync(0, null, 101).Returns([source]);
        (StreamsController controller, _, _, _) = CreateController(actor);

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        StreamReadEvent readEvent = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<StreamReadPage>().Events.Single();
        readEvent.PayloadVersion.ShouldBe(2);
        readEvent.Payload.ShouldBe(payload);
    }

    [Fact]
    public async Task ReadStreamAsyncRejectsTamperedUnprotectedPayloadWithoutReturningBytes() {
        byte[] original = "{\"name\":\"original\"}"u8.ToArray();
        ServerEventEnvelope source = BuildEnvelope(1, original, metadata: null) with { MetadataVersion = 2 };
        source = source with { ApplicationPayloadDigest = EventLogicalDigest.Compute(source, "json", EventLogicalDigest.HashPayload(original)) };
        source = source with { Payload = "{\"name\":\"tampered-secret\"}"u8.ToArray() };
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 1));
        _ = actor.ReadEventsRangeAsync(0, null, 101).Returns([source]);
        (StreamsController controller, _, _, _) = CreateController(actor);

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.CorruptEvent);
        JsonSerializer.Serialize(problem).ShouldNotContain("tampered-secret");
    }

    [Fact]
    public async Task ReadStreamAsyncRejectsTamperedDecryptedPayloadWithoutReturningBytes() {
        byte[] original = "{\"name\":\"original\"}"u8.ToArray();
        var metadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected, 1, "test-aead", "safe-test-alias", null, null);
        ServerEventEnvelope source = BuildEnvelope(1, [9, 9, 9], metadata) with { MetadataVersion = 2 };
        source = source with { ApplicationPayloadDigest = EventLogicalDigest.Compute(source, "json", EventLogicalDigest.HashPayload(original)) };
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 1));
        _ = actor.ReadEventsRangeAsync(0, null, 101).Returns([source]);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.TryUnprotectEventPayloadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
                Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>())
            .Returns(PayloadUnprotectionOutcome.Readable("{\"name\":\"decrypted-tampered-secret\"}"u8.ToArray(), "json", metadata));
        (StreamsController controller, _, _, _) = CreateController(actor, protection);

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.CorruptEvent);
        JsonSerializer.Serialize(problem).ShouldNotContain("decrypted-tampered-secret");
    }

    [Theory]
    [InlineData("foreign-tenant", Domain, AggregateId)]
    [InlineData(Tenant, "foreign-domain", AggregateId)]
    [InlineData(Tenant, Domain, "foreign-party")]
    public async Task AuthorizedLocalMetadataWithForeignEnvelope_DeniesBeforePayloadUnprotect(string tenant, string domain, string aggregate)
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 1));
        actor.ReadEventsRangeAsync(0, null, 101).Returns([BuildEnvelope(1) with
            { TenantId = tenant, Domain = domain, AggregateId = aggregate, Payload = "foreign-secret"u8.ToArray() }]);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        (StreamsController controller, _, _, _) = CreateController(actor, protection);
        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));
        ProblemDetails problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        string json = JsonSerializer.Serialize(problem);
        json.ShouldNotContain("foreign-secret");
        if (tenant != Tenant) { json.ShouldNotContain(tenant); }
        if (domain != Domain) { json.ShouldNotContain(domain); }
        if (aggregate != AggregateId) { json.ShouldNotContain(aggregate); }
        await protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncMapsMissingEventToSafeProblem() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 3));
        _ = actor.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long?>(), Arg.Any<int>())
            .ThrowsAsync(new MissingEventException(2, Tenant, Domain, AggregateId));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status404NotFound);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingEvent);
        _ = problem.Detail.ShouldNotBeNull();
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncWithUnreadableProtectedPayloadReturnsProblemDetailsInsteadOfPayload() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 1));
        _ = actor.ReadEventsRangeAsync(1, null, 2).Returns([
            BuildEnvelope(
                1,
                [9, 9, 9],
                new EventStorePayloadProtectionMetadata(
                    PayloadProtectionState.Protected,
                    1,
                    Scheme: "test-aead",
                    KeyAlias: "safe-test-alias",
                    ContentHint: null,
                    CompatibilityFlags: null)),
        ]);
        var protectionService = new FakeUnreadableProtectionService();
        protectionService.ConfigureEventUnreadable(UnreadableProtectedDataReason.MissingKey);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor, protectionService);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId, FromSequence: 1, PageSize: 1));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status422UnprocessableEntity);
        problem.Type.ShouldBe(UnreadableProtectedDataProblem.TypeUri);
        problem.Extensions[GatewayProblemDetailsExtensions.CorrelationId].ShouldBe("corr-stream-test");
        problem.Extensions["reasonCode"].ShouldBe(UnreadableProtectedDataReasonCodes.MissingKey);
        problem.Extensions[UnreadableProtectedDataProblem.ExtensionSequenceNumber].ShouldBe(1L);
        string json = JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        ProtectedDataLeakSentinel.AssertNoLeak([json, .. problem.Extensions.Values.Select(static v => v?.ToString())]);
        protectionService.EventUnprotectInvocations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReadStreamAsyncWithMissingAggregateReturnsBadRequestBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingRequiredField);
        string json = System.Text.Json.JsonSerializer.Serialize(problem, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        json.ShouldContain("\"reasonCode\"");
        json.ShouldNotContain("\"ReasonCode\"", Case.Sensitive);
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Theory]
    [InlineData(Tenant, Domain, ":actor-key")]
    // P21-6P: uppercase tenant/domain identifiers must be rejected so cross-case callers
    // cannot address different state-store rows under the illusion of "the same tenant."
    [InlineData("Tenant-A", Domain, AggregateId)]
    [InlineData(Tenant, "Party", AggregateId)]
    [InlineData("TENANT", Domain, AggregateId)]
    public async Task ReadStreamAsyncWithInvalidIdentityShapeReturnsDedicatedReasonBeforeActorProxy(
        string tenant,
        string domain,
        string aggregateId) {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(tenant, domain, aggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidAggregateIdentity);
        problem.Detail.ShouldBe("Stream identity is invalid.");
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithTooLargeFromSequenceRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: (long)int.MaxValue + 1));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidRange);
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncAllowsNearBoundaryRangeSupportedByActor() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        long fromSequence = int.MaxValue - 100L;
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: int.MaxValue - 50L));
        _ = actor.ReadEventsRangeAsync(fromSequence, null, 101).Returns([BuildEnvelope(int.MaxValue - 99L)]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: fromSequence,
            PageSize: 100));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Events.Single().SequenceNumber.ShouldBe(int.MaxValue - 99L);
        _ = await actor.Received(1).ReadEventsRangeAsync(fromSequence, null, 101);
    }

    [Fact]
    public async Task ReadStreamAsyncWithTooLargeToSequenceRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            ToSequence: (long)int.MaxValue + 1));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidRange);
        _ = actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    // P22-6P: ToSequence == FromSequence is now a valid request that returns an empty page
    // (200 OK with EventCount=0, LastSequenceReturned=null, LatestSequence=currentSequence).
    // Previously this returned 400 InvalidRange. Documented in stream-replay-api.md.
    [Fact]
    public async Task ReadStreamAsyncWithToSequenceEqualToFromSequenceReturnsEmptyPage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 10));
        _ = actor.ReadEventsRangeAsync(5, 5, Arg.Any<int>()).Returns([]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: 5,
            ToSequence: 5));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Metadata.LastSequenceReturned.ShouldBeNull();
        page.Metadata.EventCount.ShouldBe(0);
        page.Metadata.LatestSequence.ShouldBe(10);
    }

    [Fact]
    public async Task ReadStreamAsyncReturnsNullLastSequenceForEmptyPage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 10));
        _ = actor.ReadEventsRangeAsync(10, null, 101).Returns([]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId, FromSequence: 10));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Metadata.LastSequenceReturned.ShouldBeNull();
        page.Metadata.LatestSequence.ShouldBe(10);
        page.Metadata.EventCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadStreamAsyncMissingStreamReturnsSafeNotFound() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: false, CurrentSequence: 0));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status404NotFound);
        problem.Type.ShouldBe(ProblemTypeUris.NotFound);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingStream);
        _ = await actor.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncExistingZeroEventStreamReturnsEmptyPage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 0));
        _ = actor.ReadEventsRangeAsync(0, null, 101).Returns([]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Events.ShouldBeEmpty();
        page.Metadata.LatestSequence.ShouldBe(0);
        page.Metadata.LastSequenceReturned.ShouldBeNull();
    }

    [Fact]
    public async Task ReadStreamAsyncLatestSequenceDoesNotRegressBelowReturnedEvents() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 2));
        _ = actor.ReadEventsRangeAsync(1, null, 3).Returns([BuildEnvelope(2), BuildEnvelope(3)]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: 1,
            PageSize: 2));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Metadata.LatestSequence.ShouldBe(3);
    }

    [Fact]
    public async Task ReadStreamAsyncWithUnavailableActorReturnsServiceUnavailable() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 3));
        _ = actor.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long?>(), Arg.Any<int>())
            .ThrowsAsync(new HttpRequestException("dapr unavailable"));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.ServiceUnavailable);
        controller.Response.Headers.RetryAfter.ToString().ShouldBe("5");
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncWithActorApplicationFailureReturnsInternalError() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().ThrowsAsync(new ActorMethodInvocationException("actor failed", new InvalidOperationException("serializer defect"), false));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status500InternalServerError);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InternalError);
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncWithNestedApplicationFailureDoesNotBecomeServiceUnavailable() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().ThrowsAsync(new ActorMethodInvocationException(
            "actor failed",
            new InvalidOperationException("application wrapper", new HttpRequestException("transport-looking inner")),
            false));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status500InternalServerError);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InternalError);
        AssertNoForbiddenLeakage(problem);
    }

    private static (StreamsController Controller, IActorProxyFactory ActorProxyFactory, FakeTenantValidator TenantValidator, FakeRbacValidator RbacValidator) CreateController(
        IAggregateActor? actor = null,
        IEventPayloadProtectionService? payloadProtectionService = null,
        IConfiguration? configuration = null) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        if (actor is not null) {
            _ = actorProxyFactory
                .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
                .Returns(actor);
        }

        var tenantValidator = new FakeTenantValidator {
            ConfiguredResult = TenantValidationResult.Allowed,
        };
        var rbacValidator = new FakeRbacValidator {
            ConfiguredResult = RbacValidationResult.Allowed,
        };
        var controller = new StreamsController(
            actorProxyFactory,
            tenantValidator,
            rbacValidator,
            NullLogger<StreamsController>.Instance,
            payloadProtectionService,
            configuration) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", "user-1"),
                    ], "test")),
                },
            },
        };
        controller.HttpContext.Items["CorrelationId"] = "corr-stream-test";

        return (controller, actorProxyFactory, tenantValidator, rbacValidator);
    }

    private static ProblemDetails AssertProblem(IActionResult result, int statusCode) {
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(statusCode);
        return objectResult.Value.ShouldBeOfType<ProblemDetails>();
    }

    private static void AssertNoForbiddenLeakage(ProblemDetails problem) {
        string serialized = System.Text.Json.JsonSerializer.Serialize(problem);
        foreach (string forbidden in new[] {
            "state store",
            "statestore",
            "dapr://",
            "projection-rebuild-checkpoints:",
            "redis://",
            "localhost:",
            "127.0.0.1",
            "Bearer ",
            "stack trace",
            "at Hexalith.",
            "payload",
            "protected",
            "display name",
        }) {
            serialized.ShouldNotContain(forbidden, Case.Insensitive);
        }

        foreach (string forbiddenPattern in new[] { "\\bAggregateActor\\b", "\\bETag\\b" }) {
            System.Text.RegularExpressions.Regex.IsMatch(
                serialized,
                forbiddenPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).ShouldBeFalse();
        }
    }

    private static ServerEventEnvelope BuildEnvelope(long seq)
        => BuildEnvelope(seq, [], metadata: null);

    private static ServerEventEnvelope BuildEnvelope(
        long seq,
        byte[] payload,
        EventStorePayloadProtectionMetadata? metadata)
        => new(
            MessageId: $"msg-{seq}",
            AggregateId: AggregateId,
            AggregateType: "Party",
            TenantId: Tenant,
            Domain: Domain,
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: DateTimeOffset.UnixEpoch.AddSeconds(seq),
            CorrelationId: $"corr-{seq}",
            CausationId: $"cause-{seq}",
            UserId: "user-1",
            DomainServiceVersion: "v1",
            EventTypeName: "PartyRenamed",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: payload,
            Extensions: metadata is null
                ? null
                : EventStorePayloadProtectionMetadataCarrier.Write((IDictionary<string, string>?)null, metadata));
}
