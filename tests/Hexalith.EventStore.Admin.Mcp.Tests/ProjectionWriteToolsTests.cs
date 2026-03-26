namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tests.TestHelpers;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class ProjectionWriteToolsTests
{
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-123","message":"Paused","errorCode":null}""";

    // --- projection-pause ---

    [Fact]
    public async Task PauseProjection_ReturnsPreview_WhenConfirmFalse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.PauseProjection(client, "t1", "OrderSummary", confirm: false);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("projection-pause");
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("OrderSummary");
        doc.RootElement.GetProperty("endpoint").GetString()!.ShouldContain("/pause");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldNotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("parameters").GetProperty("tenantId").GetString().ShouldBe("t1");
    }

    [Fact]
    public async Task PauseProjection_ReturnsPreview_WhenConfirmOmitted()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.PauseProjection(client, "t1", "OrderSummary");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task PauseProjection_ExecutesAndReturnsResult_WhenConfirmTrue()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.PauseProjection(client, "t1", "OrderSummary", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("preview", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("operationId").GetString().ShouldBe("op-123");
    }

    [Theory]
    [InlineData("", "OrderSummary")]
    [InlineData("t1", "")]
    [InlineData("   ", "OrderSummary")]
    public async Task PauseProjection_ReturnsValidationError_WhenRequiredParamsEmpty(string tenantId, string projectionName)
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.PauseProjection(client, tenantId, projectionName, confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task PauseProjection_ReturnsErrorJson_OnHttpException()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.PauseProjection(client, "t1", "p1", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task PauseProjection_ReturnsErrorJson_OnTimeout()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new TaskCanceledException("Request timed out"));
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.PauseProjection(client, "t1", "p1", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("timeout");
    }

    [Fact]
    public async Task PauseProjection_ReturnsParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.PauseProjection(client, "t1", "p1", confirm: true);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }

    // --- projection-resume ---

    [Fact]
    public async Task ResumeProjection_ReturnsPreview_WhenConfirmFalse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ResumeProjection(client, "t1", "OrderSummary");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("projection-resume");
        doc.RootElement.GetProperty("endpoint").GetString()!.ShouldContain("/resume");
    }

    [Fact]
    public async Task ResumeProjection_ExecutesAndReturnsResult_WhenConfirmTrue()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ResumeProjection(client, "t1", "OrderSummary", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ResumeProjection_ReturnsValidationError_WhenRequiredParamsEmpty()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ResumeProjection(client, "", "OrderSummary", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    // --- projection-reset ---

    [Fact]
    public async Task ResetProjection_ReturnsPreview_WhenConfirmFalse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ResetProjection(client, "t1", "OrderSummary", fromPosition: 100);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("projection-reset");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("destructive");
        doc.RootElement.GetProperty("parameters").GetProperty("fromPosition").GetInt64().ShouldBe(100);
    }

    [Fact]
    public async Task ResetProjection_PreviewShowsBeginning_WhenFromPositionNull()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ResetProjection(client, "t1", "OrderSummary");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("beginning");
    }

    [Fact]
    public async Task ResetProjection_ExecutesAndReturnsResult_WhenConfirmTrue()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ResetProjection(client, "t1", "OrderSummary", confirm: true, fromPosition: 50);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ResetProjection_Returns422AsInvalidOperation()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Cannot reset running projection", null, HttpStatusCode.UnprocessableEntity));
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ResetProjection(client, "t1", "p1", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-operation");
    }

    // --- projection-replay ---

    [Fact]
    public async Task ReplayProjection_ReturnsPreview_WhenConfirmFalse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ReplayProjection(client, "t1", "OrderSummary", 10, 50);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("projection-replay");
        doc.RootElement.GetProperty("parameters").GetProperty("fromPosition").GetInt64().ShouldBe(10);
        doc.RootElement.GetProperty("parameters").GetProperty("toPosition").GetInt64().ShouldBe(50);
    }

    [Fact]
    public async Task ReplayProjection_ExecutesAndReturnsResult_WhenConfirmTrue()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ReplayProjection(client, "t1", "OrderSummary", 10, 50, confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ReplayProjection_ReturnsValidationError_WhenRequiredParamsEmpty()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ReplayProjection(client, "t1", "", 10, 50, confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task ReplayProjection_ReturnsValidationError_WhenFromPositionGreaterThanToPosition()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ReplayProjection(client, "t1", "OrderSummary", 50, 10, confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("fromPosition");
    }

    [Fact]
    public async Task ReplayProjection_ReturnsValidationError_WhenFromPositionGreaterThanToPosition_Preview()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ReplayProjection(client, "t1", "OrderSummary", 50, 10);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task ReplayProjection_AllowsEqualPositions()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ReplayProjection(client, "t1", "OrderSummary", 10, 10);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ReplayProjection_ReturnsParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await ProjectionWriteTools.ReplayProjection(client, "t1", "p1", 10, 50, confirm: true);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
