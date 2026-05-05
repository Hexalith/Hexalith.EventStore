# Sprint Change Proposal: Tenant Management Debug Cluster — Four Mutually-Masking Bugs in the Tenant CRUD Path

**Date:** 2026-05-05
**Triggered by:** Live debug session on the Admin UI tenant management flow. User reported "no error message, nothing happens when I create a tenant, refresh shows nothing." Root-cause investigation via Aspire MCP traces / structured logs / Redis state-store inspection uncovered **four distinct bugs** that compound to produce the observed symptom. Each bug masks the next: fixing one only surfaces the next layer, repeatedly.
**Scope Classification:** Moderate — coordinated patches across two repositories (`Hexalith.EventStore` + `Hexalith.Tenants` submodule). One consolidated story.
**Related:** Companion to `sprint-change-proposal-2026-05-05-event-detail-endpoint-missing.md` (same session, separately triaged and shipped as Story 15.12b — commit `15895b8b`).
**Supersedes:** None.
**Story:** New consolidated story `tenant-management-debug-cluster-fix` filed in `sprint-status.yaml` (epic to be assigned).

---

## Section 1: Issue Summary

**Symptom (observed by user):**
1. On `/tenants` page, clicking "Create Tenant" → filling the form → clicking the dialog's "Create Tenant" button produced **no toast, no error, no entry in the list**, even after refresh. The user described it as "rien ne se passe — pas de message, rien sur la page."
2. After working through the chain, clicking on a tenant row in the list to view details intermittently shows "Tenant details unavailable — Tenants service is not responding." The error appears, then disappears, then reappears with no apparent trigger, often after long loading delays.

**Root cause (four-layer onion, not one bug):**

### Bug A — UI bind on `FluentTextInput` is `onchange` not `oninput` (Fluent UI v5)

`src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor:232-250` — the three Create Tenant dialog inputs use `@bind-Value` with no `Immediate="true"`. Fluent UI v5 default is `onchange` (focus-loss), not `oninput` (per keystroke). When the user types into the inputs and clicks the dialog's submit button **without losing focus first**, `_createTenantId` / `_createName` remain `string.Empty` (initialized in `OpenCreateDialog`), `IsCreateFormValid()` returns false, and the submit button is silently disabled. The click is inert. **No POST is ever sent.**

Evidence: trace `8a9f8c1` (12:48 reproduction): only one HTTP call resulting from the FluentButton click — `GET .../api/v1/admin/tenants` (LoadDataAsync), no POST. Same pattern across multiple repro attempts pre-fix.

### Bug B — No projection handler for the `global-administrators` domain in `Hexalith.Tenants`

`Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs` switches on event types from the `tenants` domain only (`TenantCreated`, `TenantUpdated`, `UserAddedToTenant`, etc.). **There is no analogous handler for the `global-administrators` domain.** `Hexalith.Tenants/src/Hexalith.Tenants.Server/Projections/GlobalAdministratorProjection.cs` is just a marker `[EventStoreDomain("global-administrators")]` with an empty body — it is auto-discovered by EventStore's assembly scan but does not drive any projection writer toward `projection:global-administrators:singleton`.

Consequences:

- `TenantBootstrapHostedService` sends a `BootstrapGlobalAdmin` command at app startup with `Tenants__BootstrapGlobalAdminUserId = "admin-user"` (env var configured in `src/Hexalith.EventStore.AppHost/Program.cs:91`). The command flows successfully through EventStore — the event is persisted (Redis key `eventstore||AggregateActor||system:tenants:global-administrators||system:tenants:global-administrators:events:1` is present). But on the projection side, nothing writes to `projection:global-administrators:singleton`. The corresponding per-tenant projection `projection:tenants:global-administrators` is created but stays **completely empty** (`{"tenantId":"","name":"","members":{},"createdAt":"0001-01-01..."}`) because `TenantProjectionHandler` doesn't recognize the bootstrap event.
- `TenantsProjectionActor.IsGlobalAdminAsync` reads `projection:global-administrators:singleton` and returns false for every user (key never exists).
- `TenantsProjectionActor.HandleListTenantsAsync` therefore filters by `userTenants[envelope.UserId]`, which is also empty (no `UserAddedToTenant` event for `admin-user`). Result: empty paginated list **even though the tenant is correctly recorded in `projection:tenant-index:singleton.tenants`**.

Evidence: Redis dump after wipe + clean reboot:
- `projection:tenant-index:singleton.data` = `{"tenants":{"acme-corp":{"name":"testid1","status":0}},"userTenants":{}}` — tenant recorded but no userTenants
- `projection:tenants:global-administrators.data` = `{"tenantId":"","name":"","members":{},...}` — empty, never populated by handler
- `projection:global-administrators:singleton` — **does not exist in Redis**

### Bug C — `JsonStringEnumConverter` missing on the admin server's `DaprTenantQueryService._options`

`src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs:31-33` declares `_options` with `PropertyNameCaseInsensitive = true` only. The `Hexalith.Tenants` projection actor serializes payloads with `JsonStringEnumConverter` enabled (statuses serialize as strings like `"Active"`). The admin server receives that payload and uses `_options` to deserialize it into `Hexalith.Tenants.Contracts.Queries.PaginatedResult<TenantSummary>`, where `TenantSummary.Status` is a `TenantStatus` enum. The default `EnumConverter` cannot read a string into an enum; it throws `JsonException` with `Path: $.items[0].status`, which the controller maps to `503 Service Unavailable`.

Net effect on UI: even after Bug B is worked around (admin recognized as global admin → list query returns the tenants), the response cannot be deserialized → admin server returns 503 → UI surfaces "Tenants service is not responding" → user sees nothing. Same defect class re-bites the user despite the underlying state-store being fully correct.

Evidence: log id `2621` on `eventstore-admin`:
```
System.Text.Json.JsonException: The JSON value could not be converted to
  Hexalith.Tenants.Contracts.Queries.TenantSummary.
  Path: $.items[0].status | LineNumber: 0 | BytePositionInLine: 68.
  at System.Text.Json.Serialization.Converters.EnumConverter`1.Read(...)
  at DaprTenantQueryService.ListTenantsAsync(...) line 125
```

### Bug D — `DaprTenantQueryService.GetTenantDetailAsync` does not handle `QueryResult.Success == false`

`DaprTenantQueryService.cs:65-87` reads `response.Payload.Deserialize<ContractsTenantDetail>(_options)` unconditionally. When the upstream `TenantsProjectionActor.HandleGetTenantAsync` returns `new QueryResult(false, default, ErrorMessage: "Forbidden")` — for example when the user is not a member and is not (yet) recognized as global admin — `Payload` is empty/default. Deserialization quietly produces a record with all fields default (`TenantId = ""`). The code then calls the `Hexalith.EventStore.Admin.Abstractions.Models.Tenants.TenantDetail` constructor, which validates `TenantId` non-empty and throws `ArgumentException`. The exception is caught by the controller's generic catch arm and mapped to a misleading **503 "Tenants service is not responding"** — exactly the same UI banner the user sees, but for an entirely different upstream cause (authorization, not transport).

Evidence: log id `5534` on `eventstore-admin`:
```
System.ArgumentException: TenantId cannot be null, empty, or whitespace.
  at TenantDetail..ctor (TenantDetail.cs:18)
  at DaprTenantQueryService.GetTenantDetailAsync (DaprTenantQueryService.cs:77)
  at AdminTenantsController.GetTenantDetail (AdminTenantsController.cs:67)
```

This is the cause of the user's reported "Tenant details unavailable" intermittence: when the actor's internal cache happens to see the global-admin hack via fresh state load, the click works; when the cached state is stale, the user is treated as Forbidden, the error path returns empty payload, and the UI sees 503.

**Cross-cutting NFR observation (not in this proposal's scope):** Each tenant action (create / disable / enable / detail load) takes several seconds to a minute, with the dialog's progress spinner held for the full duration. This is consistent with DAPR actor cold-start (each `AggregateActor.<aggregate-id>` must be activated, hydrate state from Redis, and process the command), plus Polly retries on transient timeouts that the admin server widens further. Filed as a separate follow-up below — **does not block this story**.

**Evidence summary table:**

| # | Source | Reference |
|---|---|---|
| A | UI dialog click trace `8a9f8c1` | Only HTTP egress = `GET admin/tenants`, no POST. `Tenants.razor:232-250` shows three `<FluentTextInput @bind-Value=…>` with no `Immediate="true"`. Fluent UI v5 default is `onchange`. |
| A | UI dialog click trace `8a9f8c1` (post-fix) | `aspnetcore.components.attribute.name: onclick` on `FluentButton.OnClickHandlerAsync`, no POST in Polly retry pipeline. |
| B | Redis state-store dump | `projection:global-administrators:singleton` does not exist among `projection:*` keys. `projection:tenants:global-administrators` is `{"tenantId":"","name":"","members":{},"createdAt":"0001-01-01..."}`. |
| B | Source — Hexalith.Tenants | `Hexalith.Tenants/src/Hexalith.Tenants/Projections/` contains only `TenantProjectionHandler.cs`. `Hexalith.Tenants.Server/Projections/GlobalAdministratorProjection.cs` is empty. No `[EventStoreDomain("global-administrators")]` handler writes the singleton key. |
| C | Admin server log `2621` | `JsonException ... EnumConverter Read` on `$.items[0].status`. `DaprTenantQueryService.cs:31` declares `_options = new() { PropertyNameCaseInsensitive = true }` with no `JsonStringEnumConverter`. |
| D | Admin server log `5534` | `ArgumentException: TenantId cannot be null, empty, or whitespace` from `TenantDetail..ctor` invoked from `DaprTenantQueryService.cs:77`. The Forbidden empty payload from `TenantsProjectionActor.HandleGetTenantAsync` is silently deserialized into a default record. |
| D | Source — TenantsProjectionActor | `Hexalith.Tenants/src/Hexalith.Tenants/Actors/TenantsProjectionActor.cs:147-151` returns `QueryResult(false, default, ErrorMessage: "Forbidden")` whose payload is `default` (zero bytes). |

**Why these four bugs cluster:**

The user-visible symptom — "tenant management is broken" — has four distinct origins on three different layers (UI bind, projection write, JSON contract, error handling). They mask each other in sequence:

1. Without **A** → no command ever reaches the server, all subsequent layers are dead.
2. Fix **A** → command flows, projection writes, but the UI list filter has no admin authority recognition (**B**) → list looks empty.
3. Fix **B** (hack) → list query returns the tenants, but the admin server can't deserialize the response (**C**) → still empty list with 503 banner.
4. Fix **C** → list works, but per-tenant detail goes through the same authorization gate that, on cache miss, returns Forbidden whose empty payload deserializes silently (**D**) → "Tenants service is not responding" intermittently.

This sequence was reproduced live in this session with traces and Redis dumps at each layer.

**Why these weren't caught in prior testing:**

- **A**: 21.x Fluent UI v4 → v5 migration changed the default bind event for `FluentTextInput` from `oninput` to `onchange`. No regression test exercised the dialog with a fresh focus → click sequence.
- **B**: The bootstrap path is exercised in `Hexalith.Tenants` tests via in-memory projection (`InMemoryTenantProjection.cs`), which has explicit branches for global-admin events (`InMemoryTenantProjection.cs:60-102`). Production runs through the real DAPR projection dispatch which has no equivalent handler. The contract gap is invisible to in-memory tests.
- **C**: No Tier 2 / Tier 3 test exercises `DaprTenantQueryService` end-to-end against a live tenants service producing real JSON. Unit tests mock the response.
- **D**: Same testing gap as C, plus the failure path requires an unauthorized user, which the in-memory test fixtures don't typically simulate.

---

## Section 2: Impact Analysis

### Epic Impact

The cluster spans the tenant management vertical (tenant CRUD + user assignment + admin authorization) introduced across the recent tenant epics. No epic AC needs to change — every behavior the user observed is already specified by the PRD; it's the implementation that doesn't meet the spec on the four points above. **One consolidated post-epic story** is added to track all four fixes.

### Story Impact

| Story | Action |
|-------|--------|
| Existing tenant-management stories (any covering Create / List / Detail) | Append dated note pointing at this proposal. **No AC change.** |
| **New consolidated story `tenant-management-debug-cluster-fix`** | Added to `sprint-status.yaml` as `backlog`. Scope = Proposals 4.1 + 4.2 + 4.3 + 4.4. |

### Artifact Conflicts

| Artifact | Conflict | Action |
|----------|----------|--------|
| PRD | None | The four behaviors (Create succeeds with toast, list rerenders, detail panel loads, errors map correctly) are already specified. Implementation is catching up. |
| Architecture | None | One added projection handler in `Hexalith.Tenants` (additive), one tightened JSON serializer config and one tightened error-path handler in `Hexalith.EventStore.Admin.Server`, one UI binding policy adjustment. No pattern, contract, or topology change. |
| UX Design | None | UI behavior is already as designed. |
| `epics.md` | Minor | Append dated follow-up paragraph under the relevant tenant story/stories. |
| `sprint-status.yaml` | Minor | Insert one consolidated story entry under the tenant epic. |
| Tests | Minor additive | New Tier 1 unit tests for each patch. New Tier 2 integration test for the global-admin projection writer (cross-actor verification) is recommended but out of scope for this story. |
| CI/CD / IaC / deployment | None | |

### Technical Impact

- **6 files modified, 1 file added, across 2 repositories.**

  **In `Hexalith.EventStore` (this repo):**
  - **EDIT** — `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` — add `Immediate="true"` to the three Create Tenant dialog `<FluentTextInput>` (already applied locally in this session, **uncommitted**).
  - **EDIT** — `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` — add `using System.Text.Json.Serialization;` and `Converters = { new JsonStringEnumConverter() }` to `_options` (already applied locally in this session, **uncommitted**).
  - **EDIT** — `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` — refactor `GetTenantDetailAsync` (and equivalently `ListTenantsAsync`, `GetTenantUsersAsync`) to inspect the upstream `QueryResult.Success` / `ErrorMessage` and propagate `Forbidden` / `NotFound` / etc. as appropriate HTTP status codes instead of letting `Payload.Deserialize<...>` produce a default record. Map `Forbidden` upstream → `403`, `Tenant not found` → `404`, leave transient → 503.
  - **EDIT** — `_bmad-output/planning-artifacts/epics.md` — append dated follow-up paragraph.
  - **EDIT** — `_bmad-output/implementation-artifacts/sprint-status.yaml` — add the consolidated story entry.

  **In `Hexalith.Tenants` (submodule, pinned at commit `0fecb6c` on branch `fix/domain-processor-mismatch-matcher`):**
  - **NEW** — `Hexalith.Tenants/src/Hexalith.Tenants/Projections/GlobalAdministratorProjectionHandler.cs` — analogous to `TenantProjectionHandler` but switches on `GlobalAdministratorSet` / `GlobalAdministratorRemoved` events for the `global-administrators` domain. Writes to `projection:global-administrators:singleton` via `daprClient.SaveStateAsync(...)`. Wired into the same DAPR projection dispatch as `TenantProjectionHandler` (DI registration in the existing handler-registration extension method).
  - **EDIT** — `Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs` — if the existing handler intercepts events from any aggregate id including the bootstrap one, ensure it filters out events from the `global-administrators` domain (or split routing) so the two handlers don't fight over the same per-aggregate projection key (existing key `projection:tenants:global-administrators` will become a no-op or be removed; verify migration impact).

- **0 API contract changes.** Route shapes, request DTOs, and response DTOs all remain unchanged. The fix is on the producer side (write the missing projection) and consumer side (deserialize the existing payload correctly).
- **0 schema changes / 0 infrastructure changes.**
- **Behavior change matrix:**

  | Scenario | Before | After |
  |---|---|---|
  | User types in Create Tenant dialog and clicks Create immediately | Button disabled silently, no POST | Button enabled per keystroke, POST sent, toast shown |
  | List tenants for `admin-user` (configured BootstrapGlobalAdminUserId) | Empty list (no global-admin recognition) | All tenants returned |
  | List tenants for non-admin user not in any tenant | Empty list (correct, but for wrong reason) | Empty list (correctly authorized check) |
  | List tenants response with `status: "Active"` enum string | 503 (deserialization fails) | 200 with parsed list |
  | GetTenantDetail when caller is unauthorized | 503 "Tenants service is not responding" | 403 Forbidden |
  | GetTenantDetail when tenant doesn't exist | 503 "Tenants service is not responding" | 404 Not Found |
  | GetTenantDetail when downstream is genuinely down | 503 (unchanged) | 503 (unchanged) |

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Option 1 — single consolidated story spanning two repos.

**How it works:**

1. **Fix A** (Tenants.razor `Immediate="true"`) — already applied locally in this session, ready for commit.
2. **Fix C** (`DaprTenantQueryService._options` + `JsonStringEnumConverter`) — already applied locally in this session, ready for commit.
3. **Fix B** (new `GlobalAdministratorProjectionHandler` in `Hexalith.Tenants`) — implement in the submodule. Tests (Tier 1) verify the handler writes to `projection:global-administrators:singleton` for `GlobalAdministratorSet` / `Removed` events. **This is the load-bearing fix that obsoletes the volatile Redis hack** (`HSET projection:global-administrators:singleton ...`) currently in place.
4. **Fix D** (`DaprTenantQueryService` error-path handling) — refactor the three Get/List query methods to inspect `QueryResult.Success` and propagate upstream errors via correct HTTP status codes instead of silently producing default records.
5. After all four are in, **delete the Redis hack** by re-running `redis-cli HDEL projection:global-administrators:singleton` (or just letting the next clean wipe drop it). Verify the bootstrap-on-startup flow re-creates the singleton via the new handler. The state should match what the hack manually injected.

**Why Option 1 over alternatives:**

- **Option 2 — Four separate stories**: most BMad-canonical but for a single user-reported defect family, the cumulative ceremony cost would exceed the implementation cost. The user explicitly requested a single story. The four fixes are causally linked: shipping any subset leaves the user-visible symptom partly unresolved. Bundling guarantees end-to-end fix in one merge.
- **Option 3 — Three separate stories per repo + one cross-repo coordination story**: also viable but pure overhead. The two repos have to be synchronized anyway because the Tenants submodule pin is updated in this repo on merge.
- **Option 1 (selected)**: smallest BMad surface for a coordinated cross-repo bug-cluster fix. A single story with explicit subtasks per-bug, two PRs (one per repo), and one bump of the `Hexalith.Tenants` submodule pin in this repo to incorporate Fix B.

**Effort estimate:** Moderate — ~3-5 hours total: ~15 min for A+C (already done), ~2-3 h for B (new projection handler + Tier 1 tests + DI wiring + Tier 2 verification), ~1-2 h for D (refactor three methods + Tier 1 tests).
**Risk level:** Low-to-moderate — additive projection handler (no risk on existing data), JSON serializer config (additive converter, accepts integers and strings via default `JsonStringEnumConverter` policy), error-path refactor (changes status codes on already-broken paths only).
**Timeline impact:** None blocking; can ship as a single PR cluster in next sprint.

---

## Section 4: Detailed Change Proposals

### 4.1 EDIT — `Tenants.razor` Create Tenant dialog inputs immediate-bind (Bug A)

**File:** `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`

**Status:** ✅ Already applied locally in this session, awaiting commit.

```razor
<FluentTextInput @bind-Value="_createTenantId"
                 Immediate="true"
                 Label="Tenant ID" Required="true"
                 ... />
<FluentTextInput @bind-Value="_createName"
                 Immediate="true"
                 Label="Name" Required="true"
                 ... />
<FluentTextInput @bind-Value="_createDescription"
                 Immediate="true"
                 Label="Description"
                 ... />
```

**Rationale:** Restores Fluent UI v4 binding ergonomics in the v5 codebase. The `_pendingShowCreate` + `OnAfterRenderAsync` dialog activation pattern remains correct; the issue is solely the input bind cadence.

**Follow-up audit (in scope, additive):** verify the same `Immediate="true"` is applied to the Add User / Change Role / similar dialogs across `Tenants.razor`, `Backups.razor`, `Snapshots.razor`, `Compaction.razor`, `Consistency.razor`, `DeadLetters.razor` if they exhibit the same disabled-submit symptom. Greppable via `<FluentTextInput.*@bind-Value` with no `Immediate="true"` neighbor.

### 4.2 EDIT — `DaprTenantQueryService._options` accepts string enums (Bug C)

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

**Status:** ✅ Already applied locally in this session, awaiting commit.

```csharp
using System.Text.Json.Serialization;  // added

private static readonly JsonSerializerOptions _options = new() {
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() },  // added
};
```

**Rationale:** The upstream `Hexalith.Tenants.TenantsProjectionActor` already uses `JsonStringEnumConverter` (lines 30-32 of `TenantsProjectionActor.cs`). The admin server's deserializer must match. `JsonStringEnumConverter` with default constructor is `allowIntegerValues: true` so backward compatibility with any old numeric-encoded payload is preserved. Zero risk on existing call paths.

**Tests (Tier 1, ~30 min):**
- `DaprTenantQueryServiceTests.cs` — add a test that mocks an upstream payload with `"status": "Active"` (string) and verifies `ListTenantsAsync` returns parsed items (currently throws).
- Same test with `"status": 0` (integer) verifying back-compat.

### 4.3 NEW — `GlobalAdministratorProjectionHandler` in `Hexalith.Tenants` (Bug B)

**Repo:** `Hexalith.Tenants` (submodule).

**File (new):** `Hexalith.Tenants/src/Hexalith.Tenants/Projections/GlobalAdministratorProjectionHandler.cs`

**Implementation outline:**

```csharp
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Tenants.Contracts.Events;
using Hexalith.Tenants.Server.Projections;

namespace Hexalith.Tenants.Projections;

public sealed class GlobalAdministratorProjectionHandler(DaprClient daprClient) {
    private const string StateStoreName = "statestore";
    private const string GlobalAdminProjectionKey = "projection:global-administrators:singleton";

    private static readonly JsonSerializerOptions s_options = new() {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ProjectionResponse> ProjectAsync(ProjectionRequest request) {
        ArgumentNullException.ThrowIfNull(request);

        GlobalAdministratorReadModel state = new();
        foreach (ProjectionEventDto? evt in request.Events ?? []) {
            if (evt is null) { continue; }
            ApplyEvent(state, evt);
        }

        await daprClient.SaveStateAsync(
            StateStoreName,
            GlobalAdminProjectionKey,
            state).ConfigureAwait(false);

        return new ProjectionResponse(
            "global-administrators",
            JsonSerializer.SerializeToElement(state));
    }

    private static void ApplyEvent(GlobalAdministratorReadModel state, ProjectionEventDto evt) {
        string name = evt.EventTypeName;
        if (string.IsNullOrEmpty(name)) { return; }

        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(evt.Payload, s_options);

        if (name.EndsWith(nameof(GlobalAdministratorSet), StringComparison.Ordinal)) {
            GlobalAdministratorSet? e = JsonSerializer.Deserialize<GlobalAdministratorSet>(payload, s_options);
            if (e is not null) { state.Apply(e); }
        }
        else if (name.EndsWith(nameof(GlobalAdministratorRemoved), StringComparison.Ordinal)) {
            GlobalAdministratorRemoved? e = JsonSerializer.Deserialize<GlobalAdministratorRemoved>(payload, s_options);
            if (e is not null) { state.Apply(e); }
        }
    }
}
```

**DI wiring:** register the new handler alongside `TenantProjectionHandler` in the existing extension method that wires up handlers (location to be identified — same call site as `TenantProjectionHandler`). The DAPR projection dispatch routes events by domain to the matching handler based on `[EventStoreDomain(...)]` attribute or an equivalent mechanism. Verify the routing recognizes the new domain.

**Existing per-tenant projection key for `global-administrators`:** `projection:tenants:global-administrators` is currently written empty by the catch-all `TenantProjectionHandler` for the bootstrap event. Once Fix B is in, the per-tenant projection should either be skipped (filter the global-administrators domain out of `TenantProjectionHandler.ProjectAsync`) or kept as a degenerate no-op. **Decision needed during implementation** — either is acceptable. Recommendation: filter out, since the per-tenant projection of `global-administrators` is not used by any read query.

**Tests (Tier 1, ~1 h):**
- `GlobalAdministratorProjectionHandlerTests.cs` — given a stream of `GlobalAdministratorSet(adminA)`, verify the handler writes `{"administrators":["adminA"]}` to the state-store under `projection:global-administrators:singleton`.
- Given a stream of `GlobalAdministratorSet(A)` then `GlobalAdministratorSet(B)`, verify both are present.
- Given a stream of `GlobalAdministratorSet(A)` then `GlobalAdministratorRemoved(A)`, verify the singleton is empty.
- Given an empty event list, verify the handler writes an empty model (or skips — design call).

**Tests (Tier 2 integration, recommended but optional):** boot the AppHost with `BootstrapGlobalAdminUserId="admin-user"`, verify after bootstrap completes that `projection:global-administrators:singleton` contains `admin-user`, and that `IsGlobalAdminAsync("admin-user")` from a downstream caller returns true.

**Rationale:** Fixes the architectural gap that has been masked by the in-memory test infrastructure since the global-administrators domain was introduced. After this fix, the volatile Redis hack used in the debug session is no longer needed and should be removed (`HDEL projection:global-administrators:singleton` then verify bootstrap recreates it correctly).

### 4.4 EDIT — `DaprTenantQueryService` error-path handling (Bug D)

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

Refactor `GetTenantDetailAsync`, `ListTenantsAsync`, and `GetTenantUsersAsync` to inspect `SubmitQueryResponse.Success` (or the equivalent) before deserializing `Payload`. When the upstream signals failure, propagate it as a typed exception that the controller already maps to the right HTTP status code:

```csharp
public async Task<TenantDetail?> GetTenantDetailAsync(string tenantId, CancellationToken ct = default) {
    try {
        SubmitQueryResponse response = await SendQueryAsync(
            "tenants", tenantId, "get-tenant", null, ct).ConfigureAwait(false);

        if (!response.Success) {
            // Map upstream error message to typed exception
            if (string.Equals(response.ErrorMessage, "Forbidden", StringComparison.Ordinal)) {
                throw new ForbiddenAccessException("Access denied for this tenant.");
            }
            if (string.Equals(response.ErrorMessage, "Tenant not found", StringComparison.Ordinal)) {
                return null;
            }
            // Other errors → propagate as upstream failure
            _logger.LogWarning("Upstream query failed: {Error}", response.ErrorMessage);
            throw new InvalidOperationException(response.ErrorMessage ?? "Upstream query failed.");
        }

        ContractsTenantDetail? detail = response.Payload.Deserialize<ContractsTenantDetail>(_options);
        if (detail is null) { return null; }

        return new TenantDetail(detail.TenantId, detail.Name, detail.Description, MapStatus(detail.Status), detail.CreatedAt);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) {
        return null;
    }
}
```

Apply the same pattern to `ListTenantsAsync` (Forbidden → empty list returned to caller, since the route is permitted but visibility filtered) and `GetTenantUsersAsync` (Forbidden → ForbiddenAccessException, not-found → null).

**Controller mapping:** verify `AdminTenantsController.GetTenantDetail` already catches `ForbiddenAccessException` and maps to 403, and `null` return to 404. If not, add the catch arm.

**Tests (Tier 1, ~45 min):**
- `DaprTenantQueryServiceTests.cs` — given a mocked `SubmitQueryResponse` with `Success=false, ErrorMessage="Forbidden"`, verify `GetTenantDetailAsync` throws `ForbiddenAccessException` (not silently produces empty record).
- Same with `ErrorMessage="Tenant not found"`, verify returns null.
- Given `Success=true` with a valid payload, verify happy path still works.
- Same three cases for `ListTenantsAsync` and `GetTenantUsersAsync`.

**Rationale:** Closes the silent-failure cliff that has been masquerading as 503 transport errors. After this fix, the UI's "Tenants service is not responding" banner only appears for genuine 503s; Forbidden / NotFound get distinct, accurate UI states. Direct knock-on benefit: the intermittent "Tenant details unavailable" the user observed becomes either a stable 403 (consistent UX) or a stable 200 (after Bug B is fixed and `IsGlobalAdminAsync` returns true reliably). No more cache-dependent intermittence.

### 4.5 EDIT — `epics.md` and `sprint-status.yaml` follow-ups

**File:** `_bmad-output/planning-artifacts/epics.md`

Append, under each existing tenant story that the user touched (List, Detail, Create, User Management):

```markdown
**2026-05-05 follow-up (Sprint Change Proposal):** Live debug session uncovered a four-bug cluster in the Create / List / Detail / User-Management chain: missing `Immediate="true"` on Fluent UI v5 inputs (no POST sent), missing `GlobalAdministratorProjectionHandler` in `Hexalith.Tenants` (admin authorization always false), missing `JsonStringEnumConverter` on admin server query deserializer (response unparseable), and silent-failure cliff in `DaprTenantQueryService` query methods (Forbidden / NotFound produce default records that fail validation downstream and surface as 503). Fixed in consolidated story `tenant-management-debug-cluster-fix` (this session's local patches A and C are already applied to working tree; B and D require submodule + admin server work). See `sprint-change-proposal-2026-05-05-tenant-management-debug-cluster.md`.
```

**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

Insert under the appropriate tenant epic:

```yaml
  tenant-management-debug-cluster-fix: backlog
```

Status moves to `in-progress` when implementation starts, `review` when both PRs are open, `done` when both are merged and the submodule pin is bumped.

---

## Section 5: Implementation Handoff

**Scope:** Moderate — coordinated patches across two repositories. One consolidated story.
**Story ID:** `tenant-management-debug-cluster-fix`
**Dependencies:** Story 15.12b (already merged via `15895b8b`) — same debug session, separate concern.

**Sub-tasks (single story, four sub-tasks):**

| Sub-task | File(s) | Estimate | Status |
|---|---|---|---|
| ST1 — UI Immediate bind | `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor` | 5 min | ✅ Local edit applied, **commit pending** |
| ST2 — Admin server JSON enum converter | `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` | 5 min | ✅ Local edit applied, **commit pending** |
| ST3 — Global-administrators projection handler | `Hexalith.Tenants/src/Hexalith.Tenants/Projections/GlobalAdministratorProjectionHandler.cs` (new) + DI registration + filter `TenantProjectionHandler` to skip the global-administrators domain | 2-3 h | 🔴 To implement |
| ST4 — Admin server query error-path handling | `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` (three method refactors + Tier 1 tests) | 1-2 h | 🔴 To implement |

**Verification checklist:**

| # | Check | Status |
|---|-------|--------|
| 1 | `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 warnings, 0 errors | Pending |
| 2 | `dotnet build Hexalith.Tenants/Hexalith.Tenants.sln --configuration Release` (or csproj) — 0 warnings, 0 errors | Pending |
| 3 | `dotnet test Hexalith.EventStore.slnx` — Tier 1 green, including new `DaprTenantQueryServiceTests` cases for ST2 and ST4 | Pending |
| 4 | `dotnet test Hexalith.Tenants` — Tier 1 green, including new `GlobalAdministratorProjectionHandlerTests` for ST3 | Pending |
| 5 | Bump `Hexalith.Tenants` submodule pin in this repo to the merge commit of the ST3 PR | Pending |
| 6 | Live smoke-test on AppHost: type into Create Tenant and click without losing focus → POST is sent and toast appears | Pending |
| 7 | Live smoke-test: after a clean Redis wipe + AppHost reboot, `redis-cli HGETALL projection:global-administrators:singleton` returns `{"administrators":["admin-user"]}` populated by the bootstrap (no manual hack required) | Pending |
| 8 | Live smoke-test: `GET /api/v1/admin/tenants` with admin JWT returns 200 with parsed list (status as string accepted) | Pending |
| 9 | Live smoke-test: `GET /api/v1/admin/tenants/{id}` with non-admin JWT returns 403 (not 503) | Pending |
| 10 | Move `tenant-management-debug-cluster-fix` from `backlog` → `in-progress` → `review` → `done` | Pending |

**Deliverables:**

1. PR in `Hexalith.EventStore` covering ST1, ST2, ST4, plus updated `epics.md` / `sprint-status.yaml` / submodule pin.
2. PR in `Hexalith.Tenants` covering ST3.
3. Tests (Tier 1 in both repos; Tier 2 recommended for ST3).
4. The Redis hack (`HSET projection:global-administrators:singleton ...`) used during this debug session **must be cleared** as part of verification: `redis-cli HDEL projection:global-administrators:singleton` (or full Redis wipe) and then verify the bootstrap re-creates the singleton via the new handler.

**Follow-ups (separate stories, NOT in this patch):**

- **NFR — DAPR actor cold-start latency:** Each tenant action takes several seconds to a minute on first hit due to DAPR actor activation, state hydration from Redis, and Polly retry on transient timeouts. The user explicitly observed this: "ça prend bcp de temps pour chaque requete ça charge pdt longtemps apres le clique." Consider: actor warm-up at boot, longer DAPR client timeouts in admin server, perception fix via faster UI feedback (optimistic update + reconcile). Out of scope for the correctness fix above.
- **Audit other admin pages with `FluentTextInput`-based dialogs** (Backups / Snapshots / Compaction / Consistency / DeadLetters) for the same Bug-A-class symptom. Filed via grep `<FluentTextInput.*@bind-Value` in `src/Hexalith.EventStore.Admin.UI/Pages/` cross-checked for missing `Immediate="true"`. Not all dialogs may be affected (some submit on Enter, some intentionally batch on focus loss). One disciplined sweep, ~20 min.
- **Audit other admin server query services** (`DaprStreamQueryService`, `DaprAdminApiClient`, etc.) for the same Bug-D-class silent deserialization on Forbidden upstream. Companion to the audit kicked off by `sprint-change-proposal-2026-05-05-event-detail-endpoint-missing.md` Section 5.
- **Hexalith.Tenants branch `fix/domain-processor-mismatch-matcher` (commit `0fecb6c`)** is the current submodule pin. It fixes the orthogonal processor fall-through `MissingApplyMethodException` matcher (referenced in `SCP-2026-05-04 §B / story post-epic-2-r2a1`). Should be merged to `main` of `Hexalith.Tenants` as part of this story's PR cluster, since the submodule pin needs a stable commit to point to.

---

## Appendix A: Why this session repeatedly thought it was done and wasn't

The four bugs form a Russian-doll structure: each one's symptom (no toast, empty list, 503 banner, intermittent detail error) looks superficially identical from the UI ("the tenant feature doesn't work"), but they have four entirely different causes on three different layers. Fixing one only reveals the next. This is the "onion of compounding defects" anti-pattern: each layer's contract is locally correct under unit-test isolation, but the absence of an end-to-end integration test against a live DAPR + state-store environment means the contract gaps between layers go undetected until a user reports the surface symptom.

Three preventive practices come out of this session:

1. **End-to-end integration tests on the tenant create + list + detail path** that drive the real `eventstore-admin` HTTP surface against a real `tenants` service against a real Redis state-store. This would have caught all four bugs in a single CI run. Filed as a future Tier 3 follow-up. The pattern is similar to what `tests/Hexalith.EventStore.IntegrationTests/` already does for the EventStore API itself.
2. **Cross-layer contract tests for projection handlers**: for every domain that has events, verify there is *exactly one* projection handler registered for it, and that the handler writes a state-store key that downstream consumers can read. Bug B is visible from a one-line invariant: `domains_with_events ⊆ domains_with_projection_handlers`.
3. **Generic guard against silent default-record deserialization**: in any service like `DaprTenantQueryService` that consumes envelope-shaped responses, never call `Deserialize<T>(payload)` without first checking the envelope's success flag. This is a static-analyzer-detectable pattern; consider a small Roslyn analyzer rule or a code-review checklist item.

## Appendix B: Volatile Redis hack used during the debug session — must be cleaned up

During this session, to validate the diagnosis of Bug B before implementing the fix, the global-administrators projection key was injected manually into Redis via:

```bash
echo '{"administrators":["admin-user"]}' > /tmp/admin.json
docker cp /tmp/admin.json dapr_redis:/tmp/admin.json
docker exec dapr_redis bash -c 'cat /tmp/admin.json | redis-cli -x HSET projection:global-administrators:singleton data'
docker exec dapr_redis redis-cli HSET projection:global-administrators:singleton version 1
```

This hack is **volatile**: it survives container restarts of the host process but is wiped by any explicit Redis flushall or container recreation. It exists to validate that with `IsGlobalAdminAsync = true` the rest of the chain works. **It must be cleaned up after Fix B ships:**

```bash
docker exec dapr_redis redis-cli HDEL projection:global-administrators:singleton
# then restart the AppHost — the bootstrap should recreate the key via the new handler.
```

If the cleanup is forgotten and Fix B is later regressed, the hack will silently restore the broken happy path under a particular user's session, masking the regression. Note this in the story's verification checklist (item 7 above already covers it).
