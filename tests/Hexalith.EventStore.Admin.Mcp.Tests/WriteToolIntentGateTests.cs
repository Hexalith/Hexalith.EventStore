using System.Net;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Mcp;
using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

using ModelContextProtocol.Server;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class WriteToolIntentGateTests
{
    private const string OperationResultJson = """{"success":true,"operationId":"operation-1","message":"Accepted","errorCode":null}""";

    [Theory]
    [InlineData("backup-trigger", "/api/v1/admin/backups/tenant-1?includeSnapshots=true", "Admin", null)]
    [InlineData("consistency-trigger", "/api/v1/admin/consistency/checks", "Operator", "{\"tenantId\":null,\"domain\":null,\"checkTypes\":[\"SequenceContinuity\"]}")]
    [InlineData("consistency-cancel", "/api/v1/admin/consistency/checks/check-1/cancel", "Admin", null)]
    [InlineData("projection-pause", "/api/v1/admin/projections/tenant-1/projection-1/pause", "Operator", null)]
    [InlineData("projection-resume", "/api/v1/admin/projections/tenant-1/projection-1/resume", "Operator", null)]
    [InlineData("projection-reset", "/api/v1/admin/projections/tenant-1/projection-1/reset", "Operator", "{\"fromPosition\":null}")]
    [InlineData("projection-replay", "/api/v1/admin/projections/tenant-1/projection-1/replay", "Operator", "{\"fromPosition\":10,\"toPosition\":20}")]
    public async Task EveryCallableWriteTool_RequiresIntentAndAttemptsExactlyItsRouteOnce(
        string toolName,
        string expectedPath,
        string expectedPermission,
        string? expectedRequestBody)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int requestCount = 0;
        Uri? requestedUri = null;
        HttpMethod? requestedMethod = null;
        string? requestedBody = null;
        using var handler = new MockHttpMessageHandler(async (request, ct) => {
                _ = Interlocked.Increment(ref requestCount);
                requestedUri = request.RequestUri;
                requestedMethod = request.Method;
                requestedBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(OperationResultJson, System.Text.Encoding.UTF8, "application/json"),
                };
            });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);

        string omittedPreview = await InvokeAsync(toolName, client, null, cancellationToken);
        string falsePreview = await InvokeAsync(toolName, client, false, cancellationToken);

        requestCount.ShouldBe(0);
        AssertPreview(omittedPreview, toolName, expectedPermission, expectedPath);
        AssertPreview(falsePreview, toolName, expectedPermission, expectedPath);

        _ = await InvokeAsync(toolName, client, true, cancellationToken);

        requestCount.ShouldBe(1);
        _ = requestedUri.ShouldNotBeNull();
        requestedUri.PathAndQuery.ShouldBe(expectedPath);
        requestedMethod.ShouldBe(HttpMethod.Post);
        AssertRequestBody(requestedBody, expectedRequestBody);
    }

    [Fact]
    public void CallableWriteToolInventory_IsExactlyTheExhaustivelyTestedSet()
    {
        string[] expected =
        [
            "backup-trigger",
            "consistency-cancel",
            "consistency-trigger",
            "projection-pause",
            "projection-replay",
            "projection-reset",
            "projection-resume",
        ];

        string[] actual = GetMcpTools()
            .Where(HasConfirmDefaultFalse)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected);

        GetMcpTools()
            .Where(method => method.DeclaringType?.Name.EndsWith("WriteTools", StringComparison.Ordinal) == true)
            .Where(method => !HasConfirmDefaultFalse(method))
            .Select(method => method.Name)
            .ShouldBeEmpty();

        string[] expectedPostHelpers =
        [
            "CancelConsistencyCheckAsync",
            "PauseProjectionAsync",
            "ReplayProjectionAsync",
            "ResetProjectionAsync",
            "ResumeProjectionAsync",
            "TriggerBackupAsync",
            "TriggerConsistencyCheckAsync",
        ];
        string[] actualPostHelpers = typeof(AdminApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.ReturnType.IsGenericType
                && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                && method.ReturnType.GetGenericArguments()[0] == typeof(AdminOperationResult))
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        actualPostHelpers.ShouldBe(expectedPostHelpers);
    }

    [Theory]
    [InlineData("backup-trigger")]
    [InlineData("consistency-trigger")]
    [InlineData("consistency-cancel")]
    [InlineData("projection-pause")]
    [InlineData("projection-resume")]
    [InlineData("projection-reset")]
    [InlineData("projection-replay")]
    public async Task EveryCallableWriteTool_InvalidInputIsBoundedAndPerformsZeroRequests(string toolName)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int requestCount = 0;
        using var handler = new MockHttpMessageHandler((_, _) => {
                _ = Interlocked.Increment(ref requestCount);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);

        string result = await InvokeInvalidAsync(toolName, client, cancellationToken);

        requestCount.ShouldBe(0);
        using var document = JsonDocument.Parse(result);
        JsonElement root = document.RootElement;
        root.GetProperty("error").GetBoolean().ShouldBeTrue();
        root.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
        root.GetProperty("message").GetString()!.Length.ShouldBeLessThanOrEqualTo(240);
    }

    private static void AssertRequestBody(string? actual, string? expected)
    {
        if (expected is null)
        {
            actual.ShouldBeNull();
            return;
        }

        _ = actual.ShouldNotBeNull();
        using JsonDocument actualDocument = JsonDocument.Parse(actual);
        using JsonDocument expectedDocument = JsonDocument.Parse(expected);
        JsonElement.DeepEquals(actualDocument.RootElement, expectedDocument.RootElement).ShouldBeTrue();
    }

    private static void AssertPreview(string json, string toolName, string expectedPermission, string expectedPath)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.GetProperty("preview").GetBoolean().ShouldBeTrue();
        root.GetProperty("action").GetString().ShouldBe(toolName);
        root.GetProperty("target").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("impact").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("requiredPermission").GetString().ShouldBe(expectedPermission);
        root.GetProperty("endpoint").GetString().ShouldBe($"POST {expectedPath}");
    }

    private static MethodInfo[] GetMcpTools()
        => typeof(AdminApiClient).Assembly
            .GetTypes()
            .Where(type => type.IsDefined(typeof(McpServerToolTypeAttribute), inherit: false))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.IsDefined(typeof(McpServerToolAttribute), inherit: false))
            .ToArray();

    private static bool HasConfirmDefaultFalse(MethodInfo method)
        => method.GetParameters().Any(parameter =>
            string.Equals(parameter.Name, "confirm", StringComparison.Ordinal)
            && parameter.ParameterType == typeof(bool)
            && parameter.HasDefaultValue
            && parameter.DefaultValue is false);

    private static Task<string> InvokeAsync(
        string toolName,
        AdminApiClient client,
        bool? confirm,
        CancellationToken cancellationToken)
        => (toolName, confirm) switch
        {
            ("backup-trigger", null) => BackupWriteTools.TriggerBackup(
                client,
                "tenant-1",
                cancellationToken: cancellationToken),
            ("backup-trigger", _) => BackupWriteTools.TriggerBackup(
                client,
                "tenant-1",
                confirm: confirm!.Value,
                cancellationToken: cancellationToken),
            ("consistency-trigger", null) => ConsistencyWriteTools.TriggerCheck(
                client,
                "SequenceContinuity",
                cancellationToken: cancellationToken),
            ("consistency-trigger", _) => ConsistencyWriteTools.TriggerCheck(
                client,
                "SequenceContinuity",
                confirm: confirm!.Value,
                cancellationToken: cancellationToken),
            ("consistency-cancel", null) => ConsistencyWriteTools.CancelCheck(
                client,
                "check-1",
                cancellationToken: cancellationToken),
            ("consistency-cancel", _) => ConsistencyWriteTools.CancelCheck(
                client,
                "check-1",
                confirm: confirm!.Value,
                cancellationToken: cancellationToken),
            ("projection-pause", null) => ProjectionWriteTools.PauseProjection(
                client,
                "tenant-1",
                "projection-1",
                cancellationToken: cancellationToken),
            ("projection-pause", _) => ProjectionWriteTools.PauseProjection(
                client,
                "tenant-1",
                "projection-1",
                confirm: confirm!.Value,
                cancellationToken: cancellationToken),
            ("projection-resume", null) => ProjectionWriteTools.ResumeProjection(
                client,
                "tenant-1",
                "projection-1",
                cancellationToken: cancellationToken),
            ("projection-resume", _) => ProjectionWriteTools.ResumeProjection(
                client,
                "tenant-1",
                "projection-1",
                confirm: confirm!.Value,
                cancellationToken: cancellationToken),
            ("projection-reset", null) => ProjectionWriteTools.ResetProjection(
                client,
                "tenant-1",
                "projection-1",
                cancellationToken: cancellationToken),
            ("projection-reset", _) => ProjectionWriteTools.ResetProjection(
                client,
                "tenant-1",
                "projection-1",
                confirm: confirm!.Value,
                cancellationToken: cancellationToken),
            ("projection-replay", null) => ProjectionWriteTools.ReplayProjection(
                client,
                "tenant-1",
                "projection-1",
                10,
                20,
                cancellationToken: cancellationToken),
            ("projection-replay", _) => ProjectionWriteTools.ReplayProjection(
                client,
                "tenant-1",
                "projection-1",
                10,
                20,
                confirm: confirm!.Value,
                cancellationToken: cancellationToken),
            _ => throw new InvalidOperationException($"Unknown write tool '{toolName}'."),
        };

    private static Task<string> InvokeInvalidAsync(
        string toolName,
        AdminApiClient client,
        CancellationToken cancellationToken)
        => toolName switch
        {
            "backup-trigger" => BackupWriteTools.TriggerBackup(
                client,
                string.Empty,
                confirm: true,
                cancellationToken: cancellationToken),
            "consistency-trigger" => ConsistencyWriteTools.TriggerCheck(
                client,
                string.Empty,
                confirm: true,
                cancellationToken: cancellationToken),
            "consistency-cancel" => ConsistencyWriteTools.CancelCheck(
                client,
                string.Empty,
                confirm: true,
                cancellationToken: cancellationToken),
            "projection-pause" => ProjectionWriteTools.PauseProjection(
                client,
                string.Empty,
                "projection-1",
                confirm: true,
                cancellationToken: cancellationToken),
            "projection-resume" => ProjectionWriteTools.ResumeProjection(
                client,
                string.Empty,
                "projection-1",
                confirm: true,
                cancellationToken: cancellationToken),
            "projection-reset" => ProjectionWriteTools.ResetProjection(
                client,
                string.Empty,
                "projection-1",
                confirm: true,
                cancellationToken: cancellationToken),
            "projection-replay" => ProjectionWriteTools.ReplayProjection(
                client,
                string.Empty,
                "projection-1",
                10,
                20,
                confirm: true,
                cancellationToken: cancellationToken),
            _ => throw new InvalidOperationException($"Unknown write tool '{toolName}'."),
        };
}
