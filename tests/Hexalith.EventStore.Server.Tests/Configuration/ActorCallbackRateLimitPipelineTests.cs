extern alias eventstore;

using System.Net;
using System.Security.Cryptography;
using System.Net.Http.Json;

using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>Exercises actor channel authorization and both global quotas in the production HTTP pipeline.</summary>
public sealed class ActorCallbackRateLimitPipelineTests
{
    private static readonly string ChannelToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private const string ActorType = "IdempotencyAdmissionActor";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaturatingOneActorKeepsOtherActorBoundedAndIndependentAsync(bool tenantLimit)
    {
        await using WebApplicationFactory<EventStoreProgram> factory = CreateFactory(tenantLimit);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, ChannelToken);

        using HttpResponseMessage first = await client.DeleteAsync($"/actors/{ActorType}/actor-a");
        using HttpResponseMessage second = await client.DeleteAsync($"/actors/{ActorType}/actor-a");
        using HttpResponseMessage saturated = await client.DeleteAsync($"/actors/{ActorType}/actor-a");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        saturated.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        saturated.Headers.RetryAfter.ShouldNotBeNull();

        using HttpResponseMessage otherFirst = await client.DeleteAsync($"/actors/{ActorType}/actor-b");
        using HttpResponseMessage otherSecond = await client.DeleteAsync($"/actors/{ActorType}/actor-b");
        using HttpResponseMessage otherSaturated = await client.DeleteAsync($"/actors/{ActorType}/actor-b");
        otherFirst.StatusCode.ShouldBe(HttpStatusCode.OK);
        otherSecond.StatusCode.ShouldBe(HttpStatusCode.OK);
        otherSaturated.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        otherSaturated.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("duplicate")]
    public async Task MappedActorCallbacksRejectUntrustedChannelAsync(string tokenMode)
    {
        await using WebApplicationFactory<EventStoreProgram> factory = CreateFactory(true);
        using HttpClient client = factory.CreateClient();
        if (tokenMode == "wrong")
        {
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "wrong-token");
        }
        else if (tokenMode == "duplicate")
        {
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, [ChannelToken, ChannelToken]);
        }

        using HttpResponseMessage response = await client.DeleteAsync($"/actors/{ActorType}/untrusted-actor");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnmatchedActorLookalikesCannotMintCallbackPartitionsAsync()
    {
        await using WebApplicationFactory<EventStoreProgram> factory = CreateFactory(true);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        using HttpResponseMessage first = await client.GetAsync($"/actors/{ActorType}/lookalike-a/unmapped");
        using HttpResponseMessage second = await client.GetAsync($"/actors/{ActorType}/lookalike-b/unmapped");
        using HttpResponseMessage saturated = await client.GetAsync($"/actors/{ActorType}/lookalike-c/unmapped");
        first.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        saturated.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        saturated.Headers.RetryAfter.ShouldNotBeNull();

        using HttpResponseMessage actor = await client.DeleteAsync($"/actors/{ActorType}/real-matched-actor");
        actor.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChannelTokenDoesNotAuthorizeWorkloadOperationAsync()
    {
        await using WebApplicationFactory<EventStoreProgram> factory = CreateFactory(true);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/trusted-effects", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static WebApplicationFactory<EventStoreProgram> CreateFactory(bool tenantLimit)
        => new WebApplicationFactory<EventStoreProgram>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Authority"] = "https://identity.example.test/realms/hexalith",
                ["Authentication:JwtBearer:Issuer"] = "https://identity.example.test/realms/hexalith",
                ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.RsaSha256,
                ["Authentication:JwtBearer:SigningKey"] = null,
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "true",
                ["Authentication:WorkloadIssuer:ClientId"] = "eventstore",
                ["Authentication:WorkloadIssuer:ClientSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
                ["EventStore:RateLimiting:PermitLimit"] = tenantLimit ? "2" : "100",
                ["EventStore:RateLimiting:ConsumerPermitLimit"] = tenantLimit ? "100" : "2",
                ["EventStore:RateLimiting:WindowSeconds"] = "60",
                ["EventStore:RateLimiting:ConsumerWindowSeconds"] = "60",
                ["EventStore:RateLimiting:SegmentsPerWindow"] = "1",
                ["EventStore:RateLimiting:ConsumerSegmentsPerWindow"] = "1",
                ["EventStore:RateLimiting:QueueLimit"] = "0",
            }));
            builder.ConfigureTestServices(WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService);
        });
}
