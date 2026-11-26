using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Operations.Actors;
using Hexalith.EventStore.Operations.Models;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>Verifies independent human JWT, operation, tenant, and channel checks before actor work.</summary>
public sealed partial class HttpHostSecurityTests
{
    private const string Channel = "story514-operations-channel";
    private const string Key = "story514-operations-signing-key-at-least-32-bytes";

    /// <summary>Verifies rejected Credentials Produce Zero Actor Effects.</summary>
    [Theory]
    [InlineData("forged-caller")]
    [InlineData("invalid")]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("workload")]
    [InlineData("mixed")]
    [InlineData("duplicate-bearer")]
    [InlineData("channel-only")]
    [InlineData("missing-channel")]
    [InlineData("wrong-channel")]
    [InlineData("duplicate-channel")]
    [InlineData("wrong-caller")]
    [InlineData("read-only")]
    public async Task RejectedCredentialsProduceZeroActorEffects(string scenario)
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/dead-letters/retry")
        {
            Content = JsonContent.Create(new { tenantId = "tenant-a", messageIds = new[] { "message-a" } }),
        };
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName,
            scenario == "wrong-caller" ? "peer" : "eventstore-admin");
        if (scenario != "missing-channel") request.Headers.TryAddWithoutValidation(DaprAppChannelToken.HeaderName,
            scenario == "duplicate-channel" ? [Channel, Channel] : [scenario == "wrong-channel" ? "wrong" : Channel]);
        if (scenario != "channel-only") request.Headers.TryAddWithoutValidation("Authorization",
            scenario == "duplicate-bearer" ? ["Bearer " + Token(), "Bearer " + Token()] : ["Bearer " + (scenario is "invalid" or "forged-caller" ? "unvalidated" : Token(scenario))]);
        if (scenario == "mixed") request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, Token("workload"));
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies rejected Tenant Produces Zero Actor Effects.</summary>
    [Theory]
    [InlineData("tenant-b")]
    [InlineData("system")]
    [InlineData(" tenant-a")]
    [InlineData("Tenant-a")]
    [InlineData("")]
    public async Task RejectedTenantProducesZeroActorEffects(string tenant)
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/retry");
        request.Content = JsonContent.Create(new { tenantId = tenant, messageIds = new[] { "message-a" } });
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies valid Human Relay Preserves Tenant And Uses Validated Attribution.</summary>
    [Theory]
    [InlineData("retry")]
    [InlineData("skip")]
    [InlineData("archive")]
    public async Task ValidHumanRelayPreservesTenantAndUsesValidatedAttribution(string operation)
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        actor.RetryAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted") { OperationId = "operation-a", State = "pending" });
        actor.SkipAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted") { OperationId = "operation-a", State = "completed" });
        actor.ArchiveAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted") { OperationId = "operation-a", State = "completed" });
        await using WebApplicationFactory<Program> factory = Factory(actor, isolatedRecovery: true);
        using HttpClient client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/" + operation);
        request.Content = JsonContent.Create(new { tenantId = "tenant-a", messageIds = new[] { "message-a" }, requestedBy = "forged" });
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(operation == "retry" ? HttpStatusCode.Accepted : HttpStatusCode.OK);
        AdminOperationResult outcome = (await response.Content.ReadFromJsonAsync<AdminOperationResult>()).ShouldNotBeNull();
        outcome.OperationId.ShouldBe("operation-a");
        outcome.Success.ShouldBe(operation != "retry");
        var call = actor.ReceivedCalls().ShouldHaveSingleItem();
        var action = call.GetArguments()[0].ShouldBeOfType<DeadLetterActionRequest>();
        action.TenantId.ShouldBe("tenant-a");
        action.RequestedBy.ShouldBe("validated-human");
        action.MessageIds.ShouldBe(["message-a"]);
    }

    /// <summary>Verifies list Requires One Explicit Authorized Tenant.</summary>
    [Theory]
    [InlineData("/internal/dead-letters", false)]
    [InlineData("/internal/dead-letters?tenantId=tenant-a&tenantId=tenant-a", false)]
    [InlineData("/internal/dead-letters?tenantId=tenant-a", true)]
    public async Task ListRequiresOneExplicitAuthorizedTenant(string path, bool allowed)
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        actor.ListAsync(Arg.Any<DeadLetterListRequest>()).Returns(new DeadLetterListResult([], 0, null));
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Get, path, "read-only");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        actor.ReceivedCalls().Count().ShouldBe(allowed ? 1 : 0);
    }

    /// <summary>Verifies every Operations Endpoint Has Resolved Named Classification.</summary>
    [Fact]
    public async Task EveryOperationsEndpointHasResolvedNamedClassification()
    {
        await using WebApplicationFactory<Program> factory = Factory(Substitute.For<IDeadLetterDrainActor>());
        using HttpClient client = factory.CreateClient();
        var endpoints = factory.Services.GetServices<EndpointDataSource>().SelectMany(static source => source.Endpoints);
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, factory.Services).ConfigureAwait(true)).ShouldBeEmpty();
        using HttpResponseMessage alive = await client.GetAsync("/alive", TestContext.Current.CancellationToken).ConfigureAwait(true);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies duplicate Action Scope Rejects Before Actor Work.</summary>
    [Theory]
    [InlineData("{\"tenantId\":\"tenant-a\",\"tenantId\":\"tenant-a\",\"messageIds\":[\"message-a\"]}")]
    [InlineData("{\"tenantId\":\"tenant-b\",\"TenantId\":\"tenant-a\",\"messageIds\":[\"message-a\"]}")]
    public async Task DuplicateActionScopeRejectsBeforeActorWork(string body)
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/retry");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task SummaryRequiresAuthorizedScopeAndReturnsOldestVisibleAge()
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        actor.ListAsync(Arg.Any<DeadLetterListRequest>()).Returns(new DeadLetterListResult([], 1, null)
        {
            OldestFailureAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        });
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();

        using var hidden = Authorized(HttpMethod.Get, "/internal/dead-letters/count?tenantId=tenant-b&detailed=true", "read-only");
        using HttpResponseMessage excluded = await client.SendAsync(hidden);
        excluded.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        actor.ReceivedCalls().ShouldBeEmpty();

        using var visible = Authorized(HttpMethod.Get, "/internal/dead-letters/count?tenantId=tenant-a&detailed=true", "read-only");
        using HttpResponseMessage response = await client.SendAsync(visible);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        DeadLetterSummary summary = (await response.Content.ReadFromJsonAsync<DeadLetterSummary>()).ShouldNotBeNull();
        summary.Count.ShouldBe(1);
        summary.OldestAge.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMinutes(2));
        _ = await actor.Received(1).ListAsync(new DeadLetterListRequest("tenant-a", 1, 0));
    }

    [Fact]
    public async Task ListCursorDoesNotExposeIndexAndInvalidCursorDeniesBeforeLookup()
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        actor.ListAsync(Arg.Any<DeadLetterListRequest>()).Returns(new DeadLetterListResult([], 2, 1));
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        const string path = "/internal/dead-letters?tenantId=tenant-a";

        using var initial = Authorized(HttpMethod.Get, path, "read-only");
        using HttpResponseMessage response = await client.SendAsync(initial);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResult<DeadLetterEntry>>()).ShouldNotBeNull();
        page.ContinuationToken.ShouldNotBeNullOrWhiteSpace();
        page.ContinuationToken.ShouldNotBe("1");

        using var next = Authorized(HttpMethod.Get, path + "&continuationToken=" + Uri.EscapeDataString(page.ContinuationToken!), "read-only");
        using HttpResponseMessage nextPage = await client.SendAsync(next);
        nextPage.StatusCode.ShouldBe(HttpStatusCode.OK);
        _ = await actor.Received(1).ListAsync(Arg.Is<DeadLetterListRequest>(value => value.TenantId == "tenant-a" && value.Offset == 1));

        using var request = Authorized(HttpMethod.Get, path + "&continuationToken=1", "read-only");
        using HttpResponseMessage rejected = await client.SendAsync(request);
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _ = await actor.Received(2).ListAsync(Arg.Any<DeadLetterListRequest>());
    }

    /// <summary>Verifies the Development sink's JSON shape reaches the actor and its outcome parses back.</summary>
    [Theory]
    [InlineData(EventStoreSubscriberPoisonOutcome.Retryable)]
    [InlineData(EventStoreSubscriberPoisonOutcome.Retained)]
    [InlineData(EventStoreSubscriberPoisonOutcome.Conflict)]
    public async Task SubscriberDeliveryRoundTripsTheDevelopmentSinkWireContract(EventStoreSubscriberPoisonOutcome outcome)
    {
        var actorHttp = new ActorMethodHandler(outcome);
        await using WebApplicationFactory<Program> factory = Factory(Substitute.For<IDeadLetterDrainActor>(), isolatedRecovery: true, proxies: ActorProxies(actorHttp));
        using HttpClient client = factory.CreateClient();
        EventStoreSubscriberDelivery delivery = SubscriberDelivery();
        var failure = new EventStoreSubscriberFailure(EventStoreSubscriberFailureReason.ProcessingFailure, true, 10);
        using HttpRequestMessage request = SubscriberDeliveryRequest("works", JsonContent.Create(new { delivery, failure }));

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<EventStoreSubscriberPoisonOutcome>(TestContext.Current.CancellationToken)).ShouldBe(outcome);
        actorHttp.Path.ShouldNotBeNull().ShouldEndWith("/method/" + nameof(IDeadLetterDrainActor.ObserveSubscriberDeliveryAsync));
        SubscriberDeliveryObservation forwarded = System.Text.Json.JsonSerializer.Deserialize<SubscriberDeliveryObservation>(
            actorHttp.Body.ShouldNotBeNull(), System.Text.Json.JsonSerializerOptions.Web).ShouldNotBeNull();
        forwarded.Delivery.ShouldBe(delivery with { SubscriberAppId = "works", Envelope = forwarded.Delivery.Envelope });
        forwarded.Delivery.Envelope.Payload.ShouldBe(delivery.Envelope.Payload);
        forwarded.Delivery.IsValid().ShouldBeTrue();
        forwarded.Failure.ShouldBe(failure);
        forwarded.ObservedAtUtc.ShouldNotBe(default);
    }

    /// <summary>Verifies caller, body and profile refusals on the subscriber observation route never reach the actor.</summary>
    [Theory]
    [InlineData("wrong-caller", HttpStatusCode.Forbidden)]
    [InlineData("malformed", HttpStatusCode.BadRequest)]
    public async Task SubscriberDeliveryRefusesBeforeActorWork(string scenario, HttpStatusCode expected)
    {
        var actorHttp = new ActorMethodHandler(EventStoreSubscriberPoisonOutcome.Retained);
        await using WebApplicationFactory<Program> factory = Factory(Substitute.For<IDeadLetterDrainActor>(), isolatedRecovery: true, proxies: ActorProxies(actorHttp));
        using HttpClient client = factory.CreateClient();
        HttpContent content = scenario == "malformed"
            ? new StringContent("{\"delivery\":", Encoding.UTF8, "application/json")
            : JsonContent.Create(new { delivery = SubscriberDelivery(), failure = (EventStoreSubscriberFailure?)null });
        using HttpRequestMessage request = SubscriberDeliveryRequest(scenario == "wrong-caller" ? "peer" : "works", content);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(expected);
        actorHttp.RequestCount.ShouldBe(0);
    }

    /// <summary>Verifies the observation route does not exist outside the isolated recovery profile.</summary>
    [Fact]
    public async Task SubscriberDeliveryRouteIsAbsentWhenTheRecoveryProfileIsOff()
    {
        var actorHttp = new ActorMethodHandler(EventStoreSubscriberPoisonOutcome.Retained);
        await using WebApplicationFactory<Program> factory = Factory(Substitute.For<IDeadLetterDrainActor>(), proxies: ActorProxies(actorHttp));
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = SubscriberDeliveryRequest("works",
            JsonContent.Create(new { delivery = SubscriberDelivery(), failure = (EventStoreSubscriberFailure?)null }));

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        factory.Services.GetServices<EndpointDataSource>().SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>()
            .ShouldNotContain(endpoint => endpoint.RoutePattern.RawText == "/internal/dead-letters/subscriber-delivery");
        actorHttp.RequestCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("/internal/dead-letters?tenantId=tenant-a")]
    [InlineData("/internal/dead-letters/count?tenantId=tenant-a&detailed=true")]
    public async Task OperatorReadFailureReturnsOpaqueUnavailableEvidence(string path)
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        _ = actor.ListAsync(Arg.Any<DeadLetterListRequest>())
            .Returns(Task.FromException<DeadLetterListResult>(new InvalidOperationException("private store information")));
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Get, path, "read-only");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("private store information");
    }

    [Fact]
    public async Task ValidRecoveryOutsideIsolatedProfileHasNoActorEffect()
    {
        IDeadLetterDrainActor actor = Substitute.For<IDeadLetterDrainActor>();
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/retry");
        request.Content = JsonContent.Create(new { tenantId = "tenant-a", messageIds = new[] { "message-a" } });
        using HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    private static ActorProxyFactory ActorProxies(HttpMessageHandler handler)
        => new(new ActorProxyOptions { HttpEndpoint = "http://127.0.0.1:3500" }, handler);

    private static EventStoreSubscriberDelivery SubscriberDelivery()
    {
        string id = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId();
        var envelope = new EventStoreDomainEventEnvelope(id, "work-a", "tenant-a", "WorkItemCreated", 1,
            DateTimeOffset.UnixEpoch.AddDays(1), "correlation-a", "json", "{\"value\":1}"u8.ToArray()) { Domain = "work" };
        return new EventStoreSubscriberDelivery("pubsub", "work.events", "/work/events", "hexalith-eventstore/tenant-a/work", id, envelope);
    }

    private static HttpRequestMessage SubscriberDeliveryRequest(string caller, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/dead-letters/subscriber-delivery") { Content = content };
        request.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
        request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.DaprCallerHeaderName, caller);
        return request;
    }

    private static WebApplicationFactory<Program> Factory(IDeadLetterDrainActor actor, int maxActionItems = 100, bool isolatedRecovery = false,
        IActorProxyFactory? proxies = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DaprAppChannelToken.ConfigurationKey] = Channel,
                ["Authentication:JwtBearer:Authority"] = null,
                ["Authentication:JwtBearer:Issuer"] = "story514",
                ["Authentication:JwtBearer:Audience"] = "operations",
                ["Authentication:JwtBearer:SigningKey"] = Key,
                ["EventStoreOperations:IsolatedSubscriberRecovery"] = isolatedRecovery.ToString(),
                ["EventStoreOperations:MaxActionItems"] = maxActionItems.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }));
            builder.ConfigureTestServices(services =>
            {
                foreach (ServiceDescriptor descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(DeadLetterBacklogReconciler)).ToArray()) services.Remove(descriptor);
                IActorProxyFactory factoryProxies = proxies ?? Substitute.For<IActorProxyFactory>();
                if (proxies is null)
                {
                    factoryProxies.CreateActorProxy<IDeadLetterDrainActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(actor);
                }

                services.RemoveAll<IActorProxyFactory>();
                services.AddSingleton(factoryProxies);
            });
        });

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string scenario = "valid")
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(scenario));
        request.Headers.Add(DaprAppChannelToken.HeaderName, Channel);
        return request;
    }

    private static string Token(string scenario = "valid", IReadOnlyDictionary<string, object>? additional = null)
    {
        var claims = new Dictionary<string, object>
        { ["sub"] = "validated-human", ["eventstore:tenant"] = "tenant-a" };
        if (scenario != "read-only") claims["eventstore:permission"] = "command:replay";
        if (scenario == "workload") claims[EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = "retry";
        foreach (var claim in additional ?? new Dictionary<string, object>()) claims[claim.Key] = claim.Value;
        string keyMaterial = scenario == "signature" ? Key + "wrong" : Key;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = scenario == "issuer" ? "wrong" : "story514", Audience = scenario == "audience" ? "wrong" : "operations",
            IssuedAt = DateTime.UtcNow.AddHours(-1), NotBefore = DateTime.UtcNow.AddHours(-1),
            Expires = scenario == "expired" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(5), Claims = claims,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyMaterial)), SecurityAlgorithms.HmacSha256),
        });
    }

    private sealed class ActorMethodHandler(EventStoreSubscriberPoisonOutcome outcome) : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        internal string? Path { get; private set; }

        internal byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Path = request.RequestUri?.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(outcome) };
        }
    }
}
