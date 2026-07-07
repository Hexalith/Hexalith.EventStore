extern alias SampleApi;

using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class SampleApiHostMiddlewareTests
{
    [Fact]
    public async Task CounterRoute_WhenBearerTokenIsMissing_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<SampleApi::Program> factory = new WebApplicationFactory<SampleApi::Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(static (_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DAPR_HTTP_ENDPOINT"] = "http://localhost:3500",
                    ["EventStore:Authentication:Issuer"] = "hexalith-dev",
                    ["EventStore:Authentication:Audience"] = "hexalith-eventstore",
                    ["EventStore:Authentication:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars!",
                });
            }));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client
            .GetAsync("/api/tenant-a/counter/counter-1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
