using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    [InlineData("backup-trigger", "/api/v1/admin/backups/tenant-1?includeSnapshots=true", "Admin", null, "/api/v1/admin/backups/tenant-1")]
    [InlineData("consistency-trigger", "/api/v1/admin/consistency/checks", "Operator", "{\"tenantId\":\"tenant-1\",\"domain\":null,\"checkTypes\":[0]}", null)]
    [InlineData("consistency-cancel", "/api/v1/admin/consistency/checks/check-1/cancel", "Admin", null, null)]
    [InlineData("projection-pause", "/api/v1/admin/projections/tenant-1/projection-1/pause", "Operator", null, null)]
    [InlineData("projection-resume", "/api/v1/admin/projections/tenant-1/projection-1/resume", "Operator", null, null)]
    [InlineData("projection-reset", "/api/v1/admin/projections/tenant-1/projection-1/reset", "Operator", "{\"fromPosition\":null}", null)]
    [InlineData("projection-replay", "/api/v1/admin/projections/tenant-1/projection-1/replay", "Operator", "{\"fromPosition\":10,\"toPosition\":20}", null)]
    public async Task EveryCallableWriteTool_RequiresIntentAndAttemptsExactlyItsRouteOnce(
        string toolName,
        string expectedPath,
        string expectedPermission,
        string? expectedRequestBody,
        string? expectedPreviewPath)
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
        AssertPreview(omittedPreview, toolName, expectedPermission, expectedPreviewPath ?? expectedPath);
        AssertPreview(falsePreview, toolName, expectedPermission, expectedPreviewPath ?? expectedPath);

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

    [Fact]
    public void PublishedToolInventory_MatchesCallableAssemblyExactly()
    {
        string inventory = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "brownfield", "component-inventory.md"));
        string readSection = GetInventorySection(inventory, "- **Read-only:**", "- **Write (exact callable set):**");
        string writeSection = GetInventorySection(inventory, "- **Write (exact callable set):**", "- **Session context:**");
        string sessionSection = GetInventorySection(inventory, "- **Session context:**", "## Domain-service host surface");
        int deferredDetailsStart = writeSection.IndexOf(" (deferred;", StringComparison.Ordinal);
        deferredDetailsStart.ShouldBeGreaterThan(0);
        writeSection = writeSection[..deferredDetailsStart];

        MethodInfo[] tools = GetMcpTools();
        AssertDocumentedTools(readSection, tools.Where(method => !HasConfirmDefaultFalse(method)
            && method.DeclaringType != typeof(SessionTools)));
        AssertDocumentedTools(writeSection, tools.Where(HasConfirmDefaultFalse));
        AssertDocumentedTools(sessionSection, tools.Where(method => method.DeclaringType == typeof(SessionTools)));
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

    [Fact]
    public async Task CallerBoundary_InvalidUnsafeOverlongScopeEnumPathAndPositionInputs_PerformZeroRequests()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int requestCount = 0;
        using var handler = new MockHttpMessageHandler((_, _) => {
                _ = Interlocked.Increment(ref requestCount);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);
        string unsafeValue = "Bearer eyJhbGciOiJIUzI1NiJ9.payload.signature";
        string overlong = new('x', 241);

        Task<string>[] attempts =
        [
            BackupWriteTools.TriggerBackup(client, "Tenant-1", confirm: true, cancellationToken: cancellationToken),
            BackupWriteTools.TriggerBackup(client, "tenant-1", description: unsafeValue, confirm: true, cancellationToken: cancellationToken),
            BackupWriteTools.TriggerBackup(client, "tenant-1", description: overlong, confirm: true, cancellationToken: cancellationToken),
            ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", "Tenant-1", confirm: true, cancellationToken: cancellationToken),
            ConsistencyWriteTools.TriggerCheck(client, "UnknownCheck", "tenant-1", confirm: true, cancellationToken: cancellationToken),
            ConsistencyWriteTools.TriggerCheck(client, "SequenceContinuity", "tenant-1", domain: unsafeValue, confirm: true, cancellationToken: cancellationToken),
            ConsistencyWriteTools.CancelCheck(client, "../check-1", confirm: true, cancellationToken: cancellationToken),
            ProjectionWriteTools.PauseProjection(client, "Tenant-1", "projection-1", confirm: true, cancellationToken: cancellationToken),
            ProjectionWriteTools.ResumeProjection(client, "tenant-1", "../projection-1", confirm: true, cancellationToken: cancellationToken),
            ProjectionWriteTools.ResetProjection(client, "tenant-1", "projection-1", fromPosition: -1, confirm: true, cancellationToken: cancellationToken),
            ProjectionWriteTools.ReplayProjection(client, "tenant-1", "projection-1", -1, 20, confirm: true, cancellationToken: cancellationToken),
            ProjectionWriteTools.PauseProjection(client, new string('t', 64), new string('p', 180), confirm: true, cancellationToken: cancellationToken),
        ];

        string[] results = await Task.WhenAll(attempts);

        requestCount.ShouldBe(0);
        foreach (string result in results)
        {
            using JsonDocument document = JsonDocument.Parse(result);
            document.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
            document.RootElement.GetProperty("message").GetString()!.Length.ShouldBeLessThanOrEqualTo(240);
        }
    }

    [Fact]
    public async Task BoundedBackupDescriptionDoesNotOverflowComposedPreviewFields()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int requestCount = 0;
        using var handler = new MockHttpMessageHandler((_, _) => {
                _ = Interlocked.Increment(ref requestCount);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);
        string tenantId = new('t', 64);
        string optionalText = new('x', 180);

        string backupPreview = await BackupWriteTools.TriggerBackup(
            client,
            tenantId,
            description: optionalText,
            confirm: false,
            cancellationToken: cancellationToken);
        string consistencyResult = await ConsistencyWriteTools.TriggerCheck(
            client,
            "SequenceContinuity",
            tenantId,
            domain: optionalText,
            confirm: true,
            cancellationToken: cancellationToken);

        requestCount.ShouldBe(0);
        using (JsonDocument previewDocument = JsonDocument.Parse(backupPreview))
        {
            previewDocument.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
            previewDocument.RootElement.GetProperty("target").GetString()!.Length.ShouldBeLessThanOrEqualTo(240);
            previewDocument.RootElement.GetProperty("endpoint").GetString()!.Length.ShouldBeLessThanOrEqualTo(240);
            previewDocument.RootElement.GetProperty("parameters").GetProperty("description").GetString().ShouldBe(optionalText);
        }

        using JsonDocument consistencyDocument = JsonDocument.Parse(consistencyResult);
        consistencyDocument.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task OptionalWhitespaceInputs_AreOmittedConsistentlyFromPreviewAndExecution()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Uri? requestedUri = null;
        string? requestedBody = null;
        using var handler = new MockHttpMessageHandler(async (request, ct) => {
                requestedUri = request.RequestUri;
                requestedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(OperationResultJson, System.Text.Encoding.UTF8, "application/json"),
                };
            });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);

        string backupPreview = await BackupWriteTools.TriggerBackup(client, "tenant-1", description: "   ", cancellationToken: cancellationToken);
        _ = await BackupWriteTools.TriggerBackup(client, "tenant-1", description: "   ", confirm: true, cancellationToken: cancellationToken);
        using (JsonDocument document = JsonDocument.Parse(backupPreview))
        {
            document.RootElement.GetProperty("parameters").GetProperty("description").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        _ = requestedUri.ShouldNotBeNull();
        requestedUri.Query.ShouldNotContain("description", Case.Insensitive);

        string consistencyPreview = await ConsistencyWriteTools.TriggerCheck(
            client,
            "SequenceContinuity",
            "tenant-1",
            domain: "   ",
            cancellationToken: cancellationToken);
        _ = await ConsistencyWriteTools.TriggerCheck(
            client,
            "SequenceContinuity",
            "tenant-1",
            domain: "   ",
            confirm: true,
            cancellationToken: cancellationToken);
        using (JsonDocument document = JsonDocument.Parse(consistencyPreview))
        {
            document.RootElement.GetProperty("parameters").GetProperty("domain").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        _ = requestedBody.ShouldNotBeNull();
        using JsonDocument bodyDocument = JsonDocument.Parse(requestedBody);
        bodyDocument.RootElement.GetProperty("domain").ValueKind.ShouldBe(JsonValueKind.Null);
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

    private static void AssertDocumentedTools(string section, IEnumerable<MethodInfo> methods)
    {
        string[] documented = Regex.Matches(section, "`([a-z]+(?:-[a-z]+)*)`", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] callable = methods
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        documented.ShouldBe(callable);
    }

    private static string GetInventorySection(string inventory, string startMarker, string endMarker)
    {
        int start = inventory.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        int end = inventory.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);
        return inventory[start..end];
    }

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
                "tenant-1",
                cancellationToken: cancellationToken),
            ("consistency-trigger", _) => ConsistencyWriteTools.TriggerCheck(
                client,
                "SequenceContinuity",
                "tenant-1",
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
                "tenant-1",
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Path.GetDirectoryName(typeof(WriteToolIntentGateTests).Assembly.Location)!);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate the Hexalith.EventStore repository root.");
    }
}
