namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

public class AdminApiClientStorageTests
{
    [Fact]
    public async Task GetDaprComponentStatusAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetDaprComponentStatusAsync(CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/health/dapr");
    }

    [Fact]
    public async Task GetStorageOverviewAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"totalEventCount":1000,"totalSizeBytes":5000,"tenantBreakdown":[],"totalStreamCount":50}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetStorageOverviewAsync(null, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/storage/overview");
    }

    [Fact]
    public async Task GetStorageOverviewAsync_IncludesTenantIdWhenProvided()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"totalEventCount":1000,"totalSizeBytes":5000,"tenantBreakdown":[],"totalStreamCount":50}""");
        var client = new AdminApiClient(httpClient);

        _ = await client.GetStorageOverviewAsync("tenant1", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/storage/overview?tenantId=tenant1");
    }
}
