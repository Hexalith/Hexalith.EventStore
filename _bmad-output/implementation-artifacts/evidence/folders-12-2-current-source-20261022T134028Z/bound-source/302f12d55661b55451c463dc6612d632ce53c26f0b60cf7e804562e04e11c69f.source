using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminProjectionApiClientTests {
    private static AdminProjectionApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminProjectionApiClient(factory, NullLogger<AdminProjectionApiClient>.Instance);
    }

    // === ListProjectionsAsync ===

    [Fact]
    public async Task ListProjectionsAsync_ReturnsProjections_WhenApiResponds() {
        string json = """[{"name":"Counter","tenantId":"t1","status":0,"lag":0,"throughput":10.5,"errorCount":0,"lastProcessedPosition":100,"lastProcessedUtc":"2026-01-01T00:00:00Z"}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminProjectionApiClient client = CreateClient(httpClient);

        IReadOnlyList<ProjectionStatus> result = await client.ListProjectionsAsync(null);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Counter");
        result[0].TenantId.ShouldBe("t1");
    }

    [Fact]
    public async Task ListProjectionsAsync_ReturnsEmpty_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminProjectionApiClient client = CreateClient(httpClient);

        IReadOnlyList<ProjectionStatus> result = await client.ListProjectionsAsync(null);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListProjectionsAsync_ThrowsForbidden_When403() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Forbidden);

        AdminProjectionApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ForbiddenAccessException>(
            () => client.ListProjectionsAsync(null));
    }
}
