
using System.Net;

using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class AdminApiClientProjectionTests {
    [Fact]
    public async Task ListProjectionsAsync_SendsGetToCorrectPath() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListProjectionsAsync(null, CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/projections");
    }

    [Fact]
    public async Task ListProjectionsAsync_IncludesTenantIdWhenProvided() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListProjectionsAsync("tenant1", CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/projections?tenantId=tenant1");
    }

    [Fact]
    public async Task GetProjectionDetailAsync_SendsGetToCorrectPath() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"name":"OrderSummary","tenantId":"t1","status":0,"lag":0,"throughput":0,"errorCount":0,"lastProcessedPosition":0,"lastProcessedUtc":"2026-01-01T00:00:00Z","errors":[],"configuration":"{}","subscribedEventTypes":[]}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetProjectionDetailAsync("tenant1", "OrderSummary", CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/projections/tenant1/OrderSummary");
    }

    [Fact]
    public async Task GetProjectionDetailAsync_UriEncodesProjectionName() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"name":"Order Summary","tenantId":"t1","status":0,"lag":0,"throughput":0,"errorCount":0,"lastProcessedPosition":0,"lastProcessedUtc":"2026-01-01T00:00:00Z","errors":[],"configuration":"{}","subscribedEventTypes":[]}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetProjectionDetailAsync("tenant1", "Order Summary", CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("Order%20Summary");
    }
}
