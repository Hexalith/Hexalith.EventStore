using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminActorApiClientTests
{
    private static AdminActorApiClient CreateClient(HttpClient httpClient)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminActorApiClient(factory, NullLogger<AdminActorApiClient>.Instance);
    }

    // === GetActorRuntimeInfoAsync ===

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ReturnsInfo_WhenApiResponds()
    {
        // remoteMetadataStatus: 1 = Available (enum integer value)
        string json = """{"actorTypes":[],"totalActiveActors":0,"configuration":{"idleTimeout":"01:00:00","scanInterval":"00:00:30","drainOngoingCallTimeout":"00:01:00","drainRebalancedActors":true,"reentrancyEnabled":false,"reentrancyMaxStackDepth":32},"remoteMetadataStatus":1,"remoteEndpoint":"http://localhost:3501"}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminActorApiClient client = CreateClient(httpClient);

        DaprActorRuntimeInfo? result = await client.GetActorRuntimeInfoAsync();

        result.ShouldNotBeNull();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        result.ActorTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ReturnsNull_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminActorApiClient client = CreateClient(httpClient);

        DaprActorRuntimeInfo? result = await client.GetActorRuntimeInfoAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ThrowsServiceUnavailable_When503()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.ServiceUnavailable);

        AdminActorApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetActorRuntimeInfoAsync());
    }
}
