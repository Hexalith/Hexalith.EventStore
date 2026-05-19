# Sprint Change Proposal — 2026-05-19

**Trigger:** `list-tenants` query fails with `NullReferenceException` in `QueryRouter` (Admin UI Tenants page blocked)
**Mode:** Batch
**Author:** Jerome (via bmad-correct-course)
**Status:** **Approved 2026-05-19** by Jerome (batch mode, no revisions) — handoff to bmad-agent-dev (Amelia)

---

## 1. Issue Summary

**Problem statement.** Every `POST /api/v1/queries` call carrying `QueryType=list-tenants` against the running EventStore (Aspire `aspire run` on `2026-05-19`) returns HTTP 500. The Admin UI Tenants section is therefore unusable — the Polly resilience pipeline retries 3× and ultimately surfaces a 502/timeout in `eventstore-admin-ui` after ~30s. All other admin endpoints (`/health`, `/streams`, `/types/aggregates`) respond 200.

**Discovery context.** Bug surfaced during runtime smoke validation of the live Aspire topology, performed through the Aspire MCP server immediately after Docker Desktop was started. No new story implementation was in flight when this was observed — but the affected code path (`Hexalith.EventStore.Server.Queries.QueryRouter`) was last touched by Story **22.2 — projection-adapter-contract-and-generic-query-actor-model** (merged 2026-05-13, status `done`).

**Issue category.**

- **Primary:** *Technical limitation discovered during runtime validation* — the DAPR SDK weak-typed `ActorProxy.InvokeMethodAsync<TRequest, TResponse>(...)` path NRE'd when invoked on a proxy created via `actorProxyFactory.CreateActorProxy<IProjectionActor>(...)`. The strong-typed dispatch proxy returned by the factory is initialized for Service Remoting only; its `ActorProxy` base members required by the weak/JSON path are uninitialized, so `is ActorProxy actorProxy` succeeds but `actorProxy.InvokeMethodAsync<TRequest,TResponse>(...)` throws.
- **Secondary cleanup:** The DAPR `configstore` component is **declared** (`src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml`) but is **never wired** into any sidecar in `HexalithEventStoreExtensions.cs`. The `dapr-configstore` health check therefore reports `Degraded` (by design — see Story 14.3 deployment guide), but the orphan YAML misleads anyone reading the DaprComponents folder.

**Evidence (collected via Aspire MCP `list_structured_logs` + `list_console_logs`, AppHost PID 34848).**

Console (`eventstore` resource):

```
EventId 1202 ERROR Hexalith.EventStore.Server.Queries.QueryRouter
Projection actor invocation failed: ActorId=tenants:system:index, QueryType=list-tenants
System.NullReferenceException: Object reference not set to an instance of an object.
   at Dapr.Actors.Client.ActorProxy.InvokeMethodAsync[TRequest,TResponse](
       String method, TRequest data, CancellationToken cancellationToken)
   at Hexalith.EventStore.Server.Queries.QueryRouter.RouteQueryAsync(
       SubmitQuery query, CancellationToken cancellationToken)
     in src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:line 62
```

Structured cascade (representative trace `4c73b3e`):

```
eventstore-admin-ui  GET https://eventstore-admin/api/v1/admin/tenants → 502 (3× retries) → Polly timeout 10s
eventstore-admin     AdminTenantsController.ListTenants → TenantQueryFailedException 'actor-exception'
                     POST http://.../v1.0/invoke/eventstore/method/api/v1/queries → 500
eventstore           QueryRouter.RouteQueryAsync → NRE inside Dapr.Actors.Client.ActorProxy
```

Health (also from MCP):

```
WARNING Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService
Health check dapr-configstore with status Degraded:
  'Dapr config store component configstore not found in metadata.'
```

---

## 2. Impact Analysis

### Epic impact

- **Epic 22 — Public Gateway and Downstream Integration Contracts** (`in-progress`):
  - Story 22.2 (`done`, 2026-05-13) introduced the generic projection-actor query model. Its production correctness is **functionally broken** for any caller that exercises the routed query path (`list-tenants` is the only call in the live topology that hits the new code today; other admin queries either short-circuit via direct state-store reads or hit different routes).
  - No reason to reopen Story 22.2 — instead, this is a typical **review-driven post-epic patch** matching the pattern already established by `post-epic-1-r1a1-*`, `post-epic-9-r9a1-*`, etc. and consistent with project memory `[[feedback_minimize_bureaucracy]]`.
- **All other epics (1–21):** Unaffected — they ship `done` and do not depend on the new `IProjectionActor` weak/JSON proxy invocation path.
- **Walking Skeleton readiness gate (`ws-1-clone-to-command-flow-walking-skeleton`, `backlog`):** Indirectly affected. WS-1 is the readiness prerequisite added by the 2026-05-17 readiness addendum; this NRE would block any WS-1 evidence capture that touches the Admin UI tenants page. WS-1 itself stays in scope.

### Story impact

- **Story 22.2 (done):** *Source of the regression* — leave `done` (already merged). Address via dedicated follow-up story.
- **No in-flight or upcoming Epic 22 stories** are blocked by this issue, because the four 22.7d-* children all merged on 2026-05-19 and `epic-22-retrospective: optional`.
- **Admin UI manual-test follow-up cluster (`tenant-management-debug-cluster-fix` and siblings, all `done`):** Their evidence captures predate the 22.2 generic actor model and therefore did not exercise the broken path.

### Artifact conflicts

| Artifact | Path | Conflict? | Action |
|---|---|---|---|
| PRD | `_bmad-output/planning-artifacts/prd.md` | **No** | None — gateway query contract still as specified; this is a runtime defect, not a requirement change. |
| Epics | `_bmad-output/planning-artifacts/epics.md` | **No** | None — Epic 22 narrative is unchanged. |
| Architecture | `_bmad-output/planning-artifacts/architecture.md` | **Minor** | Add a one-paragraph clarification in the QueryRouter/Projection Actor section: typed actor proxies built via `CreateActorProxy<T>` must not be cast back to `ActorProxy` for the weak/JSON path; create a non-typed proxy when the weak path is required (to carry per-call `CancellationToken`). |
| UX Design | `_bmad-output/planning-artifacts/ux-design-specification.md` | **No** | None — Tenants page UX is correctly specified; current failure is purely infrastructural. |
| Spec files | `_bmad-output/planning-artifacts/*spec-*.md` | **No** | None — no Epic 22 spec files reference this code path explicitly. |
| Project knowledge | `docs/reference/api/...`, `docs/guides/...` | **No (functional)**, **Yes (cleanup)** | The orphan `configstore.yaml` doesn't need a doc change, but the `DaprComponents/` folder should reflect what is actually wired. |

### Technical impact

- **Code (1 file, ~10 LOC delta):** `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs` — invocation strategy fix.
- **Test (1 new file ~80 LOC, 1 small edit):** `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` — coverage for the weak-path NRE regression.
- **AppHost cleanup (1 file deletion):** `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml` — remove orphan to match what `HexalithEventStoreExtensions.cs` actually wires.
- **Sprint tracker (1 file edit):** `_bmad-output/implementation-artifacts/sprint-status.yaml` — add new `post-epic-22-r22a1-query-router-actor-proxy-fix` row.
- **No DAPR component, no API contract, no public NuGet surface change.**

---

## 3. Recommended Path Forward

**Selected approach: Option 1 — Direct Adjustment (single focused story under Epic 22 follow-up).**

| Option | Viable? | Rationale |
|---|---|---|
| **Option 1 — Direct Adjustment** (single post-Epic-22 follow-up story) | **YES (recommended)** | Tight scope (1 prod file, 1 test file, 1 YAML delete). Matches the repo's existing `post-epic-N-rNaM-*` pattern. Honors `[[feedback_minimize_bureaucracy]]` — no multi-story decomposition. No timeline impact (≤½ day implement + review). |
| Option 2 — Rollback Story 22.2 | NOT viable | 22.2 has shipped; Stories 22.3–22.7d depend on its contracts. Rollback cost would dwarf the targeted fix. |
| Option 3 — MVP / PRD review | NOT viable | MVP and PRD scope unaffected; the gateway query contract is correct, only its current implementation needs a one-line invocation strategy change. |

**Effort estimate:** Low (≤½ day implement; review-driven-patch pass per CLAUDE.md). **Risk:** Low (no public contract change, change is isolated to a single private helper). **Reviewer attention:** ensure the new test fails on the *previous* `QueryRouter.cs:62` shape — i.e. the test must exercise the weak path and prove NRE is gone.

---

## 4. Detailed Change Proposals

### 4.1 New post-epic-22 follow-up story

**Story ID:** `post-epic-22-r22a1-query-router-actor-proxy-fix`
**Location:** `_bmad-output/implementation-artifacts/post-epic-22-r22a1-query-router-actor-proxy-fix.md` (to be created by `bmad-create-story` when the work starts)
**Status workflow:** `backlog → ready-for-dev → in-progress → review → done`

**Acceptance criteria (draft, to be finalized at story-creation time):**

1. `Hexalith.EventStore.Server.Queries.QueryRouter.RouteQueryAsync` no longer NRE's when routing a query whose `ProjectionActorType` is the default `ProjectionActor`.
2. Tier 1 unit test in `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` reproduces the previous failure with an `IActorProxyFactory` substitute that returns a proxy mirroring the strong-typed dispatch-proxy contract, and now asserts that the call completes successfully and the `QueryResult.Success` value is honored.
3. Tier 1 test asserts that the `CancellationToken` is still observed when the proxy throws `OperationCanceledException`, preserving the original intent of the weak-path cast at `QueryRouter.cs:62`.
4. Aspire smoke verification: with Docker Desktop + `aspire run`, the Admin UI `Tenants` page reaches `200` from `eventstore-admin` (record evidence under `_bmad-output/test-artifacts/post-epic-22-r22a1-*`).
5. Orphan `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml` removed (or moved to `archive/`), and `aspire run` no longer reports it as a stray YAML in `DaprComponents/`. The `dapr-configstore` health check remains `Degraded`-by-design — this is documented behavior per Story 14.3 deployment guide and is NOT in scope to flip.

### 4.2 Code change — `QueryRouter.cs` (the actual fix)

**File:** `src/Hexalith.EventStore.Server/Queries/QueryRouter.cs`

**Section:** lines 57–63 of `RouteQueryAsync` and lines 125–142 (`InvokeProjectionActorAsync` helper).

**OLD (current):**

```csharp
try {
    IProjectionActor proxy = actorProxyFactory.CreateActorProxy<IProjectionActor>(
        new ActorId(actorId),
        actorTypeName);

    QueryResult? result = await InvokeProjectionActorAsync(proxy, envelope, cancellationToken).ConfigureAwait(false);
    ...
}
...
private static Task<QueryResult> InvokeProjectionActorAsync(
    IProjectionActor proxy,
    QueryEnvelope envelope,
    CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();

    // DAPR strongly typed actor proxies are generated from the actor interface and derive from
    // ActorProxy. The actor interface method stays source-compatible while the weak proxy call
    // carries the route-level cancellation token to the DAPR invocation operation.
    if (proxy is ActorProxy actorProxy) {
        return actorProxy.InvokeMethodAsync<QueryEnvelope, QueryResult>(
            nameof(IProjectionActor.QueryAsync),
            envelope,
            cancellationToken);
    }

    return proxy.QueryAsync(envelope);
}
```

**NEW (proposed):**

```csharp
try {
    // Use the untyped ActorProxy overload so the SDK initializes the weak/JSON
    // invocation path (CreateActorProxy<TActorInterface> returns a DispatchProxy
    // wired only for Service Remoting; its ActorProxy base members for the weak
    // path are uninitialized and NRE when InvokeMethodAsync<TRequest,TResponse>
    // is called). This preserves per-call CancellationToken propagation that
    // IProjectionActor.QueryAsync(QueryEnvelope) cannot carry on its own.
    ActorProxy actorProxy = actorProxyFactory.CreateActorProxy(
        new ActorId(actorId),
        actorTypeName);

    QueryResult? result = await actorProxy
        .InvokeMethodAsync<QueryEnvelope, QueryResult>(
            nameof(IProjectionActor.QueryAsync),
            envelope,
            cancellationToken)
        .ConfigureAwait(false);
    ...
}
```

…and **delete** the `InvokeProjectionActorAsync` helper (no longer needed — the type-check shim was only there to preserve compile compatibility with the strong-typed proxy).

**Rationale.** The Dapr .NET SDK's `IActorProxyFactory.CreateActorProxy<TActorInterface>` returns a `DispatchProxy`-backed instance that derives from `ActorProxy` for type-system reasons but is initialized *only* for Service Remoting. The weak-typed `InvokeMethodAsync<TRequest, TResponse>(string, TRequest, CancellationToken)` path is a separate invocation route (JSON over HTTP) whose internal state is set by the **non-generic** `CreateActorProxy(ActorId, string)` overload. The pattern documented by the Dapr team for "weak/JSON path with cancellation" is to use the non-generic overload directly. The current code casting from the typed proxy down to `ActorProxy` and calling the weak path is the bug.

### 4.3 New test — `QueryRouterTests.cs`

**File:** `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` (new)

Add Tier 1 tests, structured per CLAUDE.md (xUnit + Shouldly + NSubstitute, no DAPR sidecar required):

1. `RouteQueryAsync_uses_untyped_actor_proxy_factory_overload_for_cancellation_threading` — substitutes `IActorProxyFactory.CreateActorProxy(ActorId, string)` and verifies the untyped overload is called exactly once with `actorId = "tenants:system:index"` and `actorTypeName = "ProjectionActor"` for a `list-tenants` query.
2. `RouteQueryAsync_returns_router_result_when_actor_proxy_returns_query_result` — substitute returns a `QueryResult` with serialized payload; assert `QueryRouterResult.Success == true` and payload bytes round-trip.
3. `RouteQueryAsync_returns_actor_exception_router_result_when_proxy_throws_unspecified` — substitute throws `InvalidOperationException`; assert `ErrorMessage == QueryAdapterFailureReason.ActorException` (i.e. unknown exceptions still surface as `actor-exception`, matching today's contract).
4. `RouteQueryAsync_propagates_OperationCanceledException` — substitute throws `OperationCanceledException`; assert exception flows through unchanged (preserves cancellation contract used by request scope).

These four tests pin down the contract surface of the fix and protect against regression of *either* the strong-typed misuse OR cancellation-token loss.

### 4.4 AppHost cleanup — remove orphan `configstore.yaml`

**File:** `src/Hexalith.EventStore.AppHost/DaprComponents/configstore.yaml`

**OLD:** entire file (21 lines, declares `configuration.redis` component named `configstore` scoped to `eventstore`).

**NEW:** *(file deleted)*

**Rationale.** The component is never loaded: `HexalithEventStoreExtensions.cs:78–83` adds only `statestore` and `pubsub` to the sidecar reference graph, so the `--resources-path` flags passed to each sidecar never include `configstore.yaml`. Sprint Change Proposal **2026-03-28** already defaulted `DomainServiceOptions.ConfigStoreName` to `null`, so production code does **not** call `DaprClient.GetConfiguration` by default. The remaining `dapr-configstore` health check is intentionally `Degraded`-by-design (see Story 14.3 deployment guide and `_bmad-output/test-artifacts/nfr-reliability.json`); deleting the orphan YAML removes a misleading file without changing runtime behavior.

If a future deployment *does* want to enable a config store, the operator declares one in their environment-specific overlay (Helm values / kustomize / aspirate), exactly as Story 14.3 documents. Keeping the orphan in `src/Hexalith.EventStore.AppHost/DaprComponents/` only obscures this.

### 4.5 Architecture doc — clarification

**File:** `_bmad-output/planning-artifacts/architecture.md`

**Section:** wherever QueryRouter / projection actor invocation is described.

**Add a short note** (one paragraph, in the existing voice):

> Projection actor invocation uses the Dapr SDK's **non-generic** `ActorProxy.CreateActorProxy(ActorId, string)` overload to obtain a proxy initialized for the weak/JSON invocation path. The non-generic overload is required because `QueryRouter` needs to thread a per-call `CancellationToken` (which `IProjectionActor.QueryAsync(QueryEnvelope)` does not accept). Strong-typed proxies returned by `CreateActorProxy<IProjectionActor>(...)` are wired only for Service Remoting and will NRE if cast to `ActorProxy` and invoked via `InvokeMethodAsync<TRequest, TResponse>(...)`.

### 4.6 Sprint tracker — `sprint-status.yaml`

**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Edit 1 — header note (line 38, `last_updated`):** Replace the current `last_updated` comment with a new entry that records the Sprint Change Proposal and the new post-epic-22 row.

**Edit 2 — add a row under Epic 22:** Insert after `epic-22-retrospective: optional`, in keeping with the established `post-epic-N-*` pattern:

```yaml
  # Post-Epic-22 Follow-Up (sprint-change-proposal-2026-05-19-queryrouter-actor-proxy-nre.md)
  # Story 22.2 generic projection-actor model NRE'd on the weak/JSON ActorProxy path.
  # Single focused follow-up: switch QueryRouter to the untyped CreateActorProxy overload,
  # add regression tests, remove orphan configstore.yaml.
  post-epic-22-r22a1-query-router-actor-proxy-fix: backlog
```

No other rows change.

---

## 5. PRD MVP Impact & High-Level Action Plan

**MVP impact:** **None.** The PRD's gateway query and tenant-management requirements are unchanged. This is a single-day implementation patch correcting a runtime defect introduced by Story 22.2 plumbing, not a scope change.

**Action plan (sequenced):**

1. Materialize `post-epic-22-r22a1-query-router-actor-proxy-fix.md` via `bmad-create-story` using §4.1's acceptance criteria.
2. Move the new row in `sprint-status.yaml` from `backlog → ready-for-dev`.
3. Developer agent (Amelia, `bmad-agent-dev`) implements §4.2 + §4.3 + §4.4 + §4.5. Move to `in-progress`, then `review`.
4. Run the project's standard adversarial code review (per CLAUDE.md "Code Review Process").
5. Capture Aspire smoke evidence proving Admin UI Tenants page returns 200.
6. Merge to `main`. Move story to `done`. Update `sprint-status.yaml` `last_updated` accordingly.

**Dependencies:** None — this fix is independent of WS-1 walking-skeleton readiness and of any Epic 22 retrospective work. Walking Skeleton WS-1 evidence capture, when it runs, will benefit from this fix.

---

## 6. Implementation Handoff

**Scope classification: MINOR.**

| Recipient | Responsibility |
|---|---|
| **bmad-agent-dev (Amelia)** | Materialize the story file, implement code/test/AppHost changes (§4.2–§4.4), update architecture note (§4.5), submit for code review. |
| **bmad-code-review (adversarial review per CLAUDE.md)** | Standard senior code review (Opus 4.7 1M, three layers). Specifically verify (a) the new Tier 1 tests fail on the *old* `QueryRouter.cs` shape, (b) `CancellationToken` still propagates through the new untyped overload, (c) the orphan YAML deletion does not break any test fixture (`tests/**` greppable). |
| **Operator (Jerome)** | Aspire smoke evidence capture once the patch lands locally; sign off on `done`. |

**Success criteria:**

- `dotnet test tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs` — all new tests green; full Server.Tests project unaffected.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` — clean (0 warnings, 0 errors).
- Live Aspire smoke: `aspire run` + open Admin UI → `Tenants` page renders without error; `eventstore` console logs no longer emit `EventId 1202` `Projection actor invocation failed: ... ActorId=tenants:system:index ... QueryType=list-tenants`.
- Conventional commit: `fix(server): use untyped actor proxy overload in QueryRouter to thread cancellation without NRE`.

---

## 7. Out of Scope (explicit)

- Reopening Story 22.2 — leave `done`. The contract surface is correct; only the invocation strategy needed to change.
- Wiring a real DAPR config store. The `Degraded` health-check status for `dapr-configstore` is documented and intentional (Story 14.3 deployment guide).
- Adding new query types or projection actor capabilities. This proposal is strictly a defect fix.
- Cross-cutting integration test changes for Story 22.x — already covered by their respective stories.

---

## 8. Approval Record

- [x] **Jerome:** approved as written (2026-05-19, batch mode via bmad-correct-course)
- [ ] **Jerome:** requested revisions (capture below)

_Revision notes:_ none.
