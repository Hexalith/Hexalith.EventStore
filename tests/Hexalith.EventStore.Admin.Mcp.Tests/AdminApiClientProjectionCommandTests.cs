namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

public class AdminApiClientProjectionCommandTests
{
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-123","message":"Done","errorCode":null}""";

    [Fact]
    public async Task PauseProjectionAsync_SendsPostToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.PauseProjectionAsync("tenant1", "OrderSummary", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/projections/tenant1/OrderSummary/pause");
    }

    [Fact]
    public async Task ResumeProjectionAsync_SendsPostToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.ResumeProjectionAsync("tenant1", "OrderSummary", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/projections/tenant1/OrderSummary/resume");
    }

    [Fact]
    public async Task ResetProjectionAsync_SendsPostToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.ResetProjectionAsync("tenant1", "OrderSummary", null, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/projections/tenant1/OrderSummary/reset");
    }

    [Fact]
    public async Task ResetProjectionAsync_WithNullFromPosition_SendsNullInBody()
    {
        string? capturedBody = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedBody = r.Content!.ReadAsStringAsync().Result,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.ResetProjectionAsync("tenant1", "OrderSummary", null, CancellationToken.None);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("\"fromPosition\":null");
    }

    [Fact]
    public async Task ResetProjectionAsync_WithFromPosition_SendsPositionInBody()
    {
        string? capturedBody = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedBody = r.Content!.ReadAsStringAsync().Result,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.ResetProjectionAsync("tenant1", "OrderSummary", 100, CancellationToken.None);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("\"fromPosition\":100");
    }

    [Fact]
    public async Task ReplayProjectionAsync_SendsPostToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.ReplayProjectionAsync("tenant1", "OrderSummary", 10, 50, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/projections/tenant1/OrderSummary/replay");
    }

    [Fact]
    public async Task ReplayProjectionAsync_SendsBodyWithPositions()
    {
        string? capturedBody = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedBody = r.Content!.ReadAsStringAsync().Result,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.ReplayProjectionAsync("tenant1", "OrderSummary", 10, 50, CancellationToken.None);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("\"fromPosition\":10");
        capturedBody.ShouldContain("\"toPosition\":50");
    }

    [Theory]
    [InlineData("tenant/with/slashes", "OrderSummary", "tenant%2Fwith%2Fslashes", "OrderSummary")]
    [InlineData("tenant1", "Order Summary", "tenant1", "Order%20Summary")]
    [InlineData("t+e", "p+n", "t%2Be", "p%2Bn")]
    public async Task PauseProjectionAsync_UriEncodesPathSegments(
        string tenantId, string projectionName, string expectedTenant, string expectedProjection)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.PauseProjectionAsync(tenantId, projectionName, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe($"/api/v1/admin/projections/{expectedTenant}/{expectedProjection}/pause");
    }

    [Fact]
    public async Task PauseProjectionAsync_UsesHttpPost()
    {
        HttpMethod? capturedMethod = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedMethod = r.Method,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.PauseProjectionAsync("t1", "p1", CancellationToken.None);

        capturedMethod.ShouldBe(HttpMethod.Post);
    }
}
