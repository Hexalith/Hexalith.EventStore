Read `/home/administrator/projects/hexalith/eventstore/_bmad/render/bmad-build/eventstore-5ec6a32020fe/949c1652f308ba6a0e7e/review-prompts/edge-case-hunter.md` completely and follow it as your review instructions.

Review content:

diff --git a/references/Hexalith.Builds b/references/Hexalith.Builds
index 22a578b5..5c3ff35c 160000
--- a/references/Hexalith.Builds
+++ b/references/Hexalith.Builds
@@ -1 +1 @@
-Subproject commit 22a578b576a515d2af214fe81859447fffc97981
+Subproject commit 5c3ff35c590cfae9f3a9784b75d08dd065c55cef
diff --git a/references/Hexalith.PolymorphicSerializations b/references/Hexalith.PolymorphicSerializations
index 93bcc44a..65fc3361 160000
--- a/references/Hexalith.PolymorphicSerializations
+++ b/references/Hexalith.PolymorphicSerializations
@@ -1 +1 @@
-Subproject commit 93bcc44a65cd42fcc4558de8f8a8e4d523486157
+Subproject commit 65fc33613db30f562dfe7daf92bf84b5cbb7eb4c
diff --git a/src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs b/src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs
index d7e28693..948a81cd 100644
--- a/src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs
+++ b/src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs
@@ -89,7 +89,7 @@ public class AdminTenantsController(
     /// <summary>
     /// Creates a new tenant.
     /// </summary>
-    [HttpPost("CreateTenant")]
+    [HttpPost]
     [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
     [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
     [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
@@ -244,7 +244,7 @@ public class AdminTenantsController(
     /// </summary>
     // Restricted to Admin: this endpoint has no route tenantId to scope on, so a tenant-scoped
     // ReadOnly/Operator caller must not be able to enumerate every tenant in the platform.
-    [HttpGet("ListTenants")]
+    [HttpGet]
     [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
     [ProducesResponseType(typeof(IReadOnlyList<TenantSummary>), StatusCodes.Status200OK)]
     [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
diff --git a/tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs b/tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs
index 6b80e05b..0ee67d9d 100644
--- a/tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs
+++ b/tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs
@@ -1,9 +1,15 @@
 using System.Net;
+using System.Net.Http.Json;
 using System.Security.Claims;
 using System.Text.Json;
 
+using Hexalith.EventStore.Admin.Abstractions.Models.Common;
+using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
+using Hexalith.EventStore.Admin.Abstractions.Services;
 using Hexalith.EventStore.Admin.Server.Authorization;
 
+using NSubstitute;
+
 namespace Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;
 
 public class AdminAuthorizationIntegrationTests : IDisposable {
@@ -68,29 +74,63 @@ public class AdminAuthorizationIntegrationTests : IDisposable {
     }
 
     [Fact]
-    public async Task OperatorRole_GetTenants_NotForbiddenOrUnauthorized() {
-        // Tenant list is a read operation — ReadOnly policy allows Operator access (AC14)
+    public async Task OperatorRole_GetTenants_ReturnsForbidden() {
         SetClaims(
             new Claim(AdminClaimTypes.AdminRole, "Operator"),
             new Claim(AdminClaimTypes.Tenant, "tenant-a"));
 
-        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");
+        using HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");
 
-        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
-        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
+        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
     }
 
     [Fact]
-    public async Task AdminRole_GetTenants_NotForbiddenOrUnauthorized() {
+    public async Task AdminRole_GetTenants_ReturnsOk() {
+        ITenantQueryService tenantQueryService = _host.GetService<ITenantQueryService>();
+        IReadOnlyList<TenantSummary> expectedTenants =
+        [
+            new("tenant-a", "Tenant A", TenantStatusType.Active),
+        ];
+        _ = tenantQueryService
+            .ListTenantsAsync(Arg.Any<CancellationToken>())
+            .Returns(expectedTenants);
+
         SetClaims(
             new Claim(AdminClaimTypes.AdminRole, "Admin"),
             new Claim(AdminClaimTypes.Tenant, "tenant-a"));
 
-        HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");
+        using HttpResponseMessage response = await _client.GetAsync("/api/v1/admin/tenants");
 
-        // Authorization should pass for Admin role (mock service may return null → 204)
-        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
-        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
+        response.StatusCode.ShouldBe(HttpStatusCode.OK);
+        IReadOnlyList<TenantSummary>? tenants = await response.Content
+            .ReadFromJsonAsync<IReadOnlyList<TenantSummary>>();
+        tenants.ShouldNotBeNull();
+        tenants.Count.ShouldBe(1);
+        tenants[0].ShouldBe(expectedTenants[0]);
+        _ = tenantQueryService.Received(1).ListTenantsAsync(Arg.Any<CancellationToken>());
+    }
+
+    [Fact]
+    public async Task AdminRole_PostTenant_ReturnsAccepted() {
+        ITenantCommandService tenantCommandService = _host.GetService<ITenantCommandService>();
+        _ = tenantCommandService
+            .CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
+            .Returns(new AdminOperationResult(true, "01JAXYZ1234567890ABCDEFGH", "Accepted", null));
+
+        SetClaims(
+            new Claim(AdminClaimTypes.AdminRole, "Admin"),
+            new Claim(AdminClaimTypes.Tenant, "tenant-a"));
+
+        var request = new CreateTenantRequest("tenant-a", "Tenant A", "Test tenant");
+        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/admin/tenants", request);
+
+        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
+        _ = tenantCommandService.Received(1).CreateTenantAsync(
+            Arg.Is<CreateTenantRequest>(value =>
+                value.TenantId == request.TenantId
+                && value.Name == request.Name
+                && value.Description == request.Description),
+            Arg.Any<CancellationToken>());
     }
 
     [Fact]
diff --git a/_bmad-output/implementation-artifacts/spec-admin-ui-tenants-page-error.md b/_bmad-output/implementation-artifacts/spec-admin-ui-tenants-page-error.md
new file mode 100644
index 00000000..1efd888a
--- /dev/null
+++ b/_bmad-output/implementation-artifacts/spec-admin-ui-tenants-page-error.md
@@ -0,0 +1,70 @@
+---
+title: 'Fix Admin UI tenants collection routes'
+type: 'bugfix'
+created: '2026-08-26'
+status: 'in-review'
+review_loop_iteration: 0
+baseline_commit: '5e8f175b2ced4715f7c6f765386812cc1001dbb4'
+context: []
+---
+
+<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">
+
+## Intent
+
+**Problem:** The Admin UI tenants page shows “Unable to load tenant data” because its Dapr-routed `GET api/v1/admin/tenants` request receives 404. A controller refactor changed the server-only collection route templates to action-name paths even though the Admin UI, MCP client, and established REST contract still use the collection root; tenant creation is broken by the same mismatch.
+
+**Approach:** Restore the tenant controller’s list and create actions to the HTTP GET/POST collection root, preserve their current authorization and error semantics, and strengthen HTTP-boundary regression tests so an unmapped 404 cannot pass as successful authorization again.
+
+## Boundaries & Constraints
+
+**Always:** Keep tenant enumeration restricted to `AdminAuthorizationPolicies.Admin`; keep tenant creation Admin-only; preserve existing controller service calls, 200/202 results, ProblemDetails mappings, Dapr routing, and client route strings. Add route-level tests that assert exact status codes rather than only “not 401/403.” Preserve all pre-existing working-tree and submodule changes.
+
+**Ask First:** Any solution that changes authorization policy, public client route strings, the AppHost resource model, or files in a `references/` submodule.
+
+**Never:** Add compatibility aliases for the accidental `/ListTenants` or `/CreateTenant` paths, edit generated artifacts, broaden the fix to unrelated Admin UI route failures, weaken fail-closed tenant authorization, or treat a 404 as tenants-service unavailability.
+
+## I/O & Edge-Case Matrix
+
+| Scenario | Input / State | Expected Output / Behavior | Error Handling |
+|----------|---------------|---------------------------|----------------|
+| List tenants | Admin-authenticated `GET /api/v1/admin/tenants` | Controller invokes `ITenantQueryService.ListTenantsAsync` and returns 200 with the collection | Existing 401/403/502/503 mappings remain unchanged |
+| Create tenant | Admin-authenticated `POST /api/v1/admin/tenants` with a valid request | Controller invokes `ITenantCommandService.CreateTenantAsync` and returns 202 for accepted work | Existing command failure and service-unavailable mappings remain unchanged |
+| Non-admin enumeration | Operator-authenticated `GET /api/v1/admin/tenants` | Request is rejected before tenant enumeration | Return 403 without disclosing tenant existence |
+
+</frozen-after-approval>
+
+## Code Map
+
+- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs:92` -- `CreateTenantAsync` currently maps to the accidental `CreateTenant` child route; only its route template changes.
+- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs:247` -- `ListTenantsAsync` currently maps to the accidental `ListTenants` child route; retain the later Admin-only security decision while restoring the collection route.
+- `src/Hexalith.EventStore.Admin.UI/Services/AdminTenantApiClient.cs:24` -- authoritative UI consumer uses the collection root for list and create; read-only evidence, no edit expected.
+- `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Tenants.cs:12` -- second consumer confirms the collection-root list contract; read-only evidence, no edit expected.
+- `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs:67` -- current weak assertions allow an unmapped 404; strengthen Admin/Operator coverage and add POST collection-route coverage.
+- `src/Hexalith.EventStore.AppHost/Program.cs:47` -- Aspire topology names `eventstore-admin-ui`, `eventstore-admin`, and source-mode `tenants`; runtime verification only.
+
+## Tasks & Acceptance
+
+**Execution:**
+- [x] `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs` -- map list/create to bare `[HttpGet]` and `[HttpPost]` while preserving policies and implementation.
+- [x] `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs` -- assert Admin GET returns 200, Operator GET returns 403, and Admin POST reaches the collection action and returns 202.
+
+**Acceptance Criteria:**
+- Given the source-mode Aspire topology is healthy and the Admin UI uses its configured Admin service account, when `/tenants` loads, then the list call no longer returns 404 and the page renders tenant data or its legitimate empty state without the route-mismatch error banner.
+- Given the regression test host, when tenant collection routes are exercised with Admin and Operator claims, then exact 200/202/403 results prove both route reachability and current authorization behavior.
+- Given the completed change, when the focused server test assembly and repository formatting checks run, then they pass without changing unrelated files or submodules.
+
+## Spec Change Log
+
+## Design Notes
+
+The root route is the intended REST collection contract and was used before commit `48d5126e`; the Async method rename should not have changed the URL. Restoring controller attributes fixes all existing consumers at the ownership boundary and avoids duplicating an accidental action-name convention in each client.
+
+## Verification
+
+**Commands:**
+- `dotnet build tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj --configuration Release -m:1` -- expected: focused test project builds with zero errors.
+- `dotnet tests/Hexalith.EventStore.Admin.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.Server.Tests.dll -class Hexalith.EventStore.Admin.Server.Tests.IntegrationTests.AdminAuthorizationIntegrationTests` -- expected: route and authorization integration tests pass.
+- `UseHexalithProjectReferences=true aspire start --non-interactive` followed by `aspire wait tenants --non-interactive`, `aspire wait eventstore-admin --non-interactive`, and `aspire wait eventstore-admin-ui --non-interactive` -- expected: source-mode tenants topology is healthy.
+- Browser-open `https://localhost:8093/tenants`, then inspect `aspire otel logs eventstore-admin-ui --non-interactive` -- expected: no 404 for `GET /api/v1/admin/tenants` and no route-mismatch error banner.
+- `git diff --check` -- expected: no whitespace errors.

Do not invoke any skill. If the instruction file is unreadable, report that exact failure and stop. Return only the review result.

