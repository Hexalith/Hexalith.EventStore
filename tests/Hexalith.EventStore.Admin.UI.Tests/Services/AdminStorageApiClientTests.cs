using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.Admin.UI.Services.Exceptions;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminStorageApiClientTests
{
    private static AdminStorageApiClient CreateClient(HttpClient httpClient)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AdminApi").Returns(httpClient);
        return new AdminStorageApiClient(factory, NullLogger<AdminStorageApiClient>.Instance);
    }

    // === GetStorageOverviewAsync ===

    [Fact]
    public async Task GetStorageOverviewAsync_ReturnsOverview_WhenApiResponds()
    {
        string json = """{"totalEventCount":1000,"totalSizeBytes":50000,"tenantBreakdown":[],"totalStreamCount":10}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminStorageApiClient client = CreateClient(httpClient);

        StorageOverview result = await client.GetStorageOverviewAsync();

        result.ShouldNotBeNull();
        result.TotalEventCount.ShouldBe(1000);
        result.TotalSizeBytes.ShouldBe(50000);
        result.TotalStreamCount.ShouldBe(10);
    }

    [Fact]
    public async Task GetStorageOverviewAsync_ThrowsServiceUnavailable_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminStorageApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetStorageOverviewAsync());
    }

    // === GetHotStreamsAsync ===

    [Fact]
    public async Task GetHotStreamsAsync_ReturnsStreams_WhenApiResponds()
    {
        string json = """[{"tenantId":"t1","domain":"Counter","aggregateId":"a1","aggregateType":"CounterAggregate","eventCount":500,"sizeBytes":25000,"hasSnapshot":true,"snapshotAge":"01:00:00"}]""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, json);

        AdminStorageApiClient client = CreateClient(httpClient);

        IReadOnlyList<StreamStorageInfo> result = await client.GetHotStreamsAsync();

        result.Count.ShouldBe(1);
        result[0].TenantId.ShouldBe("t1");
        result[0].EventCount.ShouldBe(500);
    }

    [Fact]
    public async Task GetHotStreamsAsync_ThrowsServiceUnavailable_WhenApiReturnsError()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, "{}");

        AdminStorageApiClient client = CreateClient(httpClient);

        await Should.ThrowAsync<ServiceUnavailableException>(
            () => client.GetHotStreamsAsync());
    }
}
