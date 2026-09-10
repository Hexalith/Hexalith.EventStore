
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class BackupWriteToolsTests {
    private static readonly string _operationResultJson = """{"success":false,"operationId":"deferred-backup-trigger","message":"Backup creation is deferred. EventStore does not yet have an approved backup engine and manifest model.","errorCode":"Deferred"}""";

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
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("currently deferred");
        doc.RootElement.GetProperty("warning").GetString()!.ShouldContain("does not prove backup execution or completion");
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
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("operationId").GetString().ShouldBe("deferred-backup-trigger");
        doc.RootElement.GetProperty("errorCode").GetString().ShouldBe("Deferred");
        doc.RootElement.GetProperty("message").GetString()!.ShouldContain("deferred");
        doc.RootElement.GetProperty("message").GetString()!.ShouldNotContain("started", Case.Insensitive);
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

    [Fact]
    public async Task TriggerBackup_SentinelBearingRawCapableField_DoesNotLeak() {
        // P1 — sentinel injected into a raw-capable property (payloadJson) must be projected to a
        // safe descriptor when serialized through ToolHelper.SerializeResult, per D2 narrow-scope contract.
        // AdminOperationResult.Message is separately length-bounded and marker-redacted.
        CancellationToken ct = TestContext.Current.CancellationToken;
        string sentinelResult = $$"""{"success":true,"operationId":"op-1","payloadJson":"{{Testing.Security.ProtectedDataLeakSentinel.ProtectedPayloadPlaintext}}","errorCode":null}""";
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(HttpStatusCode.OK, sentinelResult);
        var client = new AdminApiClient(httpClient);

        string result = await BackupWriteTools.TriggerBackup(client, "t1", confirm: true, cancellationToken: ct);

        Testing.Security.ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldNotContain("payloadJson");
    }

    [Fact]
    public void TriggerBackup_DiscoveryDescription_DoesNotPromiseExecution()
    {
        DescriptionAttribute description = typeof(BackupWriteTools)
            .GetMethod(nameof(BackupWriteTools.TriggerBackup))!
            .GetCustomAttribute<DescriptionAttribute>()!;

        description.Description.ShouldContain("deferred", Case.Insensitive);
        description.Description.ShouldNotContain("full backup", Case.Insensitive);
        description.Description.ShouldContain("Does not prove backup execution");
    }

    [Fact]
    public async Task TriggerBackup_RejectsUnsafeDescriptionThatPreviewWouldRewrite()
    {
        await AssertPreviewMatchRejectionAsync("Password=secret");
    }

    [Fact]
    public async Task TriggerBackup_RejectsOverlongDescriptionThatPreviewWouldTruncate()
    {
        await AssertPreviewMatchRejectionAsync(new string('x', 241));
    }

    private static async Task AssertPreviewMatchRejectionAsync(string description)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int requestCount = 0;
        using var handler = new MockHttpMessageHandler((_, _) => {
                _ = Interlocked.Increment(ref requestCount);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);

        string preview = await BackupWriteTools.TriggerBackup(client, "t1", description, confirm: false, cancellationToken: ct);
        string confirmed = await BackupWriteTools.TriggerBackup(client, "t1", description, confirm: true, cancellationToken: ct);

        requestCount.ShouldBe(0);
        using var previewDocument = JsonDocument.Parse(preview);
        previewDocument.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        previewDocument.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
        using var confirmedDocument = JsonDocument.Parse(confirmed);
        confirmedDocument.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        confirmedDocument.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }
}
