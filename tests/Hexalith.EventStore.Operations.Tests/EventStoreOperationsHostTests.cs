using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Operations.Security;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies the executable host exposes the exact Dapr subscription contract.
/// </summary>
public sealed class EventStoreOperationsHostTests
{
    /// <summary>Verifies the sidecar discovers exactly the configured capture subscription.</summary>
    [Fact]
    public async Task SubscribeDocumentContainsExactDeadLetterMapping()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dapr/subscribe");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement document = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement subscription = document.EnumerateArray().ShouldHaveSingleItem();
        subscription.EnumerateObject()
            .Single(static property => string.Equals(property.Name, "pubsubname", StringComparison.OrdinalIgnoreCase))
            .Value.GetString().ShouldBe("pubsub");
        subscription.GetProperty("topic").GetString().ShouldBe("deadletter.work.events");
        subscription.GetProperty("route").GetString().ShouldBe("dead-letters/work/events");
        using HttpResponseMessage capture = await client.PostAsync(
            "/dead-letters/work/events",
            new ByteArrayContent([]));
        capture.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Verifies sidecar-token requests can reach every application-channel surface in Production.</summary>
    [Theory]
    [InlineData("GET", "/alive")]
    [InlineData("GET", "/dapr/subscribe")]
    [InlineData("GET", "/internal/dead-letters/count")]
    [InlineData("POST", "/dead-letters/work/events")]
    [InlineData("POST", "/actors/EventStoreDeadLetterDrainActor/topic/method/ListAsync")]
    public async Task ConfiguredSidecarTokenPassesApplicationChannelMiddleware(string method, string path)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                _ = builder.UseEnvironment("Production");
                _ = builder.UseSetting(DaprAppChannelSecurity.ConfigurationKey, "sidecar-secret");
            });
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.TryAddWithoutValidation(DaprAppChannelTokenMiddleware.HeaderName, "sidecar-secret")
            .ShouldBeTrue();
        if (string.Equals(method, "POST", StringComparison.Ordinal))
        {
            request.Content = new ByteArrayContent([]);
        }

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }
}
