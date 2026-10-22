using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminResiliencyApiClientTests {
    private static AdminResiliencyApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminResiliencyApiClient(factory, NullLogger<AdminResiliencyApiClient>.Instance);
    }

    [Fact]
    public async Task GetResiliencySpecAsync_ReturnsSpec_WhenApiResponds() {
        string json = """{"retryPolicies":[],"timeoutPolicies":[],"circuitBreakerPolicies":[],"targetBindings":[],"isConfigurationAvailable":true,"rawYamlContent":null,"errorMessage":null}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminResiliencyApiClient client = CreateClient(httpClient);

        DaprResiliencySpec? result = await client.GetResiliencySpecAsync();

        _ = result.ShouldNotBeNull();
        result.IsConfigurationAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task GetResiliencySpecAsync_ReturnsNull_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminResiliencyApiClient client = CreateClient(httpClient);

        DaprResiliencySpec? result = await client.GetResiliencySpecAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResiliencySpecAsync_ThrowsUnauthorized_When401() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Unauthorized);

        AdminResiliencyApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => client.GetResiliencySpecAsync());
    }

    [Fact]
    public async Task GetResiliencySpecAsync_ThrowsForbidden_When403() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Forbidden);

        AdminResiliencyApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ForbiddenAccessException>(
            () => client.GetResiliencySpecAsync());
    }

    [Fact]
    public async Task GetResiliencySpecAsync_ThrowsServiceUnavailable_When503() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.ServiceUnavailable);

        AdminResiliencyApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetResiliencySpecAsync());
    }
}
