---
title: 'Fix Admin UI tenants collection routes'
type: 'bugfix'
created: '2026-08-26'
status: 'done'
review_loop_iteration: 0
baseline_commit: '5e8f175b2ced4715f7c6f765386812cc1001dbb4'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Admin UI tenants page shows “Unable to load tenant data” because its Dapr-routed `GET api/v1/admin/tenants` request receives 404. A controller refactor changed the server-only collection route templates to action-name paths even though the Admin UI, MCP client, and established REST contract still use the collection root; tenant creation is broken by the same mismatch.

**Approach:** Restore the tenant controller’s list and create actions to the HTTP GET/POST collection root, preserve their current authorization and error semantics, and strengthen HTTP-boundary regression tests so an unmapped 404 cannot pass as successful authorization again.

## Boundaries & Constraints

**Always:** Keep tenant enumeration restricted to `AdminAuthorizationPolicies.Admin`; keep tenant creation Admin-only; preserve existing controller service calls, 200/202 results, ProblemDetails mappings, Dapr routing, and client route strings. Add route-level tests that assert exact status codes rather than only “not 401/403.” Preserve all pre-existing working-tree and submodule changes.

**Ask First:** Any solution that changes authorization policy, public client route strings, the AppHost resource model, or files in a `references/` submodule.

**Never:** Add compatibility aliases for the accidental `/ListTenants` or `/CreateTenant` paths, edit generated artifacts, broaden the fix to unrelated Admin UI route failures, weaken fail-closed tenant authorization, or treat a 404 as tenants-service unavailability.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| List tenants | Admin-authenticated `GET /api/v1/admin/tenants` | Controller invokes `ITenantQueryService.ListTenantsAsync` and returns 200 with the collection | Existing 401/403/502/503 mappings remain unchanged |
| Create tenant | Admin-authenticated `POST /api/v1/admin/tenants` with a valid request | Controller invokes `ITenantCommandService.CreateTenantAsync` and returns 202 for accepted work | Existing command failure and service-unavailable mappings remain unchanged |
| Non-admin enumeration | Operator-authenticated `GET /api/v1/admin/tenants` | Request is rejected before tenant enumeration | Return 403 without disclosing tenant existence |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs:92` -- `CreateTenantAsync` currently maps to the accidental `CreateTenant` child route; only its route template changes.
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs:247` -- `ListTenantsAsync` currently maps to the accidental `ListTenants` child route; retain the later Admin-only security decision while restoring the collection route.
- `src/Hexalith.EventStore.Admin.UI/Services/AdminTenantApiClient.cs:24` -- authoritative UI consumer uses the collection root for list and create; read-only evidence, no edit expected.
- `src/Hexalith.EventStore.Admin.Mcp/AdminApiClient.Tenants.cs:12` -- second consumer confirms the collection-root list contract; read-only evidence, no edit expected.
- `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs:67` -- current weak assertions allow an unmapped 404; strengthen Admin/Operator coverage and add POST collection-route coverage.
- `src/Hexalith.EventStore.AppHost/Program.cs:47` -- Aspire topology names `eventstore-admin-ui`, `eventstore-admin`, and source-mode `tenants`; runtime verification only.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs` -- map list/create to bare `[HttpGet]` and `[HttpPost]` while preserving policies and implementation.
- [x] `tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs` -- assert Admin GET returns 200, Operator GET returns 403, and Admin POST reaches the collection action and returns 202.

**Acceptance Criteria:**
- Given the source-mode Aspire topology is healthy and the Admin UI uses its configured Admin service account, when `/tenants` loads, then the list call no longer returns 404 and the page renders tenant data or its legitimate empty state without the route-mismatch error banner.
- Given the regression test host, when tenant collection routes are exercised with Admin and Operator claims, then exact 200/202/403 results prove both route reachability and current authorization behavior.
- Given the completed change, when the focused server test assembly and repository formatting checks run, then they pass without changing unrelated files or submodules.

## Spec Change Log

## Design Notes

The root route is the intended REST collection contract and was used before commit `48d5126e`; the Async method rename should not have changed the URL. Restoring controller attributes fixes all existing consumers at the ownership boundary and avoids duplicating an accidental action-name convention in each client.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj --configuration Release -m:1` -- expected: focused test project builds with zero errors.
- `dotnet tests/Hexalith.EventStore.Admin.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Admin.Server.Tests.dll -class Hexalith.EventStore.Admin.Server.Tests.IntegrationTests.AdminAuthorizationIntegrationTests` -- expected: route and authorization integration tests pass.
- `UseHexalithProjectReferences=true aspire start --non-interactive` followed by `aspire wait tenants --non-interactive`, `aspire wait eventstore-admin --non-interactive`, and `aspire wait eventstore-admin-ui --non-interactive` -- expected: source-mode tenants topology is healthy.
- Browser-open `https://localhost:8093/tenants`, then inspect `aspire otel logs eventstore-admin-ui --non-interactive` -- expected: no 404 for `GET /api/v1/admin/tenants` and no route-mismatch error banner.
- `git diff --check` -- expected: no whitespace errors.

## Suggested Review Order

**Collection routing**

- Restore the failing list request at the controller-owned collection boundary.
  [`AdminTenantsController.cs:247`](../../src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs#L247)

- Restore create semantics on the same collection without compatibility aliases.
  [`AdminTenantsController.cs:92`](../../src/Hexalith.EventStore.Admin.Server/Controllers/AdminTenantsController.cs#L92)

**Authorization and response contracts**

- Prove non-admin enumeration remains fail-closed with an exact 403.
  [`AdminAuthorizationIntegrationTests.cs:77`](../../tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs#L77)

- Prove the restored list route reaches its service and returns 200.
  [`AdminAuthorizationIntegrationTests.cs:88`](../../tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs#L88)

- Prove the restored create route reaches its service and returns 202.
  [`AdminAuthorizationIntegrationTests.cs:113`](../../tests/Hexalith.EventStore.Admin.Server.Tests/IntegrationTests/AdminAuthorizationIntegrationTests.cs#L113)

**Review disposition**

- Keep unrelated broad-baseline findings outside this focused bugfix.
  [`deferred-work.md:4008`](deferred-work.md#L4008)
