# Sprint Change Proposal: Admin UI Stream State-Inspection Cluster — Three Bugs, Two Root Causes

**Date:** 2026-05-05
**Triggered by:** Live debug session on Admin UI `/streams/system/tenants/acme-corp` page. User reported three failures around aggregate state inspection: "State After This Event" always empty; "Inspect State" dialog visually broken; "Diff with Previous" always reports range too large.
**Scope Classification:** Moderate — server endpoint additions in `Hexalith.EventStore` + UI defect fixes in `Hexalith.EventStore.Admin.UI`. Single repo. One consolidated story.
**Related:** Companion to `sprint-change-proposal-2026-05-05-tenant-management-debug-cluster.md` and `sprint-change-proposal-2026-05-05-event-detail-endpoint-missing.md` (same Story 15.x admin-debugging surface; same pattern of UI-first delivery outpacing the EventStore-side endpoint).
**Supersedes:** None.
**Story:** New consolidated story `admin-ui-state-inspection-cluster-fix` filed in `sprint-status.yaml`.

---

## Section 1: Issue Summary

User reported three independent-looking symptoms on the stream detail page:

1. **State After This Event panel** — always shows *"State reconstruction not available at this position"* after a brief load, on every event clicked, on every stream.
2. **Inspect State dialog** — clicking the magnifier "Inspect State" opens a dialog whose body is largely clipped/empty: only the title bar, "Mode: by Timestamp" toggle (partially rendered), sequence input + `−1`/`+1` buttons, and a stuck progress bar are visible. Form, fetch button, and result region are unreachable.
3. **Diff with Previous page** — loads, then reports *"State diff not available — Diff range too large — try comparing closer positions."* Trips on tiny 3-event streams where "too large" is impossible.

**Two distinct root causes, one of which explains symptoms 1 and 3:**

### RC1 — EventStore is missing `/state`, `/diff`, and `/causation` route handlers

`src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` proxies UI calls to the EventStore via DAPR service invocation:

- `GetAggregateStateAtPositionAsync` → `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/state?at={n}` (`:179`)
- `DiffAggregateStateAsync` → `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/diff?from={n}&to={n}` (`:202`)
- `TraceCausationChainAsync` → `api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/causation?at={n}` (`:402`)

But `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` only declares routes for `bisect`, `timeline`, `events/{sequenceNumber:long}`, `blame`, `step`, `sandbox`. **`state`, `diff`, and `causation` do not exist.**

End-to-end mechanism:

- EventStore returns 404 → Admin Server's `EnsureSuccessStatusCode` (`DaprStreamQueryService.cs:513`) throws `HttpRequestException` → `AdminStreamsController` catches and returns 5xx → UI's `AdminStreamApiClient.GetAggregateStateAtPositionAsync` (`:250-257`) returns `null` → `EventDetailPanel.razor:74-79` shows *"State reconstruction not available at this position"*.
- For Diff: same chain, but the UI's hardcoded 5-second timeout (`StateDiffViewer.razor:192`) fires *before* the upstream Admin Server times out (default `ServiceInvocationTimeoutSeconds` ≈ 30s). `Task.WhenAll` propagates the `OperationCanceledException` → diff page lands in the wrong catch branch (`:240-243`) → shows the misleading *"Diff range too large"* message regardless of actual range.

**Compounding defect:** `EventDetailPanel.razor:190-193` uses a bare `catch { _stateSnapshot = null; }` that swallows every exception including auth failures, 503s, and connection errors — making them all indistinguishable from a legitimate "no state at this position".

**Causation collateral:** the "Trace Causation" button on the same panel will fail identically once clicked, but the user did not click it during the report. Folded into this story since same root cause and same file.

### RC2 — `StateInspectorModal` uses an invalid Fluent UI v5 dialog structure

`src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor:6-103`:

```razor
<FluentDialog @ref="_dialog" Modal="true"
              Style="min-width: 500px; max-width: 800px; max-height: 80vh;">
    <FluentDialogBody Style="overflow-y: auto;">
        <TitleTemplate>...</TitleTemplate>
        <ChildContent>...</ChildContent>
    </FluentDialogBody>
</FluentDialog>
```

In Fluent UI Blazor v5, `FluentDialogBody` does not expose `TitleTemplate` / `ChildContent` as separate render-fragment slots; the body is a flat content host. Inline `Style` on `FluentDialog` does not propagate to the v5 dialog wrapper for sizing. Other dialogs in this codebase (`EventDebugger.razor`, `CommandSandbox.razor`, `Tenants.razor`) use the plain `<FluentDialog><FluentDialogBody>…</FluentDialogBody></FluentDialog>` shape with no nested template slots, OR the service pattern `IDialogService.ShowDialogAsync<TComponent>(parameters, options)`. **This is a v5 migration regression** (Epic 21 `fluent-ui-v5-migration`); the test file `StateInspectorModalTests.cs` predates the v5 migration and was not retargeted.

### Inspect State purpose (user asked)

"State After This Event" gives the inline view of aggregate state immediately after the selected event. **Inspect State** is the free-form variant: open a dialog that lets you jump to *any* sequence number (not just the events shown in the timeline), step through with `−1`/`+1`, or look up state by *timestamp* ("what did the aggregate look like at 12:34 UTC?"). Useful for incident forensics when you don't yet know which event matters. Worth keeping if it works — but right now it shares Bug 1's root cause (calls the same missing `/state` endpoint), so even when the layout is fixed it would still display "no state available" until ST1 ships.

### Evidence summary

| # | Source | Reference |
|---|---|---|
| RC1 | EventStore controller route audit | `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` declares `bisect`, `timeline`, `events/{sequenceNumber:long}`, `blame`, `step`, `sandbox`. No `state`, `diff`, or `causation`. |
| RC1 | Admin Server proxy code | `DaprStreamQueryService.cs:179, :202, :402` — endpoints invoked but never defined upstream. |
| RC1 | UI silent-swallow | `EventDetailPanel.razor:190-193` — `catch { _stateSnapshot = null; }`. |
| RC1 | UI timeout misclassification | `StateDiffViewer.razor:192` `new(TimeSpan.FromSeconds(5))` and `:240-243` catch arms. |
| RC2 | Dialog v5 migration | `StateInspectorModal.razor:6-103` nested `TitleTemplate`/`ChildContent` inside `FluentDialogBody`; inline `Style` on `FluentDialog`. Compare with working v5 dialogs in `EventDebugger.razor`, `CommandSandbox.razor`. |

---

## Section 2: Impact Analysis

| Layer | Affected | What breaks |
|---|---|---|
| EventStore service | `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` | Missing 3 route handlers (state, diff, causation) |
| Admin Server (no change needed) | `DaprStreamQueryService.cs`, `AdminStreamsController.cs` | Already correctly proxies — was waiting for upstream that never shipped |
| Admin UI panel | `Components/EventDetailPanel.razor` | Bare catch hides real errors; no retry/distinguishing UX |
| Admin UI dialog | `Components/StateInspectorModal.razor` | Broken layout; inline `Style` ineffective; nested templates |
| Admin UI diff | `Components/StateDiffViewer.razor` | 5 s client timeout shorter than upstream → misleading error message |
| Tests | `StreamDetailPageTests.cs` (Tier 1), `StateInspectorModalTests.cs` (Tier 1), `StateDiffViewerTests.cs` (Tier 1), new Tier 2 EventStore endpoint tests | Existing tests pass against mocks — they did not catch the missing endpoints. Per CLAUDE.md R2-A6 rule, integration tests must hit a real Redis backplane and inspect end-state |

**Epic impact:** none. This is rework on Story 15-4 (aggregate state inspector & diff viewer), one in-flight bundled fix matching the prior `tenant-management-debug-cluster` pattern. No epic restructuring or resequencing.

**MVP impact:** none — the features exist on the UI, they just don't work. Restoring them returns the documented MVP behavior.

---

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment (single bundled story).**

| Option | Verdict | Why |
|---|---|---|
| 1. Direct Adjustment | ✅ **Selected** — one story, low risk | Pure additive on the EventStore side (routes that should always have existed); UI fixes are localized to 3 files. Effort: Medium. Risk: Low. |
| 2. Rollback Story 15-4 | ❌ Rejected | Would remove a working timeline/inspector UI to fix a backend gap. Net loss to user. |
| 3. PRD/MVP review | ❌ Not applicable | Feature set is correct; only missing implementation. No scope dispute. |

Rationale: matches the prior `tenant-management-debug-cluster-fix` and `event-detail-endpoint-missing` (Story 15.12b, commit `15895b8b`) pattern — narrow, surgical, one commit, sprint-status carries one entry.

---

## Section 4: Detailed Change Proposals

### ST1 — Add `/state` endpoint to `AdminStreamQueryController`

**File:** `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`
**Where:** new action method, alongside `GetEventStepFrameAsync` (after `:501`).

- Route: `[HttpGet("{tenantId}/{domain}/{aggregateId}/state")]`
- Query: `[FromQuery] long at` (matches what `DaprStreamQueryService.cs:179` sends — `?at={sequenceNumber}`)
- Behavior: load `allEvents` via `IAggregateActor.GetEventsAsync(0)`; if `at == 0` return empty-state snapshot; else call existing private `ReconstructState(allEvents, at)` (`:1119`); wrap into `AggregateStateSnapshot(tenantId, domain, aggregateId, at, timestamp, stateJson)`. Reuses code already proven in step / blame / bisect paths.
- 400 if `at < 0`. 404 if stream has no events. 200 with snapshot otherwise.

**Rationale:** this is the 80%-shared substrate already used by `step`, `bisect`, `sandbox`. Extracting it as a public route is the missing piece.

### ST2 — Add `/diff` endpoint to `AdminStreamQueryController`

**File:** same controller.

- Route: `[HttpGet("{tenantId}/{domain}/{aggregateId}/diff")]`
- Query: `[FromQuery] long from`, `[FromQuery] long to` (matches `DaprStreamQueryService.cs:202`)
- Behavior: validate `0 ≤ from < to`; reconstruct `stateAtFrom` and `stateAtTo`; call existing private `JsonDiff(stateAtFrom, stateAtTo, "")` (`:1067`); project to `AggregateStateDiff(from, to, changedFields)`.
- 400 on invalid range. 404 if stream empty. 200 with diff (possibly with empty `ChangedFields`) otherwise.

### ST3 — Add `/causation` endpoint to `AdminStreamQueryController`

**File:** same controller.

- Route: `[HttpGet("{tenantId}/{domain}/{aggregateId}/causation")]`
- Query: `[FromQuery] long at`
- Behavior: walk events from sequence 1 to `at`, build `CausationChain` from `CorrelationId`/`CausationId` linkage in `EventEnvelope`. (Confirm exact `CausationChain` DTO contract during implementation.)
- Folded in because same file, same root cause, same defect class — separating buys nothing.

### ST4 — Replace bare catch in `EventDetailPanel.razor` with categorized handling

**File:** `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor`
**Lines:** 187-197 (the `_stateSnapshot` fetch try/finally with bare catch).

New behavior:

- `UnauthorizedAccessException` / `ForbiddenAccessException` → show "Sign-in required to view state" with sign-in retry
- `ServiceUnavailableException` → show "Backend unavailable — retry"
- Other `Exception` → log via injected `ILogger`, show "Failed to reconstruct state — see console" (NOT swallow)
- Genuine `null` (200 OK with empty snapshot) → keep current "State reconstruction not available at this position" — but only this real case should produce this message

**Rationale:** today this catch can hide auth/connection bugs forever. Distinguishing them is the only way the user can self-diagnose.

### ST5 — Fix `StateInspectorModal.razor` to v5-correct dialog structure

**File:** `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor`
**Lines:** 6-103.

Approach: mirror the working pattern from `EventDebugger.razor` / `CommandSandbox.razor`:

- Drop `<TitleTemplate>` and `<ChildContent>` slot wrappers; put the X-button title row and the form as direct flat children of `<FluentDialogBody>`.
- Drop inline `Style="min-width:…; max-width:…; max-height:…"` from `<FluentDialog>`; convert to a CSS class on the inner content wrapper, OR migrate to `IDialogService.ShowDialogAsync<StateInspectorModal>(parameters, new DialogParameters { Width = "640px", Height = "80vh", PreventDismissOnOverlayClick = false })` — final choice to be confirmed during implementation by checking the Fluent UI v5 API via the Fluent UI MCP server.
- Update `StateInspectorModalTests.cs` to assert the new structure renders (test was written against pre-v5 markup and survived a layout-broken upgrade — this is an R2-A6 lesson: tests must assert real DOM, not just lifecycle).

### ST6 — Reduce false-positive timeout on `StateDiffViewer.razor`

**File:** `src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor`
**Lines:** 192, 240-243.

- Raise the 5-second client timeout to 15 s (or align with `ServiceInvocationTimeoutSeconds`). Once `/diff` exists upstream, real diffs return in <1 s; the 15 s ceiling protects against a runaway state replay without misclassifying server-side exceptions as range errors.
- Distinguish `OperationCanceledException` (true client timeout — keep current message but rephrase to *"Diff timed out — try a smaller range"*) from `HttpRequestException` (server failure — show *"Diff request failed — see console"*).

### ST7 — Tests

- **Tier 1 (Admin.UI.Tests):** update `StreamDetailPageTests.cs`, `StateInspectorModalTests.cs`, `StateDiffViewerTests.cs` to assert the corrected error-message branches and the v5-correct dialog DOM.
- **Tier 2 (EventStore.Server.Tests):** add tests for the 3 new endpoints. Per CLAUDE.md **R2-A6**: tests must hit a real Redis backplane and verify the persisted event payload is read and the JSON state is reconstructed correctly — *not* assert on mock invocation counts. Per **R2-A7**: any aggregateId in the URL is treated as ULID-or-opaque; do not `Guid.TryParse`.

### ST8 — Update `sprint-status.yaml`

Add at the same spot as the prior debug cluster entry (around `:348`):

```yaml
  # Admin UI Stream State-Inspection Cluster (sprint-change-proposal-2026-05-05-admin-ui-state-inspection-cluster.md)
  # ST1-ST3: add missing /state, /diff, /causation endpoints in Hexalith.EventStore.
  # ST4-ST7: UI defect fixes + categorized error handling + Fluent UI v5 dialog repair + tests.
  admin-ui-state-inspection-cluster-fix: backlog
```

---

## Section 5: Implementation Handoff

**Scope:** Moderate — single repo, two layers (server endpoints + UI fixes), one consolidated story.

**Hand-off:** Developer agent (Amelia) — direct implementation per the 8 sub-tasks above. No PM/Architect involvement needed.

**Definition of done:**

1. All three endpoints return correct payloads against a real Redis backplane (Tier 2 verifies end-state, not mocks).
2. On `/streams/system/tenants/acme-corp`, clicking each event populates the State After This Event panel with real JSON.
3. Inspect State dialog renders end-to-end (title, mode toggle, form, fetch button, result region) at the documented size.
4. Diff with Previous on a 2-event stream shows actual changed fields, no false-positive timeout.
5. Senior code review (per CLAUDE.md mandatory pipeline stage) passes; review-driven patches applied; commit on a `fix/` branch with Conventional Commits message.

**Out of scope (deferred):**

- Tenant action latency (DAPR cold-start) — separate ticket already noted in the prior tenant-management debug cluster proposal §70.
- Sandbox/Bisect/Blame end-to-end audits — same pattern likely applies; spawn a dedicated audit story if desired (do not bundle here).
