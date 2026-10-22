using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminHealthHistoryApiClientTests {
    private static AdminHealthHistoryApiClient CreateClient(HttpClient httpClient) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminHealthHistoryApiClient(factory, NullLogger<AdminHealthHistoryApiClient>.Instance);
    }

    [Fact]
    public async Task GetHealthHistoryAsync_ReturnsHistory_WhenApiResponds() {
        string json = """{"entries":[{"componentName":"statestore","componentType":"state.redis","status":0,"capturedAtUtc":"2026-01-01T00:00:00Z"}],"hasData":true,"isTruncated":false}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminHealthHistoryApiClient client = CreateClient(httpClient);

        DaprComponentHealthTimeline? result = await client.GetHealthHistoryAsync(
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow);

        _ = result.ShouldNotBeNull();
        result.HasData.ShouldBeTrue();
        result.Entries.Count.ShouldBe(1);
        result.Entries[0].ComponentName.ShouldBe("statestore");
    }

    [Fact]
    public async Task GetHealthHistoryAsync_ReturnsNull_WhenApiReturnsNotImplemented() {
        using HttpClient httpClient = MockHttpMessageHandler.CreateStatusClient(HttpStatusCode.NotImplemented);

        AdminHealthHistoryApiClient client = CreateClient(httpClient);

        DaprComponentHealthTimeline? result = await client.GetHealthHistoryAsync(
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow);

        result.ShouldBeNull();
    }
}
