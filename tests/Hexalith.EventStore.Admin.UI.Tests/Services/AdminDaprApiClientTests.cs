using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminDaprApiClientTests {
    private static AdminDaprApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminDaprApiClient(factory, NullLogger<AdminDaprApiClient>.Instance);
    }

    // === GetComponentsAsync ===

    [Fact]
    public async Task GetComponentsAsync_ReturnsComponents_WhenApiResponds() {
        string json = """[{"componentName":"statestore","componentType":"state.redis","category":1,"version":"v1","status":0,"lastCheckUtc":"2026-01-01T00:00:00Z","capabilities":[]}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminDaprApiClient client = CreateClient(httpClient);

        IReadOnlyList<DaprComponentDetail> result = await client.GetComponentsAsync();

        result.Count.ShouldBe(1);
        result[0].ComponentName.ShouldBe("statestore");
    }

    [Fact]
    public async Task GetComponentsAsync_ReturnsEmpty_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminDaprApiClient client = CreateClient(httpClient);

        // InternalServerError triggers HttpRequestException which is caught
        IReadOnlyList<DaprComponentDetail> result = await client.GetComponentsAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetComponentsAsync_ThrowsUnauthorized_When401() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Unauthorized);

        AdminDaprApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => client.GetComponentsAsync());
    }

    [Fact]
    public async Task GetComponentsAsync_ThrowsForbidden_When403() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Forbidden);

        AdminDaprApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ForbiddenAccessException>(
            () => client.GetComponentsAsync());
    }

    [Fact]
    public async Task GetComponentsAsync_ThrowsServiceUnavailable_When503() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.ServiceUnavailable);

        AdminDaprApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetComponentsAsync());
    }

    // === GetSidecarInfoAsync ===

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsSidecarInfo_WhenApiResponds() {
        string json = """{"appId":"eventstore","runtimeVersion":"1.14.0","componentCount":3,"subscriptionCount":2,"httpEndpointCount":1}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminDaprApiClient client = CreateClient(httpClient);

        DaprSidecarInfo? result = await client.GetSidecarInfoAsync();

        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("eventstore");
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ReturnsNull_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminDaprApiClient client = CreateClient(httpClient);

        DaprSidecarInfo? result = await client.GetSidecarInfoAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetSidecarInfoAsync_ThrowsUnauthorized_When401() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Unauthorized);

        AdminDaprApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => client.GetSidecarInfoAsync());
    }
}
