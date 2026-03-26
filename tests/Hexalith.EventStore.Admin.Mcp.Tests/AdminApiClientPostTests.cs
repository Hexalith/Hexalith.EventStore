namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

public class AdminApiClientPostTests
{
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-123","message":"Done","errorCode":null}""";

    [Fact]
    public async Task PostAsync_SendsPostWithNullContent()
    {
        HttpMethod? capturedMethod = null;
        HttpContent? capturedContent = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r =>
            {
                capturedMethod = r.Method;
                capturedContent = r.Content;
            },
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.PostAsync("/api/v1/admin/test", CancellationToken.None);

        capturedMethod.ShouldBe(HttpMethod.Post);
        capturedContent.ShouldBeNull();
    }

    [Fact]
    public async Task PostAsync_DeserializesAdminOperationResult()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        var result = await client.PostAsync("/api/v1/admin/test", CancellationToken.None);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-123");
        result.Message.ShouldBe("Done");
    }

    [Fact]
    public async Task PostAsync_ThrowsHttpRequestExceptionOn4xx()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.NotFound, """{"error":"not found"}""");
        var client = new AdminApiClient(httpClient);

        await Should.ThrowAsync<HttpRequestException>(() => client.PostAsync("/api/v1/admin/test", CancellationToken.None));
    }

    [Fact]
    public async Task PostAsync_ThrowsHttpRequestExceptionOn5xx()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.InternalServerError, """{"error":"server error"}""");
        var client = new AdminApiClient(httpClient);

        await Should.ThrowAsync<HttpRequestException>(() => client.PostAsync("/api/v1/admin/test", CancellationToken.None));
    }

    [Fact]
    public async Task PostAsyncWithBody_SendsPostWithJsonContent()
    {
        HttpMethod? capturedMethod = null;
        string? capturedBody = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r =>
            {
                capturedMethod = r.Method;
                capturedBody = r.Content!.ReadAsStringAsync().Result;
            },
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.PostAsync("/api/v1/admin/test", new { fromPosition = 42 }, CancellationToken.None);

        capturedMethod.ShouldBe(HttpMethod.Post);
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("42");
    }

    [Fact]
    public async Task PostAsyncWithBody_SetsJsonContentType()
    {
        string? capturedContentType = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedContentType = r.Content?.Headers.ContentType?.MediaType,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.PostAsync("/api/v1/admin/test", new { data = "test" }, CancellationToken.None);

        capturedContentType.ShouldBe("application/json");
    }

    [Fact]
    public async Task PostAsyncWithBody_DeserializesAdminOperationResult()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        var result = await client.PostAsync("/api/v1/admin/test", new { data = "test" }, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-123");
    }

    [Fact]
    public async Task PostAsyncWithBody_ThrowsHttpRequestExceptionOn4xx()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.BadRequest, """{"error":"bad request"}""");
        var client = new AdminApiClient(httpClient);

        await Should.ThrowAsync<HttpRequestException>(() => client.PostAsync("/api/v1/admin/test", new { data = "test" }, CancellationToken.None));
    }

    [Fact]
    public async Task PostAsync_SendsToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.PostAsync("/api/v1/admin/specific/path", CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/specific/path");
    }
}
