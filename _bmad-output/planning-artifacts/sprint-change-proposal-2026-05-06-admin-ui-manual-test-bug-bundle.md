# Sprint Change Proposal - Admin UI Manual-Test Unblocker

**Date:** 2026-05-06
**Triggered by:** End-to-end manual test of the Admin UI using `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`. Five issues surfaced before reaching `/health` section 3.6.
**User outcome protected:** An admin can inspect seeded event streams and trust what the Admin UI says.
**Scope classification:** Manual-test unblocker with correctness blockers and interaction polish. No NuGet contract changes.
**Risk classification:** High overall. Two blocking issues silently return wrong or incomplete operational data.
**Mode:** Revised batch proposal. Implement as split work lanes with explicit acceptance gates, not as one vague catch-all story.
**Source data:**
- Issue inventory: `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues.md`
- Test guide: `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`

This is scoped to unblock credible manual validation in the Admin UI. It is not a full Admin UI trust/security hardening program.

## Why Now

Manual testing is blocked because the Admin UI is not yet a trustworthy evidence surface. For tenant-scoped EventStore behavior, the UI must only display aggregate state, tenant lists, metrics, and copyable values when they are backed by authorized, unambiguous source data. Missing or ambiguous data must be shown as unavailable, not inferred, merged, or defaulted.

First principles:

1. The same ordered event stream must produce the same aggregate state everywhere.
2. Tenant and metric data shown by the Admin UI must come from authorized, source-backed paths.
3. Manual testing must not be blocked by misleading UI behavior.

Decision summary:

- State replay uses a shared replay contract, not Admin-specific merge logic.
- Tenant discovery comes from authorization-enforcing sources.
- Metrics distinguish real zero from unavailable, stale, loading, and error states.
- Copy actions are isolated from row navigation and neighboring rows.
- Timeline type filtering remains deferred and absent.

---

## Section 1: Issue Summary

Five issues were observed during sequential testing of `/`, `/commands`, `/events`, `/streams`, and `/streams/{tenant}/{domain}/{aggregate}`.

Canonical seed scenario:

- Tenant: `tenant-a`
- Domain: `counter`
- Aggregate: `counter-1`
- Events: 18 total
- Sequence: 5 increments, 2 decrements, reset, 10 increments
- Expected final state: `Count = 10`, `IsTerminated = false`
- Evidence source: events visible in Redis

### Group A - State Inspection Silently Returns `{}` (BLOCKING)

**Issue #5 - `ReconstructState` deep-merges raw payloads instead of running aggregate `Apply()`**

`src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1477-1497`

Current behavior:

```csharp
private static JsonObject ReconstructState(ServerEventEnvelope[] allEvents, long upToSequence) {
    var state = new JsonObject();
    foreach (ServerEventEnvelope evt in allEvents) {
        if (evt.SequenceNumber > upToSequence) break;
        var eventPayload = JsonNode.Parse(evt.Payload, ...);
        if (eventPayload is JsonObject payloadObj) {
            DeepMerge(state, payloadObj);   // merges raw payloads
        }
    }
    return state;
}
```

The implementation bypasses aggregate `Apply(Event)` methods and deep-merges raw JSON payloads. This fails for marker events whose state transition is encoded by event type, not payload fields.

Sample marker events:

```csharp
public sealed record CounterIncremented : IEventPayload;   // payload = {}
public sealed record CounterDecremented : IEventPayload;   // payload = {}
public sealed record CounterReset      : IEventPayload;    // payload = {}
public sealed record CounterClosed     : IEventPayload;    // payload = {}
```

`CounterState.Apply(CounterIncremented) => Count++` is what should produce `Count = 10` after 18 events. The current implementation returns `{}` because every marker payload is `{}`.

Affected tools:

| Tool | Endpoint | Observed result | Expected result |
|---|---|---|---|
| Step Through | `GET /api/v1/admin/streams/{...}/state?at=N` | `{}` for all N | Real state at sequence N |
| Blame Viewer | `GET /api/v1/admin/streams/{...}/blame` | "Aggregate state has no fields at this position" | Fields bound to source events |
| StateDiffViewer | `GET /api/v1/admin/streams/{...}/diff?fromSequence=...` | Empty diff | Real field-level diff |
| Bisect | `GET /api/v1/admin/streams/{...}/bisect?good=...&bad=...` | All states equal `{}` | First divergent event located |
| Sandbox | `POST /api/v1/admin/streams/{...}/sandbox` | Produced event is correct; resulting state is `{}` | Correct post-command state |
| CausationChainView | TBD | TBD | Verify after shared replay fix |

User-visible evidence:

- Blame at sequence 18: `Aggregate state has no fields at this position`
- Step Through at sequences 1 through 18: `{}` everywhere
- Sandbox: command accepted and produced `Hexalith.EventStore.Sample.Counter.Events.CounterIncremented`, but Resulting State was `{}`

Severity rationale:

The state-inspection cluster is the headline feature of `/streams/{tenant}/{domain}/{aggregate}`. The current UI gives a confident but false answer. This is worse than an explicit failure because an operator may conclude the aggregate is corrupt when the inspection path is wrong.

### Group B - Tenant Dropdowns Ignore Observed Event Data (BLOCKING)

**Issue #1 - Tenant filter dropdowns are empty across pages**

Every page that filters by tenant calls `GET /api/v1/admin/tenants`. The sample tenant `tenant-a` has events in Redis but is not registered in the Tenants service, so dropdowns show only `All Tenants`.

Confirmed pages:

- `/commands`
- `/events`
- `/streams`

Likely affected:

- `/projections`
- Any page using `AdminStreamApiClient.GetTenantsAsync()` or `AdminTenantApiClient.GetTenantsAsync()`

Relevant code:

- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:240-251`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs:123-127`
- Same load pattern in `Events.razor`, `Streams.razor`, and `Projections.razor`

Severity rationale:

Filtering by tenant is a core operation in a multi-tenant event store. A fresh `aspire run` produces data the UI cannot discover unless the user manually registers the tenant first.

### Group C - Home Dashboard Shows Hardcoded Zero Metrics (VISIBLE REGRESSION)

**Issue #2 - `DaprHealthQueryService` returns zero metrics regardless of data**

`src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:130-136`

```csharp
return new SystemHealthReport(
    overallStatus,
    TotalEventCount: 0,       // stub
    EventsPerSecond: 0,       // stub
    ErrorPercentage: 0,       // stub
    components,
    links);
```

The DTO contract exists, but metric aggregation was not wired. `Index.razor:50` gates the ActivityChart and Recent Streams section on `_healthReport.TotalEventCount > 0`, so the dashboard always renders the empty state.

Important correction from review:

Do not replace hardcoded zeros with fake precision. Each metric must have a named source of truth, freshness expectation, and fallback behavior when data is unavailable.

### Group D - Interaction Polish (LOWER RISK)

**Issue #3 - Aggregate ID cell click both copies and navigates**

`src/Hexalith.EventStore.Admin.UI/Pages/Streams.razor:51-53`

```razor
<span class="monospace grid-cell-truncate" title="@context.AggregateId"
      style="cursor: pointer;"
      @onclick="() => CopyAggregateId(context.AggregateId)">@TruncateId(context.AggregateId)</span>
```

The nested click bubbles into the DataGrid row click handler and triggers navigation. Add event propagation control and audit other grids with the same pattern.

**Issue #4 - StreamDetail timeline has no event-type-name filter (ENHANCEMENT, DEFER)**

`src/Hexalith.EventStore.Admin.UI/Components/TimelineFilterBar.razor` supports kind filtering and correlation-prefix search, but not event or command type-name filtering.

Decision:

Defer this to a separate enhancement unless the fixed manual guide still cannot inspect the 18-event seed stream without it.

Deferred means absent, not partially wired. This proposal must not add hidden or incomplete type-name filtering behavior.

### User-Visible Behavior

| Area | Before | After | Why it matters |
|---|---|---|---|
| State replay | Aggregate state can show `{}` for marker-event streams | State is derived from shared replay semantics or shown as failed/partial | Testers can trust stream debugging output |
| Tenant discovery | Authorized event data can exist while tenant filters show only `All Tenants` | Authorized registered and observed tenants are visible; unauthorized tenants are hidden | Testers can inspect the correct tenant scope |
| Metrics | Hardcoded zeros can appear as real values | Real zero is distinguishable from unavailable, stale, loading, or error | Testers do not mistake missing telemetry for healthy inactivity |
| Copy isolation | Copying an aggregate ID can navigate away or target the wrong row | Copy acts only on the intended row/field and does not trigger parent behavior | Testers can preserve context while collecting evidence |
| Timeline filter | No type-name filter | Still no type-name filter in this change | Deferred scope is explicit and not half-implemented |

---

## Section 2: Revised Scope

### Recommended Work Lanes

**Lane A - Correctness blockers**

1. Aggregate state reconstruction uses the same event replay semantics as the runtime.
2. Tenant dropdowns can discover authorized observed tenants without changing tenant lifecycle semantics.
3. Dashboard metrics stop lying. Implement only metrics with a clear source; otherwise show unavailable or approximate status explicitly.

**Lane B - Interaction polish**

4. Aggregate ID copy affordance does not navigate.
5. Timeline type-name filter is deferred.

These are trust-boundary correctness fixes needed for manual testing: state trust, tenant trust, metric trust, and click-target trust. The phrase does not imply broader security certification or complete production observability.

### Recommended Split

**Story 1 - Aggregate State Replay Correctness**

Owns Issue #5 only. This is the highest blast-radius item and deserves focused architecture, test fixtures, and failure semantics.

**Story 2 - Authorized Tenant Discovery**

Owns Issue #1. This should not change tenant registration semantics.

**Story 3 - Truthful Metrics, No Silent Defaults**

Owns Issue #2. Split or narrow if the source of `EventsPerSecond` or error rate is unavailable.

**Story 4 - Copy Click Isolation**

Owns Issue #3. Small, isolated, and safe to implement early.

**Deferred - Timeline Type Filter**

Owns Issue #4. Defer unless manual testing remains blocked.

Sequencing note:

Manual testing is unblocked only after Stories 1 and 2 are complete. Story 3 improves confidence in observed system health. Story 4 removes interaction friction. Timeline filtering remains deferred unless manual testing shows event volume prevents validation.

Capacity cut line:

If capacity is constrained, deliver Stories 1 and 2 first. Defer Story 3 only if metrics are not required for the manual-test script, and defer Story 4 only if copy behavior does not block the script.

### Sprint-Critical vs Deferrable

Sprint-critical:

- Aggregate state replay, because false state invalidates stream inspection.
- Authorized tenant discovery, because filters must expose data the tester is allowed to inspect.
- Copy click isolation, because accidental navigation corrupts the tester workflow.
- Truthful metrics only where misleading counts would invalidate manual validation.

Deferrable unless already cheap:

- Expanded metrics polish beyond source-backed truthfulness.
- Broad empty/error state polish beyond the listed failure contracts.
- Timeline type-name filtering.
- Fixture matrix expansion beyond cases that prove the acceptance criteria.

---

## Section 3: Architecture Decisions Required

### ADR-1 - Admin State Inspection Reuses Runtime Replay Semantics

Decision:

Admin state inspection must delegate to a shared aggregate/event replay service rather than implement reflection directly in the controller.

Candidate service:

```csharp
public interface IAggregateStateReconstructor
{
    Task<AggregateReconstructionResult> ReconstructAsync(
        StreamIdentity stream,
        IReadOnlyList<ServerEventEnvelope> events,
        long upToSequence,
        CancellationToken cancellationToken);
}
```

Required behavior:

- Uses the same aggregate discovery source as command processing.
- Uses stored event metadata/type name to resolve concrete event types. Payload shape must not be used to infer marker-event identity.
- Uses the same serializer options, type mapping, and version/upcaster behavior as the runtime path when available.
- Replays only events with `SequenceNumber <= upToSequence`.
- Uses stream sequence/version order only. It must not depend on arrival order, UI sort order, timestamp order, or grouped event-type order.
- Surfaces explicit degraded-result semantics instead of returning `{}` on failure.
- Does not retain deep-merge semantics as an aggregate-state fallback.

Failure contract:

The API must distinguish valid empty state from reconstruction failure.

At minimum, failed or partial reconstruction must include:

- `status`: `Succeeded`, `Partial`, or `Failed`
- `failedSequenceNumber`
- `failedEventType`
- `errorCategory`: `UnknownAggregateType`, `UnknownEventType`, `DeserializationFailed`, `ApplyHandlerMissing`, `ApplyFailed`, `UnsupportedVersion`, or `Unexpected`
- `message`: safe operator-facing summary

Controller rule:

`AdminStreamQueryController` may orchestrate request/response mapping, but must not own aggregate discovery, serializer/type mapping, or reflective replay logic.

Decision options considered:

- **UI-owned reconstruction adapter:** keeps the fix local, but risks drift from domain/runtime semantics.
- **Domain-owned replay service:** preferred. It prevents Admin UI drift, but requires a stable query/replay contract and explicit versioned event-handler behavior.
- **Strict handler replay:** preferred for correctness. It may expose missing or invalid historical handlers sooner.
- **Best-effort payload projection:** rejected for aggregate state because it can silently misrepresent state, which is the failure being fixed.

### ADR-2 - Tenant Dropdowns Are Discovery, Not Registration

Decision:

Tenant dropdown fallback should union registered tenants and authorized observed tenants, but must preserve provenance.

Rules:

- Tenant source precedence is: authenticated authorization context, then observed authorized tenants from API data, then explicit local/config fallback if one exists.
- Tenant discovery must be backed by an authorization-enforcing source. The UI must not fetch a broader tenant list and reduce it client-side as the primary authorization boundary.
- Registered tenant: comes from Tenants service.
- Observed tenant: comes from event/stream data visible to the current user.
- Observed-only tenants are selectable for filtering, but are not treated as registered lifecycle entities.
- UI should distinguish observed-only tenants from registered tenants when feasible.
- Unauthorized observed tenants must not leak through dropdown options.
- Empty state must explain the situation instead of showing only `All Tenants`.
- Partial authorization failures must not collapse into a misleading empty dropdown or leak raw resource existence. The UI should show an authorization/freshness diagnostic appropriate for operators.

Recommended empty message:

`No tenants found yet. Send a command, register a tenant, or check your tenant permissions.`

### ADR-3 - Dashboard Metrics Need Named Sources

Decision:

Dashboard metrics must be truthful before they are precise.

Metric rules:

- `TotalEventCount`: acceptable if derived from an existing bounded stream summary/read model. Avoid scanning unbounded storage on every health request.
- `EventsPerSecond`: only implement if event timestamps and a deterministic rolling window source are available. Use an injectable clock for tests.
- `ErrorPercentage`: rename or clarify if it is command rejection/failure rate rather than infrastructure error rate.
- If a metric source is unavailable, stale, unauthorized, failed, or still loading, return an explicit unavailable/null/unknown/stale/loading state and render that honestly in the UI.
- Never fallback to `0` unless zero is the actual measured value for the named source and selected scope.
- Each metric must define source, unit, calculation owner, refresh cadence/freshness, authorization behavior, and failure behavior.

### ADR-4 - Row Navigation and Embedded Actions Are Separate Interaction Contracts

Decision:

Embedded row actions such as copy, expand, select, navigate, and filter controls must not accidentally trigger parent row behavior.

Rules:

- Row navigation remains a row-level contract.
- Cell actions remain action-level contracts.
- Every embedded action in a clickable row needs explicit propagation behavior and interaction tests.

### Trust Boundary Rules

- Data allowed from Admin APIs: stream summaries, event envelopes, aggregate state/replay responses, tenant option data, and metric values returned by authorization-enforcing endpoints.
- Data required from authenticated context or authorization-enforcing APIs: tenant access, tenant visibility, selected tenant validity, and operator permissions.
- Data never inferred client-side as an authorization boundary: tenant access, cross-tenant visibility, whether missing metrics mean zero, whether failed replay means empty state, and whether observed tenant data implies tenant registration.
- Fixture data may prove rendering and empty states, but must never become runtime fallback display data when API calls fail or return incomplete data.
- The Admin UI must make uncertainty visible. Missing, unauthorized, failed, partial, or stale data must be distinguishable from valid empty or zero values.

---

## Section 4: Technical Impact

### Backend Changes

**Issue #5 - Aggregate reconstruction**

- Introduce or reuse a shared aggregate state reconstruction service.
- Replace `AdminStreamQueryController.ReconstructState` with delegation to that service.
- Resolve event type from stored metadata/type name, not payload shape.
- Test persisted logical event names separately from CLR type names.
- Invoke `Apply(TEvent)` using the same runtime conventions.
- Define and return failure/degraded reconstruction details.
- Fail visibly rather than returning plausible partial state when handler dispatch, deserialization, or version binding fails.
- Re-validate Step Through, Blame, StateDiff, Bisect, Sandbox, and CausationChainView.

**Issue #2 - Dashboard metrics**

- Replace hardcoded zero fields only where a metric has an available source of truth.
- Add deterministic time handling before implementing rolling metrics.
- Rename or clarify `ErrorPercentage` if the available data measures command rejection/failure rate.
- Reject UI-derived counters that silently mix stale projections with fresh command state.
- Label stale/projection-lagged values honestly or withhold them.

### Frontend Changes

**Issue #1 - Tenant filters**

- Add shared tenant option provider, for example `AdminTenantOptionsProvider`.
- Consume it from `Commands.razor`, `Events.razor`, `Streams.razor`, and `Projections.razor`.
- Deduplicate, normalize, and sort tenant options deterministically.
- Preserve provenance: registered vs observed-only.
- Filter observed tenants by current user authorization.
- Ensure selected tenant, authorized source, observed source, and query tenant can be traced in logs or test output.
- Handle auth-context refresh, login switch, token refresh, and tenant claim changes without leaking stale tenant options.

**Issue #3 - Copy bubbling**

- Add `@onclick:stopPropagation="true"` to the aggregate ID copy affordance.
- Prefer an accessible button or equivalent focusable control with clear label/tooltip.
- Audit all Razor components that combine DataGrid `OnRowClick` with nested `@onclick`.
- Verify pointer and keyboard activation both preserve the row/action separation contract.
- Provide copy success/failure feedback that is available without relying only on pointer interaction.

**Issue #4 - Timeline type-name filter**

- Deferred.
- When scheduled, define exact/contains behavior, case sensitivity, full vs short type-name matching, query-string persistence, clearing behavior, and interaction with existing filters.

---

## Section 5: Test Impact

### Shared Test Assets

Create a canonical seeded stream fixture for `tenant-a/counter/counter-1`.

Fixture rule:

Fixtures should exist only when they prove one of the trust-boundary acceptance criteria. Avoid growing this into a general demo-data project.

Fixture must define:

- Raw event list and type names.
- Sequence numbers.
- Expected state snapshots at selected sequences.
- Expected final state.
- Expected tenant discovery result.
- Expected dashboard summary after deterministic seeding, if dashboard metrics are in scope.
- Conflicting tenant data:
  - one tenant the user can see
  - one forbidden-but-existing tenant
  - one admin/service tenant if that role exists in the app model
- Cross-tenant lookalikes and duplicate aggregate identifiers across tenants.
- True zero metrics and unavailable metrics.
- Event streams whose replay would produce visibly different states if events were deep-merged incorrectly.
- At least one real or historically shaped event sequence, not only synthetic happy-path events, when historical data is available.

Minimum state checkpoints:

| Sequence | Expected state |
|---:|---|
| 0 | Initial state, or explicit not-created semantics |
| 1 | `Count = 1`, `IsTerminated = false` |
| 5 | `Count = 5`, `IsTerminated = false` |
| 7 | `Count = 3`, `IsTerminated = false` |
| 8 | `Count = 0`, `IsTerminated = false` |
| 18 | `Count = 10`, `IsTerminated = false` |
| >18 | Same as sequence 18, or explicit validation error if out-of-range is not supported |

### Automated Regression Requirements

**Issue #5**

- Unit or contract tests for event type resolution.
- Tests for persisted logical event-name mapping vs CLR type-name mapping.
- Contract tests for `Apply` method resolution:
  - supported visibility rules
  - overload ambiguity
  - inherited/base handlers if supported
  - missing handler behavior
  - async handler rejection if unsupported
- Integration test for `GET /api/v1/admin/streams/{...}/state?at=N` across multiple checkpoints.
- Acceptance paths for:
  - fresh aggregate from event stream
  - aggregate after intermediate snapshots/checkpoints, if snapshots/checkpoints are present in the runtime path
- Regression coverage for each affected surface, or proof that they share the same tested reconstruction service:
  - Step Through
  - Blame
  - StateDiff
  - Bisect
  - Sandbox
- Failure tests:
  - unknown aggregate type
  - unknown event type
  - malformed payload
  - `Apply` throws
  - obsolete/versioned payload
  - version/upcaster unsupported, if applicable
- Guard test proving the Admin aggregate reconstruction path cannot use deep-merge semantics as a fallback.
- Ordering test proving reconstruction uses stream sequence/version order, not timestamp, arrival, UI sort, or grouped type order.

**Issue #1**

- Unit tests for tenant option provider:
  - registered-only tenant
  - observed-only tenant
  - duplicate registered/observed tenant
  - unauthorized observed tenant excluded
  - registered-but-empty tenant included
  - stable sort and normalization
- Auth refresh tests:
  - login switch
  - token refresh
  - tenant claim changes
- Partial authorization failure test: some tenant/resource lookups fail without leaking forbidden resource existence or collapsing into a misleading empty state.
- bUnit smoke tests for tenant dropdown pages:
  - `/commands`
  - `/events`
  - `/streams`
  - `/projections`

**Issue #2**

- Unit tests for metric aggregation with deterministic input.
- Injected/test clock for rolling windows.
- Denominator behavior for zero accepted/rejected events.
- Test unavailable/null source behavior if a metric cannot be computed honestly.
- Contract tests for metric source name, unit, freshness, and authorization behavior.
- UI test proving loading state is shown before values and does not flash fake zeros.
- Stale/projection-lag test: stale values are labeled or withheld.
- Thin integration test only after source-of-truth is confirmed.

**Issue #3**

- bUnit test: clicking copy affordance copies and does not navigate/select row.
- bUnit test: clicking row still navigates/selects row.
- Keyboard interaction test if the copy control is focusable.
- Screen-reader-accessible success/failure feedback test if the UI exposes feedback.
- Propagation test: copy does not expand rows, trigger parent commands, mutate filters, or select the row.
- Audit test or explicit file checklist for all `OnRowClick` plus nested `@onclick` patterns.

**Issue #4**

- No tests in this bundle unless the enhancement is pulled back into scope.

---

## Section 6: Acceptance Criteria

### AC-1 - Manual-Test Unblocker Purpose

The manual guide can proceed through the affected pages using the seeded `tenant-a/counter/counter-1` stream without encountering the five recorded defects.

### AC-2 - State Reconstruction Correctness

For `tenant-a/counter/counter-1`, state reconstruction returns real aggregate state at multiple sequence checkpoints, including sequence 18 with:

```json
{
  "Count": 10,
  "IsTerminated": false
}
```

The old all-sequences-return-`{}` behavior is covered by automated regression tests.

The displayed or exposed replay evidence includes tenant, aggregate id, source event count/version, replay status, last applied event/version, and last event timestamp when available.

### AC-3 - State Reconstruction Failure Visibility

When reconstruction cannot be completed, the API and UI show an explicit failure or partial-result status with sequence number, event type, and error category. They must not silently return `{}` as if it were a valid state.

### AC-4 - Shared Replay Path

Step Through, Blame, StateDiff, Bisect, Sandbox, and CausationChainView either directly use the same tested reconstruction service or have dedicated regression coverage proving equivalent behavior.

Given the same aggregate/event stream, Admin UI and server-side/query replay produce structurally equivalent state through the shared replay path. No Admin-specific merge logic remains reachable for aggregate reconstruction.

### AC-5 - Tenant Discovery

Tenant dropdowns on `/commands`, `/events`, `/streams`, and `/projections` include authorized registered tenants and authorized observed tenants. They exclude unauthorized observed tenants.

If the user has no authorized tenants, or a tenant exists but is not authorized for that user, the UI must not show, copy, select, infer, or query that tenant's data.

### AC-6 - Tenant Provenance

Observed-only tenants are not treated as registered tenant lifecycle records. The UI or API model preserves enough provenance to distinguish registered from observed-only options.

### AC-7 - Dashboard Metrics

The home dashboard no longer renders hardcoded zero metrics when seeded event data exists. Each displayed metric has a named data source and deterministic tests. Metrics without a trustworthy source are rendered as unavailable, stale, loading, or approximate, not as precise fake values.

Truthful metrics means the UI never knowingly displays fabricated, stale, unauthorized, or cross-tenant counts as if they were current for the selected scope. It does not require perfect observability or complete reporting coverage.

The UI distinguishes `loading`, `empty`, `zero`, `unavailable`, `unauthorized`, `stale`, and `error` states for metrics and tenant-scoped data. Missing numeric data must not render as `0`.

### AC-8 - Copy Interaction

Clicking or keyboard-activating the aggregate ID copy affordance copies the value and does not navigate, select, expand, mutate filters, or trigger parent row commands. Clicking the row outside the copy affordance still navigates.

Copy actions must copy only the value from the intended row, tenant, and field. Manual tests must verify this with visually similar neighboring rows and duplicate aggregate ids across tenants.

### AC-9 - Automated Before Manual

Each included defect has automated regression coverage at the lowest reliable level before final manual verification is accepted.

### AC-10 - Manual Verification Evidence

After implementation, rerun the relevant sections of `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` and capture evidence in the guide or issue log.

### AC-11 - Manual Tester Script

Provide a short deterministic manual tester script using the fixture:

1. Select an authorized tenant.
2. Inspect replayed aggregate state at multiple sequence checkpoints.
3. Compare displayed metrics against their named source/freshness.
4. Copy the aggregate ID.
5. Confirm no accidental navigation or row-side effects occur.
6. If UI evidence disagrees with backend behavior, record tenant, aggregate id, selected tenant context, visible state, copied value if relevant, endpoint/request id or correlation id when available, timestamp, and screenshot.

Manual-test success statement:

A tester can select an authorized tenant, inspect replayed aggregate state, trigger copy actions without accidental navigation, and trust that displayed counts are either accurate for the selected scope or explicitly unavailable/stale/loading.

### AC-12 - Negative Evidence States

The UI must not present normal successful data when:

- the current user has no authorized tenants
- a tenant exists but is not authorized for the current user
- a metrics endpoint returns missing, partial, stale, unauthorized, or failed data
- a replay/state endpoint returns failed, malformed, stale, or partial state

### AC-13 - Deferred Timeline Filter Guardrail

Timeline type filtering is deferred because it is not required to unblock trust-boundary manual testing. This change must preserve truthful unfiltered timeline behavior and must not introduce partial or misleading filter semantics.

### Definition of Done

- Implementation complete for the included stories and no hidden runtime fallback to fixture data.
- Automated checks complete for replay correctness, tenant authorization, metrics truthfulness, and copy isolation.
- Manual verification script completed with evidence recorded in the guide or issue log.
- Deferred work remains explicitly out of scope and has no partial implementation.

---

## Section 7: Recommended Implementation Order

1. **Issue #3 - Copy bubbling**
   - Smallest isolated fix.
   - Gives a quick regression test win.

2. **Issue #1 - Tenant discovery**
   - Clarify authorization/provenance first.
   - Then implement shared provider and page adoption.

3. **Issue #5 - Aggregate state reconstruction**
   - Highest correctness risk.
   - Implement as focused story with shared service and fixture-backed tests.

4. **Issue #2 - Dashboard metrics**
   - Implement only the metrics whose sources are confirmed.
   - Split if rolling rate or error percentage requires telemetry work.

5. **Issue #4 - Timeline type-name filter**
   - Defer to backlog unless manual verification remains blocked.

Alternate PM priority:

If sequencing by user trust rather than implementation risk, start with Issue #5, then Issue #1, Issue #2, Issue #3, and defer Issue #4.

---

## Section 8: Out of Scope

- Aspire or Dapr infrastructure changes.
- Tenant service contract changes.
- Auto-registration of tenants on first event write.
- Generalized client-side state reconstruction framework beyond aggregate views required for manual testing.
- Full tenant-management UX.
- New authorization model.
- Production-grade metrics redesign.
- Cross-tenant security certification beyond the listed acceptance criteria.
- Timeline type-name filter unless explicitly pulled back into scope.
- Partial or hidden timeline type-name filtering behavior.
- Other non-validated pages after `/health`:
  - `/dapr*`
  - `/services`
  - `/tenants`
  - `/storage`
  - `/snapshots`
  - `/compaction`
  - `/backups`
  - `/consistency`
  - `/settings`

---

## Section 9: Open Questions

1. **Aggregate replay source of truth.** Which existing runtime component owns aggregate discovery, event type mapping, serializer options, and version/upcaster behavior? The admin path should reuse it.
2. **Replay failure model.** Should reconstruction return partial state with diagnostics, or fail the whole response? Default recommendation: return explicit `Partial` or `Failed` status with diagnostics and avoid presenting partial state as complete truth.
3. **Tenant authorization.** Which service/API supplies the current user's authorized tenant set for filtering observed tenants?
4. **Tenant provenance UX.** Should observed-only tenants be visually labeled in dropdowns, or should provenance remain API-only for now?
5. **Dashboard metric sources.** Which source can provide bounded, reliable `TotalEventCount`, `EventsPerSecond`, and command failure/rejection rate?
6. **Metric naming.** Is `ErrorPercentage` intended to mean infrastructure/API errors or command rejection/failure rate?
7. **Issue #4 deferral.** Confirm the 18-event seed stream is inspectable without type-name filtering after the correctness fixes land.
