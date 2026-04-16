
using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class BackupWriteToolsTests {
    private static readonly string _operationResultJson = """{"success":true,"operationId":"op-456","message":"Backup started","errorCode":null}""";

    [Fact]
    public async Task TriggerBackup_ReturnsPreview_WhenConfirmFalse() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "acme-corp", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("backup-trigger");
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("acme-corp");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("backup");
        doc.RootElement.GetProperty("parameters").GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("parameters").GetProperty("includeSnapshots").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task TriggerBackup_PreviewIncludesDescription_WhenProvided() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "acme-corp", description: "Pre-release", cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("description").GetString()!.ShouldContain("Pre-release");
    }

    [Fact]
    public async Task TriggerBackup_ExecutesAndReturnsResult_WhenConfirmTrue() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "acme-corp", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("preview", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("operationId").GetString().ShouldBe("op-456");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TriggerBackup_ReturnsValidationError_WhenTenantIdEmpty(string tenantId) {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, tenantId, confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task TriggerBackup_ReturnsErrorJson_OnHttpException() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new HttpRequestException("Server Error", null, HttpStatusCode.InternalServerError));
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("server-error");
    }

    [Fact]
    public async Task TriggerBackup_ReturnsErrorJson_OnTimeout() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateThrowingClient(
            new TaskCanceledException("Request timed out"));
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", confirm: true, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("timeout");
    }

    [Fact]
    public async Task TriggerBackup_IncludeSnapshotsFalse_FlowsThrough() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", includeSnapshots: false, cancellationToken: ct);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("parameters").GetProperty("includeSnapshots").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task TriggerBackup_ReturnsParseableJson() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, _operationResultJson);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", confirm: true, cancellationToken: ct);

        _ = Should.NotThrow(() => JsonDocument.Parse(result));
    }
}
