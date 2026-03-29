namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class ConsistencyWriteToolsTests
{
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-789","message":"Check started","errorCode":null}""";

    // --- consistency-trigger ---

    [Fact]
    public async Task TriggerCheck_ReturnsPreview_WhenConfirmFalse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity,SnapshotIntegrity", tenantId: "t1");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("consistency-trigger");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("integrity check");
        doc.RootElement.GetProperty("parameters").GetProperty("checkTypes").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task TriggerCheck_ParsesCommaSeparatedCheckTypes()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity , SnapshotIntegrity , ProjectionPositions");

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement types = doc.RootElement.GetProperty("parameters").GetProperty("checkTypes");
        types.GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task TriggerCheck_SingleCheckType()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity");

        using JsonDocument doc = JsonDocument.Parse(result);
        JsonElement types = doc.RootElement.GetProperty("parameters").GetProperty("checkTypes");
        types.GetArrayLength().ShouldBe(1);
        types[0].GetString().ShouldBe("SequenceContinuity");
    }

    [Fact]
    public async Task TriggerCheck_ExecutesAndReturnsResult_WhenConfirmTrue()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("preview", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("operationId").GetString().ShouldBe("op-789");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TriggerCheck_ReturnsValidationError_WhenCheckTypesEmpty(string checkTypes)
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, checkTypes, confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task TriggerCheck_ReturnsInvalidInputError_WhenOnlyCommas()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, ",,,", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("At least one check type");
    }

    [Fact]
    public async Task TriggerCheck_ReturnsErrorJson_OnHttpException()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden));
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task TriggerCheck_IncludesTenantAndDomainInPreview()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", tenantId: "acme", domain: "Orders");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("acme");
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("Orders");
    }

    // --- consistency-cancel ---

    [Fact]
    public async Task CancelCheck_ReturnsPreview_WhenConfirmFalse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "chk-42");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("consistency-cancel");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("cancel");
        doc.RootElement.GetProperty("parameters").GetProperty("checkId").GetString().ShouldBe("chk-42");
    }

    [Fact]
    public async Task CancelCheck_ExecutesAndReturnsResult_WhenConfirmTrue()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "chk-42", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelCheck_ReturnsValidationError_WhenCheckIdEmpty(string checkId)
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, checkId, confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task CancelCheck_ReturnsErrorJson_OnNotFound()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Check not found", null, HttpStatusCode.NotFound));
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "nonexistent", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("not-found");
    }

    [Fact]
    public async Task CancelCheck_ReturnsParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.CancelCheck(client, "chk-42", confirm: true);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }

    [Fact]
    public async Task TriggerCheck_ReturnsParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", confirm: true);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
