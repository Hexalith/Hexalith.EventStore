namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

public class AdminApiClientTypeTests
{
    [Fact]
    public async Task ListEventTypesAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListEventTypesAsync(null, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/types/events");
    }

    [Fact]
    public async Task ListCommandTypesAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListCommandTypesAsync(null, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/types/commands");
    }

    [Fact]
    public async Task ListAggregateTypesAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListAggregateTypesAsync(null, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/types/aggregates");
    }

    [Fact]
    public async Task ListEventTypesAsync_IncludesDomainFilterWhenProvided()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListEventTypesAsync("Orders", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/types/events?domain=Orders");
    }

    [Fact]
    public async Task ListCommandTypesAsync_IncludesDomainFilterWhenProvided()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListCommandTypesAsync("Orders", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/types/commands?domain=Orders");
    }

    [Fact]
    public async Task ListAggregateTypesAsync_IncludesDomainFilterWhenProvided()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);

        _ = await client.ListAggregateTypesAsync("Orders", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/types/aggregates?domain=Orders");
    }
}
