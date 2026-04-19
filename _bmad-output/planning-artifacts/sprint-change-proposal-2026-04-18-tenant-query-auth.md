# Sprint Change Proposal: DaprTenantQueryService Drops Bearer Token (401 on /api/v1/queries)

**Date:** 2026-04-18
**Triggered by:** Investigation during the `sprint-change-proposal-2026-04-18-events-page-slow.md` course-correct — live `eventstore-admin` structured logs show repeated `POST http://localhost:49974/v1.0/invoke/eventstore/method/api/v1/queries → 401` originating from `AdminTenantsController.ListTenants`.
**Scope Classification:** Minor — Direct implementation by dev team
**Related prior proposals:** N/A (regression since service introduction)

---

## Section 1: Issue Summary

**Symptom:** Every Admin.Server → EventStore tenant query returns `401 Unauthorized`. Live Aspire structured logs (`trace_id 401fbfe`) show dozens of occurrences of:

```
POST http://localhost:49974/v1.0/invoke/eventstore/method/api/v1/queries → 401
```

Each one emanates from `Hexalith.EventStore.Admin.Server.Controllers.AdminTenantsController.ListTenants`, and because `DashboardRefreshService` re-invokes the tenant flow both on a 30 s timer and on every SignalR signal, the 401 flood is continuous — visible as a log-noise issue today, but also means the `/tenants` page and every tenant filter dropdown in the Admin UI silently degrade to "no tenants".

**Root cause:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` receives `IAdminAuthContext authContext` as a constructor parameter (line 46) but **never stores it in a field**. The service holds `_daprClient`, `_httpClientFactory`, `_serverOptions`, `_logger` — but no `_authContext`. Consequently, `SendQueryAsync` (lines 142-173) creates the DAPR invoke request **without an `Authorization: Bearer <token>` header**, so EventStore's `[Authorize]`-protected `QueriesController` (`src/Hexalith.EventStore/Controllers/QueriesController.cs:17-18`) returns 401.

**Why the other 8 sibling services are not affected:** Ripgrep `_authContext\s*=` across `src/Hexalith.EventStore.Admin.Server/Services/*.cs` confirms that `DaprStreamQueryService`, `DaprTenantCommandService`, `DaprProjectionQueryService`, `DaprHealthQueryService`, `DaprStorageCommandService`, `DaprBackupCommandService`, `DaprDeadLetterCommandService`, and `DaprProjectionCommandService` all correctly store `_authContext` in a field and forward the token. `DaprTenantQueryService` is the sole outlier — the parameter is declared and validated-away-to-nothing.

**Evidence:**

| Source | Reference |
|---|---|
| Symptom (logs) | `eventstore-admin` structured logs, `trace_id 401fbfe`, `log_id 840-1138` — pairs of `POST .../api/v1/queries` → `401` originating from `AdminTenantsController.ListTenants` |
| Broken call site | `DaprTenantQueryService.SendQueryAsync` (`DaprTenantQueryService.cs:142-173`) — builds request, no `Authorization` header |
| Ghost parameter | `DaprTenantQueryService` ctor (`DaprTenantQueryService.cs:42-56`) — accepts `IAdminAuthContext authContext` but never assigns it |
| Working reference pattern | `DaprStreamQueryService.InvokeEventStoreAsync` (`DaprStreamQueryService.cs:496-517`) — reads `_authContext.GetToken()` and sets `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token)` |
| Sibling services (all correct) | 8 of 9 Admin.Server Dapr* services store `_authContext` via line-matching grep `_authContext\s*=` |

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|------|--------|
| **Epic 15 (Admin Web UI)** | Fixes tenant listing across multiple pages (`/tenants`, tenant dropdowns on `/streams`, `/events`, `/commands`, etc.). |
| Other epics | None. |

### Story Impact

| Story | Action |
|-------|--------|
| Tenant-listing-touching stories (multiple) | No AC changes. This is a regression fix, not a feature modification. |

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | |
| Architecture | None | Fix restores the documented JWT-forwarding invariant (Admin.Server forwards caller's bearer to EventStore via DAPR invoke). |
| UX Design | None | |
| epics.md | None | No user-visible surface area change. |
| Tests | Minor additive | Add a Tier 1 unit test asserting `DaprTenantQueryService.ListTenantsAsync` sets the `Authorization` header on the outgoing `HttpRequestMessage` — prevents future regression. Optional but recommended. |
| CI/CD / IaC / deployment | None | |

### Technical Impact

- **1 modified source file, 3 hunks**
  - `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`:
    1. Add `_authContext` private readonly field.
    2. Assign `_authContext = authContext` and `ArgumentNullException.ThrowIfNull(authContext)` in the ctor.
    3. In `SendQueryAsync`, read `_authContext.GetToken()` and set `request.Headers.Authorization` before attaching `request.Content`.
- **0 new files** / **0 API contract changes** / **0 schema changes** / **0 infrastructure changes**.
- **Build verified:** `dotnet build src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj --configuration Release` → 0 warnings, 0 errors.

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Option 1.

**How it works:** Three-line change that brings `DaprTenantQueryService` in line with the other 8 Admin.Server services. Uses the exact same pattern as `DaprStreamQueryService.InvokeEventStoreAsync:504-511`.

**Why Option 1 over alternatives:**

- **Option 2 — Extract a shared `AuthForwardingHttpRequest` helper**: Useful long-term hygiene to remove duplication across the 8 services, but out of scope for a single regression fix. File as a follow-up refactor.
- **Option 3 — MVP reduction**: Not applicable — no scope change under discussion.
- **Option 1 (selected)**: Smallest possible blast radius. 3 hunks, same pattern as 8 other services, zero API/schema/contract changes, builds clean on first try.

**Effort estimate:** Low — applied and built in this session.
**Risk level:** Low — the fix restores expected behaviour shared by every sibling service.
**Timeline impact:** None.

---

## Section 4: Detailed Change Proposals

### 4.1 Modified — Field declaration

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` (line 33-37)

```diff
+ private readonly IAdminAuthContext _authContext;
  private readonly DaprClient _daprClient;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ILogger<DaprTenantQueryService> _logger;
  private readonly AdminServerOptions _serverOptions;
```

### 4.2 Modified — Constructor assignment and null-check

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` (ctor body)

```diff
  ArgumentNullException.ThrowIfNull(daprClient);
  ArgumentNullException.ThrowIfNull(httpClientFactory);
  ArgumentNullException.ThrowIfNull(options);
+ ArgumentNullException.ThrowIfNull(authContext);
  ArgumentNullException.ThrowIfNull(logger);
  _daprClient = daprClient;
  _httpClientFactory = httpClientFactory;
  _serverOptions = options.Value;
+ _authContext = authContext;
  _logger = logger;
```

### 4.3 Modified — Forward Bearer token in `SendQueryAsync`

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` (around line 149)

```diff
  HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
      HttpMethod.Post, _serverOptions.EventStoreAppId, QueryEndpoint);

+ string? token = _authContext.GetToken();
+ if (token is not null) {
+     request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
+ }
+
  request.Content = JsonContent.Create(new SubmitQueryRequest(
```

**Rationale:** Mirrors `DaprStreamQueryService.cs:504-511` exactly. Using fully-qualified `System.Net.Http.Headers.AuthenticationHeaderValue` avoids introducing a new `using` statement in the file and matches the minimal-edit discipline already in place; if preferred, the caller can promote to a `using` at the top.

---

## Section 5: Implementation Handoff

**Scope:** Minor — direct implementation by dev team. Edits applied and built in this session.

**Verification checklist:**

| # | Check | Status |
|---|-------|--------|
| 1 | `dotnet build src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj --configuration Release` — 0 warnings, 0 errors | Done (this session) |
| 2 | `dotnet build Hexalith.EventStore.slnx --configuration Release` — full solution clean | Pending developer |
| 3 | `dotnet test tests/Hexalith.EventStore.Server.Tests/` (Tier 2) — no regression | Pending developer (requires `dapr init`) |
| 4 | Rebuild `eventstore-admin` resource in Aspire (live host) | Pending developer |
| 5 | Reload `https://localhost:60034/tenants` — tenants grid populated | Pending developer |
| 6 | Reload `/streams` and `/events` — tenant filter dropdowns now list tenants | Pending developer |
| 7 | Tail `eventstore-admin` structured logs — `POST .../api/v1/queries → 401` entries no longer present on dashboard-refresh cadence | Pending developer |
| 8 | Optional: add Tier 1 test `DaprTenantQueryServiceTests.SendQueryAsync_ForwardsBearerTokenFromAuthContext` using NSubstitute for `IAdminAuthContext` and a fake `HttpMessageHandler` capturing the request | Pending developer |

**New Story ID (optional):** `15-auth-forwarding-tenant-query-fix` — or append as a dated note under the story that introduced `DaprTenantQueryService` (find via `git log -- src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`).

**Dependencies:** None.

---

## Appendix: Investigation Evidence Highlights

- **Symptom:** Aspire live `eventstore-admin` structured logs — pairs of `POST http://localhost:49974/v1.0/invoke/eventstore/method/api/v1/queries` → `401`, originating from `AdminTenantsController.ListTenants` (`ActionId 996b25f4-0bec-4424-b960-baa2eefd573d`).
- **Broken call site:** `DaprTenantQueryService.cs:142-173` (`SendQueryAsync`).
- **Ghost parameter:** `DaprTenantQueryService.cs:46` receives `IAdminAuthContext authContext` but the ctor body (lines 48-55) has no `_authContext = authContext` assignment.
- **Established pattern:** `DaprStreamQueryService.cs:504-511` — adds `Authorization: Bearer` header via `_authContext.GetToken()`.
- **Grep-confirmed scope:** `rg "_authContext\s*=" src/Hexalith.EventStore.Admin.Server/Services/` → 8 matches across 8 files; `DaprTenantQueryService.cs` is the only missing entry.
- **Downstream authorizer:** `src/Hexalith.EventStore/Controllers/QueriesController.cs:17` (`[Authorize]`) — returns 401 when no bearer is present, exactly matching the observed behaviour.
