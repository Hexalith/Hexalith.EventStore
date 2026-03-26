namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;

public class AdminApiClientDiagnosticTests
{
    private static readonly string _diffJson = """{"fromSequence":1,"toSequence":5,"changedFields":[{"fieldPath":"$.status","oldValue":"\"pending\"","newValue":"\"completed\""}]}""";
    private static readonly string _causationJson = """{"originatingCommandType":"PlaceOrder","originatingCommandId":"cmd-1","correlationId":"corr-1","userId":"user-1","events":[{"sequenceNumber":1,"eventTypeName":"OrderPlaced","timestamp":"2026-01-01T00:00:00Z"}],"affectedProjections":["OrderSummary"]}""";

    [Fact]
    public async Task DiffAggregateStateAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _diffJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.DiffAggregateStateAsync("tenant1", "Orders", "order-123", 1, 5, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/streams/tenant1/Orders/order-123/diff?fromSequence=1&toSequence=5");
    }

    [Fact]
    public async Task TraceCausationChainAsync_SendsGetToCorrectPath()
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _causationJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TraceCausationChainAsync("tenant1", "Orders", "order-123", 3, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldBe("/api/v1/admin/streams/tenant1/Orders/order-123/causation?sequenceNumber=3");
    }

    [Theory]
    [InlineData("simple-id", "simple-id")]
    [InlineData("id/with/slashes", "id%2Fwith%2Fslashes")]
    [InlineData("id with spaces", "id%20with%20spaces")]
    [InlineData("id+plus", "id%2Bplus")]
    public async Task DiffAggregateStateAsync_UriEncodesAggregateId(string aggregateId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _diffJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.DiffAggregateStateAsync("t1", "d1", aggregateId, 1, 5, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/t1/d1/{expectedEncoded}/diff");
    }

    [Theory]
    [InlineData("simple-id", "simple-id")]
    [InlineData("id/with/slashes", "id%2Fwith%2Fslashes")]
    [InlineData("id with spaces", "id%20with%20spaces")]
    [InlineData("id+plus", "id%2Bplus")]
    public async Task TraceCausationChainAsync_UriEncodesAggregateId(string aggregateId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _causationJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TraceCausationChainAsync("t1", "d1", aggregateId, 1, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/t1/d1/{expectedEncoded}/causation");
    }

    [Theory]
    [InlineData("simple-tenant", "simple-tenant")]
    [InlineData("tenant/sub", "tenant%2Fsub")]
    [InlineData("tenant with spaces", "tenant%20with%20spaces")]
    [InlineData("tenant+id", "tenant%2Bid")]
    public async Task DiffAggregateStateAsync_UriEncodesTenantId(string tenantId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _diffJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.DiffAggregateStateAsync(tenantId, "d1", "a1", 1, 5, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/api/v1/admin/streams/{expectedEncoded}/");
    }

    [Theory]
    [InlineData("simple-domain", "simple-domain")]
    [InlineData("domain/sub", "domain%2Fsub")]
    [InlineData("domain with spaces", "domain%20with%20spaces")]
    [InlineData("domain+name", "domain%2Bname")]
    public async Task DiffAggregateStateAsync_UriEncodesDomain(string domain, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _diffJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.DiffAggregateStateAsync("t1", domain, "a1", 1, 5, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/t1/{expectedEncoded}/");
    }

    [Theory]
    [InlineData("simple-tenant", "simple-tenant")]
    [InlineData("tenant/sub", "tenant%2Fsub")]
    [InlineData("tenant with spaces", "tenant%20with%20spaces")]
    [InlineData("tenant+id", "tenant%2Bid")]
    public async Task TraceCausationChainAsync_UriEncodesTenantId(string tenantId, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _causationJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TraceCausationChainAsync(tenantId, "d1", "a1", 1, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/api/v1/admin/streams/{expectedEncoded}/");
    }

    [Theory]
    [InlineData("simple-domain", "simple-domain")]
    [InlineData("domain/sub", "domain%2Fsub")]
    [InlineData("domain with spaces", "domain%20with%20spaces")]
    [InlineData("domain+name", "domain%2Bname")]
    public async Task TraceCausationChainAsync_UriEncodesDomain(string domain, string expectedEncoded)
    {
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _causationJson);
        var client = new AdminApiClient(httpClient);

        _ = await client.TraceCausationChainAsync("t1", domain, "a1", 1, CancellationToken.None);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain($"/t1/{expectedEncoded}/");
    }
}
