using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Testing.Security;

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
    public async Task GetRecentlyActiveStreamsAsync_ThrowsUnavailable_WhenApiReturnsError() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminStreamApiClient client = CreateClient(httpClient);

        _ = await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetRecentlyActiveStreamsAsync(null, null, 10));
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ProtectedProblemDetails_PreservesSafeFieldsWithoutRawBody() {
        string json = $$"""
            {
              "type": "https://hexalith.io/problems/unreadable-protected-data",
              "title": "Protected data is unreadable",
              "status": 503,
              "detail": "Protection provider is temporarily unavailable. Retry later with backoff.",
              "correlationId": "corr-1",
              "reasonCode": "provider-unavailable",
              "stage": "admin-inspection",
              "providerPrivateMetadata": "{{ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob}}",
              "rawException": "{{ProtectedDataLeakSentinel.ProtectedProviderExceptionText}}"
            }
            """;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.ServiceUnavailable, json);
        AdminStreamApiClient client = CreateClient(httpClient);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.GetRecentCommandsAsync(null, null, null, 10));

        ex.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        ex.ProblemType.ShouldBe("https://hexalith.io/problems/unreadable-protected-data");
        ex.ErrorCode.ShouldBe("provider-unavailable");
        ex.TraceId.ShouldBe("corr-1");
        ex.Extensions["stage"].ShouldBe("admin-inspection");
        ex.Extensions.ContainsKey("providerPrivateMetadata").ShouldBeFalse();
        ex.Extensions.ContainsKey("rawException").ShouldBeFalse();
        ProtectedDataLeakSentinel.AssertNoLeak([
            ex.Message,
            ex.Title ?? string.Empty,
            ex.Detail ?? string.Empty,
            System.Text.Json.JsonSerializer.Serialize(ex.Extensions),
        ]);
    }

    [Fact]
    public async Task GetEventDetailAsync_ProtectedProblemDetails_PropagatesSanitizedProblemException() {
        string json = """
            {
              "type": "https://hexalith.io/problems/unreadable-protected-data",
              "title": "Protected data is unreadable",
              "status": 503,
              "detail": "Protection provider is temporarily unavailable. Retry later with backoff.",
              "correlationId": "corr-1",
              "reasonCode": "provider-unavailable",
              "stage": "admin-inspection"
            }
            """;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.ServiceUnavailable, json);
        AdminStreamApiClient client = CreateClient(httpClient);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.GetEventDetailAsync("tenant-a", "Counter", "agg-1", 5));

        ex.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        ex.ProblemType.ShouldBe("https://hexalith.io/problems/unreadable-protected-data");
        ex.ErrorCode.ShouldBe("provider-unavailable");
        ex.TraceId.ShouldBe("corr-1");
        ex.Extensions["stage"].ShouldBe("admin-inspection");
    }

    [Fact]
    public async Task GetRecentCommandsAsync_ProtectedProblemDetails_DropsNestedValuesEvenWhenExtensionNameIsAllowed() {
        string json = $$"""
            {
              "type": "https://hexalith.io/problems/unreadable-protected-data",
              "title": "Protected data is unreadable",
              "status": 503,
              "detail": "Protection provider is temporarily unavailable. Retry later with backoff.",
              "correlationId": "corr-1",
              "reasonCode": {
                "code": "provider-unavailable",
                "providerPrivateMetadata": "{{ProtectedDataLeakSentinel.ProtectedProviderPrivateBlob}}"
              },
              "stage": "admin-inspection"
            }
            """;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.ServiceUnavailable, json);
        AdminStreamApiClient client = CreateClient(httpClient);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.GetRecentCommandsAsync(null, null, null, 10));

        ex.ErrorCode.ShouldBeNull();
        ex.Extensions.ContainsKey("reasonCode").ShouldBeFalse();
        ex.Extensions["stage"].ShouldBe("admin-inspection");
        ProtectedDataLeakSentinel.AssertNoLeak([
            ex.Message,
            ex.Detail ?? string.Empty,
            System.Text.Json.JsonSerializer.Serialize(ex.Extensions),
        ]);
    }
}
