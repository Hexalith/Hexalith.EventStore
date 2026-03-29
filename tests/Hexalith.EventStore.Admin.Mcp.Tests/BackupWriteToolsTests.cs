namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class BackupWriteToolsTests
{
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-456","message":"Backup started","errorCode":null}""";

    [Fact]
    public async Task TriggerBackup_ReturnsPreview_WhenConfirmFalse()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "acme-corp");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("backup-trigger");
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("acme-corp");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("backup");
        doc.RootElement.GetProperty("parameters").GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("parameters").GetProperty("includeSnapshots").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task TriggerBackup_PreviewIncludesDescription_WhenProvided()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "acme-corp", description: "Pre-release");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("Pre-release");
    }

    [Fact]
    public async Task TriggerBackup_ExecutesAndReturnsResult_WhenConfirmTrue()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "acme-corp", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("preview", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("operationId").GetString().ShouldBe("op-456");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TriggerBackup_ReturnsValidationError_WhenTenantIdEmpty(string tenantId)
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, tenantId, confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task TriggerBackup_ReturnsErrorJson_OnHttpException()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Server Error", null, HttpStatusCode.InternalServerError));
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("server-error");
    }

    [Fact]
    public async Task TriggerBackup_ReturnsErrorJson_OnTimeout()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new TaskCanceledException("Request timed out"));
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", confirm: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("timeout");
    }

    [Fact]
    public async Task TriggerBackup_IncludeSnapshotsFalse_FlowsThrough()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", includeSnapshots: false);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("parameters").GetProperty("includeSnapshots").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task TriggerBackup_ReturnsParseableJson()
    {
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", confirm: true);

        Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
