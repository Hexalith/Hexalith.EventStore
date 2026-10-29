using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

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
        actor.RetryAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted"));
        actor.SkipAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted"));
        actor.ArchiveAsync(Arg.Any<DeadLetterActionRequest>()).Returns(new DeadLetterActorActionResult(true, "accepted"));
        await using WebApplicationFactory<Program> factory = Factory(actor);
        using HttpClient client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Post, "/internal/dead-letters/" + operation);
        request.Content = JsonContent.Create(new { tenantId = "tenant-a", messageIds = new[] { "message-a" }, requestedBy = "forged" });
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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

    private static WebApplicationFactory<Program> Factory(IDeadLetterDrainActor actor, int maxActionItems = 100)
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
                ["EventStoreOperations:MaxActionItems"] = maxActionItems.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }));
            builder.ConfigureTestServices(services =>
            {
                foreach (ServiceDescriptor descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(DeadLetterBacklogReconciler)).ToArray()) services.Remove(descriptor);
                IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
                proxies.CreateActorProxy<IDeadLetterDrainActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(actor);
                services.RemoveAll<IActorProxyFactory>();
                services.AddSingleton(proxies);
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
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = scenario == "issuer" ? "wrong" : "story514", Audience = scenario == "audience" ? "wrong" : "operations",
            IssuedAt = DateTime.UtcNow.AddHours(-1), NotBefore = DateTime.UtcNow.AddHours(-1),
            Expires = scenario == "expired" ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddMinutes(5), Claims = claims,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(scenario == "signature" ? Key + "wrong" : Key)), SecurityAlgorithms.HmacSha256),
        });
    }
}
