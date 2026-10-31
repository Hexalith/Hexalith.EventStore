extern alias SampleApi;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Sample.Tests.SampleApi;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Hexalith.EventStore.Sample.Tests;

/// <summary>Exercises generated API classification and credential-kind rejection in the real host.</summary>
public sealed class HttpHostSecurityTests
{
    private const string Key = "story514-sample-api-signing-key-at-least-32-bytes";

    /// <summary>Verifies generated Api Only Admits Human Bearers.</summary>
    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("channel", HttpStatusCode.Unauthorized)]
    [InlineData("workload", HttpStatusCode.Forbidden)]
    [InlineData("mixed", HttpStatusCode.Forbidden)]
    [InlineData("human", HttpStatusCode.OK)]
    public async Task GeneratedApiOnlyAdmitsHumanBearers(string credential, HttpStatusCode expected)
    {
        var gateway = new FakeEventStoreGatewayClient
        {
            QueryHandler = static (_, _, _) => Task.FromResult(new EventStoreQueryResult("query", JsonSerializer.SerializeToElement(new { value = 1 }), false, null)),
        };
        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:JwtBearer:Authority"] = null, ["Authentication:JwtBearer:Issuer"] = "story514",
                ["Authentication:JwtBearer:Audience"] = "sample-api", ["Authentication:JwtBearer:SigningKey"] = Key,
            }));
            builder.ConfigureTestServices(services => { services.AddSampleApiCommittedCatalog(); services.RemoveAll<IEventStoreGatewayClient>(); services.AddSingleton<IEventStoreGatewayClient>(gateway); });
        });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenant-a/counter/counter-1");
        if (credential is "human" or "workload" or "mixed") request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "story514", Audience = "sample-api", Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = credential == "workload" ? new Dictionary<string, object> { [EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType] = "peer" }
                : new Dictionary<string, object> { ["sub"] = "human" },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256),
        }));
        if (credential == "channel") request.Headers.Add(DaprAppChannelToken.HeaderName, "channel");
        if (credential == "mixed") request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, "assertion");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(expected);
        gateway.QueryCallCount.ShouldBe(credential == "human" ? 1 : 0);
        var endpoints = factory.Services.GetServices<EndpointDataSource>().SelectMany(static source => source.Endpoints).ToArray();
        endpoints.ShouldNotBeEmpty();
        (await EventStoreHttpEndpointInventory.ValidateAsync(endpoints, factory.Services).ConfigureAwait(true)).ShouldBeEmpty();
    }
}
