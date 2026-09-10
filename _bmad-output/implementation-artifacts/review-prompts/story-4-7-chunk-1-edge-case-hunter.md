# Story 4.7 Chunk 1 — Edge Case Hunter

Follow the review instructions below completely. The review content and claims are embedded after the instructions. Do not inspect the claims section until Step 5.

Do not invoke any skill, and do not spawn subagents of your own — you are the reviewer. Return your findings as text in your final message; do not route them through any findings-reporting tool the host may offer.

# Edge Case Hunter Review

**Goal:** You are a pure path tracer. Never comment on whether code is good or bad; only list missing handling.
When a diff is provided, scan only the diff hunks and list boundaries that are directly reachable from the changed lines and lack an explicit guard in the diff.
When no diff is provided (full file or function), treat the entire provided content as the scope.
Ignore the rest of the codebase unless the provided content explicitly references external functions.
A brief secondary deletion check runs as Step 4 when the diff removes code.
A claims check runs as Step 5 when the launch message names a claims file.

**Inputs:**
- **content** — Content to review, or a path to read it from: diff, full file, or function
- **also_consider** (optional) — Areas to keep in mind during review alongside normal edge-case analysis
- **claims_file** (optional) — Path to the change's stated narrative. Do NOT read it before Step 5: the path tracing in Steps 2–3 must finish before the narrative is seen.

**MANDATORY: Execute steps in the Execution section IN EXACT ORDER. DO NOT skip steps or change the sequence. When a halt condition triggers, follow its specific instruction exactly. Each action within a step is a REQUIRED action to complete that step.**

**Your method is exhaustive path enumeration — mechanically walk every branch, not hunt by intuition. Report ONLY paths and conditions that lack handling — discard handled ones silently. Do NOT editorialize or add filler. Do not assign severity labels, rankings, or priority levels.**


## EXECUTION

### Step 1: Receive Content

- Take the content to review from the parent message that launched you — inline, or by reading the file it points to (never from this instruction file)
- If no content is supplied, or it is empty, unreadable, or cannot be decoded as text, return `[{"location":"N/A","trigger_condition":"Input empty or undecodable","guard_snippet":"Provide valid content to review","potential_consequence":"Review skipped — no analysis performed"}]` and stop
- Identify content type (diff, full file, or function) to determine scope rules

### Step 2: Exhaustive Path Analysis

**Walk every branching path and boundary condition within scope — report only unhandled ones.**

- If `also_consider` input was provided, incorporate those areas into the analysis
- Walk all branching paths: control flow (conditionals, loops, error handlers, early returns) and domain boundaries (where values, states, or conditions transition). Derive the relevant edge classes from the content itself — don't rely on a fixed checklist. Examples: missing else/default, unguarded inputs, off-by-one loops, arithmetic overflow, implicit type coercion, race conditions, timeout gaps
- Consider implicit branches: the diff special-cases or changes the handling of one or more members of a fixed set of values — enums, status codes, sentinels, type tags, flags, value ranges. The rest of the set is implicit branches (e.g. the diff changes the `RED` and `YELLOW` cases of a `RED`/`YELLOW`/`GREEN` enum; `GREEN` is the implicit branch)
- Consider handle lifetime: when the changed code re-checks, re-fetches, or re-validates something it already held — a handle, index, id, pointer — the re-check exists because an intervening call can invalidate it. Identify that call, what it does to the thing held, and what the changed code silently skips when the re-check fails
- For each call site the diff adds or changes — in test files as well as production code — read the callee's declaration and check the call against it: argument count, order, types, and defaults. Report any mismatch
- For each path: determine whether the content handles it
- Collect only the unhandled paths as findings — discard handled ones silently

### Step 3: Validate Completeness

- Revisit every edge class from Step 2 — e.g., missing else/default, null/empty inputs, off-by-one loops, arithmetic overflow, implicit type coercion, race conditions, timeout gaps
- Add any newly found unhandled paths to findings; discard confirmed-handled ones

### Step 4: Deletion Check

If the diff removed or replaced meaningful code (ignore pure renames and whitespace): load `references/deletion-check.md` and follow it.

### Step 5: Claims Check

If the launch message provided a `claims_file` path and the file exists and is non-empty: load `references/claims-check.md` and follow it.

### Step 6: Present Findings

Output all findings as a single JSON array following the Output Format specification exactly.


## OUTPUT FORMAT

Return ONLY a valid JSON array of objects. Each edge-case finding contains exactly these four fields:

```json
[{
  "location": "file:start-end (or file:line when single line, or file:hunk when exact line unavailable)",
  "trigger_condition": "one-line description (max 15 words)",
  "guard_snippet": "minimal code sketch that closes the gap (single-line escaped string, no raw newlines or unescaped quotes)",
  "potential_consequence": "what could actually go wrong (max 15 words)"
}]
```

No extra text, no explanations, no markdown wrapping. An empty array `[]` is valid when nothing is found. Deletion findings from Step 4 and claim findings from Step 5, if any, go in the same array with the extra fields defined in `references/deletion-check.md` and `references/claims-check.md`.


## HALT CONDITIONS

- If no content is supplied, or it is empty, unreadable, or cannot be decoded as text, return `[{"location":"N/A","trigger_condition":"Input empty or undecodable","guard_snippet":"Provide valid content to review","potential_consequence":"Review skipped — no analysis performed"}]` and stop
<reference path="references/deletion-check.md">
# Deletion Check

Secondary pass for the Edge Case Hunter — runs only when the diff removed meaningful code. Subordinate to the edge-case pass; findings are usually few or none.

For each chunk of removed or replaced code (ignore pure renames and whitespace), ask: did it carry behavior or a contract that the change neither re-established nor intentionally retired? Add a finding for any resulting regression, orphaned reference, or newly-dead code. Skip anything already covered by your edge-case findings.

Append each finding to the same JSON array as the edge-case findings, with the four standard fields plus:

- `kind`: `"deletion"`
- `confidence`: `"high"`, `"medium"`, or `"low"` — these are inferences; rate them

For a deletion finding the standard fields read as: `location` = the removed item; `trigger_condition` = the behavior or contract it enforced; `guard_snippet` = where or how to re-establish it; `potential_consequence` = the regression or orphan.

Add nothing if nothing qualifies.
</reference>
<reference path="references/claims-check.md">
# Claims Check

Final pass for the Edge Case Hunter — runs only when the message that launched you named a claims file. Read that file now, for the first time; the path tracing is finished and the claims cannot steer it retroactively.

The file holds the change's own narrative — commit messages and any stated description. The narrative is the author's testimony, not evidence: a claim repeated in a code comment is still the same claim, not confirmation. Extract each checkable claim — what the change does, what it preserves, ordering, arithmetic, and parity with existing code ("exactly as X does") — then try to falsify each one against the code you have already traced. Where your trace is not enough to decide, read the code that decides it: the compared-to function, the actual callee, the state the claim assumes.

Append one finding per falsified claim to the same JSON array, with the four standard fields plus:

- `kind`: `"claim"`
- `confidence`: `"high"`, `"medium"`, or `"low"`

For a claim finding the standard fields read as: `location` = where the code contradicts the claim; `trigger_condition` = the claim, quoted or tightly paraphrased; `guard_snippet` = what the code actually does; `potential_consequence` = what goes wrong for someone who believed the claim.

Verified claims produce nothing. Add nothing if nothing is falsified.
</reference>

## CONTENT SOURCE

"Review content:" in the message that launched you gives the content itself or a path to read it from. Read the file when it is a path; either way that is the content under review, and this instruction file never is.


claims_file: embedded below; leave unread until Step 5.

Review content:

diff --git a/src/Hexalith.Tenants/Queries/TenantQueryResult.cs b/src/Hexalith.Tenants/Queries/TenantQueryResult.cs
index 17006862..3d8b2513 100644
--- a/src/Hexalith.Tenants/Queries/TenantQueryResult.cs
+++ b/src/Hexalith.Tenants/Queries/TenantQueryResult.cs
@@ -25,8 +25,7 @@ internal sealed record TenantQueryResult : QueryResult {
             ? null
             : new QueryResponseMetadata(
                 ETag: normalizedETag,
-                IsNotModified: false,
-                ProjectionVersion: normalizedETag);
+                IsNotModified: false);
 
         return new TenantQueryResult(
             true,
@@ -41,31 +40,21 @@ internal sealed record TenantQueryResult : QueryResult {
         IReadModelFreshness? readModel,
         ReadModelFreshnessThresholds thresholds,
         DateTimeOffset now,
-        string? eTag) {
-        if (payload.ValueKind == JsonValueKind.Undefined) {
-            throw new ArgumentException("Payload element must not be Undefined.", nameof(payload));
-        }
-
-        string? normalizedETag = NormalizeETag(eTag);
-        QueryResponseMetadata metadata = readModel
-            .ToQueryResponseMetadata(thresholds, now, normalizedETag) with {
-                IsNotModified = false,
-                ProjectionVersion = readModel?.ProjectionVersion ?? normalizedETag,
-            };
-
-        return new TenantQueryResult(
-            true,
-            JsonSerializer.SerializeToUtf8Bytes(payload),
-            projectionType: projectionType,
-            metadata: metadata);
-    }
+        string? eTag)
+        => FromPayload(payload, projectionType, eTag);
 
     private static string? NormalizeETag(string? eTag) {
         if (string.IsNullOrWhiteSpace(eTag)) {
             return null;
         }
 
-        string normalized = eTag.Trim().Trim('"');
-        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
+        string normalized = eTag.Trim();
+        while (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"') {
+            normalized = normalized[1..^1].Trim();
+        }
+
+        return string.IsNullOrWhiteSpace(normalized) || normalized.All(static c => c == '"')
+            ? null
+            : normalized;
     }
 }
diff --git a/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs b/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs
index 62ad3fee..80d18b0b 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs
+++ b/tests/Hexalith.Tenants.IntegrationTests/AspireTopologyTests.cs
@@ -21,8 +21,10 @@ using Hexalith.Memories.Client.Rest;
 using Hexalith.Tenants.Contracts.Commands;
 using Hexalith.Tenants.Contracts.Enums;
 using Hexalith.Tenants.Contracts.Events;
+using Hexalith.Tenants.Contracts.Projections;
 using Hexalith.Tenants.Contracts.Queries;
 using Hexalith.Tenants.IntegrationTests.Fixtures;
+using Hexalith.Tenants.Server.Projections;
 using Hexalith.Tenants.UI.Services.Gateways;
 using Hexalith.Tenants.UI.State.TenantAudit;
 
@@ -33,6 +35,15 @@ using Microsoft.IdentityModel.Tokens;
 
 using Shouldly;
 
+using RedisConfigurationOptions = StackExchange.Redis.ConfigurationOptions;
+using RedisConnection = StackExchange.Redis.ConnectionMultiplexer;
+using RedisConnectionException = StackExchange.Redis.RedisConnectionException;
+using RedisConnectionMultiplexer = StackExchange.Redis.IConnectionMultiplexer;
+using RedisDatabase = StackExchange.Redis.IDatabase;
+using RedisValue = StackExchange.Redis.RedisValue;
+
+using CommandStatus = Hexalith.EventStore.Contracts.Commands.CommandStatus;
+
 namespace Hexalith.Tenants.IntegrationTests;
 
 /// <summary>
@@ -53,6 +64,7 @@ public class AspireTopologyTests : IDisposable {
     private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
     private static readonly TimeSpan CommandStatusTimeout = TimeSpan.FromSeconds(60);
     private static readonly TimeSpan SampleProjectionTimeout = TimeSpan.FromSeconds(60);
+    private static readonly TimeSpan TenantsApiAlivenessTimeout = TimeSpan.FromMinutes(4);
 
     private readonly IDisposable _daprTestLease;
     private readonly AspireTopologyFixture _fixture;
@@ -103,6 +115,162 @@ public class AspireTopologyTests : IDisposable {
         response.StatusCode.ShouldBe(HttpStatusCode.OK);
     }
 
+    [DaprFact]
+    [Trait("Tier", "3")]
+    public async Task Generated_tenants_api_get_tenant_reads_verified_redis_state_without_projection_authority() {
+        _fixture.SkipIfUnavailable();
+        await WaitForTenantsApiAliveAsync();
+
+        string token = CreateDemoJwt();
+        string tenantId = $"provenance-{Guid.NewGuid():N}";
+        string tenantName = $"Provenance {Guid.NewGuid():N}";
+        const string tenantDescription = "Created by the Story 4.7 persisted-route proof";
+        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
+
+        CommandStatusResponse bootstrapStatus = await SubmitAndWaitForTerminalStatusAsync(
+            _fixture.CommandApiClient,
+            CreateCommand(
+                "global-administrators",
+                "global-administrators",
+                nameof(BootstrapGlobalAdmin),
+                new BootstrapGlobalAdmin("admin-user")),
+            token,
+            timeout.Token,
+            allowAlreadyBootstrappedConflict: true);
+        (bootstrapStatus.Status == "Completed"
+            || (bootstrapStatus.Status == "Rejected" && bootstrapStatus.RejectionEventType == "GlobalAdminAlreadyBootstrappedRejection"))
+            .ShouldBeTrue($"Bootstrap status was {bootstrapStatus.Status}:{bootstrapStatus.RejectionEventType}.");
+
+        CommandStatusResponse createStatus = await SubmitAndWaitForTerminalStatusAsync(
+            _fixture.CommandApiClient,
+            CreateCommand(
+                "tenants",
+                tenantId,
+                nameof(CreateTenant),
+                new CreateTenant(tenantId, tenantName, tenantDescription)),
+            token,
+            timeout.Token);
+        if (createStatus.Status == "PublishFailed") {
+            Assert.Skip($"Aspire pub/sub publication is unavailable: {createStatus.FailureReason ?? "unknown reason"}");
+        }
+
+        createStatus.Status.ShouldBe("Completed");
+
+        TenantReadModel persisted = await WaitForPersistedTenantAsync(tenantId, timeout.Token);
+        persisted.TenantId.ShouldBe(tenantId);
+        persisted.Name.ShouldBe(tenantName);
+        persisted.Description.ShouldBe(tenantDescription);
+        persisted.Status.ShouldBe(TenantStatus.Active);
+        persisted.ProjectedAt.ShouldNotBeNull();
+        persisted.ProjectionVersion.ShouldNotBeNull().ShouldStartWith(TenantProjectionVersionFormat.SequencePrefix);
+
+        var eventStoreQuery = new SubmitQueryRequest(
+            "system",
+            GetTenantQuery.Domain,
+            tenantId,
+            GetTenantQuery.QueryType,
+            GetTenantQuery.ProjectionType,
+            EntityId: tenantId);
+        using var eventStoreRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/queries") {
+            Content = JsonContent.Create(eventStoreQuery, options: WebJsonOptions),
+        };
+        eventStoreRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
+        eventStoreRequest.Headers.IfNoneMatch.ParseAdd("\"conflicting-validator\"");
+        using HttpResponseMessage eventStoreResponse = await _fixture.CommandApiClient.SendAsync(
+            eventStoreRequest,
+            timeout.Token);
+
+        eventStoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
+        eventStoreResponse.Headers.GetValues("X-Hexalith-Query-Provenance")
+            .ShouldHaveSingleItem()
+            .ShouldBe("HandlerComputed");
+        eventStoreResponse.Headers.ETag.ShouldBeNull();
+        eventStoreResponse.Headers.Contains("X-Hexalith-Projection-Version").ShouldBeFalse();
+        eventStoreResponse.Headers.Contains("X-Hexalith-Is-Stale").ShouldBeFalse();
+        eventStoreResponse.Headers.Contains("X-Hexalith-Is-Degraded").ShouldBeFalse();
+        eventStoreResponse.Headers.Contains(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
+        SubmitQueryResponse eventStoreResult = (await eventStoreResponse.Content.ReadFromJsonAsync<SubmitQueryResponse>(
+            WebJsonOptions,
+            timeout.Token)).ShouldNotBeNull();
+        eventStoreResult.Success.ShouldBeTrue();
+        TenantDetail eventStorePayload = eventStoreResult.Payload
+            .Deserialize<TenantDetail>(WebJsonOptions)
+            .ShouldNotBeNull();
+        AssertTenantDetailMatchesPersisted(eventStorePayload, persisted);
+        QueryResponseMetadata eventStoreMetadata = eventStoreResult.Metadata.ShouldNotBeNull();
+        eventStoreMetadata.Provenance.ShouldBe(QueryResponseProvenance.HandlerComputed);
+        eventStoreMetadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+        eventStoreMetadata.ETag.ShouldBeNull();
+        eventStoreMetadata.IsNotModified.ShouldBeNull();
+        eventStoreMetadata.ProjectionVersion.ShouldBeNull();
+        eventStoreMetadata.IsStale.ShouldBeNull();
+        eventStoreMetadata.IsDegraded.ShouldBeNull();
+
+        using var rawRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/tenants/{tenantId}");
+        rawRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
+        rawRequest.Headers.IfNoneMatch.ParseAdd("\"conflicting-validator\"");
+        using HttpResponseMessage rawResponse = await _fixture.TenantsApiClient.SendAsync(
+            rawRequest,
+            timeout.Token);
+        string rawContent = await rawResponse.Content.ReadAsStringAsync(timeout.Token);
+
+        rawResponse.StatusCode.ShouldBe(HttpStatusCode.OK, rawContent);
+        rawResponse.Headers.GetValues("X-Hexalith-Query-Provenance").ShouldHaveSingleItem().ShouldBe("HandlerComputed");
+        rawResponse.Headers.ETag.ShouldBeNull();
+        rawResponse.Headers.Contains("X-Hexalith-Projection-Version").ShouldBeFalse();
+        rawResponse.Headers.Contains("X-Hexalith-Is-Stale").ShouldBeFalse();
+        rawResponse.Headers.Contains("X-Hexalith-Is-Degraded").ShouldBeFalse();
+        rawResponse.Headers.Contains(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
+
+        using JsonDocument rawDocument = JsonDocument.Parse(rawContent);
+        JsonElement rawPayload = rawDocument.RootElement;
+        TenantDetail rawTenant = rawPayload.Deserialize<TenantDetail>(WebJsonOptions).ShouldNotBeNull();
+        AssertTenantDetailMatchesPersisted(rawTenant, persisted);
+        rawPayload.TryGetProperty("metadata", out _).ShouldBeFalse();
+        rawPayload.TryGetProperty("projectionVersion", out _).ShouldBeFalse();
+        rawPayload.TryGetProperty("projectedAt", out _).ShouldBeFalse();
+
+        using HttpClient typedHttp = CreateIsolatedTenantsApiClient(_fixture.TenantsApiClient, token);
+        var client = new TenantsRestQueryClient(typedHttp);
+
+        TenantsRestQueryResponse<TenantDetail> typed = await client.GetTenantAsync(
+            new GetTenantQuery { TenantId = tenantId },
+            "conflicting-validator",
+            timeout.Token);
+
+        typed.FailureKind.ShouldBe(TenantsRestQueryFailureKind.None);
+        typed.StatusCode.ShouldBe((int)HttpStatusCode.OK);
+        AssertTenantDetailMatchesPersisted(typed.Payload.ShouldNotBeNull(), persisted);
+        typed.Metadata.Provenance.ShouldBe(QueryResponseProvenance.HandlerComputed);
+        typed.Metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+        typed.Metadata.ETag.ShouldBeNull();
+        typed.Metadata.IsNotModified.ShouldBe(false);
+        typed.Metadata.ProjectionVersion.ShouldBeNull();
+        typed.Metadata.IsStale.ShouldBeNull();
+        typed.Metadata.IsDegraded.ShouldBeNull();
+    }
+
+    private static void AssertTenantDetailMatchesPersisted(
+        TenantDetail actual,
+        TenantReadModel persisted) {
+        actual.TenantId.ShouldBe(persisted.TenantId);
+        actual.Name.ShouldBe(persisted.Name);
+        actual.Description.ShouldBe(persisted.Description);
+        actual.Status.ShouldBe(persisted.Status);
+        actual.CreatedAt.ShouldBe(persisted.CreatedAt);
+        actual.Members.Count.ShouldBe(persisted.Members.Count);
+        foreach (KeyValuePair<string, TenantRole> member in persisted.Members) {
+            actual.Members.ShouldContain(candidate =>
+                candidate.UserId == member.Key && candidate.Role == member.Value);
+        }
+
+        actual.Configuration.Count.ShouldBe(persisted.Configuration.Count);
+        foreach (KeyValuePair<string, string> setting in persisted.Configuration) {
+            actual.Configuration.TryGetValue(setting.Key, out string? value).ShouldBeTrue();
+            value.ShouldBe(setting.Value);
+        }
+    }
+
     [DaprFact]
     public async Task CommandApi_process_endpoint_dispatches_command() {
         _fixture.SkipIfUnavailable();
@@ -270,6 +438,78 @@ public class AspireTopologyTests : IDisposable {
         return await WaitForTerminalStatusAsync(client, accepted.CorrelationId, token, cancellationToken);
     }
 
+    private static async Task<TenantReadModel> WaitForPersistedTenantAsync(
+        string tenantId,
+        CancellationToken cancellationToken) {
+        string redisEndpoint = $"localhost:{DaprDiagnostics.DefaultRedisPort}";
+        string persistedKey = $"tenants||projection:tenants:{tenantId}";
+        RedisConnectionMultiplexer redis;
+        try {
+            redis = await RedisConnection.ConnectAsync(new RedisConfigurationOptions {
+                EndPoints = { redisEndpoint },
+                ConnectTimeout = 5_000,
+                SyncTimeout = 5_000,
+                AbortOnConnectFail = true,
+                AllowAdmin = false,
+            });
+        }
+        catch (RedisConnectionException ex) {
+            throw new InvalidOperationException(
+                $"Redis at '{redisEndpoint}' was unreachable while waiting for persisted tenant '{tenantId}'. {ex.Message}",
+                ex);
+        }
+
+        using (redis) {
+            RedisDatabase database = redis.GetDatabase();
+            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(SampleProjectionTimeout);
+            string? lastPayload = null;
+            string? lastJsonError = null;
+
+            while (DateTimeOffset.UtcNow <= deadline) {
+                cancellationToken.ThrowIfCancellationRequested();
+                RedisValue value;
+                try {
+                    value = await database
+                        .HashGetAsync(persistedKey, "data")
+                        .WaitAsync(cancellationToken);
+                }
+                catch (RedisConnectionException ex) {
+                    throw new InvalidOperationException(
+                        $"Redis at '{redisEndpoint}' dropped the connection while waiting for '{persistedKey}'. {ex.Message}",
+                        ex);
+                }
+
+                if (value.HasValue) {
+                    lastPayload = value.ToString();
+                    TenantReadModel? model;
+                    try {
+                        model = JsonSerializer.Deserialize<TenantReadModel>(lastPayload, WebJsonOptions);
+                        lastJsonError = null;
+                    }
+                    catch (JsonException ex) {
+                        lastJsonError = ex.Message;
+                        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
+                        continue;
+                    }
+
+                    if (model is not null
+                        && string.Equals(model.TenantId, tenantId, StringComparison.Ordinal)) {
+                        return model;
+                    }
+                }
+
+                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
+            }
+
+            string jsonSuffix = lastJsonError is null
+                ? string.Empty
+                : $" Last JSON error: {lastJsonError}.";
+            throw new TimeoutException(
+                $"Redis key '{persistedKey}' did not contain the expected tenant read model within {SampleProjectionTimeout}. "
+                + $"Last payload present: {lastPayload is not null}.{jsonSuffix}");
+        }
+    }
+
     private static async Task<CommandStatusResponse> WaitForTerminalStatusAsync(
         HttpClient client,
         string correlationId,
@@ -515,4 +755,70 @@ public class AspireTopologyTests : IDisposable {
     }
 
     private sealed record FixedUserContextAccessor(string? TenantId, string? UserId) : IUserContextAccessor;
+
+    private async Task WaitForTenantsApiAliveAsync() {
+        using var timeout = new CancellationTokenSource(TenantsApiAlivenessTimeout);
+        HttpStatusCode? lastStatus = null;
+        string? lastError = null;
+
+        while (!timeout.IsCancellationRequested) {
+            try {
+                using HttpResponseMessage response = await _fixture.TenantsApiClient.GetAsync("/alive", timeout.Token);
+                lastStatus = response.StatusCode;
+                lastError = null;
+                if (response.StatusCode == HttpStatusCode.OK) {
+                    return;
+                }
+            }
+            catch (HttpRequestException ex) {
+                lastError = ex.Message;
+            }
+            catch (TaskCanceledException) when (timeout.IsCancellationRequested) {
+                break;
+            }
+            catch (TaskCanceledException) {
+                lastError = "request timed out";
+            }
+
+            try {
+                await Task.Delay(TimeSpan.FromSeconds(2), timeout.Token);
+            }
+            catch (TaskCanceledException) {
+                break;
+            }
+        }
+
+        throw new TimeoutException(
+            $"tenants-api /alive did not return HTTP 200 within {TenantsApiAlivenessTimeout}. "
+            + $"Last status: {lastStatus?.ToString() ?? "n/a"}, Last error: {lastError ?? "n/a"}.");
+    }
+
+    private static HttpClient CreateIsolatedTenantsApiClient(HttpClient shared, string token) {
+        ArgumentNullException.ThrowIfNull(shared);
+        ArgumentException.ThrowIfNullOrWhiteSpace(token);
+
+        var client = new HttpClient(new SharedClientRelayHandler(shared), disposeHandler: true) {
+            BaseAddress = shared.BaseAddress,
+            Timeout = shared.Timeout,
+        };
+        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
+        return client;
+    }
+
+    private sealed class SharedClientRelayHandler(HttpClient inner) : HttpMessageHandler {
+        protected override Task<HttpResponseMessage> SendAsync(
+            HttpRequestMessage request,
+            CancellationToken cancellationToken) {
+            ArgumentNullException.ThrowIfNull(request);
+            var clone = new HttpRequestMessage(request.Method, request.RequestUri) {
+                Version = request.Version,
+                VersionPolicy = request.VersionPolicy,
+            };
+            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers) {
+                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
+            }
+
+            return inner.SendAsync(clone, cancellationToken);
+        }
+    }
 }
diff --git a/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs b/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs
index 396610ce..e039e0fd 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs
+++ b/tests/Hexalith.Tenants.IntegrationTests/Fixtures/AspireTopologyFixture.cs
@@ -12,8 +12,10 @@ namespace Hexalith.Tenants.IntegrationTests.Fixtures;
 /// </remarks>
 public sealed class AspireTopologyFixture : AspireTopologyFixtureBase<Projects.Hexalith_Tenants_AppHost> {
     private static readonly TimeSpan CommandApiHealthTimeout = TimeSpan.FromMinutes(4);
+    private static readonly TimeSpan TenantsApiHealthTimeout = TimeSpan.FromMinutes(4);
     private static readonly TimeSpan SampleHealthTimeout = TimeSpan.FromMinutes(2);
     private static readonly TimeSpan CommandApiClientTimeout = TimeSpan.FromSeconds(60);
+    private static readonly TimeSpan TenantsApiClientTimeout = TimeSpan.FromSeconds(60);
     private static readonly TimeSpan SampleClientTimeout = TimeSpan.FromSeconds(30);
 
     /// <inheritdoc/>
@@ -23,6 +25,10 @@ public sealed class AspireTopologyFixture : AspireTopologyFixtureBase<Projects.H
         new("tenants", "http", CommandApiClientTimeout, CommandApiHealthTimeout, WaitForAliveness: true, CommandApiHealthTimeout),
         new("tenants-ui", "http", CommandApiClientTimeout, CommandApiHealthTimeout, WaitForAliveness: false, CommandApiHealthTimeout),
         new("sample", "http", SampleClientTimeout, SampleHealthTimeout, WaitForAliveness: true, SampleHealthTimeout),
+        // HTTPS is required because tenants-api redirects HTTP. Aliveness is polled by the Story 4.7
+        // proof with that test's timeout so a slow or unhealthy generated API does not stall the
+        // shared fixture's 6-minute startup budget or the other topology tests.
+        new("tenants-api", "https", TenantsApiClientTimeout, TenantsApiHealthTimeout, WaitForAliveness: false, TenantsApiHealthTimeout),
     ];
 
     /// <inheritdoc/>
@@ -34,6 +40,9 @@ public sealed class AspireTopologyFixture : AspireTopologyFixtureBase<Projects.H
     /// <summary>Gets the HTTP client for the Tenants domain service (exposes /process endpoint).</summary>
     public HttpClient TenantsClient => Client("tenants");
 
+    /// <summary>Gets the HTTP client for the generated Tenants REST API.</summary>
+    public HttpClient TenantsApiClient => Client("tenants-api");
+
     /// <summary>Gets the HTTP client for the Tenants UI resource.</summary>
     public HttpClient TenantsUiClient => Client("tenants-ui");
 
diff --git a/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj b/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj
index c88dfa7f..4b5ef2d3 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj
+++ b/tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj
@@ -24,6 +24,7 @@
          (e.g. Projects.Hexalith_Tenants_AppHost) used by the topology fixture. -->
     <PackageReference Include="Aspire.Hosting.Testing" />
     <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
+    <PackageReference Include="StackExchange.Redis" />
   </ItemGroup>
 
   <ItemGroup>
diff --git a/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs b/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs
index c56b7c49..e364b574 100644
--- a/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs
+++ b/tests/Hexalith.Tenants.IntegrationTests/TenantsApiGeneratedControllerTests.cs
@@ -113,6 +113,52 @@ public sealed class TenantsApiGeneratedControllerTests
         payload.GetProperty("pageSize").GetInt32().ShouldBe(25);
     }
 
+    [Fact]
+    public async Task Generated_query_route_suppresses_projection_headers_for_handler_computed_result()
+    {
+        CapturingEventStoreGatewayClient gateway = new();
+        gateway.EnqueueQueryResult(
+            new TenantDetail(
+                "tenant.alpha",
+                "Alpha",
+                "Tenant Alpha",
+                TenantStatus.Active,
+                [],
+                new Dictionary<string, string>(StringComparer.Ordinal),
+                DateTimeOffset.Parse("2026-07-03T05:20:00Z", CultureInfo.InvariantCulture)),
+            eTag: "opaque-store-etag",
+            metadata: new QueryResponseMetadata(
+                ETag: "opaque-store-etag",
+                IsNotModified: false,
+                IsStale: false,
+                ProjectionVersion: "tenant-sequence:42")
+            {
+                Provenance = QueryResponseProvenance.HandlerComputed,
+                Lifecycle = ProjectionLifecycleState.Current,
+            });
+        await using var factory = new TenantsApiWebApplicationFactory(gateway);
+        using HttpClient client = CreateAuthenticatedClient(factory);
+        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenants/tenant.alpha");
+        request.Headers.IfNoneMatch.ParseAdd("\"conflicting-validator\"");
+
+        using HttpResponseMessage response = await client.SendAsync(
+            request,
+            TestContext.Current.CancellationToken);
+
+        response.StatusCode.ShouldBe(HttpStatusCode.OK);
+        response.Headers.GetValues("X-Hexalith-Query-Provenance").ShouldHaveSingleItem().ShouldBe("HandlerComputed");
+        response.Headers.ETag.ShouldBeNull();
+        response.Headers.Contains("X-Hexalith-Projection-Version").ShouldBeFalse();
+        response.Headers.Contains("X-Hexalith-Is-Stale").ShouldBeFalse();
+        response.Headers.Contains(ProjectionLifecyclePolicy.HeaderName).ShouldBeFalse();
+
+        TenantDetail? detail = await response.Content.ReadFromJsonAsync<TenantDetail>(
+            JsonOptions,
+            TestContext.Current.CancellationToken);
+        detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
+        gateway.SubmittedQueries.ShouldHaveSingleItem().IfNoneMatch.ShouldBe("\"conflicting-validator\"");
+    }
+
     [Fact]
     public async Task UserTenants_generated_absolute_route_submits_index_query_for_target_user_entity()
     {
diff --git a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs
index 583fb2e5..c85dd181 100644
--- a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs
+++ b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryFreshnessTests.cs
@@ -21,39 +21,100 @@ using Shouldly;
 
 namespace Hexalith.Tenants.Server.Tests.Queries;
 
-public sealed class TenantQueryFreshnessTests {
+public sealed class TenantQueryFreshnessTests
+{
     private static readonly DateTimeOffset Now = new(2026, 6, 25, 13, 0, 0, TimeSpan.Zero);
-    private static readonly ReadModelFreshnessOptions Thresholds = new() {
+    private const string GenuineSequenceVersion = TenantProjectionVersionFormat.SequencePrefix + "42";
+    private static readonly ReadModelFreshnessOptions Thresholds = new()
+    {
         Aging = TimeSpan.FromMinutes(10),
         Stale = TimeSpan.FromMinutes(30),
     };
 
+    public static IEnumerable<object?[]> HandlerFreshnessCases()
+    {
+        (string QueryType, string PrimaryKey, string ETag)[] routes =
+        [
+            (ListTenantsQuery.QueryType, TenantQueryHandlerBase.TenantIndexProjectionKey, "index-etag-1"),
+            (GetUserTenantsQuery.QueryType, TenantQueryHandlerBase.TenantIndexProjectionKey, "index-etag-1"),
+            (GetTenantQuery.QueryType, TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha", "tenant-etag-1"),
+            (GetTenantUsersQuery.QueryType, TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha", "tenant-etag-1"),
+            (GetTenantAuditQuery.QueryType, TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha", "audit-etag-1"),
+            (GetGlobalAdministratorsQuery.QueryType, TenantQueryHandlerBase.GlobalAdminProjectionKey, "admin-etag-1"),
+        ];
+
+        int?[] projectedAges = [5, 40, null];
+        foreach ((string queryType, string primaryKey, string eTag) in routes)
+        {
+            foreach (int? projectedAge in projectedAges)
+            {
+                yield return [queryType, primaryKey, eTag, projectedAge];
+            }
+        }
+    }
+
     [Theory]
-    [InlineData(5, false)]
-    [InlineData(20, false)]
-    [InlineData(40, true)]
-    public async Task Get_tenant_classifies_projected_at_age_server_sideAsync(int projectedAgeMinutes, bool expectedIsStale) {
+    [MemberData(nameof(HandlerFreshnessCases))]
+    public async Task Query_handlers_ignore_primary_read_model_timestamp_and_sequence_authorityAsync(
+        string queryType,
+        string expectedPrimaryKey,
+        string expectedETag,
+        int? projectedAgeMinutes)
+    {
+        DateTimeOffset? primaryProjectedAt = projectedAgeMinutes.HasValue
+            ? Now - TimeSpan.FromMinutes(projectedAgeMinutes.Value)
+            : null;
         IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(store, "tenant-etag-1", Now - TimeSpan.FromMinutes(projectedAgeMinutes));
-        SetupGlobalAdministrators(store, "admin-user");
+        TenantIndexReadModel index = SetupTenantIndex(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? expectedETag : "index-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? primaryProjectedAt : Now);
+        TenantReadModel tenant = SetupTenant(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "tenant-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? primaryProjectedAt : Now);
+        TenantAuditReadModel audit = SetupTenantAudit(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "audit-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? primaryProjectedAt : Now);
+        GlobalAdministratorReadModel administrators = SetupGlobalAdministrators(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey ? expectedETag : "admin-etag-2",
+            expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey ? primaryProjectedAt : Now,
+            "admin-user");
+
+        AssertPrimaryReadModelInputsExist(
+            expectedPrimaryKey,
+            primaryProjectedAt,
+            index,
+            tenant,
+            audit,
+            administrators);
 
         TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
             store,
             CreateCursorCodec(),
-            CreateEnvelope(GetTenantQuery.QueryType),
+            CreateEnvelope(queryType),
             freshnessOptions: Thresholds,
             timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
 
-        result.Metadata.ShouldNotBeNull().IsStale.ShouldBe(expectedIsStale);
-        result.Metadata.ServedAt.ShouldBe(Now);
-        result.Metadata.ProjectionVersion.ShouldBe("tenant-etag-1");
+        await AssertPrimaryReadModelWasReadAsync(store, expectedPrimaryKey);
+        AssertValidatorOnly(result, expectedETag);
     }
 
-    [Fact]
-    public async Task Get_tenant_without_projected_at_reports_unknown_freshnessAsync() {
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData("   ")]
+    [InlineData("\"\"")]
+    [InlineData("\"")]
+    [InlineData("\"\"\"")]
+    [InlineData("  \" \"  ")]
+    public async Task Query_handler_omits_metadata_for_degenerate_etagAsync(string? eTag)
+    {
         IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(store, "tenant-etag-1", projectedAt: null);
-        SetupGlobalAdministrators(store, "admin-user");
+        SetupTenant(store, eTag, Now - TimeSpan.FromMinutes(40));
+        SetupGlobalAdministrators(store, "admin-etag", Now, "admin-user");
 
         TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
             store,
@@ -62,83 +123,102 @@ public sealed class TenantQueryFreshnessTests {
             freshnessOptions: Thresholds,
             timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
 
-        result.Metadata.ShouldNotBeNull().IsStale.ShouldBeNull();
-        result.Metadata.ServedAt.ShouldBe(Now);
+        result.Metadata.ShouldBeNull();
     }
 
-    [Fact]
-    public async Task Get_tenant_with_projected_at_and_no_etag_still_classifies_freshnessAsync() {
-        IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(store, " ", Now - TimeSpan.FromMinutes(40));
-        SetupGlobalAdministrators(store, "admin-user");
+    private static void AssertValidatorOnly(TenantQueryResult result, string expectedETag)
+    {
+        result.Success.ShouldBeTrue();
+        QueryResponseMetadata metadata = result.Metadata.ShouldNotBeNull();
+        metadata.ETag.ShouldBe(expectedETag);
+        metadata.IsNotModified.ShouldBe(false);
+        metadata.ProjectionVersion.ShouldBeNull();
+        metadata.IsStale.ShouldBeNull();
+        metadata.IsDegraded.ShouldBeNull();
+        metadata.ServedAt.ShouldBeNull();
+        metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
+        metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+    }
 
-        TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
-            store,
-            CreateCursorCodec(),
-            CreateEnvelope(GetTenantQuery.QueryType),
-            freshnessOptions: Thresholds,
-            timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
+    private static void AssertPrimaryReadModelInputsExist(
+        string expectedPrimaryKey,
+        DateTimeOffset? primaryProjectedAt,
+        TenantIndexReadModel index,
+        TenantReadModel tenant,
+        TenantAuditReadModel audit,
+        GlobalAdministratorReadModel administrators)
+    {
+        IReadModelFreshness freshness;
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey)
+        {
+            freshness = index;
+        }
+        else if (expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha")
+        {
+            freshness = tenant;
+        }
+        else if (expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha")
+        {
+            freshness = audit;
+        }
+        else if (expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey)
+        {
+            freshness = administrators;
+        }
+        else
+        {
+            throw new ArgumentOutOfRangeException(nameof(expectedPrimaryKey), expectedPrimaryKey, "Unsupported primary key.");
+        }
 
-        result.Metadata.ShouldNotBeNull().ETag.ShouldBeNull();
-        result.Metadata.IsStale.ShouldBe(true);
-        result.Metadata.ServedAt.ShouldBe(Now);
-        result.Metadata.ProjectionVersion.ShouldBeNull();
+        freshness.ProjectedAt.ShouldBe(primaryProjectedAt);
+        freshness.ProjectionVersion.ShouldBe(GenuineSequenceVersion);
     }
 
-    [Fact]
-    public async Task Get_tenant_prefers_persisted_projection_version_over_etagAsync() {
-        IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenant(
-            store,
-            "opaque-store-etag",
-            Now - TimeSpan.FromMinutes(5),
-            projectionVersion: TenantProjectionVersionFormat.SequencePrefix + "10");
-        SetupGlobalAdministrators(store, "admin-user");
-
-        TenantQueryResult result = (await TenantQueryTestHarness.ExecuteAsync(
-            store,
-            CreateCursorCodec(),
-            CreateEnvelope(GetTenantQuery.QueryType),
-            freshnessOptions: Thresholds,
-            timeProvider: new FixedTimeProvider(Now))).ShouldBeOfType<TenantQueryResult>();
+    private static async Task AssertPrimaryReadModelWasReadAsync(IReadModelStore store, string expectedPrimaryKey)
+    {
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey)
+        {
+            _ = await store.Received().GetAsync<TenantIndexReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-        result.Metadata.ShouldNotBeNull().ETag.ShouldBe("opaque-store-etag");
-        result.Metadata.ProjectionVersion.ShouldBe(TenantProjectionVersionFormat.SequencePrefix + "10");
-    }
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha")
+        {
+            _ = await store.Received().GetAsync<TenantReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-    [Theory]
-    [InlineData("list-tenants", "projection:tenant-index:singleton", "index-etag-1", false)]
-    [InlineData("get-user-tenants", "projection:tenant-index:singleton", "index-etag-1", false)]
-    [InlineData("get-tenant", "projection:tenants:tenant.alpha", "tenant-etag-1", false)]
-    [InlineData("get-tenant-users", "projection:tenants:tenant.alpha", "tenant-etag-1", false)]
-    [InlineData("get-tenant-audit", "audit:tenant.alpha", "audit-etag-1", true)]
-    [InlineData("get-global-administrators", "projection:global-administrators:singleton", "admin-etag-1", false)]
-    public async Task Query_handlers_classify_from_primary_read_model_projected_atAsync(
-        string queryType,
-        string expectedPrimaryKey,
-        string expectedETag,
-        bool expectedIsStale) {
-        IReadModelStore store = Substitute.For<IReadModelStore>();
-        SetupTenantIndex(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? Now.AddMinutes(-5) : Now.AddMinutes(-40));
-        SetupTenant(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "tenant-etag-2", Now.AddMinutes(-5));
-        SetupTenantAudit(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? Now.AddMinutes(-40) : Now.AddMinutes(-5));
-        SetupGlobalAdministrators(store, "admin-user", projectedAt: Now.AddMinutes(-5));
+        if (expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha")
+        {
+            _ = await store.Received().GetAsync<TenantAuditReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-        QueryResult result = await TenantQueryTestHarness.ExecuteAsync(
-            store,
-            CreateCursorCodec(),
-            CreateEnvelope(queryType),
-            freshnessOptions: Thresholds,
-            timeProvider: new FixedTimeProvider(Now));
+        if (expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey)
+        {
+            _ = await store.Received().GetAsync<GlobalAdministratorReadModel>(
+                TenantQueryHandlerBase.StateStoreName,
+                expectedPrimaryKey,
+                Arg.Any<CancellationToken>());
+            return;
+        }
 
-        TenantQueryResult tenantResult = result.ShouldBeOfType<TenantQueryResult>();
-        tenantResult.Metadata.ShouldNotBeNull().ETag.ShouldBe(expectedETag);
-        tenantResult.Metadata.IsStale.ShouldBe(expectedIsStale);
-        tenantResult.Metadata.ServedAt.ShouldBe(Now);
+        throw new ArgumentOutOfRangeException(nameof(expectedPrimaryKey), expectedPrimaryKey, "Unsupported primary key.");
     }
 
-    private static QueryEnvelope CreateEnvelope(string queryType) {
-        if (string.Equals(queryType, ListTenantsQuery.QueryType, StringComparison.Ordinal)) {
+    private static QueryEnvelope CreateEnvelope(string queryType)
+    {
+        if (string.Equals(queryType, ListTenantsQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 ListTenantsQuery.Domain,
@@ -150,7 +230,8 @@ public sealed class TenantQueryFreshnessTests {
                 "admin-user");
         }
 
-        if (string.Equals(queryType, GetUserTenantsQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetUserTenantsQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetUserTenantsQuery.Domain,
@@ -162,7 +243,8 @@ public sealed class TenantQueryFreshnessTests {
                 "target-user");
         }
 
-        if (string.Equals(queryType, GetTenantQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetTenantQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetTenantQuery.Domain,
@@ -174,7 +256,8 @@ public sealed class TenantQueryFreshnessTests {
                 "tenant.alpha");
         }
 
-        if (string.Equals(queryType, GetTenantUsersQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetTenantUsersQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetTenantUsersQuery.Domain,
@@ -186,7 +269,8 @@ public sealed class TenantQueryFreshnessTests {
                 "tenant.alpha");
         }
 
-        if (string.Equals(queryType, GetTenantAuditQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetTenantAuditQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetTenantAuditQuery.Domain,
@@ -198,7 +282,8 @@ public sealed class TenantQueryFreshnessTests {
                 "tenant.alpha");
         }
 
-        if (string.Equals(queryType, GetGlobalAdministratorsQuery.QueryType, StringComparison.Ordinal)) {
+        if (string.Equals(queryType, GetGlobalAdministratorsQuery.QueryType, StringComparison.Ordinal))
+        {
             return new QueryEnvelope(
                 TenantIdentity.DefaultTenantId,
                 GetGlobalAdministratorsQuery.Domain,
@@ -217,30 +302,44 @@ public sealed class TenantQueryFreshnessTests {
     private static IQueryCursorCodec CreateCursorCodec()
         => new QueryCursorCodec(new EphemeralDataProtectionProvider(), "Hexalith.Tenants.QueryCursor.v1");
 
-    private static void SetupGlobalAdministrators(
+    private static GlobalAdministratorReadModel SetupGlobalAdministrators(
         IReadModelStore store,
-        string administratorId,
-        DateTimeOffset? projectedAt = null) {
-        var model = new GlobalAdministratorReadModel {
-            Administrators = [administratorId],
+        string eTag,
+        DateTimeOffset? projectedAt,
+        params string[] administratorIds)
+    {
+        var model = new GlobalAdministratorReadModel
+        {
+            Administrators = administratorIds.ToHashSet(StringComparer.Ordinal),
             ProjectedAt = projectedAt,
+            ProjectionVersion = GenuineSequenceVersion,
         };
 
         _ = store.GetAsync<GlobalAdministratorReadModel>(
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.GlobalAdminProjectionKey,
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, "admin-etag-1")));
+            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, eTag)));
+        return model;
     }
 
-    private static void SetupTenantIndex(IReadModelStore store, DateTimeOffset? projectedAt) {
-        var model = new TenantIndexReadModel {
+    private static TenantIndexReadModel SetupTenantIndex(
+        IReadModelStore store,
+        string eTag,
+        DateTimeOffset? projectedAt)
+    {
+        var model = new TenantIndexReadModel
+        {
             ProjectedAt = projectedAt,
-            Tenants = {
+            ProjectionVersion = GenuineSequenceVersion,
+            Tenants =
+            {
                 ["tenant.alpha"] = new TenantIndexEntry("Tenant Alpha", TenantStatus.Active),
             },
-            UserTenants = {
-                ["target-user"] = new Dictionary<string, TenantRole>(StringComparer.Ordinal) {
+            UserTenants =
+            {
+                ["target-user"] = new Dictionary<string, TenantRole>(StringComparer.Ordinal)
+                {
                     ["tenant.alpha"] = TenantRole.TenantReader,
                 },
                 ["admin-user"] = new Dictionary<string, TenantRole>(StringComparer.Ordinal),
@@ -251,22 +350,25 @@ public sealed class TenantQueryFreshnessTests {
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.TenantIndexProjectionKey,
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<TenantIndexReadModel>(model, "index-etag-1")));
+            .Returns(Task.FromResult(new ReadModelEntry<TenantIndexReadModel>(model, eTag)));
+        return model;
     }
 
-    private static void SetupTenant(
+    private static TenantReadModel SetupTenant(
         IReadModelStore store,
-        string eTag,
-        DateTimeOffset? projectedAt,
-        string? projectionVersion = null) {
-        var model = new TenantReadModel {
+        string? eTag,
+        DateTimeOffset? projectedAt)
+    {
+        var model = new TenantReadModel
+        {
             TenantId = "tenant.alpha",
             Name = "Tenant Alpha",
             Status = TenantStatus.Active,
             CreatedAt = DateTimeOffset.Parse("2026-06-07T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
             ProjectedAt = projectedAt,
-            ProjectionVersion = projectionVersion,
-            Members = {
+            ProjectionVersion = GenuineSequenceVersion,
+            Members =
+            {
                 ["test-user"] = TenantRole.TenantReader,
             },
         };
@@ -276,12 +378,20 @@ public sealed class TenantQueryFreshnessTests {
                 TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha",
                 Arg.Any<CancellationToken>())
             .Returns(Task.FromResult(new ReadModelEntry<TenantReadModel>(model, eTag)));
+        return model;
     }
 
-    private static void SetupTenantAudit(IReadModelStore store, DateTimeOffset? projectedAt) {
-        var model = new TenantAuditReadModel {
+    private static TenantAuditReadModel SetupTenantAudit(
+        IReadModelStore store,
+        string eTag,
+        DateTimeOffset? projectedAt)
+    {
+        var model = new TenantAuditReadModel
+        {
             ProjectedAt = projectedAt,
-            Entries = [
+            ProjectionVersion = GenuineSequenceVersion,
+            Entries =
+            [
                 new TenantAuditEntry(
                     "event-1",
                     "TenantCreated",
@@ -297,10 +407,12 @@ public sealed class TenantQueryFreshnessTests {
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha",
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<TenantAuditReadModel>(model, "audit-etag-1")));
+            .Returns(Task.FromResult(new ReadModelEntry<TenantAuditReadModel>(model, eTag)));
+        return model;
     }
 
-    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider {
+    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
+    {
         public override DateTimeOffset GetUtcNow() => utcNow;
     }
 }
diff --git a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs
index d8836ac2..0f2fd115 100644
--- a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs
+++ b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryHandlerETagTests.cs
@@ -29,7 +29,8 @@ public sealed class TenantQueryHandlerETagTests
     [InlineData("get-tenant", "projection:tenants:tenant.alpha", "tenant-etag-1")]
     [InlineData("get-tenant-users", "projection:tenants:tenant.alpha", "tenant-etag-2")]
     [InlineData("get-tenant-audit", "audit:tenant.alpha", "audit-etag-1")]
-    public async Task Query_handlers_surface_primary_read_model_etag_as_projection_version(
+    [InlineData("get-global-administrators", "projection:global-administrators:singleton", "admin-etag-1")]
+    public async Task Query_handlers_surface_primary_read_model_etag_only_as_opaque_validator(
         string queryType,
         string expectedPrimaryKey,
         string expectedETag)
@@ -38,7 +39,10 @@ public sealed class TenantQueryHandlerETagTests
         SetupTenantIndex(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantIndexProjectionKey ? expectedETag : "index-etag");
         SetupTenant(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "tenant-etag");
         SetupTenantAudit(store, expectedPrimaryKey == TenantQueryHandlerBase.TenantAuditProjectionKeyPrefix + "tenant.alpha" ? expectedETag : "audit-etag");
-        SetupGlobalAdministrators(store, "admin-user");
+        SetupGlobalAdministrators(
+            store,
+            expectedPrimaryKey == TenantQueryHandlerBase.GlobalAdminProjectionKey ? expectedETag : "admin-etag",
+            "admin-user");
 
         QueryResult result = await TenantQueryTestHarness.ExecuteAsync(
             store,
@@ -48,10 +52,15 @@ public sealed class TenantQueryHandlerETagTests
 
         result.Success.ShouldBeTrue();
         TenantQueryResult tenantResult = result.ShouldBeOfType<TenantQueryResult>();
-        tenantResult.Metadata.ShouldNotBeNull().ETag.ShouldBe(expectedETag);
-        tenantResult.Metadata.ProjectionVersion.ShouldBe(expectedETag);
-        tenantResult.Metadata.IsStale.ShouldBe(false);
-        tenantResult.Metadata.ServedAt.ShouldBe(Now);
+        QueryResponseMetadata metadata = tenantResult.Metadata.ShouldNotBeNull();
+        metadata.ETag.ShouldBe(expectedETag);
+        metadata.IsNotModified.ShouldBe(false);
+        metadata.ProjectionVersion.ShouldBeNull();
+        metadata.IsStale.ShouldBeNull();
+        metadata.IsDegraded.ShouldBeNull();
+        metadata.ServedAt.ShouldBeNull();
+        metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
+        metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
     }
 
     private static QueryEnvelope CreateEnvelope(string queryType)
@@ -121,13 +130,30 @@ public sealed class TenantQueryHandlerETagTests
                 "tenant.alpha");
         }
 
+        if (string.Equals(queryType, GetGlobalAdministratorsQuery.QueryType, StringComparison.Ordinal))
+        {
+            return new QueryEnvelope(
+                TenantIdentity.DefaultTenantId,
+                GetGlobalAdministratorsQuery.Domain,
+                TenantIdentity.GlobalAdministratorsAggregateId,
+                GetGlobalAdministratorsQuery.QueryType,
+                JsonSerializer.SerializeToUtf8Bytes(new { cursor = (string?)null, pageSize = 20 }),
+                "correlation-1",
+                "admin-user",
+                TenantIdentity.GlobalAdministratorsAggregateId,
+                isGlobalAdmin: true);
+        }
+
         throw new ArgumentOutOfRangeException(nameof(queryType), queryType, "Unsupported query type.");
     }
 
     private static IQueryCursorCodec CreateCursorCodec()
         => new QueryCursorCodec(new EphemeralDataProtectionProvider(), "Hexalith.Tenants.QueryCursor.v1");
 
-    private static void SetupGlobalAdministrators(IReadModelStore store, params string[] administratorIds)
+    private static void SetupGlobalAdministrators(
+        IReadModelStore store,
+        string eTag,
+        params string[] administratorIds)
     {
         var model = new GlobalAdministratorReadModel
         {
@@ -139,7 +165,7 @@ public sealed class TenantQueryHandlerETagTests
                 TenantQueryHandlerBase.StateStoreName,
                 TenantQueryHandlerBase.GlobalAdminProjectionKey,
                 Arg.Any<CancellationToken>())
-            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, "admin-etag")));
+            .Returns(Task.FromResult(new ReadModelEntry<GlobalAdministratorReadModel>(model, eTag)));
     }
 
     private static void SetupTenantIndex(IReadModelStore store, string eTag)
diff --git a/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryResultTests.cs b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryResultTests.cs
new file mode 100644
index 00000000..4924ed77
--- /dev/null
+++ b/tests/Hexalith.Tenants.Server.Tests/Queries/TenantQueryResultTests.cs
@@ -0,0 +1,150 @@
+using System.Text.Json;
+
+using Hexalith.EventStore.Client.Projections;
+using Hexalith.EventStore.Contracts.Queries;
+using Hexalith.Tenants.Contracts.Projections;
+using Hexalith.Tenants.Queries;
+using Hexalith.Tenants.Server.Projections;
+
+using Shouldly;
+
+namespace Hexalith.Tenants.Server.Tests.Queries;
+
+public sealed class TenantQueryResultTests
+{
+    private static readonly JsonElement Payload = JsonSerializer.SerializeToElement(new { tenantId = "tenant.alpha" });
+    private static readonly ReadModelFreshnessThresholds Thresholds = new(
+        TimeSpan.FromMinutes(10),
+        TimeSpan.FromMinutes(30));
+
+    [Theory]
+    [InlineData("opaque-etag", "opaque-etag")]
+    [InlineData("  opaque-etag  ", "opaque-etag")]
+    [InlineData("\"opaque-etag\"", "opaque-etag")]
+    [InlineData("  \"opaque-etag\"  ", "opaque-etag")]
+    [InlineData("W/\"abc\"", "W/\"abc\"")]
+    public void Validator_only_factory_normalizes_opaque_etag(
+        string eTag,
+        string expectedETag)
+    {
+        TenantQueryResult result = TenantQueryResult.FromPayload(Payload, "tenants", eTag);
+
+        AssertValidatorOnly(result, expectedETag);
+    }
+
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData("   ")]
+    [InlineData("\"\"")]
+    [InlineData("\"")]
+    [InlineData("\"\"\"")]
+    [InlineData("  \" \"  ")]
+    public void Validator_only_factory_omits_metadata_for_degenerate_etag(string? eTag)
+    {
+        TenantQueryResult result = TenantQueryResult.FromPayload(Payload, "tenants", eTag);
+
+        result.Metadata.ShouldBeNull();
+    }
+
+    [Theory]
+    [InlineData("2026-06-25T13:00:00Z", TenantProjectionVersionFormat.SequencePrefix + "42")]
+    [InlineData("2026-06-25T12:00:00Z", null)]
+    [InlineData(null, TenantProjectionVersionFormat.SequencePrefix + "42")]
+    public void Freshness_overload_ignores_timestamp_and_sequence_authority(
+        string? projectedAt,
+        string? projectionVersion)
+    {
+        var readModel = new TenantReadModel
+        {
+            TenantId = "tenant.alpha",
+            ProjectedAt = projectedAt is null ? null : DateTimeOffset.Parse(projectedAt, System.Globalization.CultureInfo.InvariantCulture),
+            ProjectionVersion = projectionVersion,
+        };
+
+        TenantQueryResult result = TenantQueryResult.FromPayload(
+            Payload,
+            "tenants",
+            readModel,
+            Thresholds,
+            DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            "\"opaque-store-etag\"");
+
+        AssertValidatorOnly(result, "opaque-store-etag");
+    }
+
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData("   ")]
+    [InlineData("\"\"")]
+    [InlineData("\"")]
+    [InlineData("\"\"\"")]
+    [InlineData("  \" \"  ")]
+    public void Freshness_overload_omits_metadata_for_degenerate_etag(string? eTag)
+    {
+        var readModel = new TenantReadModel
+        {
+            TenantId = "tenant.alpha",
+            ProjectedAt = DateTimeOffset.Parse("2026-06-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            ProjectionVersion = TenantProjectionVersionFormat.SequencePrefix + "42",
+        };
+
+        TenantQueryResult result = TenantQueryResult.FromPayload(
+            Payload,
+            "tenants",
+            readModel,
+            Thresholds,
+            DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            eTag);
+
+        result.Metadata.ShouldBeNull();
+    }
+
+    [Fact]
+    public void Validator_only_factory_rejects_undefined_payload()
+    {
+        ArgumentException exception = Should.Throw<ArgumentException>(
+            () => TenantQueryResult.FromPayload(default, "tenants", "opaque-etag"));
+
+        exception.ParamName.ShouldBe("payload");
+        exception.Message.ShouldContain("Undefined");
+    }
+
+    [Fact]
+    public void Freshness_overload_rejects_undefined_payload()
+    {
+        var readModel = new TenantReadModel
+        {
+            TenantId = "tenant.alpha",
+            ProjectedAt = DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+            ProjectionVersion = TenantProjectionVersionFormat.SequencePrefix + "42",
+        };
+
+        ArgumentException exception = Should.Throw<ArgumentException>(
+            () => TenantQueryResult.FromPayload(
+                default,
+                "tenants",
+                readModel,
+                Thresholds,
+                DateTimeOffset.Parse("2026-06-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
+                "opaque-etag"));
+
+        exception.ParamName.ShouldBe("payload");
+        exception.Message.ShouldContain("Undefined");
+    }
+
+    private static void AssertValidatorOnly(TenantQueryResult result, string expectedETag)
+    {
+        result.Success.ShouldBeTrue();
+        QueryResponseMetadata metadata = result.Metadata.ShouldNotBeNull();
+        metadata.ETag.ShouldBe(expectedETag);
+        metadata.IsNotModified.ShouldBe(false);
+        metadata.ProjectionVersion.ShouldBeNull();
+        metadata.IsStale.ShouldBeNull();
+        metadata.IsDegraded.ShouldBeNull();
+        metadata.ServedAt.ShouldBeNull();
+        metadata.Provenance.ShouldBe(QueryResponseProvenance.Unknown);
+        metadata.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
+    }
+}
diff --git a/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs b/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs
index 96d87a76..1107b599 100644
--- a/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs
+++ b/tests/Hexalith.Tenants.UI.Tests/State/TenantLifecycleCommandSnapshotTests.cs
@@ -329,9 +329,9 @@ public sealed class TenantLifecycleCommandSnapshotTests
     [Fact]
     public void Opaque_tokens_sharing_a_prefix_and_ending_in_increasing_digits_do_not_confirm()
     {
-        // TenantQueryResult falls back to the state-store ETag when no read model carries a projection
-        // version. Two such markers can share a textual prefix and end in increasing digits without those
-        // digits expressing causal order, so only the aggregate sequence token may satisfy the ordered gate.
+        // Opaque validators are not projection versions. Two such markers can share a textual prefix and end
+        // in increasing digits without those digits expressing causal order, so only an aggregate sequence
+        // token supplied by an authoritative projection-backed route may satisfy the ordered gate.
         TenantLifecycleCommandSnapshot pending = Pending(
             TenantLifecycleOperation.DisableTenant,
             TenantStatus.Active,
diff --git a/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs b/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs
index 8c64d0e6..d521aa8d 100644
--- a/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs
+++ b/tests/Hexalith.Tenants.UI.Tests/State/TenantMembershipCommandProvenanceTests.cs
@@ -9,11 +9,10 @@ namespace Hexalith.Tenants.UI.Tests.State;
 /// Pins the projection-version contract membership confirmation depends on.
 /// </summary>
 /// <remarks>
-/// Confirmation requires an ordered advancement of the value the query path publishes as
-/// <c>ProjectionVersion</c>. The tenant projection publishes the aggregate-local EventStore sequence
-/// as <c>tenant-sequence:&lt;n&gt;</c>; <c>TenantQueryResult</c> falls back to the state-store ETag only for
-/// legacy read models that do not yet carry that value. A legacy store whose ETag is a hash, GUID, or
-/// other token without a stable prefix and trailing number does not satisfy the ordered contract.
+/// Confirmation requires an ordered advancement of an authoritative <c>ProjectionVersion</c>. The tenant
+/// projection persists the aggregate-local EventStore sequence as <c>tenant-sequence:&lt;n&gt;</c>, while
+/// handler-computed query responses publish no projection version and never substitute the state-store
+/// ETag. Defensive comparison of legacy tokens remains fail-closed for hashes, GUIDs, and other opaque values.
 /// </remarks>
 public sealed class TenantMembershipCommandProvenanceTests
 {


CLAIMS FILE CONTENT — DO NOT INSPECT UNTIL STEP 5:

2fac18396ff11a4459de053b3ebb7ddfe7c13e30
fix: update Keycloak realm configuration to use environment variables for admin user credentials

---
54fc4040dc6348e5560fffc246a27e72dc6558fe
fix: enhance eTag handling to omit metadata for additional degenerate cases

---
e7f366623733abdb64f173e9843edc7fc7d22193
chore: update status of global administrator and tenant audit trail stories to in-progress; update submodule references for Hexalith.EventStore, Hexalith.FrontComposer, and Hexalith.Memories

---
d2c03ca12e952ce99b729e2b40140cf2f35280ff
fix(badge): update status and baseline revision for accessible names removal

---
2d3d5f0f4042f1271debba054985cfc24a0c39d7
test(ui): cover disappeared-preview fallback on last-administrator remove end sentinel
Queued wrap after preview close must land on the lifecycle region so keyboard users are not stranded when the last-administrator remove dialog has already left the page.

Co-authored-by: Cursor <cursoragent@cursor.com>

---
e739c6970d0860262fc3dfe7e48a290f5d6bc9f0
fix(ui): remove duplicate badge accessible names

---
f75cdacc8eca458778c7109fd3f713f8907bed02
chore: update submodule references for Hexalith.EventStore and Hexalith.Memories

---
11d375db90917538a5adc152d1a3403eefe59529
chore: update status of global administrator removal story to in-progress

---
610b926a372040278ae987faa318b89290bee953
chore(sweep): record deferred-work decisions

---
453df88d9c4b738f0413d5675f682c316351f3d8
chore(decisions): pre-answer DW-352

---
182d72eebbe8b60f7adfabaf72ae4e1247efffca
chore: update submodule references for Hexalith.Builds and Hexalith.EventStore

---
bbead80a6fb2823aaa5df566c32456d85b73d325
chore(sweep): close resolved deferred-work entries

---
13afc5de1ce082ec63d4a9c028a4f446110aa35a
chore(sweep): migrate legacy deferred-work entries to DW format

---
8f2fbc9cef22e4cab363d3dbfc9db1219ce938c6
chore: update submodule reference for Hexalith.FrontComposer

---
19a0f105b72c807bf87fda0eb16d8d82189b40a9
refactor: update baseline revision for tenant workspace state identifiers

---
8175a5a407e02c0ca3bd66cc06541a1e984bd106
chore(decisions): pre-answer DW-337

---
b5a9b4111fb513e5e543f461aabb049fab2f99b3
feat(ui): close last-administrator removal after tracked confirmation
Keep the last-administrator hard stop, retained command identity, and
causal proof through retry preflight and dialog wrap so operators cannot
dispatch the final administrator or lose keyboard recovery after close.

Co-authored-by: Cursor <cursoragent@cursor.com>

---
511c56c65e4440bcfd310429163b32ab64456eac
fix: normalize eTag handling and improve tenant query tests

---
46a2f04b460bc4f22665a350472b8d55cb6e7bf3
feat: enhance global administrator removal process with improved preflight handling and UI updates
- Added CSS rules to hide mutation initiation elements on narrow viewports.
- Updated `GlobalAdministratorRemoveCommandSnapshot` to clarify ambiguous preflight retention.
- Implemented `RetainAmbiguousPreflight` method in `GlobalAdministratorCorrectionSnapshot` to maintain state during retries.
- Expanded unit tests for `GlobalAdministratorCorrectionPanel` to cover new scenarios for ambiguous submissions and viewport changes.
- Enhanced `GlobalAdministratorsPage` tests to ensure correct behavior for correlationless removal notifications and failed delivery handling.
- Added new localization keys for improved user feedback during removal processes.

---
5d6601edf357772c80f5c02c487742eace0739e2
build: sync local changes via /pushall

---
5930c8e06f8a9a1e75959bdfcde455fb0c4db599
fix: update submodule references for Hexalith.Builds, Hexalith.EventStore, Hexalith.FrontComposer, and Hexalith.Memories

---
5e23e12c5cd9ffd6efc957eb9ab339466c9eddf8
feat: update global administrator removal spec and sprint status
- Changed status of the global administrator removal spec to 'in-progress' and added review findings.
- Updated tenant workspace state constants spec status to 'ready-for-dev' and revised boundaries and constraints.
- Modified sprint status to reflect the current state of epics and stories, marking several as 'in-progress' and 'review'.

---
6e52036edbecffdde147fbaff4e53ae84c57e5da
refactor(ui): centralize tenant workspace state identifiers

---
b7d3619ec285147f3eb8057471984ee4562409e6
fix: update Aspire.AppHost.Sdk version to 13.5.3 and enable CLI bundle usage

---
7dc9482f517de8bccd6619311c4914a29923275d
fix: update submodule references for Hexalith.Builds and Hexalith.EventStore

---
510eae0544d146cad21dddf79e9a8064230ef19e
chore(sweep): record deferred-work decisions

---
d914b13bd9b2354dbb6b556c9887664f68672069
fix: update submodule references for Hexalith.EventStore and Hexalith.Memories

---
281e3c3c3d2ce13a5282383216c104914a724390
feat(audit): enhance tenant audit functionality and validation
- Updated TenantQueryCursorCodecTests to reflect changes in tenant audit scope, ensuring user context is correctly handled.
- Added tests to reject tampered cursors and ensure proper error handling for different user scopes.
- Enhanced AuditDataGridCorrectionTests to include viewport measurement and correction safety checks.
- Improved TenantAuditPageTests with additional validation for date filters and user role changes.
- Implemented comprehensive validation in TenantQueryGatewayTests for tenant audit requests, ensuring robust error handling for invalid filters and event references.

---
0ca32a5cf6448f35b67f29f0ddcbce44d144b05e
fix: update submodule reference for Hexalith.FrontComposer

---
6031670ac8ee271ca320894f0e0268fc7b4d04f7
chore(sweep): migrate legacy deferred-work entries to DW format

---
b4fdda4583b22b13ab469d0f7e3966aa4c55d92f
fix: update submodule references for Hexalith.EventStore and Hexalith.Memories

---
df6414606bcb43d4d94978fbe1d8ef31c8e3f6ca
fix: update global administrator removal status to review

---
51f762be5b6dda51c52747ceb772a88a64df2df0
feat(ui): complete last-administrator removal with causal confirmation
Retain an ambiguous UnableToVerify identity through retry preflight, contain renderer disposal on status and projection paths, and prove timeout reconstruction plus held-refresh adoption.

Co-authored-by: Cursor <cursoragent@cursor.com>

---
6eb579fa9425e50292ddd1c7e3eb1cd7d0a83a74
ci: remove unsupported publication-authority input
Co-authored-by: Cursor <cursoragent@cursor.com>

---
5b595d1df4103a019b84556db555b60c208dbdf5
build(deps): pin Memories packages to 2.25.0
Co-authored-by: Cursor <cursoragent@cursor.com>

---
0d3384b64577c3472f52af1f41492720a7ed4ff7
fix: update global administrator removal messages for clarity and consistency
- Refined messages related to removal rejection and recovery to enhance user understanding.
- Adjusted test cases to reflect updated messaging for better accuracy in assertions.
- Updated submodule references for Hexalith.EventStore and Hexalith.FrontComposer to the latest commits.
- Added a new file to document the build auto result status as blocked due to modified tests.

---
d90b074e37bd31bc817b7c5f39d3245ba20738fc
docs(deps): complete Hexalith dependency refresh

---
282ed1429b1477dc2128b3d5142187aa860dbd7a
test(deps): verify Memories secret-store wiring

---
3f7553685b6abc1b66f67b5905afc81120ac6102
chore(sweep): close resolved deferred-work entries

---
f5ed833a259f1599589ad8e7849ee4b8b849ba95
build(deps): update Hexalith dependencies

---
de5784ca751f51a4cfe282f67e11b13dd1ed4b45
feat: enhance global administrator removal process with additional states and recovery mechanisms
- Added new required fact keys for global administrator removal, including titles, descriptions, and various lifecycle states.
- Improved the CSS for the global administrators page to include box-sizing for better layout control.
- Enhanced the GlobalAdministratorRemoveCommandSnapshot to handle additional states and ensure correct lifecycle transitions.
- Introduced methods in TenantAggregateCommandAdmissionGate for managing reconciliation dispatch and aborting operations.
- Updated tests to cover new scenarios for removal status, including handling unsupported and ambiguous submissions.
- Ensured that the removal process correctly reflects state changes and maintains integrity across component replacements.

---
a3321266b61fc98f98f700901bd2cc5166eff4b0
feat: enhance global administrator removal process with reconciliation and recovery mechanisms
- Added SafeRecoveryKey and IsSubmissionAmbiguous properties to GlobalAdministratorRemoveCommandSnapshot for better error handling.
- Implemented terminal failure checks in GlobalAdministratorCorrectionSnapshot to improve command status management.
- Introduced reconciliation dispatch methods in TenantAggregateCommandAdmissionGate to track command delivery status.
- Enhanced TenantAggregateCommandLease with reconciliation dispatch capabilities.
- Added focus management functions in tenantsFocus.js for improved user experience during global administrator removal.
- Updated tests to cover new reconciliation and recovery scenarios for global administrator removal.
- Ensured localization keys for removal processes are comprehensive and validated against shipped resources.

---
b67547ffe3d4d4eff1c23a52f4681dce42356ea6
build: sync local changes via /pushall

---
cf31675b2e860c4be23e22355e361ce977da57c3
feat(tests): enhance GlobalAdministratorsPageTests with Fluent UI components and improve remove preview functionality
- Added Fluent UI components to the GlobalAdministratorsPageTests.
- Improved the remove preview dialog interactions and assertions.
- Updated test cases to reflect changes in the UI and logic.
- Ensured proper handling of administrator removal scenarios.
- Added new tests for focus containment and ambiguous removal retries.
- Enhanced localization checks for required keys in TenantsBffCompositionTests.
- Created a new spec for refreshing dependencies in the project.

---
b60f49c656723fb8cdff83a9164d1fa84b652707
fix: update baseline_revision for Global Administrator removal spec

---
37fcfded0addc05f99de229aa1fef52e5fa7b172
refactor(tests): simplify tenant detail assertions in Aspire topology tests

---
b5e9907c938a8384e3bd4a37cdadbddf6dc39cfa
fix(apphost): restore Memories Aspire NuGet-compatible secret-store path
Published Hexalith.Memories.Aspire 2.22.1 still expects secretStoreComponentPath.
The Story 29.2 IResourceBuilder overload is unpublished, so UseNuGetDeps Standalone builds fail CS1503 and block FrontComposer dependency-governance.

Co-authored-by: Cursor <cursoragent@cursor.com>

---
ce0e2aab7b783e850df704e599555b88b4a20c6a
test(tests): add integration tests for Aspire topology and Redis interactions

---
91d233558ad830555e5ed09803498a6d36c8de50
fix: update BMAD 6.12.0

---
84e86af275e65bcb16f0152f1e51c9ef0160e168
chore(submodules): update submodule references for Hexalith components

---
dc232cb088e073b3ac74aa59d29e97fcf428de55
refactor(tests): update status and baseline revision for behavioral UI guard tests

---
a54f0b952eb213026b95fd64810f686d1403c17c
test(tests): add integration test for tenant API to verify Redis state without projection authority

---
2a204a03530ac19ece315597ec5b354d6cc50028
refactor(tests): streamline TenantQueryResult and enhance query handler tests

---

