using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminStreamApiClientTests {
    private static AdminStreamApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminStreamApiClient(factory, NullLogger<AdminStreamApiClient>.Instance);
    }

    // === GetSystemHealthAsync ===

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsHealth_WhenApiResponds() {
        string json = """{"overallStatus":0,"totalEventCount":100,"eventsPerSecond":5.0,"errorPercentage":0.1,"daprComponents":[],"observabilityLinks":{"traceUrl":null,"metricsUrl":null,"logsUrl":null}}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminStreamApiClient client = CreateClient(httpClient);

        SystemHealthReport? result = await client.GetSystemHealthAsync();

        _ = result.ShouldNotBeNull();
        result.TotalEventCount.ShouldBe(100);
        result.EventsPerSecond.ShouldBe(5.0);
    }

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsNull_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminStreamApiClient client = CreateClient(httpClient);

        SystemHealthReport? result = await client.GetSystemHealthAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetSystemHealthAsync_ThrowsUnauthorized_When401() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.Unauthorized);

        AdminStreamApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => client.GetSystemHealthAsync());
    }

    // === GetRecentlyActiveStreamsAsync ===

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ReturnsStreams_WhenApiResponds() {
        string json = """{"items":[],"totalCount":0,"continuationToken":null}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminStreamApiClient client = CreateClient(httpClient);

        PagedResult<StreamSummary> result = await client.GetRecentlyActiveStreamsAsync(null, null, 10);

        _ = result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_ReturnsEmpty_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminStreamApiClient client = CreateClient(httpClient);

        PagedResult<StreamSummary> result = await client.GetRecentlyActiveStreamsAsync(null, null, 10);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }
}
