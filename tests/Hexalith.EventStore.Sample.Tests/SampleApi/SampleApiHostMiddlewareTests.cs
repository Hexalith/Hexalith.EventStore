extern alias SampleApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class SampleApiHostMiddlewareTests
{
    private const string Audience = "hexalith-eventstore";
    private const string Issuer = "hexalith-dev";
    private const string SigningKey = "DevOnlySigningKey-AtLeast32Chars!";

    [Fact]
    public async Task CounterRoute_WhenBearerTokenIsMissing_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(ConfigureSampleAuth));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client
            .GetAsync("/api/tenant-a/counter/counter-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CounterRoute_WhenBearerTokenIsValid_RoutesThroughGeneratedControllerAndGateway()
    {
        var gateway = new FakeEventStoreGatewayClient
        {
            QueryHandler = static (_, _, _) =>
            {
                JsonElement payload = JsonSerializer.SerializeToElement(new
                {
                    counterId = "counter-1",
                    value = 7,
                });

                return Task.FromResult(new EventStoreQueryResult(
                    "01KTESTQUERYHTTP00000000",
                    payload,
                    IsNotModified: false,
                    ETag: "counter-version")
                {
                    Metadata = new QueryResponseMetadata(
                        IsStale: false,
                        ProjectionVersion: "42",
                        ServedAt: new DateTimeOffset(2026, 7, 7, 10, 30, 0, TimeSpan.Zero))
                    {
                        Provenance = QueryResponseProvenance.ProjectionBacked,
                        Lifecycle = ProjectionLifecycleState.Current,
                    },
                });
            },
        };

        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(ConfigureSampleAuth);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEventStoreGatewayClient>();
                    services.AddSingleton<IEventStoreGatewayClient>(gateway);
                });
            });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        using HttpResponseMessage response = await client
            .GetAsync("/api/tenant-a/counter/counter-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe("\"counter-version\"");
        response.Headers.GetValues("X-Hexalith-Query-Provenance").Single().ShouldBe("ProjectionBacked");
        response.Headers.GetValues(ProjectionLifecyclePolicy.HeaderName).Single().ShouldBe("Current");
        response.Headers.GetValues("X-Hexalith-Projection-Version").Single().ShouldBe("42");

        await using Stream body = await response.Content
            .ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            body,
            cancellationToken: TestContext.Current.CancellationToken);
        document.RootElement.GetProperty("counterId").GetString().ShouldBe("counter-1");
        document.RootElement.GetProperty("value").GetInt32().ShouldBe(7);

        gateway.QueryCallCount.ShouldBe(1);
        SubmitQueryRequest request = gateway.LastQueryRequest.ShouldNotBeNull();
        request.Tenant.ShouldBe("tenant-a");
        request.Domain.ShouldBe("counter");
        request.AggregateId.ShouldBe("counter-1");
        request.EntityId.ShouldBe("counter-1");
        request.QueryType.ShouldBe("get-counter-status");
    }

    private static void ConfigureSampleAuth(WebHostBuilderContext _, IConfigurationBuilder configuration)
        => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
            ["EventStore:Authentication:Issuer"] = Issuer,
            ["EventStore:Authentication:Audience"] = Audience,
            ["EventStore:Authentication:SigningKey"] = SigningKey,
        });

    private static string CreateToken()
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity([new Claim("sub", "sample-user")]),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
