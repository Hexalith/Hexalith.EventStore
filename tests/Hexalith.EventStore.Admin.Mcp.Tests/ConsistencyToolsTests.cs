
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class ConsistencyToolsTests {
    private static readonly string _checksJson = """[{"checkId":"chk-1","status":2,"tenantId":"t1","domain":"Orders","checkTypes":[0],"startedAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T00:05:00Z","timeoutUtc":"2026-01-01T01:00:00Z","streamsChecked":100,"anomaliesFound":2}]""";
    private static readonly string _emptyChecksJson = """[]""";
    private static readonly string _checkResultJson = """{"checkId":"chk-1","status":2,"tenantId":"t1","domain":"Orders","checkTypes":[0],"startedAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T00:05:00Z","timeoutUtc":"2026-01-01T01:00:00Z","streamsChecked":100,"anomaliesFound":2,"anomalies":[{"anomalyId":"a1","checkType":0,"severity":1,"tenantId":"t1","domain":"Orders","aggregateId":"o1","description":"Sequence gap","details":null,"expectedSequence":3,"actualSequence":5}],"truncated":false,"errorMessage":null}""";
    private static readonly string _truncatedCheckResultJson = """{"checkId":"chk-2","status":2,"tenantId":"t1","domain":null,"checkTypes":[0,1],"startedAtUtc":"2026-01-01T00:00:00Z","completedAtUtc":"2026-01-01T00:10:00Z","timeoutUtc":"2026-01-01T01:00:00Z","streamsChecked":5000,"anomaliesFound":750,"anomalies":[],"truncated":true,"errorMessage":null}""";

    [Fact]
    public async Task ListChecks_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _checksJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.ListChecks(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task ListChecks_ReturnsEmptyArray_WhenNoChecksExist() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _emptyChecksJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.ListChecks(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ListChecks_PassesTenantIdFilter() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            _checksJson);
        var client = new AdminApiClient(httpClient);

        _ = await ConsistencyTools.ListChecks(client, new InvestigationSession(), "tenant1", ct);

        _ = capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=tenant1");
    }

    [Fact]
    public async Task ListChecks_ReturnsErrorJson_OnFailure() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(new HttpRequestException("Connection refused"));
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.ListChecks(client, new InvestigationSession(), cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unreachable");
    }

    [Fact]
    public async Task GetCheckDetail_ReturnsValidJson_OnSuccess() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _checkResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.GetCheckDetail(client, new InvestigationSession(), "chk-1", ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("checkId").GetString().ShouldBe("chk-1");
    }

    [Fact]
    public async Task GetCheckDetail_ReturnsNotFound_WhenNull() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, "null");
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.GetCheckDetail(client, new InvestigationSession(), "nonexistent", ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task GetCheckDetail_ReturnsUnauthorizedError_OnHttp401() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.GetCheckDetail(client, new InvestigationSession(), "chk-1", ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task GetCheckDetail_SerializesTruncatedFlag_WhenAnomaliesCapped() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _truncatedCheckResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.GetCheckDetail(client, new InvestigationSession(), "chk-2", ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("anomaliesFound").GetInt32().ShouldBe(750);
    }

    [Fact]
    public async Task ListChecks_ReturnsParseableJson() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _checksJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.ListChecks(client, new InvestigationSession(), cancellationToken: ct);

        _ = Should.NotThrow(() => JsonDocument.Parse(result));
    }

    [Fact]
    public async Task GetCheckDetail_ReturnsParseableJson() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _checkResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.GetCheckDetail(client, new InvestigationSession(), "chk-1", ct);

        _ = Should.NotThrow(() => JsonDocument.Parse(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCheckDetail_ReturnsInvalidInputError_WhenCheckIdEmpty(string checkId) {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _checkResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyTools.GetCheckDetail(client, new InvestigationSession(), checkId, ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }
}
