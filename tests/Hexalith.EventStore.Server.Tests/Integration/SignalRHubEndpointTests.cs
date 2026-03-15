extern alias commandapi;

using System.Net;
using System.Net.Http.Headers;

using Hexalith.EventStore.CommandApi.SignalR;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Integration;

public class SignalRHubEndpointTests : IClassFixture<SignalRHubWebApplicationFactory> {
    private readonly SignalRHubWebApplicationFactory _factory;

    public SignalRHubEndpointTests(SignalRHubWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task NegotiateEndpoint_WhenSignalREnabled_IsHostedAtExpectedPath() {
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, $"{ProjectionChangedHub.HubPath}/negotiate?negotiateVersion=1") {
            Content = new StringContent(string.Empty),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}