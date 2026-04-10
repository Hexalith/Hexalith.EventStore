using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminPubSubApiClientTests
{
    private static AdminPubSubApiClient CreateClient(HttpClient httpClient)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminPubSubApiClient(factory, NullLogger<AdminPubSubApiClient>.Instance);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ReturnsOverview_WhenApiResponds()
    {
        string json = """{"pubSubComponents":[],"subscriptions":[],"remoteMetadataStatus":1,"remoteEndpoint":"http://localhost:3501"}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminPubSubApiClient client = CreateClient(httpClient);

        DaprPubSubOverview? result = await client.GetPubSubOverviewAsync();

        result.ShouldNotBeNull();
        result.RemoteMetadataStatus.ShouldBe(RemoteMetadataStatus.Available);
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ReturnsNull_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminPubSubApiClient client = CreateClient(httpClient);

        DaprPubSubOverview? result = await client.GetPubSubOverviewAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ThrowsUnauthorized_When401()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Unauthorized);

        AdminPubSubApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => client.GetPubSubOverviewAsync());
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ThrowsForbidden_When403()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Forbidden);

        AdminPubSubApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => client.GetPubSubOverviewAsync());
    }

    [Fact]
    public async Task GetPubSubOverviewAsync_ThrowsServiceUnavailable_When503()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.ServiceUnavailable);

        AdminPubSubApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetPubSubOverviewAsync());
    }
}
