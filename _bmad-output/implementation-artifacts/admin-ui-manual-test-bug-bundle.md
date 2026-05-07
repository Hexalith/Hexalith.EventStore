# Story: admin-ui-manual-test-bug-bundle

Status: done

Context created: 2026-05-07
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-06-admin-ui-manual-test-bug-bundle.md`
Carve-out proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-bundle-carveout.md`
Carved-out sibling story: `_bmad-output/implementation-artifacts/admin-ui-aggregate-state-replay-correctness.md`
Triggering test artifacts:
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues.md`
Review posture: in-progress with QA conditions documented below. **Group A (Issue #5 replay correctness, AC #1–#3, ST1–ST3) was carved out 2026-05-07 per user direction "Option B" and now lives in `admin-ui-aggregate-state-replay-correctness`.** Remaining scope: Issue #1 (tenant discovery, Group B), Issue #2 (truthful metrics, Group C), Issue #3 (copy isolation, Group D), and the Issue #4 deferred guardrail.

## Story

As an EventStore administrator running the manual Admin UI test guide,
I want aggregate state replay, tenant filtering, dashboard metrics, and copy interactions to be truthful and consistent across the affected pages,
so that I can complete end-to-end manual validation against the seeded `tenant-a/counter/counter-1` stream without encountering silently wrong state, empty tenant dropdowns, fake zero metrics, or accidental navigation.

## Bundle Justification

The source proposal recommends splitting this work into four lanes (Stories 1–4) plus one deferred enhancement. Original story bundled all four; on 2026-05-07 the user authorized "Option B" to carve out Group A (Issue #5 — aggregate state replay correctness). Group A now lives in `admin-ui-aggregate-state-replay-correctness`; this story keeps Groups B/C/D plus the Issue #4 deferred guardrail. The carve-out matches the seam the original bundle reserved: two PRs along the Group A vs Groups B/C/D boundary (Issue #5 alone vs the rest).

Manual verification (ST11) for this story is restricted to the B/C/D surface. The state-inspection cluster will remain visibly broken until `admin-ui-aggregate-state-replay-correctness` ships — that is expected residual risk for this story's `review` transition. Reviewers should not block this story on Group A evidence.

Issue #4 (timeline type-name filter) remains explicitly **out of scope** and must not be partially implemented — see AC10.

## Acceptance Criteria

### Group A — Aggregate State Replay Correctness (Issue #5)

1–3. **Carved out 2026-05-07 to `admin-ui-aggregate-state-replay-correctness`.** AC #1, #2, #3 (verbatim) and ST1, ST2, ST3 (verbatim) live in that story. The deep-merge fallback in `AdminStreamQueryController.ReconstructState` is unchanged by this story. Manual verification of the state-inspection cluster is deferred to the Group A story. AC numbering below preserves the bundle's original numbering (#4 onward) so that downstream references in the source proposal stay valid.

### Group B — Authorized Tenant Discovery (Issue #1, blocking)

4. **Tenant dropdowns expose authorized observed tenants without changing tenant lifecycle semantics.**
   - Given the seeded sample produces events under `tenant-a` but `tenant-a` is not registered in the Tenants service
   - When the user opens the Tenant dropdown on `/commands`, `/events`, `/streams`, or `/projections`
   - Then the dropdown lists `tenant-a` (observed-only) alongside any registered tenants the current user is authorized to see, with stable normalization and deterministic sort.
   - And the dropdown is hydrated by a shared tenant option provider (e.g. `AdminTenantOptionsProvider`) consumed by all four pages — no per-page ad-hoc query.
   - And tenant discovery is backed by an authorization-enforcing source. The UI must not fetch a broader tenant list and reduce it client-side as the primary authorization boundary.
   - And unauthorized observed tenants must not leak through dropdown options.
   - And no tenant lifecycle records are auto-created. Observed-only tenants are filter values, not registered entities.

5. **Tenant provenance is preserved and the empty case is honest.**
   - Given a tenant option appears in the dropdown
   - Then the underlying model distinguishes registered from observed-only tenants. UX labeling is optional in this story (see Open Question 4 in the source proposal); the API/model distinction is mandatory.
   - And duplicates between registered and observed sources are deduplicated.
   - And when the user has no authorized tenants, the dropdown shows the empty message `No tenants found yet. Send a command, register a tenant, or check your tenant permissions.` instead of a silent "All Tenants"-only list.
   - And partial authorization failures must not collapse into a misleading empty dropdown or leak raw resource existence; show an authorization/freshness diagnostic appropriate for operators.
   - And auth-context refresh, login switch, token refresh, and tenant claim changes do not leak stale tenant options.

### Group C — Truthful Dashboard Metrics (Issue #2)

6. **Home dashboard metrics are truthful, never hardcoded zeros, and distinguish state classes.**
   - Given seeded event data exists under `tenant-a/counter/counter-1`
   - When the home dashboard renders
   - Then `TotalEventCount`, `EventsPerSecond`, and the error-rate metric in `DaprHealthQueryService.GetSystemHealthAsync()` (`src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:130-136`) are no longer hardcoded `0`.
   - And each metric implemented in this story has a named source of truth, unit, refresh cadence/freshness window, authorization behavior, and explicit failure behavior documented in Dev Notes.
   - And metrics whose source is genuinely unavailable in this story (e.g. rolling rate without injectable clock or telemetry) are rendered as `unavailable` / `loading` / `stale` rather than fake `0`. Withholding a metric is acceptable; lying with `0` is not.
   - And `Index.razor:50` (or its replacement) does not gate the ActivityChart and Recent Streams section on a value that is hardcoded `0`. ActivityChart already builds buckets locally from `data.Streams.Items` (`Index.razor:162`); use that source or a real metric source, not the stub.
   - And `ErrorPercentage` is renamed or its label clarified if the implemented source is command rejection/failure rate rather than infrastructure error rate.

7. **The UI distinguishes seven data states across metrics and tenant-scoped data.**
   - The Admin UI must visually distinguish `loading`, `empty`, `zero`, `unavailable`, `unauthorized`, `stale`, and `error` for metric tiles, tenant-filtered grids, and replay panels.
   - Missing numeric data must not render as `0`. Real measured zero may render as `0` only when the named source returned an authoritative zero for the selected scope.
   - Loading state must be shown before values; the UI must not flash fake zeros while a request is in flight.

### Group D — Copy Click Isolation (Issue #3)

8. **Aggregate ID copy on `/streams` copies without navigating, and the row click contract still works.**
   - Given the user clicks the truncated Aggregate ID span (`src/Hexalith.EventStore.Admin.UI/Pages/Streams.razor:51-53`)
   - Then the value is copied to the clipboard and the toast (or equivalent feedback) is shown.
   - And the click does **not** navigate to `/streams/{tenant}/{domain}/{aggregateId}`, expand rows, mutate filters, select the row, or trigger any parent row command.
   - And clicking the row outside the copy affordance still navigates as before.
   - And keyboard activation of the copy affordance preserves the same row/action separation contract.
   - And copy success/failure feedback is available without relying only on pointer interaction (e.g. accessible status region).
   - And copy targets only the value of the intended row/tenant/field; manual verification covers visually similar neighboring rows and duplicate aggregate ids across tenants.
   - And every Razor component combining DataGrid `OnRowClick` with nested `@onclick` is audited and either fixed or explicitly documented as already correct (audit checklist in Dev Notes).

### Cross-Cutting

9. **Negative evidence states are never presented as successful data.**
   - The UI must not present normal successful data when:
     - the current user has no authorized tenants;
     - a tenant exists but is not authorized for the current user;
     - a metrics endpoint returns missing, partial, stale, unauthorized, or failed data;
     - a replay/state endpoint returns failed, malformed, stale, or partial state.
   - Fixture data may prove rendering and empty states in tests, but must never become runtime fallback display data when API calls fail or return incomplete data.

10. **Deferred timeline type filter guardrail.**
    - Issue #4 (timeline type-name filtering on `TimelineFilterBar.razor`) is explicitly **out of scope** for this story.
    - This change must preserve truthful unfiltered timeline behavior and must **not** introduce partial, hidden, or misleading type-name filter semantics. No half-wired query parameters, no inert UI controls, no commented-out scaffolding.

11. **Automated regression coverage exists at the lowest reliable level before manual verification is accepted.**
    - Each defect (#1, #2, #3, #5) has unit, contract, and/or bUnit tests as detailed in Dev Notes "Test Plan". Tier 2/3 integration tests inspect state-store end-state where applicable (per repo rule R2-A6), not only mock invocation counts.

12. **Manual verification against the seeded fixture is captured as evidence.**
    - With Aspire running and Redis flushed, the operator follows the manual tester script in Dev Notes and records evidence (screenshots, copied values, endpoint responses, correlation ids where available) in this story file or in `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` before the story moves to `review`.
    - The five recorded defects from the 2026-05-06 manual session do not reproduce.

## Tasks / Subtasks

- ST1, ST2, ST3 — **Carved out 2026-05-07 to `admin-ui-aggregate-state-replay-correctness`.** Verbatim copies live in that story file; do not duplicate work here. Group A is not part of this story's Definition of Done.

- [x] **ST4 — Shared tenant option provider.** (AC: 4, 5)
  - [x] Confirm authorization source for the current user's tenant set (Open Question 3 in the source proposal). **Resolved**: API-enforcing endpoints (`AdminTenantApiClient.ListTenantsAsync` + `AdminStreamApiClient.GetRecentlyActiveStreamsAsync`) are the boundary. The UI provider only unions what the backend already filtered for the current user.
  - [x] Add `AdminTenantOptionsProvider` (or equivalent) that unions registered tenants from the Tenants service with authorized observed tenants from event/stream data.
  - [x] Preserve provenance in the model (registered vs observed-only). API/model distinction is mandatory; UX label is optional in this story.
  - [x] Filter observed tenants by current user authorization. Do not collapse partial authorization failures into a silent empty dropdown.
  - [x] Deduplicate, normalize, and sort options deterministically.
  - [x] Implement the empty-state copy: `No tenants found yet. Send a command, register a tenant, or check your tenant permissions.`
  - [x] Handle auth-context refresh, login switch, token refresh, and tenant claim changes without leaking stale options. **Implementation note**: the provider is registered as scoped, so each authentication-state refresh creates a fresh instance with no cached tenants from the previous principal.

- [x] **ST5 — Adopt the provider in all four pages.** (AC: 4)
  - [x] Replace `LoadTenantsAsync` in `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor` with the shared provider.
  - [x] Apply the same in `Events.razor`, `Streams.razor`, and `Projections.razor`.
  - [x] Remove or deprecate ad-hoc per-page tenant fetch helpers if they become unused.
  - [x] bUnit smoke tests for each page: registered-only, observed-only, mixed, unauthorized-excluded, empty-with-help-message. (See `AdminTenantOptionsAdoptionTests` and `AdminTenantOptionsProviderTests`.)

- [x] **ST6 — Wire honest dashboard metrics in `DaprHealthQueryService`.** (AC: 6, 7)
  - [x] In `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs`, replace the hardcoded zeros only where a real source is available in this story.
  - [x] `TotalEventCount`: derive from an existing bounded stream summary/read model. **Source**: `IStreamQueryService.GetRecentlyActiveStreamsAsync` (backed by `admin:stream-activity:all` Redis key). Sum of `StreamSummary.EventCount` across the bounded list — no unbounded scan.
  - [x] `EventsPerSecond`: explicit `Unavailable` status. No deterministic rolling-window source and no injectable clock available in this build.
  - [x] Error-rate metric: explicit `Unavailable` status. The historical `ErrorPercentage` field name conflated infrastructure errors with command rejection rate; resolution of the name change is deferred until a real source is wired.
  - [x] `Index.razor` no longer gates ActivityChart on the previously hardcoded zero. New gate: `_streams is not null && _streams.Items.Count > 0`. Streams are now fetched unconditionally rather than gated on `TotalEventCount > 0`.
  - [x] Document each metric's source, unit, refresh cadence, authorization behavior, and failure behavior in Dev Notes "Metric Contract" (filled below).

- [x] **ST7 — UI state class plumbing.** (AC: 7, 9)
  - [x] Ensure metric tiles, tenant-filtered grids, and replay panels can render `loading` / `empty` / `zero` / `unavailable` / `unauthorized` / `stale` / `error` distinctly. **Mapping**: loading = skeleton card / "—" placeholder; empty = canonical EmptyState copy; zero = real `Available` value of 0; unavailable = explicit "unavailable" string in metric tiles + provider `Empty`/`Unavailable`/`Forbidden` diagnostic in dropdowns; unauthorized = provider `Unauthorized` diagnostic; stale = `_isStale` page flag adds "(stale)" suffix; error = `IssueBanner` / "Access Denied" empty state.
  - [x] Add tests proving loading state shows before values and does not flash fake zeros. (`LandingPage_WhenLoading_DoesNotFlashFakeZeros`.)
  - [x] Add a stale/projection-lag test ensuring stale values are labeled or withheld, not presented as fresh. (Pre-existing `LandingPage_WhenApiTimesOut_ShowsStaleData` covers this contract; ST6 changes preserve the suffix-based stale labeling.)

- [x] **ST8 — Copy click isolation on `/streams`.** (AC: 8)
  - [x] Add `@onclick:stopPropagation="true"` to the aggregate ID span in `src/Hexalith.EventStore.Admin.UI/Pages/Streams.razor`. **Implementation**: replaced the bare `<span @onclick="...">` with a focusable `<button>` carrying `@onclick:stopPropagation="true"` and `@onkeydown:stopPropagation="true"`.
  - [x] Prefer an accessible button/equivalent focusable control with clear label/tooltip. Now a real `<button type="button">` with `aria-label="Copy aggregate ID {id} to clipboard"` and `title="{id} (click to copy)"`.
  - [x] Provide accessible copy success/failure feedback (status region, not pointer-only toast). Added `<div role="status" aria-live="polite" data-testid="copy-status">` plus a Fluent toast on success.
  - [x] Audit every component combining DataGrid `OnRowClick` with nested `@onclick` (`Commands.razor`, `Events.razor`, `Projections.razor`, plus any other admin grid). Record each as fixed-in-this-story or already-correct in the audit checklist in Dev Notes. (Audit table filled below.)

- [x] **ST9 — Copy and propagation tests.** (AC: 8, 11)
  - [x] bUnit: clicking the copy affordance copies and does not navigate / select / expand / mutate filters / trigger parent commands. (`AggregateIdCopy_Click_DoesNotNavigate`, `AggregateIdCopy_Click_InvokesClipboardWriteText_AndAnnouncesAccessibleStatus`.)
  - [x] bUnit: clicking the row outside the affordance still navigates. (`Row_Click_OutsideCopyButton_StillNavigatesToStreamDetail`.)
  - [x] Keyboard interaction test if the control is focusable. **Implementation note**: bUnit cannot directly synthesise native browser keyboard activation, but the affordance is now a `<button>` (browsers handle Enter/Space natively) and `@onkeydown:stopPropagation="true"` prevents key events from bubbling to the row. The `AggregateIdCopy_IsRenderedAsAccessibleButton_NotPlainSpan` test pins the focusable-button shape.
  - [x] Screen-reader-accessible feedback test if exposed. (`AggregateIdCopy_Click_InvokesClipboardWriteText_AndAnnouncesAccessibleStatus` asserts the `aria-live="polite"` region announces the copied value.)

- [x] **ST10 — Deferred timeline filter guardrail.** (AC: 10)
  - [x] Confirm no partial type-name filter scaffolding lands in this story. No new query string params, no inert UI controls. Verified by reading `TimelineFilterBar.razor`; confirmed unchanged. New regression test `TimelineFilterBar_DoesNotIntroduceTypeNameFilterScaffolding_ST10Guardrail` pins the contract by asserting forbidden control labels and parameter names are absent.
  - [x] If the manual test in ST11 demonstrates that the seeded 18-event stream is inspectable without type-name filtering after Group A lands, record that confirmation in the manual evidence and close Open Question 7 from the source proposal as resolved. **Status**: deferred to the operator-driven manual smoke. Group A timeline-without-type-filter inspection is verified in `admin-ui-aggregate-state-replay-correctness` ST4.

- [x] **ST11 — Manual verification with seeded fixture.** (AC: 12) — **OPERATOR SIGNED OFF 2026-05-07**
  - [x] Flush Redis, build, `aspire run` (per the project memory's restart procedure). **Evidence**: Aspire stopped/restarted, Redis `FLUSHALL` returned `OK`, resources healthy.
  - [x] Seed `tenant-a/counter/counter-1` via the Sample Blazor UI Pattern 2: Increment ×5, Decrement ×2, Reset, Increment ×10.
  - [x] Run the manual tester script in Dev Notes for this story's B/C/D surface:
    1. Select the authorized tenant `tenant-a`.
    2. Inspect replayed aggregate state at sequences 1, 5, 7, 8, 18 and confirm match against the checkpoint table. **Excluded from this story's sign-off per Group A carve-out to `admin-ui-aggregate-state-replay-correctness`.**
    3. Compare displayed dashboard metrics against their named source/freshness.
    4. Copy the aggregate ID and confirm no navigation/row side effects.
    5. Confirm the five 2026-05-06 defects do not reproduce.
    6. If UI evidence disagrees with backend behavior, record tenant, aggregate id, selected tenant context, visible state, copied value if relevant, endpoint/request id or correlation id, timestamp, and screenshot.
  - [x] Capture the manual evidence in this story's Dev Agent Record or in `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`.
  - [x] **Operator follow-up completed.** Jerome confirmed dashboard metrics, tenant dropdowns, and stream copy/navigation behavior pass after Redis flush and full Aspire restart.

### Review Findings

- [x] [Review][Patch] `/projections` still hides the tenant dropdown when the only authorized observed tenant is `tenant-a` [src/Hexalith.EventStore.Admin.UI/Components/ProjectionFilterBar.razor:17] — resolved 2026-05-07 by rendering the projection tenant select whenever at least one authorized tenant exists and pinning the single-tenant case in `ProjectionFilterBarTests`.
- [x] [Review][Patch] Observed-tenant lookup failures can still collapse into an empty successful tenant list [src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs:82] — resolved 2026-05-07 by propagating unexpected stream-list failures as `ServiceUnavailableException` and pinning the API-client error contract in `AdminStreamApiClientTests`.

## QA Conditions

- Treat this story as in-progress only if implementation commits keep the scope to the three remaining defects (#1, #2, #3) plus the deferred guardrail (#4). Group A (Issue #5) is carved out and must not be touched here. Do not mix in unrelated Admin UI cleanup.
- Before moving to `review`, the dev agent must demonstrate:
  - Required: bUnit coverage for tenant dropdown empty-state copy, observed-only tenant inclusion, and unauthorized exclusion across `/commands`, `/events`, `/streams`, `/projections`.
  - Required: tests proving each shipped dashboard metric returns its named source result (or an explicit unavailable indicator), and that the UI does not flash fake zeros during load.
  - Required: bUnit coverage for the copy/row separation contract on `/streams`.
  - Required: manual Aspire smoke per ST11 (B/C/D surface only) with evidence recorded. The dev agent must hand this off to the operator if it cannot drive the browser. Group A surface (state-inspection cluster) is explicitly excluded from this manual smoke and is verified separately in `admin-ui-aggregate-state-replay-correctness`.
  - Nice-to-have: per-page audit table for `OnRowClick` + nested `@onclick` patterns beyond `/streams`.
- Residual risk: the manual-test guide cannot fully unblock until Group A also ships. That is expected and not a blocker for this story's `review` transition.

## Dev Notes

### Bundle Justification

See "Bundle Justification" section above. The four lanes share one motivation (manual-test unblocker) but no implementation seam. The justification for keeping them in one story is reviewability of the unblocker as a unit, not technical coupling.

### Defect Chain

Five defects surfaced during sequential testing of `/`, `/commands`, `/events`, `/streams`, and `/streams/{tenant}/{domain}/{aggregate}` on 2026-05-06 against the canonical seeded scenario:

1. **Issue #5 (blocking)** — `AdminStreamQueryController.ReconstructState` deep-merges raw payloads instead of running aggregate `Apply()`. For the 18-event Counter stream of marker events (`CounterIncremented`, `CounterDecremented`, `CounterReset`, `CounterClosed` — all empty payloads), this returns `{}` for every sequence. Blame, Step Through, StateDiff, Bisect, Sandbox, and likely CausationChainView are all systematically wrong.
2. **Issue #1 (blocking)** — Tenant dropdowns on `/commands`, `/events`, `/streams` (and likely `/projections`) call `GET /api/v1/admin/tenants`. `tenant-a` is not registered in the Tenants service so the dropdown renders only `All Tenants` even when the store visibly contains data under `tenant-a`.
3. **Issue #2 (regression)** — `DaprHealthQueryService.GetSystemHealthAsync()` returns hardcoded `TotalEventCount: 0`, `EventsPerSecond: 0`, `ErrorPercentage: 0`. `Index.razor:50` gates ActivityChart on `_healthReport.TotalEventCount > 0`, so the home page renders the empty state forever.
4. **Issue #3 (UX)** — The aggregate ID span on `/streams` calls `CopyAggregateId` but does not stop propagation; the parent row click handler navigates away.
5. **Issue #4 (enhancement, deferred)** — `TimelineFilterBar.razor` only supports `All` / `Command` / `Event` / `Query` kind buttons; no filter on event/command type name.

### Architecture Decisions (carried from the source proposal)

- **ADR-1: Admin state inspection reuses runtime replay semantics.** Domain-owned replay service preferred over UI-owned reconstruction adapter. Strict handler replay preferred over best-effort payload projection. Failure visibility is mandatory.
- **ADR-2: Tenant dropdowns are discovery, not registration.** Union registered + authorized observed; preserve provenance; do not auto-register tenants on first event write.
- **ADR-3: Dashboard metrics need named sources.** Truthful before precise. Never fall back to `0` unless `0` is the actual measured value for the named source.
- **ADR-4: Row navigation and embedded actions are separate interaction contracts.** Every embedded action in a clickable row needs explicit propagation behavior and interaction tests.

### Trust Boundary Rules

- Data allowed from Admin APIs: stream summaries, event envelopes, aggregate state/replay responses, tenant option data, metric values from authorization-enforcing endpoints.
- Data required from authenticated context or authorization-enforcing APIs: tenant access, tenant visibility, selected tenant validity, operator permissions.
- Data never inferred client-side as an authorization boundary: tenant access, cross-tenant visibility, whether missing metrics mean zero, whether failed replay means empty state, whether observed tenant data implies tenant registration.
- Fixture data may prove rendering and empty states in tests but must never become runtime fallback display data when API calls fail.

### Canonical Fixture and Checkpoint Table

Stream identity: `tenant-a` / `counter` / `counter-1`
Sequence: 5 increments, 2 decrements, reset, 10 increments (18 events total)

| Sequence | Expected state |
|---:|---|
| 0 | Initial state, or explicit not-created semantics |
| 1 | `Count = 1`, `IsTerminated = false` |
| 5 | `Count = 5`, `IsTerminated = false` |
| 7 | `Count = 3`, `IsTerminated = false` |
| 8 | `Count = 0`, `IsTerminated = false` (after reset at #8) |
| 18 | `Count = 10`, `IsTerminated = false` |
| > 18 | Same as 18, or explicit out-of-range validation error |

Marker event records (payload serialized as `{}`):

```csharp
public sealed record CounterIncremented : IEventPayload;
public sealed record CounterDecremented : IEventPayload;
public sealed record CounterReset      : IEventPayload;
public sealed record CounterClosed     : IEventPayload;
```

`CounterState.Apply(CounterIncremented) => Count++` is the runtime semantic that must produce `Count = 10` after the 18-event seed.

### Failure Semantics Matrix (Group A)

| Failure | Server contract | UI copy class |
|---|---|---|
| Unknown aggregate type | `Failed` + `errorCategory: UnknownAggregateType` | Configuration / not-applicable |
| Unknown event type | `Failed` + `errorCategory: UnknownEventType` | Configuration / not-applicable |
| Deserialization failed | `Failed` + `errorCategory: DeserializationFailed` | Backend failure |
| Apply handler missing | `Failed` + `errorCategory: ApplyHandlerMissing` | Configuration / not-applicable |
| Apply throws | `Partial` (state up to last good event) + `errorCategory: ApplyFailed` + `failedSequenceNumber` + `failedEventType` | Partial result, retry advisable |
| Unsupported version | `Failed` + `errorCategory: UnsupportedVersion` | Configuration / version mismatch |
| Unexpected | `Failed` + `errorCategory: Unexpected` | Backend failure, retry / report |

### Metric Contract (filled during ST6)

| Metric | Source | Unit | Refresh / freshness | Authorization | Failure behavior |
|---|---|---|---|---|---|
| `TotalEventCount` | `IStreamQueryService.GetRecentlyActiveStreamsAsync` (Redis key `admin:stream-activity:all`, sum of `StreamSummary.EventCount`) | events | Same as the bounded stream-activity index — refreshed by `StreamActivityWriter` on each event publish; freshness is observable in the index entry's `LastActivityUtc`. | Tenant scope is enforced upstream by the Tenants service / API. Provider does not reduce client-side. | `SystemHealthMetricStatus.Unavailable` when the index read fails. UI renders the literal string `"unavailable"` rather than `0`. |
| `EventsPerSecond` | **Not implemented in this story.** Requires injectable clock + rolling-window source (e.g. an OpenTelemetry meter aggregator). | events/sec | n/a | n/a | Always `SystemHealthMetricStatus.Unavailable`. UI renders `"unavailable"` instead of `0.0/s`. |
| `ErrorPercentage` | **Not implemented in this story.** Historical name conflated infrastructure errors with command rejection rate; rename pending a real source decision. | % | n/a | n/a | Always `SystemHealthMetricStatus.Unavailable`. UI renders `"unavailable"` instead of `0.00%`, severity becomes `neutral` rather than `success`. |

### `OnRowClick` + nested `@onclick` audit (filled during ST8)

| Component | DataGrid with `OnRowClick`? | Nested `@onclick`? | Status |
|---|---|---|---|
| `Pages/Streams.razor` | Yes | Yes (Aggregate ID copy) | **Fixed in this story (ST8).** Replaced bare `<span @onclick=...>` with `<button @onclick:stopPropagation="true">` carrying an `aria-label`, accessible status region, and toast. |
| `Pages/Commands.razor` | Yes | No | Already correct. Aggregate ID and Correlation ID spans use `cursor: pointer` styling but no `@onclick` — no row-click bubbling happens because no nested handler exists. The cursor styling is cosmetic; no copy affordance is wired. |
| `Pages/Events.razor` | Yes | No | Already correct. Same as Commands.razor — Aggregate ID and Correlation ID spans use `cursor: pointer` but no `@onclick`. |
| `Pages/Projections.razor` | Yes | No | Already correct. No nested `@onclick` handlers on cells. The detail panel is opened via the row-click contract. |
| `Components/ProjectionDetailPanel.razor` | No (button-driven) | Yes (action buttons) | Already correct. Buttons are not nested inside any `OnRowClick` row context. |
| `Layout/Breadcrumb.razor` | No | Yes (copy link) | Already correct. Breadcrumb sits outside any `DataGrid` and has its own click target. |

Conclusion: the only `OnRowClick` + nested `@onclick` bug pattern in the Admin.UI is the Streams.razor aggregate-ID span. Other admin grids (`Storage`, `Snapshots`, `Compaction`, `Backups`, `Consistency`, `DeadLetters`, `DaprComponents`, `DaprActors`, `DaprPubSub`, `DaprResiliency`, `DaprHealthHistory`, `Tenants`) were spot-checked during the audit and have no nested `@onclick` handlers within their row templates.

### Test Plan Summary

- **Tier 1 unit:** event type resolution, persisted-name vs CLR-name mapping, Apply method resolution and overload rules, tenant option provider permutations, metric aggregation with deterministic input and injected clock, copy/propagation behavior, UI state-class rendering.
- **Tier 2 integration:** state endpoint at every checkpoint sequence; tenant filter end-to-end through `AdminStreamApiClient` / `AdminTenantApiClient`; dashboard metric end-to-end with persisted Redis state per R2-A6.
- **Tier 3 Aspire:** at minimum, the canonical 18-event fixture round-trips through `aspire run` and the state endpoint returns `Count = 10` at sequence 18.
- **bUnit:** dropdown empty-state copy, observed-only inclusion, unauthorized exclusion, copy/row separation, accessible feedback.

### Manual Tester Script (full)

1. `docker exec dapr_redis redis-cli FLUSHALL`
2. `dotnet build Hexalith.EventStore.slnx --configuration Release`
3. `dotnet run --project src/Hexalith.EventStore.AppHost`
4. Sample Blazor UI → Pattern 2 → Increment ×5, Decrement ×2, Reset, Increment ×10
5. Open Admin UI `/`. Verify dashboard metrics are not all `0`, or are explicitly `unavailable` / `loading` / `stale` rather than fake `0`.
6. Open `/streams`. Verify the Tenant dropdown lists `tenant-a`. Click on the truncated aggregate ID — confirm copy and confirm no navigation. Click the row outside the copy span — confirm navigation works.
7. Open `/streams/tenant-a/counter/counter-1`. Inspect:
   - Step Through at sequences 1, 5, 7, 8, 18 — confirm against checkpoint table.
   - Blame at sequence 18 — confirm fields bind to source events.
   - StateDiff between adjacent and non-adjacent sequences — confirm real field-level diff.
   - Bisect against a known divergence — confirm it locates the divergent event.
   - Sandbox a command — confirm the resulting state reflects applied state, not `{}`.
8. On `/commands`, `/events`, `/streams`, `/projections` — confirm the Tenant dropdown is populated and correctly filtered.
9. Negative cases: log in as a user with no authorized tenants — confirm the empty-state copy. Force a metrics endpoint failure — confirm the UI renders `unavailable` rather than `0`.
10. Record evidence (screenshots + endpoint correlation ids) in this story's Dev Agent Record.

### Out of Scope (carried from the source proposal)

- Aspire or Dapr infrastructure changes.
- Tenant service contract changes.
- Auto-registration of tenants on first event write.
- Generalized client-side state reconstruction framework beyond aggregate views required for manual testing.
- Full tenant-management UX.
- New authorization model.
- Production-grade metrics redesign.
- Cross-tenant security certification beyond the listed acceptance criteria.
- Timeline type-name filter (Issue #4).
- Partial or hidden timeline type-name filtering behavior.
- Other non-validated pages after `/health`: `/dapr*`, `/services`, `/tenants`, `/storage`, `/snapshots`, `/compaction`, `/backups`, `/consistency`, `/settings`.

### Open Questions to Resolve During Implementation

Carried verbatim from the source proposal — the dev agent should resolve or escalate each before marking the story complete:

1. Aggregate replay source of truth — which existing runtime component owns aggregate discovery, event type mapping, serializer options, and version/upcaster behavior?
2. Replay failure model — return `Partial` with diagnostics, or fail the whole response? Default: explicit `Partial` or `Failed` with diagnostics.
3. Tenant authorization — which service/API supplies the current user's authorized tenant set?
4. Tenant provenance UX — visually labeled in dropdowns, or API-only for now? (UX label is optional in this story.)
5. Dashboard metric sources — bounded source for `TotalEventCount`, `EventsPerSecond`, command failure/rejection rate?
6. Metric naming — is `ErrorPercentage` infrastructure errors or command rejection?
7. Issue #4 deferral — confirm the 18-event seed is inspectable without type-name filtering after Group A lands.

### Project Structure Notes

- Backend changes touch `src/Hexalith.EventStore` (controller + new shared replay service) and `src/Hexalith.EventStore.Admin.Server` (metrics aggregation).
- Frontend changes touch `src/Hexalith.EventStore.Admin.UI/Pages` (Commands, Events, Streams, Projections, Index) and `src/Hexalith.EventStore.Admin.UI/Services` (new tenant option provider).
- New tests live in the existing tier projects: `Hexalith.EventStore.Server.Tests` (Tier 2), `Hexalith.EventStore.Admin.Server.Tests`, `Hexalith.EventStore.Admin.UI.Tests`, and `Hexalith.EventStore.IntegrationTests` (Tier 3).
- No NuGet contract changes — the source proposal explicitly classifies this as a manual-test unblocker, not a contract revision.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-06-admin-ui-manual-test-bug-bundle.md` (source proposal, all sections)
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` (manual test guide that surfaced the defects)
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues.md` (issue inventory with reproduction steps and exact code references)
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1477-1497` (deep-merge to remove)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs:130-136` (hardcoded zero metrics)
- `src/Hexalith.EventStore.Admin.UI/Pages/Streams.razor:51-53` (copy/navigate bubbling)
- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor:240-251` (per-page tenant load to replace)
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs:123-127` (`GetTenantsAsync`)
- `src/Hexalith.EventStore.Admin.UI/Pages/Index.razor:50` (ActivityChart gate) and `:162` (`BuildActivityBuckets`)
- `_bmad-output/implementation-artifacts/15-7-health-dashboard-with-observability-deep-links.md` (DTO origin for SystemHealthReport)
- `_bmad-output/implementation-artifacts/admin-ui-state-inspection-cluster-fix.md` (sibling story; format and review-posture conventions)
- `CLAUDE.md` — Fluent Convention discovery, R2-A6 integration test rule

## Dev Agent Record

### Agent Model Used

`claude-opus-4-7[1m]` (Opus 4.7, 1M context). Single dev session 2026-05-07.

### Debug Log References

- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests` (Release): 705/705 pass, 0 skipped.
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests` (Release): 564/564 pass, 18 skipped (pre-existing DW2 ATDD red-phase skips, unrelated to this story).
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests` (Release): 282/282 pass.
- `dotnet test tests/Hexalith.EventStore.Client.Tests` (Release): 334/334 pass.
- `dotnet build src/Hexalith.EventStore.Admin.UI` / `src/Hexalith.EventStore.Admin.Server` / `src/Hexalith.EventStore` (Release): 0 warn / 0 err.
- New test files: `AdminTenantOptionsProviderTests` (13 tests), `AdminTenantOptionsAdoptionTests` (12 tests), `StreamsPageCopyIsolationTests` (4 tests). `TimelineFilterBarTests` extended with ST10 guardrail.
- Code review patch validation (2026-05-07): `dotnet build src/Hexalith.EventStore.Admin.UI/Hexalith.EventStore.Admin.UI.csproj --configuration Release --no-restore` passed; `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --configuration Release --no-restore` passed 705/705, 0 skipped.
- ST11 operator sign-off (2026-05-07): Redis flushed; Aspire relaunched with `EnableKeycloak=false`; resources healthy; `tenant-a/counter/counter-1` seeded; dashboard rendered Total Events = 19 with `Events/sec` and `Error Rate` as expected `unavailable`; tenant dropdowns passed on `/commands`, `/events`, `/streams`, `/projections`; `/streams` copy isolation passed.

### Completion Notes List

- **Carve-out (Group A)**: ST1, ST2, ST3 and AC #1–#3 were carved out of this story per user direction "Option B" on 2026-05-07. They now live in `admin-ui-aggregate-state-replay-correctness` (ready-for-dev). This story scope is reduced to Groups B/C/D plus ST10 deferred guardrail. Sprint-change-proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-bundle-carveout.md`.
- **ST4 (provider)**: New `AdminTenantOptionsProvider` unions registered tenants from `AdminTenantApiClient.ListTenantsAsync` with authorized observed tenants extracted from `AdminStreamApiClient.GetRecentlyActiveStreamsAsync`. Returns `TenantOptionsResult` with `Loaded`/`Empty`/`Unauthorized`/`Forbidden`/`Unavailable`/`Partial` status. Provenance is preserved per option. Authorization is enforced upstream at the API; provider does not reduce client-side.
- **ST5 (adoption)**: All four pages (`/commands`, `/events`, `/streams`, `/projections`) now consume the provider. `StreamFilterBar` parameter changed from `IReadOnlyList<TenantSummary>?` to `IReadOnlyList<TenantOption>?` to preserve provenance. `Projections` no longer derives the tenant list from projection data. Diagnostics from `Partial`/`Unauthorized`/`Empty` outcomes render as a polite aria-live region next to each filter bar.
- **ST6 (metrics)**: `SystemHealthReport` extended with three new optional `SystemHealthMetricStatus` fields (default `Available` for `TotalEventCount`, `Unavailable` for the others — preserves wire-format compatibility). `DaprHealthQueryService` now sums `EventCount` from the bounded `admin:stream-activity:all` index for `TotalEventCount` and reports `Unavailable` for the other two metrics. `Index.razor` renders `"unavailable"` instead of fake `0.0/s` / `0.00%`. ActivityChart no longer gates on the previously hardcoded zero.
- **ST7 (state classes)**: All seven UI states (loading/empty/zero/unavailable/unauthorized/stale/error) are now visibly distinguished via the metric-status fields, the provider's outcome enum, the page-level `_isStale` flag, and the existing `IssueBanner`. New `LandingPage_WhenLoading_DoesNotFlashFakeZeros` test pins the no-flash contract.
- **ST8 (copy isolation)**: Streams.razor aggregate-id span replaced with a focusable `<button>` carrying `@onclick:stopPropagation="true"`, `@onkeydown:stopPropagation="true"`, accessible label/title, and an `aria-live="polite"` status region for screen-reader feedback. Audit table confirms no other Admin.UI grid combines `OnRowClick` with a nested `@onclick`.
- **ST9 (tests)**: `StreamsPageCopyIsolationTests` proves the click contract: copy click invokes the clipboard JS and announces the accessible status, copy click does not navigate, row click outside the copy affordance still navigates to `/streams/tenant/domain/aggregate`.
- **ST10 (guardrail)**: TimelineFilterBar untouched. New `TimelineFilterBar_DoesNotIntroduceTypeNameFilterScaffolding_ST10Guardrail` test asserts forbidden control labels and parameter names are absent.
- **ST11 (manual smoke)**: **Operator-only** — handed off. Story moves to `review` without ST11 checked. Operator must follow `Manual Tester Script` in Dev Notes against the seeded `tenant-a/counter/counter-1` fixture and capture evidence in `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`.
- **Code review patches**: Resolved both review findings. `ProjectionFilterBar` now renders a tenant selector for a single authorized tenant so `/projections` exposes `tenant-a`; `AdminStreamApiClient.GetRecentlyActiveStreamsAsync` now propagates unexpected stream-list failures as `ServiceUnavailableException` so `AdminTenantOptionsProvider` can surface `Partial`/`Unavailable` diagnostics instead of silently treating the observed source as empty.
- **ST11 operator sign-off**: Completed 2026-05-07 after Redis flush and full Aspire restart. Jerome verified the dashboard metrics contract, tenant dropdowns across `/commands`, `/events`, `/streams`, `/projections`, and stream Aggregate ID copy isolation. Evidence recorded in `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`.
- **Residual risk acknowledged in QA Conditions**: the manual-test guide cannot fully unblock until `admin-ui-aggregate-state-replay-correctness` also ships.
- **Open Questions resolved**: OQ3 (tenant authorization source — resolved as the existing API endpoints `AdminTenantApiClient.ListTenantsAsync` + `AdminStreamApiClient.GetRecentlyActiveStreamsAsync`); OQ4 (tenant provenance UX — kept API-only; `TenantOption.Provenance` exists in the model but the dropdown does not visually badge observed-only tenants); OQ5 (`TotalEventCount` source — `admin:stream-activity:all`; `EventsPerSecond` and command-rejection rate remain unimplemented); OQ6 (`ErrorPercentage` rename — deferred until source decision); OQ7 (Issue #4 deferral confirmation — deferred to the operator-driven manual smoke). OQ1 and OQ2 are resolved in the carved-out Group A story.

### File List

Production:

- `src/Hexalith.EventStore.Admin.UI/Services/AdminTenantOptionsProvider.cs` (new)
- `src/Hexalith.EventStore.Admin.UI/AdminUIServiceExtensions.cs` (registered provider)
- `src/Hexalith.EventStore.Admin.UI/Components/StreamFilterBar.razor` (parameter type changed to `IReadOnlyList<TenantOption>?`)
- `src/Hexalith.EventStore.Admin.UI/Components/ProjectionFilterBar.razor` (review patch: single authorized tenant remains selectable)
- `src/Hexalith.EventStore.Admin.UI/Pages/Streams.razor` (provider adoption + copy-button + a11y status region)
- `src/Hexalith.EventStore.Admin.UI/Pages/Commands.razor` (provider adoption)
- `src/Hexalith.EventStore.Admin.UI/Pages/Events.razor` (provider adoption)
- `src/Hexalith.EventStore.Admin.UI/Pages/Projections.razor` (provider adoption + diagnostic region)
- `src/Hexalith.EventStore.Admin.UI/Pages/Index.razor` (honest metric rendering + ungated ActivityChart)
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` (review patch: stream-list failures propagate as unavailable)
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/SystemHealthMetricStatus.cs` (new)
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Health/SystemHealthReport.cs` (added 3 optional `SystemHealthMetricStatus` fields)
- `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthQueryService.cs` (real `TotalEventCount` source + explicit `Unavailable` for unimplemented metrics)

Tests:

- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminTenantOptionsProviderTests.cs` (new — 13 tests)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/AdminTenantOptionsAdoptionTests.cs` (new — 12 tests)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StreamsPageCopyIsolationTests.cs` (new — 4 tests)
- `tests/Hexalith.EventStore.Admin.UI.Tests/AdminUITestContext.cs` (registered default `AdminTenantOptionsProvider` substitute)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StreamFilterBarTests.cs` (parameter type updated)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/TimelineFilterBarTests.cs` (added ST10 guardrail test)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/IndexPageTests.cs` (added 3 tests for honest unavailable rendering, no fake-zero flash, ungated ActivityChart; updated existing empty-state test)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/EventsPageTests.cs` (`Received(2).GetTenantsAsync` assertion replaced with provider-aware assertion)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StreamsPageTests.cs` (updated truncation test to match new button title/aria-label)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/ProjectionFilterBarTests.cs` (review patch: single-tenant projection dropdown regression)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminStreamApiClientTests.cs` (review patch: stream-list failure propagation regression)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceTests.cs` (factory + 3 new metric-availability tests)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprHealthQueryServiceHistoryTests.cs` (factory updated for new constructor signature)

Carved out (separate story):

- `_bmad-output/implementation-artifacts/admin-ui-aggregate-state-replay-correctness.md` (new — Group A)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-bundle-carveout.md` (new — carve-out authorization)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (added the new ready-for-dev row + last_updated comment)

## Change Log

- 2026-05-07 — Story moved ready-for-dev → in-progress. Group A (Issue #5 replay correctness, AC #1–#3, ST1–ST3) carved out into new story `admin-ui-aggregate-state-replay-correctness` per `sprint-change-proposal-2026-05-07-admin-ui-manual-test-bundle-carveout.md` (user authorized "Option B" 2026-05-07). Remaining scope: Groups B/C/D plus ST10 deferred guardrail and ST11 operator-only manual smoke (B/C/D surface only).
- 2026-05-07 — Implemented ST4 `AdminTenantOptionsProvider` (union of registered + authorized observed tenants with provenance, dedupe, deterministic sort, partial-failure handling, canonical empty copy) + 13 unit tests.
- 2026-05-07 — Implemented ST5 (provider adoption in Streams/Commands/Events/Projections + StreamFilterBar parameter migration to `IReadOnlyList<TenantOption>?` + diagnostic surfacing) + 12 page-adoption smoke tests; pre-existing `EventsPage_RefreshPreservesFiltersPageAndScrollState` assertion updated to assert page-data refresh instead of legacy `GetTenantsAsync` calls.
- 2026-05-07 — Implemented ST6 (`SystemHealthReport` extended with `SystemHealthMetricStatus` per metric; `DaprHealthQueryService` now sums `EventCount` from `admin:stream-activity:all` for `TotalEventCount` and reports `Unavailable` for `EventsPerSecond` / `ErrorPercentage`; `Index.razor` renders explicit `"unavailable"` and ungates ActivityChart from the previously hardcoded zero) + 3 metric-availability tests + Index honest-unavailable / no-fake-zero-flash / ungated-chart tests.
- 2026-05-07 — Implemented ST7 (UI state-class plumbing) by reusing the new `SystemHealthMetricStatus`, `TenantOptionsLoadStatus`, page-level `_isStale` flag, and existing `IssueBanner` to distinguish loading/empty/zero/unavailable/unauthorized/stale/error visually; pinned with new and existing tests.
- 2026-05-07 — Implemented ST8 (copy click isolation): replaced `<span @onclick=...>` on Streams.razor with a focusable `<button>` carrying `@onclick:stopPropagation="true"`, `@onkeydown:stopPropagation="true"`, accessible label/title, and an `aria-live="polite"` status region. Filled the `OnRowClick` + nested `@onclick` audit table. Confirmed Commands/Events/Projections and other admin grids do not reproduce the bug.
- 2026-05-07 — Implemented ST9 (4 copy/propagation bUnit tests in `StreamsPageCopyIsolationTests`).
- 2026-05-07 — Implemented ST10 (Issue #4 guardrail): verified TimelineFilterBar untouched and added a regression test that locks down the absence of type-name filter scaffolding.
- 2026-05-07 — Story moved in-progress → review. ST11 manual Aspire smoke handed off to operator (story Tasks/Subtasks reflect ST11 explicitly unchecked with READY-FOR-OPERATOR marker per the original instruction). Validation: Admin.UI 705/705, Admin.Server 564/564 (18 pre-existing skips), Contracts 282/282, Client 334/334, Release builds clean.
- 2026-05-07 — Code review patches applied and marked resolved: projection tenant dropdown now renders with a single authorized tenant; stream-list API failures now propagate as `ServiceUnavailableException` for truthful observed-tenant diagnostics. Validation: Admin.UI build passed; Admin.UI.Tests 705/705 passed. Story remains `review` pending operator-only ST11 manual Aspire smoke.
- 2026-05-07 — Story moved review → done after ST11 operator sign-off. Evidence: Redis flush + full Aspire restart, healthy resources, seeded `tenant-a/counter/counter-1`, dashboard metrics truthful (`Total Events=19`, unavailable rate metrics), tenant dropdowns pass on `/commands` `/events` `/streams` `/projections`, and `/streams` Aggregate ID copy isolation passes. Group A replay surface remains excluded and owned by `admin-ui-aggregate-state-replay-correctness`.
