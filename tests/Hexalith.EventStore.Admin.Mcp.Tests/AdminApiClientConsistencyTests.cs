
using System.Net;

using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class AdminApiClientConsistencyTests {
    private static readonly string _checksJson = """[{"checkId":"chk-1","status":2,"tenantId":"t1","domain":"Orders","checkTypes":[0],"startedAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T00:05:00Z","timeoutUtc":"2026-01-01T01:00:00Z","streamsChecked":100,"anomaliesFound":2}]""";
    private static readonly string _checkResultJson = """{"checkId":"chk-1","status":2,"tenantId":"t1","domain":"Orders","checkTypes":[0],"startedAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T00:05:00Z","timeoutUtc":"2026-01-01T01:00:00Z","streamsChecked":100,"anomaliesFound":2,"anomalies":[{"anomalyId":"a1","checkType":0,"severity":1,"tenantId":"t1","domain":"Orders","aggregateId":"o1","description":"Sequence gap","details":null,"expectedSequence":3,"actualSequence":5}],"truncated":false,"errorMessage":null}""";

    [Fact]
    public async Task GetConsistencyChecksAsync_SendsGetToCorrectPath() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _checksJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetConsistencyChecksAsync(null, CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/consistency/checks");
    }

    [Fact]
    public async Task GetConsistencyChecksAsync_IncludesTenantIdWhenProvided() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _checksJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetConsistencyChecksAsync("tenant1", CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/consistency/checks?tenantId=tenant1");
    }

    [Fact]
    public async Task GetConsistencyChecksAsync_OmitsTenantIdWhenNull() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _checksJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetConsistencyChecksAsync(null, CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldNotContain("tenantId");
    }

    [Fact]
    public async Task GetConsistencyCheckResultAsync_SendsGetToCorrectPath() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _checkResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetConsistencyCheckResultAsync("chk-1", CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/consistency/checks/chk-1");
    }

    [Theory]
    [InlineData("simple-id", "simple-id")]
    [InlineData("id/with/slashes", "id%2Fwith%2Fslashes")]
    [InlineData("id with spaces", "id%20with%20spaces")]
    [InlineData("id+plus", "id%2Bplus")]
    public async Task GetConsistencyCheckResultAsync_UriEncodesCheckId(string checkId, string expectedEncoded) {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _checkResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.GetConsistencyCheckResultAsync(checkId, CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/api/v1/admin/consistency/checks/{expectedEncoded}");
    }
}
