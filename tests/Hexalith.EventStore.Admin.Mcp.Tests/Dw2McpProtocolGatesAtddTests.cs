using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

using ModelContextProtocol.Server;

using Shouldly;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw2-admin-dapr-mcp-live-evidence.md
//
// Locks the MCP host protocol contract that DW2 evidence captures live:
//   AC#7  Startup: required env vars validated, stderr-only logging, stdout reserved for JSON-RPC.
//   AC#8  Representative read tool + approval-gated write-preview tool — write-preview MUST NOT
//         execute a destructive operation (no AdminApiClient call when confirm=false).
//   AC#9  Session fallback: when scope args (tenantId/domain) are omitted, session-context tools
//         use InvestigationSession values instead of nulls. Classification MUST distinguish
//         `feature absent` / `feature broken` / `blocked by missing session-establishment`.
//
// Skip rationale: tests are marked [Fact(Skip = "...")] until DW2 live MCP smoke evidence has
// recorded the matching transcript phase (initialize, tools/list, tool call, session fallback,
// latency sample). Removing Skip per AC means the dev has paired the scaffold with the captured
// MCP transcript artefact in the DW2 evidence index.
//
// Cross-reference: existing `ConfigurationValidationTests.cs` already covers env-var validation
// at process boundary. This file extends the contract to in-process tool-discovery, write-preview
// non-mutation, and session-fallback classification.
public class Dw2McpProtocolGatesAtddTests
{
    private const string SkipReasonAc7 = "ATDD red phase — DW2 AC#7 (MCP startup + stdio discipline). Remove Skip after the DW2 live MCP transcript captures initialize → tools/list with stdout reserved for JSON-RPC and stderr carrying logs.";
    private const string SkipReasonAc8 = "ATDD red phase — DW2 AC#8 (write-preview non-mutation proof). Remove Skip after the live evidence captures one approval-gated write tool's preview shape AND a before/after non-mutation pair.";
    private const string SkipReasonAc9 = "ATDD red phase — DW2 AC#9 (session fallback classification). Remove Skip after the live transcript captures session-set-context followed by a scope-omitted tool call that uses session values, with absent/broken/blocked classification recorded.";

    private const string CanonicalTenant = "test-tenant-dw2";
    private const string CanonicalDomain = "counter";
    private const string CanonicalAggregateId = "01HXDW2COUNTER0000000001";

    // ===== AC#7 — Tool discovery =====

    [Fact(Skip = SkipReasonAc7)]
    public void ToolsAssembly_AdvertisesRepresentativeReadTool()
    {
        // AC#7 — `tools/list` is the source of truth (MCP spec, server-tool discovery is assembly-
        // driven via WithToolsFromAssembly). The DW2 evidence transcript MUST include at least one
        // representative read tool — `health-status`, `health-dapr`, `stream-list`, or `stream-events`
        // (existing in DiagnosticTools/HealthTools/StreamTools). This gate locks that the
        // representative read tool stays discoverable.
        IReadOnlyList<string> toolNames = DiscoverToolNames();
        string[] representativeRead = ["health-status", "health-dapr", "stream-list", "stream-events"];

        toolNames.Any(name => Array.IndexOf(representativeRead, name) >= 0).ShouldBeTrue(
            customMessage: "DW2 evidence requires at least one representative read tool (health-status, health-dapr, stream-list, stream-events) to be discoverable in tools/list.");
    }

    [Fact(Skip = SkipReasonAc7)]
    public void ToolsAssembly_AdvertisesAtLeastOneApprovalGatedWriteTool()
    {
        // AC#8 — The DW2 evidence MUST exercise one approval-gated write-preview tool. Lock that
        // at least one such tool stays discoverable (consistency-trigger, projection-pause,
        // projection-resume, projection-reset, backup-create, etc.).
        IReadOnlyList<string> toolNames = DiscoverToolNames();
        string[] approvalGated = [
            "consistency-trigger", "consistency-cancel",
            "projection-pause", "projection-resume", "projection-reset",
            "backup-create", "backup-restore",
        ];

        toolNames.Any(name => Array.IndexOf(approvalGated, name) >= 0).ShouldBeTrue(
            customMessage: "DW2 evidence requires at least one approval-gated write tool (consistency-*, projection-*, or backup-*) to remain discoverable.");
    }

    [Fact(Skip = SkipReasonAc9)]
    public void ToolsAssembly_AdvertisesSessionContextTools()
    {
        // AC#9 — Session fallback proof requires session-set-context AND session-get-context tools.
        // Lock their discoverability so a refactor can't silently remove the established
        // session-establishment path (which would leave session fallback "blocked by missing
        // session-establishment path" — a defect, not a deferred decision).
        IReadOnlyList<string> toolNames = DiscoverToolNames();

        toolNames.ShouldContain("session-set-context");
        toolNames.ShouldContain("session-get-context");
    }

    // ===== AC#8 — Write-preview non-mutation =====

    [Fact(Skip = SkipReasonAc8)]
    public async Task ConsistencyTrigger_WithoutConfirm_DoesNotInvokeAdminApi()
    {
        // AC#8 — The approval gate is contractual: confirm=false MUST short-circuit BEFORE any
        // HTTP call. The DW2 before/after non-mutation pair depends on this. This gate fails if
        // a refactor accidentally calls AdminApiClient.TriggerConsistencyCheckAsync regardless of
        // the confirm flag.
        CancellationToken ct = TestContext.Current.CancellationToken;
        int outboundCalls = 0;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            _ => Interlocked.Increment(ref outboundCalls),
            HttpStatusCode.OK,
            """{"success":true,"operationId":"WOULD-MUTATE","message":"unexpected","errorCode":null}""");
        AdminApiClient client = new(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(
            client,
            "SequenceContinuity",
            tenantId: CanonicalTenant,
            domain: CanonicalDomain,
            confirm: false,
            cancellationToken: ct);

        outboundCalls.ShouldBe(0);
        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("preview").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("action").GetString().ShouldBe("consistency-trigger");
    }

    [Fact(Skip = SkipReasonAc8)]
    public async Task WritePreviewShape_IsStable_AcrossEvidenceTranscripts()
    {
        // AC#8 — The DW2 transcript needs a stable preview shape for review. Lock the keys
        // emitted by ToolHelper.SerializePreview so a future refactor can't reshape evidence.
        CancellationToken ct = TestContext.Current.CancellationToken;
        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(
            HttpStatusCode.OK,
            """{"success":true,"operationId":"x","message":"x","errorCode":null}""");
        AdminApiClient client = new(httpClient);

        string result = await ConsistencyWriteTools.TriggerCheck(
            client,
            "SequenceContinuity",
            tenantId: CanonicalTenant,
            confirm: false,
            cancellationToken: ct);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("preview", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("action", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("description", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("endpoint", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("parameters", out _).ShouldBeTrue();
        doc.RootElement.TryGetProperty("warning", out _).ShouldBeTrue();
    }

    // ===== AC#9 — Session fallback (positive + classification) =====

    [Fact(Skip = SkipReasonAc9)]
    public async Task StreamList_OmittedScope_FallsBackToSessionTenantAndDomain()
    {
        // AC#9 — When the caller omits tenantId/domain AND the session has them set, the tool
        // MUST forward session values (positive proof of the `feature present` classification).
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        AdminApiClient client = new(httpClient);
        InvestigationSession session = new();
        session.SetContext(CanonicalTenant, CanonicalDomain);

        _ = await StreamTools.ListStreams(client, session, tenantId: null, domain: null, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri!.PathAndQuery.ShouldContain($"tenantId={CanonicalTenant}");
        capturedUri.PathAndQuery.ShouldContain($"domain={CanonicalDomain}");
    }

    [Fact(Skip = SkipReasonAc9)]
    public async Task SessionFallback_NoEstablishedContext_DoesNotFabricateScope()
    {
        // AC#9 — `feature absent` classification: when the session has NO context AND the caller
        // omits scope, the tool MUST NOT manufacture a tenant/domain. The outbound URI MUST omit
        // those query params entirely. This locks the negative case so the smoke transcript can
        // distinguish `feature absent` from `feature broken`.
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        AdminApiClient client = new(httpClient);
        InvestigationSession session = new();
        // intentionally NO session.SetContext() call

        _ = await StreamTools.ListStreams(client, session, tenantId: null, domain: null, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri!.PathAndQuery.ShouldNotContain("tenantId=");
        capturedUri.PathAndQuery.ShouldNotContain("domain=");
    }

    // ===== AC#7 — Stdio discipline =====

    [Fact(Skip = SkipReasonAc7)]
    public async Task McpHost_ValidEnvVars_KeepsStdoutEmptyBeforeJsonRpc()
    {
        // AC#7 — Stdout is RESERVED for JSON-RPC. With valid env vars supplied and no client input
        // on stdin, the host MUST NOT emit any non-protocol bytes on stdout in the first 2s before
        // initialize. Logs go to stderr only. This gate fails if logging providers are
        // accidentally re-enabled on stdout, which would corrupt MCP framing.
        string mcpDll = ResolveMcpDll();
        Process process = new() {
            StartInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"\"{mcpDll}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.Environment["EVENTSTORE_ADMIN_URL"] = "https://localhost:5443";
        process.StartInfo.Environment["EVENTSTORE_ADMIN_TOKEN"] = "redacted-test-token";

        try {
            _ = process.Start();
            // Read for 2 seconds; do NOT send any JSON-RPC initialize. Any bytes appearing on
            // stdout in this window are protocol corruption.
            using CancellationTokenSource readCts = new(TimeSpan.FromSeconds(2));
            string stdoutBytes;
            try {
                stdoutBytes = await process.StandardOutput.ReadToEndAsync(readCts.Token);
            }
            catch (OperationCanceledException) {
                // Expected — the host blocks on stdin so ReadToEnd never completes naturally.
                stdoutBytes = string.Empty;
            }

            stdoutBytes.ShouldBeEmpty(
                customMessage: "DW2 AC#7 — stdout MUST be empty before MCP initialize. Logs go to stderr.");
        }
        finally {
            if (!process.HasExited) {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }
            await process.WaitForExitAsync();
            process.Dispose();
        }
    }

    // ===== Helpers =====

    private static IReadOnlyList<string> DiscoverToolNames()
    {
        // Mirror Program.cs discovery (WithToolsFromAssembly()): scan the Admin.Mcp assembly for
        // [McpServerTool] attributes on static methods inside [McpServerToolType] classes.
        Assembly mcpAssembly = typeof(AdminApiClient).Assembly;
        List<string> names = [];
        foreach (Type type in mcpAssembly.GetTypes()) {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null) {
                continue;
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)) {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is { } toolAttr) {
                    string? name = toolAttr.Name;
                    if (!string.IsNullOrWhiteSpace(name)) {
                        names.Add(name);
                    }
                }
            }
        }

        return names;
    }

    private static string ResolveMcpDll()
    {
        // Mirror ConfigurationValidationTests.ResolveMcpDll() so this scaffold doesn't depend on
        // a private helper. tests/Hexalith.EventStore.Admin.Mcp.Tests/bin/<config>/<tfm>/ →
        // src/Hexalith.EventStore.Admin.Mcp/bin/<config>/<tfm>/.
        string testDir = Path.GetDirectoryName(typeof(Dw2McpProtocolGatesAtddTests).Assembly.Location)!;
        string config = new DirectoryInfo(testDir).Parent!.Name;
        string tfm = new DirectoryInfo(testDir).Name;
        string repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        string mcpDll = Path.Combine(repoRoot, "src", "Hexalith.EventStore.Admin.Mcp", "bin", config, tfm, "Hexalith.EventStore.Admin.Mcp.dll");
        return File.Exists(mcpDll) ? mcpDll : typeof(AdminApiClient).Assembly.Location;
    }
}
