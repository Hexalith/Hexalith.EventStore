namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Testing.Http;

public class AdminApiClientBackupCommandTests
{
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-123","message":"Done","errorCode":null}""";

    [Fact]
    public async Task TriggerBackupAsync_SendsPostToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerBackupAsync("tenant1", null, true, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/backups/tenant1?includeSnapshots=true");
    }

    [Fact]
    public async Task TriggerBackupAsync_IncludesDescriptionWhenProvided()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerBackupAsync("tenant1", "Pre-release backup", true, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("description=Pre-release%20backup");
    }

    [Fact]
    public async Task TriggerBackupAsync_OmitsDescriptionWhenNull()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerBackupAsync("tenant1", null, true, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldNotContain("description");
    }

    [Fact]
    public async Task TriggerBackupAsync_IncludeSnapshotsFalse()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerBackupAsync("tenant1", null, false, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("includeSnapshots=false");
    }

    [Theory]
    [InlineData("tenant/special", "tenant%2Fspecial")]
    [InlineData("tenant with spaces", "tenant%20with%20spaces")]
    public async Task TriggerBackupAsync_UriEncodesTenantId(string tenantId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerBackupAsync(tenantId, null, true, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/api/v1/admin/backups/{expectedEncoded}");
    }

    [Fact]
    public async Task TriggerBackupAsync_UsesHttpPost()
    {
        HttpMethod? capturedMethod = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedMethod = r.Method,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerBackupAsync("t1", null, true, CancellationToken.None);

        capturedMethod.ShouldBe(HttpMethod.Post);
    }
}
