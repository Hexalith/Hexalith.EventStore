
using System.Net;

using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class AdminApiClientConsistencyCommandTests {
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-123","message":"Done","errorCode":null}""";

    [Fact]
    public async Task TriggerConsistencyCheckAsync_SendsPostToCorrectPath() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerConsistencyCheckAsync(null, null, ["SequenceContinuity"], CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/consistency/checks");
    }

    [Fact]
    public async Task TriggerConsistencyCheckAsync_SendsBodyWithCheckTypes() {
        string? capturedBody = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedBody = r.Content!.ReadAsStringAsync().Result,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerConsistencyCheckAsync("t1", "Orders", ["SequenceContinuity", "SnapshotIntegrity"], CancellationToken.None);

        _ = capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("SequenceContinuity");
        capturedBody.ShouldContain("SnapshotIntegrity");
        capturedBody.ShouldContain("\"tenantId\":\"t1\"");
        capturedBody.ShouldContain("\"domain\":\"Orders\"");
    }

    [Fact]
    public async Task TriggerConsistencyCheckAsync_SendsNullTenantIdWhenNull() {
        string? capturedBody = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedBody = r.Content!.ReadAsStringAsync().Result,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerConsistencyCheckAsync(null, null, ["SequenceContinuity"], CancellationToken.None);

        _ = capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("\"tenantId\":null");
        capturedBody.ShouldContain("\"domain\":null");
    }

    [Fact]
    public async Task CancelConsistencyCheckAsync_SendsPostToCorrectPath() {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.CancelConsistencyCheckAsync("chk-42", CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/consistency/checks/chk-42/cancel");
    }

    [Theory]
    [InlineData("chk/special", "chk%2Fspecial")]
    [InlineData("chk with spaces", "chk%20with%20spaces")]
    [InlineData("chk+plus", "chk%2Bplus")]
    public async Task CancelConsistencyCheckAsync_UriEncodesCheckId(string checkId, string expectedEncoded) {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.CancelConsistencyCheckAsync(checkId, CancellationToken.None);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/api/v1/admin/consistency/checks/{expectedEncoded}/cancel");
    }

    [Fact]
    public async Task TriggerConsistencyCheckAsync_UsesHttpPost() {
        HttpMethod? capturedMethod = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedMethod = r.Method,
            HttpStatusCode.OK,
            _operationResultJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TriggerConsistencyCheckAsync(null, null, ["SequenceContinuity"], CancellationToken.None);

        capturedMethod.ShouldBe(HttpMethod.Post);
    }
}
