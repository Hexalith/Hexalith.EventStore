using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminActorApiClientTests {
    private static AdminActorApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminActorApiClient(factory, NullLogger<AdminActorApiClient>.Instance);
    }

    // === GetActorRuntimeInfoAsync ===

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ReturnsInfo_WhenApiResponds() {
        // remoteMetadataStatus: 1 = Available (enum integer value)
        string json = """{"actorTypes":[],"totalActiveActors":0,"configuration":{"idleTimeout":"01:00:00","scanInterval":"00:00:30","drainOngoingCallTimeout":"00:01:00","drainRebalancedActors":true,"reentrancyEnabled":false,"reentrancyMaxStackDepth":32},"remoteMetadataStatus":1,"remoteEndpoint":"http://localhost:3501"}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminActorApiClient client = CreateClient(httpClient);

        DaprActorRuntimeInfo? result = await client.GetActorRuntimeInfoAsync();

        _ = result.ShouldNotBeNull();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
        result.ActorTypes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ReturnsNull_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminActorApiClient client = CreateClient(httpClient);

        DaprActorRuntimeInfo? result = await client.GetActorRuntimeInfoAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ThrowsServiceUnavailable_When503() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.ServiceUnavailable);

        AdminActorApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetActorRuntimeInfoAsync());
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ReturnsNotFoundResult_WhenApiReturns404() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(
            HttpStatusCode.NotFound,
            """{"title":"Not Found","detail":"Actor instance not found"}""");

        AdminActorApiClient client = CreateClient(httpClient);

        DaprActorInstanceState? result = await client.GetActorInstanceStateAsync("AggregateActor", "tenant-a:counter:counter-1");

        _ = result.ShouldNotBeNull();
        result.ActorType.ShouldBe("AggregateActor");
        result.ActorId.ShouldBe("tenant-a:counter:counter-1");
        result.LookupStatus.ShouldBe(DaprActorLookupStatus.NotFound);
        result.Message.ShouldNotBeNull();
        result.Message!.ShouldContain("not found", Case.Insensitive);
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ReturnsLookupUnavailableResult_WhenNetworkFails() {
        using HttpClient httpClient = new(new ThrowingHandler(new HttpRequestException("network down"))) {
            BaseAddress = new Uri("https://admin.example/")
        };

        AdminActorApiClient client = CreateClient(httpClient);

        DaprActorInstanceState? result = await client.GetActorInstanceStateAsync("ETagActor", "counter:tenant-a");

        _ = result.ShouldNotBeNull();
        result.LookupStatus.ShouldBe(DaprActorLookupStatus.LookupUnavailable);
        result.Message.ShouldNotBeNull();
        result.Message!.ShouldContain("unavailable", Case.Insensitive);
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
