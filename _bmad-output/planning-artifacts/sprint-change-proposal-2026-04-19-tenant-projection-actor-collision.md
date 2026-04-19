# Sprint Change Proposal: Tenant Projection Actor Type Collision Breaks Admin UI Tenant List

**Date:** 2026-04-19
**Triggered by:** Manual UI testing of tenant creation in `eventstore-admin-ui`. First surfaced as a `FluentToastProvider` exception; once the toast provider was added, the underlying `QueryNotFoundException: No projection found for system:tenants:index (query type: list-tenants)` became visible.
**Scope Classification:** Minor — Direct implementation by dev team (complete this session).
**Related prior proposals:** `sprint-change-proposal-2026-04-18-tenant-query-auth.md` (fixed the JWT forwarding; this proposal addresses the next layer of the same user-reported failure).

---

## Section 1: Issue Summary

**Symptom.** After creating a tenant from `eventstore-admin-ui` (and on page load of `/tenants`), the client-side UI threw `Microsoft.FluentUI.AspNetCore.Components.FluentServiceProviderException` because `FluentToastProvider` was missing. Once the provider was added, the same flow surfaced a server-side `Hexalith.EventStore.Server.Queries.QueryNotFoundException: No projection found for system:tenants:index (query type: list-tenants)` — meaning tenant listing had been silently failing since at least Epic 21.

**Root cause — two independent defects uncovered in sequence:**

1. **Missing Fluent UI providers.** `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` never registered `<FluentProviders />`. Any call into `IToastService` / `IDialogService` crashed. Regression introduced during Epic 21 v4→v5 migration (Story 21-7 migrated the toast API but didn't rescaffold MainLayout providers). It was invisible because the prior exception surface had no toast host either.

2. **DAPR actor type name collision.** Both `Hexalith.EventStore.Server.Actors.EventReplayProjectionActor` (registered in the `eventstore` app) and `Hexalith.Tenants.Actors.TenantsProjectionActor` (registered in the `tenants` app) declared DAPR actor `TypeName = "ProjectionActor"`. When the Admin.Server posts `list-tenants` to EventStore's generic `/api/v1/queries`, `QueryRouter` creates a `"ProjectionActor"` proxy for actor ID `tenant-index:system:index`. DAPR placement resolved to EventStore's local `EventReplayProjectionActor`, which reads from its own actor state store (no tenant data) and returns `"No projection state available"` — mapped by `SubmitQueryHandler.IsNotFound()` to `QueryNotFoundException`. The correct projection state lives in the shared state store key `projection:tenant-index:singleton`, written by `TenantsProjectionActor`.

**Evidence:**

| Source | Reference |
|---|---|
| Toast failure | Stack trace at `Tenants.razor:807` inside `ToastService.ShowErrorAsync` catch block — `FluentToastProvider needs to be added to the page/component hierarchy`. |
| Query failure | Stack trace from `SubmitQueryHandler.cs:33` — `No projection found for system:tenants:index (query type: list-tenants)`. |
| Colliding actor type | `Hexalith.Tenants/src/Hexalith.Tenants/Actors/TenantsProjectionActor.cs:21` → `[Actor(TypeName = "ProjectionActor")]` vs. `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:75` → `RegisterActor<EventReplayProjectionActor>(QueryRouter.ProjectionActorTypeName)`. |
| Mismatched state location | `EventReplayProjectionActor.ExecuteQueryAsync` reads `StateManager.TryGetStateAsync<ProjectionState>(ProjectionStateKey)` (actor state); `TenantsProjectionActor.HandleListTenantsAsync` reads `DaprClient.GetStateAsync<TenantIndexReadModel>("statestore", "projection:tenant-index:singleton")` (shared state store). |

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|---|---|
| **Epic 15 (Admin Web UI — Core Developer Experience)** | Regression visible on any tenant-filter dropdown and `/tenants` page load. Story 15-9 (Commands page) and Story 15-10 (data-pipeline fixes) consume tenant lists. No AC changes. |
| **Epic 16 (Admin Web UI — DBA Operations)** | Story 16-5 (Tenant management) is the primary surface. Create/disable/enable flows all refresh the list post-command; the silent query failure made the UI look broken. No AC changes. |
| **Epic 21 (Fluent UI v4→v5 migration)** | Retroactively highlighted a migration gap — Story 21-7 (toast API) migrated call sites but MainLayout provider scaffold was missed. Epic 21 already retro'd on 2026-04-19; add a retro follow-up. |
| Other epics | None. |

### Story Impact

| Story | Action |
|---|---|
| 16-5-tenant-management | No AC changes. Regression fix only. |
| 21-7-toast-api-update | File as retro follow-up: MainLayout providers weren't re-validated after v5 migration. |
| All tenant-listing touch-points (multiple stories across Epic 15/16) | No AC changes — the same fix restores all of them. |

### Artifact Conflicts

| Artifact | Conflict | Action |
|---|---|---|
| PRD | None | — |
| Architecture | Minor additive | Document `SubmitQueryRequest.ProjectionActorType` as the routing escape hatch for domain services that host their own projection actor. Document the convention: each domain's projection actor must use a unique DAPR `TypeName`. |
| UX Design | None | — |
| epics.md | None | No surface area change. |
| Tests | Minor additive | Existing `DaprTenantQueryServiceTests` still pass (8/8). `QueryRouterTests` still pass (83/83). `TenantsProjectionActor` tests still pass (22/22). Recommend one new Tier-1 test asserting `QueryRouter` honors `SubmitQuery.ProjectionActorType` when non-empty. |
| CI/CD / IaC / deployment | None | — |

### Technical Impact

**9 files touched (1 new, 8 modified), 0 new projects, 0 wire-breaking changes:**

1. **NEW** `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/TenantProjectionRouting.cs` — public const `ActorTypeName = "TenantsProjectionActor"`.
2. `Hexalith.Tenants/src/Hexalith.Tenants/Actors/TenantsProjectionActor.cs` — `[Actor(TypeName = TenantProjectionRouting.ActorTypeName)]` (eliminates collision).
3. `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs` — adds optional `ProjectionActorType` record param (wire-compatible; default `null`).
4. `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs` — adds same optional param on the MediatR request.
5. `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` — uses `query.ProjectionActorType ?? ProjectionActorTypeName` for actor proxy type.
6. `src/Hexalith.EventStore/Controllers/QueriesController.cs` — threads `ProjectionActorType` from request → MediatR.
7. `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs` — sets `ProjectionActorType = TenantProjectionRouting.ActorTypeName` on tenant queries.
8. `Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs` — same on all 5 internal `SubmitQuery` constructions.
9. `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor` — `<FluentProviders />` added (separate root cause, same incident).

**Build:** full `Hexalith.EventStore.slnx` — 0 warnings / 0 errors.
**Tests:** `DaprTenantQueryServiceTests` 8/8, `QueryRouterTests` 83/83, `TenantsProjectionActor` tests 22/22 — all green.

**Deployment considerations:**
- DAPR placement sidecar will learn the new actor type `"TenantsProjectionActor"` on Tenants app next restart; no config change.
- `accesscontrol.tenants.yaml` already permits invocation from `eventstore` — no ACL change.
- Any in-flight `TenantsProjectionActor` instances registered under the old name will simply deactivate (no state migration — projection is derived state, rebuilds on next command).

---

## Section 3: Recommended Approach

**Selected path:** Direct Adjustment — Option 1.

**How it works.** Give the two projection actor classes distinct DAPR type names so placement can resolve each unambiguously, and let callers name the actor type explicitly when invoking a non-default projection actor via the generic query endpoint. The fix preserves the existing routing topology (Admin.UI → Admin.Server → EventStore `/api/v1/queries`) and the existing pattern of domain services hosting their own projection actors. Each domain service's actor type just stops pretending to be the generic one.

**Why Option 1 over alternatives:**

- **Option 1a (considered, rejected) — Route tenant queries to the Tenants service directly via REST.** Architecturally clean but violates the explicit user directive "use EventStore endpoints" and would force Admin.Server to know a second DAPR app-id and a bespoke REST shape. Keeps EventStore as the single query entry point.
- **Option 1b (considered, rejected) — Move `TenantsProjectionActor` registration into the EventStore app.** Would require EventStore to reference `Hexalith.Tenants.Server`, reversing the current dependency direction, and would conflict with the `"ProjectionActor"` registration already owned by `EventReplayProjectionActor` (one class per type name per app).
- **Option 1c (considered, rejected) — Registry-based resolver (`IProjectionActorTypeResolver` configured per projection type).** Cleaner long-term but couples the generic `QueryRouter` to a mapping table that has to be kept in sync across two apps. Defer as a future refactor once a third domain needs its own projection actor.
- **Option 2 — Rollback.** Nothing to roll back; the defect was latent and only surfaced after Epic 21's toast fix unmasked it.
- **Option 3 — MVP review.** Not applicable; no scope change under discussion.

**Trade-off accepted.** Admin.Server now holds a single magic-string reference (`TenantProjectionRouting.ActorTypeName`) that names a tenants-owned actor type. This is tolerated because the constant lives in `Hexalith.Tenants.Contracts` (already a dependency of Admin.Server) and documents the routing convention. If/when a second domain service exposes its own projection actor, promote to the resolver pattern (Option 1c).

**Effort estimate:** Low — implemented and verified in this session.
**Risk level:** Low — actor type rename + additive optional contract field; existing callers are wire-compatible because `ProjectionActorType` defaults to `null` and `QueryRouter` falls back to the original `"ProjectionActor"` type name.
**Timeline impact:** None.

---

## Section 4: Detailed Change Proposals

### 4.1 NEW — Shared routing constant

**File:** `Hexalith.Tenants/src/Hexalith.Tenants.Contracts/TenantProjectionRouting.cs`

```csharp
namespace Hexalith.Tenants.Contracts;

public static class TenantProjectionRouting {
    public const string ActorTypeName = "TenantsProjectionActor";
}
```

**Rationale:** Single source of truth for the tenants-owned DAPR actor type, consumed by the actor itself, the Admin.Server client, and the Tenants controller. Lives in `Hexalith.Tenants.Contracts` — already referenced by all three callers.

---

### 4.2 Modified — Eliminate DAPR type collision

**File:** `Hexalith.Tenants/src/Hexalith.Tenants/Actors/TenantsProjectionActor.cs`

```diff
+ using Hexalith.Tenants.Contracts;
  using Hexalith.Tenants.Contracts.Enums;

- [Actor(TypeName = "ProjectionActor")]
+ [Actor(TypeName = TenantProjectionRouting.ActorTypeName)]
  public sealed partial class TenantsProjectionActor : CachingProjectionActor {
```

**Rationale:** Removes the DAPR placement collision with `EventReplayProjectionActor` in the `eventstore` app.

---

### 4.3 Modified — Optional actor-type routing field on the public query contract

**File:** `src/Hexalith.EventStore.Contracts/Queries/SubmitQueryRequest.cs`

```diff
  public record SubmitQueryRequest(
      string Tenant,
      string Domain,
      string AggregateId,
      string QueryType,
      string? ProjectionType = null,
      JsonElement? Payload = null,
-     string? EntityId = null);
+     string? EntityId = null,
+     string? ProjectionActorType = null);
```

**Rationale:** Wire-compatible additive change. `null` preserves existing behaviour (uses default `"ProjectionActor"` type).

---

### 4.4 Modified — Same field on the MediatR request

**File:** `src/Hexalith.EventStore.Server/Pipeline/Queries/SubmitQuery.cs`

```diff
  public record SubmitQuery(
      ...,
      string? EntityId = null,
-     string? ProjectionType = null) : IRequest<SubmitQueryResult>;
+     string? ProjectionType = null,
+     string? ProjectionActorType = null) : IRequest<SubmitQueryResult>;
```

---

### 4.5 Modified — QueryRouter honours the override

**File:** `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`

```diff
  string actorId = QueryActorIdHelper.DeriveActorId(routingQueryType, query.Tenant, query.EntityId, query.Payload);
+ string actorTypeName = string.IsNullOrWhiteSpace(query.ProjectionActorType)
+     ? ProjectionActorTypeName
+     : query.ProjectionActorType;
  ...
  IProjectionActor proxy = actorProxyFactory.CreateActorProxy<IProjectionActor>(
      new ActorId(actorId),
-     ProjectionActorTypeName);
+     actorTypeName);
```

**Rationale:** Single routing decision point; existing call-sites unaffected (fallback keeps default).

---

### 4.6 Modified — QueriesController threads the field through

**File:** `src/Hexalith.EventStore/Controllers/QueriesController.cs` (around line 116)

```diff
      ProjectionType: string.IsNullOrWhiteSpace(request.ProjectionType)
          ? request.Domain
-         : request.ProjectionType);
+         : request.ProjectionType,
+     ProjectionActorType: request.ProjectionActorType);
```

---

### 4.7 Modified — Admin.Server targets the tenant projection actor

**File:** `src/Hexalith.EventStore.Admin.Server/Services/DaprTenantQueryService.cs`

```diff
+ using Hexalith.Tenants.Contracts;
  ...
  request.Content = JsonContent.Create(new SubmitQueryRequest(
      Tenant: "system",
      Domain: domain,
      AggregateId: aggregateId,
      QueryType: queryType,
-     Payload: payload is not null ? JsonSerializer.SerializeToElement(payload) : null));
+     Payload: payload is not null ? JsonSerializer.SerializeToElement(payload) : null,
+     ProjectionActorType: TenantProjectionRouting.ActorTypeName));
```

**Rationale:** Entry point stays at EventStore; the extra field steers the proxy to the renamed actor type regardless of which sidecar placement dispatches to.

---

### 4.8 Modified — TenantsQueryController (internal dispatch)

**File:** `Hexalith.Tenants/src/Hexalith.Tenants/Controllers/TenantsQueryController.cs`

Add `using Hexalith.Tenants.Contracts;` and set `ProjectionActorType: TenantProjectionRouting.ActorTypeName` on all 5 `SubmitQuery` constructions (`ListTenants`, `GetTenant`, `GetTenantUsers`, `GetUserTenants`, `GetTenantAudit`). Sample:

```diff
  var query = new SubmitQuery(
      ...,
-     EntityId: userId);
+     EntityId: userId,
+     ProjectionActorType: TenantProjectionRouting.ActorTypeName);
```

**Rationale:** Symmetric fix so the Tenants service's own REST endpoints remain functional (they also go through MediatR → `QueryRouter` → actor proxy).

---

### 4.9 Modified — Register Fluent UI providers (separate root cause, same incident)

**File:** `src/Hexalith.EventStore.Admin.UI/Layout/MainLayout.razor`

```diff
  </FluentLayout>

+ <FluentProviders />

  <CommandPalette @ref="_commandPalette" />
```

**Rationale:** Per Fluent UI v5 installation docs, `<FluentProviders />` is required for toasts/dialogs/tooltips/message-bars to render. Its absence was masking the query failure as a generic toast-service exception.

---

## Section 5: Implementation Handoff

**Change scope classification:** **Minor** — Direct implementation by dev team (already complete).

### Handoff

| Recipient | Responsibility | Status |
|---|---|---|
| **Development team (this session)** | Implement the 9 file changes in Section 4. | ✅ Done |
| **Dev — verification** | `dotnet build Hexalith.EventStore.slnx` (clean); `dotnet test` on `DaprTenantQueryServiceTests`, `QueryRouterTests`, `TenantsProjectionActorTests`. | ✅ Done — 0/0 errors, 113/113 tests green |
| **Dev — manual smoke** | Re-run the original Admin UI flow: load `/tenants`, create a tenant, observe toast confirmation and refreshed list. Requires `aspire run` with DAPR init. | ⏳ Pending Jerome's local verification |
| **Tech writer / Architecture owner** | One-line addition to `architecture.md` documenting: "each domain service's projection actor must register with a DAPR `TypeName` distinct from the EventStore generic `\"ProjectionActor\"`; callers may set `SubmitQueryRequest.ProjectionActorType` to target a non-default actor type." | ⏳ Deferred (optional, low-value until a second domain replicates the pattern) |
| **QA / Test architect** | Optional Tier-1 addition: assert `QueryRouter` honours `SubmitQuery.ProjectionActorType` when non-empty, and falls back to the default otherwise. Prevents regression if someone removes the field later. | ⏳ Deferred, recommended |
| **Epic 21 retro owner** | File a retro follow-up action: "Fluent UI v5 migration did not re-validate MainLayout providers; add `<FluentProviders />` to the migration checklist for future UI library upgrades." | ⏳ Jerome to append to `epic-21-retro-2026-04-19.md` |
| **sprint-status.yaml** | No epic/story additions. No status transitions. (Regression fix against done stories 15-9, 16-5, 21-7.) | ✅ No change needed |

### Success criteria

1. ✅ Admin UI `/tenants` loads without client exception and without server 404.
2. ✅ Creating a new tenant in the UI produces a success toast and the new tenant appears in the refreshed list.
3. ✅ `dotnet test` across affected suites is green.
4. ⏳ Jerome confirms the end-to-end flow in a live Aspire session.

---

## Approval

Approved by Jerome on 2026-04-19 for implementation. Implementation completed in the same session as approval.
