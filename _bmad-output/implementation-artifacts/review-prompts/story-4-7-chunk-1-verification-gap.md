# Story 4.7 Chunk 1 — Verification Gap Reviewer

Follow the review instructions below completely.

Do not invoke any skill, and do not spawn subagents of your own — you are the reviewer. Return your findings as text in your final message; do not route them through any findings-reporting tool the host may offer.

# Verification Gap Review

**Goal:** Find changed behavior that could break without reliable verification catching it. Ask one question — "if the behavior this change is supposed to produce broke where it's actually used, would verification fail?" Do not hunt for correctness bugs, but report genuine problems you notice while tracing verification.

The main verification gap shapes are:

1. **Regression gap:** the changed code regresses where it's used, and no test covering that use would fail.
2. **Missing-adoption gap:** a place that should now use the new behavior doesn't; it handles the same case its own way, or not at all, and no test would flag the omission.
3. **Broken-verification gap:** a test appears to cover the changed behavior, but would not actually protect it because it is skipped, flaky, not run in the normal verification path, or too weak to observe the regression.

## Evidence Rules

- Read a test before claiming what it covers, runs, asserts, or misses.
- Before claiming no test exists, search the whole repo by the symbol under test and by import references; expected file locations are not enough.
- Never assert what you did not verify. If a finding cannot be grounded, drop it.
- In a finding, say what you actually checked — "none of the tests I read cover this" — and show how far you looked. Say a test doesn't exist anywhere only when the symbol/import-reference search actually shows that.
- Do not assign severity, confidence, priority, or ranking.

## Review Sequence

### Step 1: Screen for behavioral change

Screen each part of the change separately. If a part is non-behavioral, skip it. Call a part non-behavioral only when the changed code does not alter return values, thrown errors, caller-visible side effects, or observable state (including iteration order and emitted messages). Once a part meets that test, move on; do not inspect callers or tests for extra confirmation.

Common non-behavioral examples: formatting, comments, whitespace; pure renames; trivial getters/setters and pass-throughs; type-only or compiler-enforced changes with no runtime effect; etc.

Only outcomes produced by deterministic code are worth automatically testing; tests are useless on static source text and brittle on LLM output. Skip those parts.

If every part is skipped, output the clean result (see Output Format).

### Step 2: Find the behavior that changed

Identify what behavior changed compared to the previous version: output, side effect, branch, error path, schema/event shape, config default, validation/authorization rule, external contract, etc. If the change affects more than one behavior, handle each separately.

Treat broad-impact changes as behavioral even when no single changed line looks important: dependency, toolchain, build/config, data-file, etc.

### Step 3: Trace where that behavior is used

Trace the changed behavior to the places that observe it. Start with direct callers and registered entry points (routes, commands, DI), contract consumers (schemas, events, APIs, database readers), and reverse-dependency info if already available.

Follow a path only while the changed behavior is reachable and unverified. Stop when a test at that boundary would fail, the consumer does not observe the changed behavior, or the next hop is guesswork (dynamic dispatch, reflection, outside-repo consumers, etc.). Prefer the nearest observable boundary, often one to three hops away, especially across contract, integration, or service edges. If there are more than five similar consumers, group obvious repeats and check representative paths; expand only when a consumer observes the behavior differently.

### Step 4: Qualify the consumer, then check its test

For each consumer, name the smallest realistic regression this consumer would observe: invert the branch, drop the default, omit the field, return the old error code, skip the integration call, etc. This is the Demonstration. If no such regression exists, drop the path; untested downstream code is not a finding.

A `Missing-adoption gap` qualifies not by the adoption failure alone but by a supersession signal: the change gives clear evidence the new behavior is meant to replace the local one — PR intent, naming or docs, a replaced sibling site, deleted duplicate logic, or a test defining the new rule — and the local site shares the same observable contract. Without a supersession signal and a shared observable contract, it is a refactor suggestion, not a verification-gap finding. Once both hold, check whether any test for that site would flag the non-adoption; missing coverage of the non-adoption is the gap itself, not a disqualifier.

Find and read the relevant test. Ask whether the Demonstration would make an assertion fail.

- If yes, the behavior is verified. No finding.
- For a regression-style Demonstration: if no test runs the path, the test is skipped/flaky/not run normally, or the test runs the code without checking the changed result, report a `Regression gap` or `Broken-verification gap`.
- For a qualifying Missing-adoption case: if none of the site tests you found assert it adopts the new behavior, report a `Missing-adoption gap`.

A test counts only if it runs normally and an assertion observes the changed output, branch, or contract. These do not count: no execution; source-text assertions that match a file's wording instead of running it; success/no-throw/snapshot-only checks; mock/log-call checks; human-only checks; tests that mock away the integration; e2e tests that pass through without checking the changed output; stale assertions or fixtures.

For example, `expect(x ?? DEFAULT).toBe(DEFAULT)` passes when `x` is missing.

Common patterns:

- **Caller-path gap** — helper test covers the branch, but caller values skip it.
- **Contract drift** — payload/schema/event changes must be verified at the consumer.
- **Migration compatibility** — tests only create new-format rows or fresh schemas.
- **Phantom exception** — handled partial-failure path has no test.
- **Missing-adoption gap** — sibling site should use the new rule/helper and does not.
- **Removed verification** — deleted test or weakened assertion leaves behavior unpinned; removing a source-text assertion is not this, since it never counted.

### Step 5: Confirm each finding is real

Before writing a finding, re-open the specific tests or search results the finding relies on. Verify the Demonstration would not make any test you checked fail, or that the absence claim is backed by the symbol/import-reference search. Do not claim more than you verified; drop any finding you cannot ground.

Explain why the test misses the bug using what the test sets up and checks.

Do not report: compiler/type-checker-enforced cases; behavior already verified by an integration, contract, or e2e test; implementation-detail or mock-only tests; low coverage or a missing test file by itself; legacy untested code the change did not affect.

Report genuine problems you noticed while tracing verification, even if they are not verification gaps. Put them under `Other findings` in the output. This permits reporting what you already reached, not extra hunting. A claim that code misbehaves is a defect, not a gap — it goes under `Other findings` for standard triage, however you found it.

## OUTPUT FORMAT

Emit each verification-gap finding as one block. No general advice, no severity or confidence. Triage trusts a gap finding as filed and does not re-verify it, so each block must stand on its own evidence.

```markdown
### <one-line title naming the gap>

- **Changed surface:** the exact behavior or contract that changed — `file:line`.
- **Impacted consumer or site:** named concretely with `file:line` (e.g. "the `createInvoice` mutation used by the billing dashboard at `billing/dashboard.ts:88`," not "callers of this function").
- **Existing test evidence:**
  - `Regression gap`: what the relevant test actually asserts, with `file:line`; or, if none, the symbol/import-reference searches run and their result.
  - `Missing-adoption gap`: tests for the impacted site, and whether any assert it adopts the new behavior.
  - `Broken-verification gap`: the apparent test or verification path, and why it does not count.
- **Missing verification:** the precise assertion or check that's absent.
- **Demonstration:**
  - `Regression gap` / `Broken-verification gap`: the concrete regression that would ship undetected, and why the tests you checked would not fail.
  - `Missing-adoption gap`: the case the site mishandles by not adopting the new behavior, and that none of the tests you read assert adoption.
- **Consequence:** the concrete thing that ships wrong — a regression the checked evidence would not catch, or a site that should use the new behavior and doesn't.
- **Disposition:** `patch` — name the test to add, fit to the repo's own way of verifying (don't impose a generic test pyramid) — or `defer` when the gap is real but not worth closing as part of this change, with one sentence of why.
```

If you noticed genuine non-gap problems while tracing verification, append:

```markdown
## Other findings

- <description only; no severity, confidence, priority, or ranking>
```

When you find no verification gaps and no other findings, output exactly this single line, not an empty response:

`No verification gaps found.`

## CONTENT SOURCE

"Review content:" in the message that launched you gives the content itself or a path to read it from. Read the file when it is a path; either way that is the content under review, and this instruction file never is. If no content is supplied, or it is empty or unreadable, stop with exactly: `No verification gaps found.`


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

