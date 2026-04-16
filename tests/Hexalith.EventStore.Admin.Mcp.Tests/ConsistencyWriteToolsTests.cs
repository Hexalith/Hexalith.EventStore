
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class ConsistencyWriteToolsTests {
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-789","message":"Check started","errorCode":null}""";

    // --- consistency-trigger ---

    [Fact]
    public async Task TriggerCheck_ReturnsPreview_WhenConfirmFalse() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity,SnapshotIntegrity", tenantId: "t1", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("consistency-trigger");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("integrity check");
        doc.RootElement.GetProperty("parameters").GetProperty("checkTypes").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task TriggerCheck_ParsesCommaSeparatedCheckTypes() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity , SnapshotIntegrity , ProjectionPositions", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        JsonElement types = doc.RootElement.GetProperty("parameters").GetProperty("checkTypes");
        types.GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task TriggerCheck_SingleCheckType() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        JsonElement types = doc.RootElement.GetProperty("parameters").GetProperty("checkTypes");
        types.GetArrayLength().ShouldBe(1);
        types[0].GetString().ShouldBe("SequenceContinuity");
    }

    [Fact]
    public async Task TriggerCheck_ExecutesAndReturnsResult_WhenConfirmTrue() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("preview", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("operationId").GetString().ShouldBe("op-789");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TriggerCheck_ReturnsValidationError_WhenCheckTypesEmpty(string checkTypes) {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, checkTypes, confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task TriggerCheck_ReturnsInvalidInputError_WhenOnlyCommas() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, ",,,", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("At least one check type");
    }

    [Fact]
    public async Task TriggerCheck_ReturnsErrorJson_OnHttpException() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden));
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task TriggerCheck_IncludesTenantAndDomainInPreview() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", tenantId: "acme", domain: "Orders", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("acme");
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("Orders");
    }

    // --- consistency-cancel ---

    [Fact]
    public async Task CancelCheck_ReturnsPreview_WhenConfirmFalse() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "chk-42", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("consistency-cancel");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("cancel");
        doc.RootElement.GetProperty("parameters").GetProperty("checkId").GetString().ShouldBe("chk-42");
    }

    [Fact]
    public async Task CancelCheck_ExecutesAndReturnsResult_WhenConfirmTrue() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "chk-42", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelCheck_ReturnsValidationError_WhenCheckIdEmpty(string checkId) {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, checkId, confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task CancelCheck_ReturnsErrorJson_OnNotFound() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Check not found", null, HttpStatusCode.NotFound));
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "nonexistent", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task CancelCheck_ReturnsParseableJson() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "chk-42", confirm: true, cancellationToken: ct);

        _ = Should.NotThrow(() => JsonDocument.Parse(result));
    }

    [Fact]
    public async Task TriggerCheck_ReturnsParseableJson() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", confirm: true, cancellationToken: ct);

        _ = Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
