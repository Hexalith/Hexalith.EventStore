namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

public class AdminApiClientStreamTests
{
    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetRecentlyActiveStreamsAsync(null, null, 100, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/streams?count=100");
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_IncludesFilterParameters()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetRecentlyActiveStreamsAsync("tenant1", "Orders", 50, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=tenant1");
        capturedUri.PathAndQuery.ShouldContain("domain=Orders");
        capturedUri.PathAndQuery.ShouldContain("count=50");
    }

    [Fact]
    public async Task GetRecentlyActiveStreamsAsync_OmitsNullParameters()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetRecentlyActiveStreamsAsync(null, null, 100, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldNotContain("tenantId");
        capturedUri.PathAndQuery.ShouldNotContain("domain");
    }

    [Fact]
    public async Task GetStreamTimelineAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetStreamTimelineAsync("tenant1", "Orders", "order-123", null, null, 100, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/streams/tenant1/Orders/order-123/timeline?count=100");
    }

    [Fact]
    public async Task GetStreamTimelineAsync_IncludesSequenceParameters()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetStreamTimelineAsync("tenant1", "Orders", "order-123", 5, 10, 50, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("fromSequence=5");
        capturedUri.PathAndQuery.ShouldContain("toSequence=10");
    }

    [Fact]
    public async Task GetAggregateStateAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"tenantId":"t1","domain":"Orders","aggregateId":"o1","sequenceNumber":3,"timestamp":"2026-01-01T00:00:00Z","stateJson":"{}"}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetAggregateStateAsync("tenant1", "Orders", "order-123", 3, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/streams/tenant1/Orders/order-123/state?sequenceNumber=3");
    }

    [Fact]
    public async Task GetEventDetailAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"tenantId":"t1","domain":"Orders","aggregateId":"o1","sequenceNumber":1,"eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z","correlationId":"c1","payloadJson":"{}"}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetEventDetailAsync("tenant1", "Orders", "order-123", 1, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/streams/tenant1/Orders/order-123/events/1");
    }

    [Theory]
    [InlineData("simple-id", "simple-id")]
    [InlineData("id/with/slashes", "id%2Fwith%2Fslashes")]
    [InlineData("id with spaces", "id%20with%20spaces")]
    [InlineData("id+plus", "id%2Bplus")]
    [InlineData("unicod\u00e9", "unicod%C3%A9")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task GetStreamTimelineAsync_UriEncodesAggregateId(string aggregateId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetStreamTimelineAsync("t1", "d1", aggregateId, null, null, 10, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/t1/d1/{expectedEncoded}/timeline");
    }
}
