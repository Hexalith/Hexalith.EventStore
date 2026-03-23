# Story 15.4: Aggregate State Inspector & Diff Viewer

Status: done
Size: Large — ~8 new files, 6 task groups, 12 ACs (~16-20 hours estimated)

## Definition of Done

- All 12 ACs verified
- Merge-blocking bUnit tests green (Task 5 blocking tests)
- Recommended bUnit tests green
- Project builds with zero warnings in CI (`dotnet build --configuration Release`)
- No new analyzer suppressions

## Story

As a **developer investigating aggregate state changes in the Hexalith EventStore admin dashboard**,
I want **a state inspector panel that shows aggregate state at any historical event position (including by timestamp) and a side-by-side diff viewer that highlights field-level changes between any two positions**,
so that **I can understand exactly how an aggregate's state evolved over time, pinpoint which events caused specific field changes, and debug state issues without replaying events manually**.

## Acceptance Criteria

1. **"Inspect State" button on event detail** — In the existing `EventDetailPanel.razor`, add an "Inspect State" button next to the inline state preview. Clicking it opens the `StateInspectorModal` for the current event's sequence number. The existing inline state preview (from story 15-3) continues to work unchanged.
2. **StateInspectorModal** — Modal dialog (`FluentDialog`) allowing the user to inspect state at any position. Inputs: a `FluentNumberField` for sequence number (pre-filled from clicked event) and a `FluentTextField` for ISO 8601 timestamp. Toggle between "By Sequence" and "By Timestamp" modes. On submit, fetches aggregate state and displays it via `JsonViewer`. Close via X, Escape, or backdrop click.
3. **State at timestamp** — When a developer enters a timestamp (ISO 8601 format), the system finds the nearest event at or before that timestamp and returns the aggregate state after that event. **Primary approach (client-side resolution):** fetch the timeline via `GetStreamTimelineAsync` with `toSequence` derived from a broad range, find the last `TimelineEntry` whose `Timestamp <= requestedTimestamp`, then call `GetAggregateStateAtPositionAsync` with that entry's sequence number. If no events exist before the timestamp, show "No state available before this timestamp." **Note:** The server endpoint `AdminStreamsController.GetAggregateState` currently accepts `sequenceNumber` only — it does NOT accept a `timestamp` param. A server-side timestamp resolution endpoint is a future enhancement; do NOT modify Admin.Server in this story.
4. **"Compare" context action on timeline** — In `StreamTimelineGrid.razor`, add a "Compare" toggle button in the filter bar area. When active, timeline rows show checkboxes and an instruction banner: "Select two events to compare their state changes." **In compare mode, row click toggles the checkbox** (does NOT open the detail panel). User selects up to two rows; once two are selected, remaining checkboxes are disabled, the banner updates to "2 selected — click View Diff", and the "View Diff" button is shown prominently above the grid. User must deselect one before selecting a different row. The lower sequence becomes `fromSequence`, the higher becomes `toSequence`. Opens the `StateDiffViewer` panel.
5. **StateDiffViewer panel** — Side-by-side diff view replacing the detail panel (same master-detail slot as `EventDetailPanel`). Left side: state at `fromSequence`. Right side: state at `toSequence`. Header shows "Comparing state: #{fromSeq} → #{toSeq}" with timestamps. Close button returns to normal timeline detail mode.
6. **Field-level change highlighting** — The diff viewer fetches `GET /api/v1/admin/streams/{t}/{d}/{id}/diff?fromSequence={}&toSequence={}` returning `AggregateStateDiff`. Changed fields displayed in a `FluentDataGrid` with columns: Field Path (monospace, e.g., `order.items[2].quantity`), Old Value (red background tint), New Value (green background tint). Unchanged fields are not shown.
7. **Full state context in diff** — Below the field change table, show two collapsible `JsonViewer` panels: "Full State at #{fromSeq}" and "Full State at #{toSeq}". These use separate `GetAggregateStateAtPositionAsync` calls. Default collapsed to save space; expandable for full context.
8. **Graceful 404 handling** — When state is not available at a requested position (API returns 404), show "No state at this position" message in the inspector modal or diff viewer. When diff returns 404 for either endpoint, show "State diff not available — one or both positions have no state."
9. **State diff from event detail** — In the existing `EventDetailPanel`, add a "Diff with Previous" button (visible when `SequenceNumber >= 1`). Clicking it opens `StateDiffViewer` with `fromSequence = currentSeq - 1` and `toSequence = currentSeq`, showing what changed due to this specific event. **Edge case:** When `SequenceNumber == 1`, `fromSequence` is 0 — there is no state at position 0. The diff viewer must treat missing "from" state as empty (`{}`), showing all fields as additions (green, no old value). This is the "initial state creation" diff.
10. **Deep linking** — Diff view: `/streams/{t}/{d}/{id}?diff=5-15` (fromSeq-toSeq). Inspector: `/streams/{t}/{d}/{id}?inspect=42` (sequence number). Parse URL params in `StreamDetail.razor` to restore view state on load. **Normalize `?diff=` params:** always interpret as `Min(a,b)-Max(a,b)` regardless of order in the URL — `?diff=15-5` is equivalent to `?diff=5-15`.
11. **Accessibility** — `StateDiffViewer`: field change table has `aria-label="Field changes between sequence {from} and {to}"`. Old/new values use `aria-label` with "previous value" / "new value" prefixes (not color-only). `StateInspectorModal`: focus trap, `aria-modal="true"`, auto-focus on sequence input. All semantic color uses icon + text alongside color (WCAG AA compliance).
12. **Performance** — State snapshot retrieval completes within render cycle (no blocking). Diff calculation displays within 200ms for typical diffs (<50 field changes). Large diffs (>100 changes) show first 50 with "Show all N changes" expansion button.

## Tasks / Subtasks

- [x] **Task 1: Extend AdminStreamApiClient** (AC: 3, 6, 7)
  - [x]1.1 Add `GetAggregateStateDiffAsync(string tenantId, string domain, string aggregateId, long fromSequence, long toSequence, CancellationToken ct = default)` → `AggregateStateDiff?` via `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/diff?fromSequence={}&toSequence={}`. Return `null` on 404.
  - [x]1.2 Add `GetAggregateStateAtTimestampAsync(string tenantId, string domain, string aggregateId, DateTimeOffset timestamp, CancellationToken ct = default)` → `AggregateStateSnapshot?`. **Client-side resolution with 3-call cap:** (1) Fetch timeline page with `count=200` sorted by sequence descending (most recent first). (2) Scan entries for the last `TimelineEntry` where `Timestamp <= timestamp`. (3) If not found in this page and more entries exist, fetch one more page with `toSequence` = first entry's sequence - 1. **Cap at 3 timeline API calls maximum** — if still not found after 3 calls, return `null` and the UI should show "Timestamp too far in history — use sequence number instead." Once the nearest entry is found, call `GetAggregateStateAtPositionAsync` with that entry's `SequenceNumber`. This avoids modifying Admin.Server and prevents runaway pagination for large aggregates.
  - [x]1.3 Follow existing error handling pattern: 401 → `UnauthorizedAccessException`, 403 → `ForbiddenAccessException`, 503 → `ServiceUnavailableException`. Return null on 404.
  - [x]1.4 All methods use 5-second timeout from existing HttpClient configuration.
  - [x]1.5 Mark existing `GetAggregateStateAtPositionAsync` as already implemented (story 15-3). The new methods complement it.
  - [x]1.6 **Checkpoint**: Build compiles with zero warnings.

- [x] **Task 2: StateInspectorModal component** (AC: 1, 2, 3, 8, 11)
  - [x]2.1 Create `Components/StateInspectorModal.razor` — `FluentDialog` with:
    - Parameters: `[Parameter, EditorRequired] string TenantId`, `Domain`, `AggregateId`, `long? InitialSequenceNumber`, `EventCallback OnClose`
    - Two input modes toggled by `FluentSwitch`: "By Sequence" (default, prominent) showing `FluentNumberField` with `Min="0"`, "By Timestamp" (secondary) showing `FluentTextField` with placeholder `2026-03-23T12:00:00Z` and tooltip "Enter ISO 8601 format: YYYY-MM-DDThh:mm:ssZ". Validate timestamp on submit via `DateTimeOffset.TryParse` — show inline validation error for malformed input.
    - Submit button fetches state via `GetAggregateStateAtPositionAsync` (sequence mode) or `GetAggregateStateAtTimestampAsync` (timestamp mode — client-side resolution, see Task 1.2)
    - **Modal stays open after submit** — result displayed in `JsonViewer` below inputs. User can change the sequence number and re-submit without reopening. Add **+1 / -1 navigation buttons** next to the sequence input for quick adjacent position inspection.
    - 404/null: show "No state available at this position" via `EmptyState` (inline in the modal, not closing it)
    - Loading: skeleton placeholder during fetch
  - [x]2.2 Accessibility: `aria-modal="true"`, focus trap (Fluent UI built-in for `FluentDialog`), auto-focus sequence input on open, Escape closes
  - [x]2.3 Add "Inspect State" `FluentButton` (Appearance.Outline, icon: magnifying glass) to `EventDetailPanel.razor` next to existing inline state preview section header. On click: open `StateInspectorModal` with `InitialSequenceNumber` = current event sequence.
  - [x]2.4 **Checkpoint**: Modal opens, fetches state, displays JSON.

- [x] **Task 3: Compare mode on timeline** (AC: 4, 10)
  - [x]3.1 Add "Compare" `FluentToggleButton` to `TimelineFilterBar.razor` (or `StreamTimelineGrid.razor` header area). When toggled on: (a) show checkboxes in a new first column of the timeline grid, (b) show an **inline instruction banner** above the grid with progressive text: initial "Select two events to compare their state changes" → after 1 selected "1 of 2 selected — pick another" → after 2 selected "2 selected — click View Diff". Banner dismisses when compare mode is toggled off.
  - [x]3.2 Track selected rows in `HashSet<long> _compareSequences` (max 2). When 2 are selected, **disable further checkboxes** (remaining rows' checkboxes greyed out) and show "View Diff" button prominently above the grid. User must deselect one before selecting a different row. Show selection count: "Selected: {count}/2". **On pagination (Newer/Older):** clear `_compareSequences` and show toast "Compare selection cleared — selections don't persist across pages." Track by sequence number, not row index — survives real-time row insertion from `DashboardRefreshService`.
  - [x]3.3 "View Diff" `FluentButton` (Appearance.Accent) enabled only when exactly 2 rows selected. On click: compute `fromSequence = Min(selected)`, `toSequence = Max(selected)`. Raise `EventCallback<(long From, long To)> OnCompareRequested`.
  - [x]3.4 In `StreamDetail.razor`: handle `OnCompareRequested` by setting `_diffMode = true`, `_diffFrom`, `_diffTo`. Replace detail panel content with `StateDiffViewer`. Update URL to `?diff={from}-{to}`.
  - [x]3.5 Parse `?diff=5-15` URL param in `StreamDetail.razor` `OnInitializedAsync` to restore diff view on page load. **Normalize:** always interpret as `Min(a,b)` to `Max(a,b)` regardless of URL order.
  - [x]3.6 Parse `?inspect=42` URL param to auto-open `StateInspectorModal` on page load.
  - [x]3.7 **Checkpoint**: Compare flow works end-to-end.

- [x] **Task 4: StateDiffViewer component** (AC: 5, 6, 7, 8, 9, 11, 12)
  - [x]4.1 Create `Components/StateDiffViewer.razor`:
    - Parameters: `[Parameter, EditorRequired] string TenantId`, `Domain`, `AggregateId`, `long FromSequence`, `long ToSequence`, `EventCallback OnClose`
    - On render: launch 3 API calls in parallel but **track each independently** — do NOT use bare `Task.WhenAll` that loses per-call error info. Instead: `var diffTask = ...; var fromStateTask = ...; var toStateTask = ...; await Task.WhenAll(diffTask, fromStateTask, toStateTask);` then check each `.Result` individually. If diff succeeds but a state call returns null, show diff table + warning for missing state.
    - Header: "Comparing state: #{FromSequence} → #{ToSequence}" with **"Back to Timeline"** button (not just X icon — explicit label for clarity)
  - [x]4.2 **Field change table**: `FluentDataGrid` bound to `AggregateStateDiff.ChangedFields`:
    - Column 1: Field Path — monospace, left-aligned
    - Column 2: Old Value — monospace, red background tint `rgba(var(--error-color-rgb, 248, 81, 73), 0.1)`, truncated with tooltip for long values
    - Column 3: New Value — monospace, green background tint `rgba(var(--success-color-rgb, 46, 160, 67), 0.1)`, truncated with tooltip
    - Each cell has `aria-label` prefix ("Previous value:" / "New value:") for screen readers
    - If >100 changes: show first 50 with "Show all {N} changes" `FluentButton`
  - [x]4.3 Create `Components/StateDiffViewer.razor.css` — scoped styles for diff colors (light/dark mode), field path monospace, value truncation
  - [x]4.4 **Full state panels**: Two collapsible `<details>` sections below the change table:
    - "Full State at #{FromSequence}" → `JsonViewer` with `FromState` JSON
    - "Full State at #{ToSequence}" → `JsonViewer` with `ToState` JSON
    - Default collapsed. Use `<FluentAccordion>` / `<FluentAccordionItem>` for consistent Fluent UI styling.
  - [x]4.5 **Error handling**:
    - If diff returns null (404): show "State diff not available — one or both positions have no state." via `EmptyState`.
    - If diff request **times out** (5s): show "Diff range too large — try comparing closer positions" (not generic error).
    - If diff returns **empty `ChangedFields`** (identical states): show "No differences found between sequence #{from} and #{to} — states are identical." via `EmptyState` (not an empty table).
    - If one state fetch fails but diff succeeds: show diff table with warning "Full state at #{seq} not available."
    - If `FromSequence == 0` (initial state diff): treat missing "from" state as empty `{}` — all fields shown as additions (green, no old value).
  - [x]4.6 Loading: skeleton table during fetch. Show progress: "Loading diff..." then "Loading full states..." sequentially.
  - [x]4.7 **Checkpoint**: Diff viewer displays field changes with highlighting.

- [x] **Task 4b: "Diff with Previous" in EventDetailPanel** (AC: 9)
  - [x]4b.1 In `EventDetailPanel.razor`, add "Diff with Previous" `FluentButton` (**Appearance.Accent** — visually prominent, highest-impact feature). Visible when `SequenceNumber >= 1` (see AC9 edge case for sequence 1).
  - [x]4b.2 On click: raise `EventCallback<(long From, long To)> OnDiffRequested` with `(SequenceNumber - 1, SequenceNumber)`. For `SequenceNumber == 1`, this sends `(0, 1)` — `StateDiffViewer` handles the `FromSequence == 0` edge case (see Task 4.5). Parent `StreamDetail.razor` handles this same as compare flow.
  - [x]4b.3 **Checkpoint**: One-click diff from event detail works.

- [x] **Task 5: Unit tests (bUnit)** (AC: 1-12)
  - **Mock `AdminStreamApiClient`** — use NSubstitute
  - **Merge-blocking tests** (must pass):
  - [x]5.1 Test `StateInspectorModal` renders with pre-filled sequence number and fetches state on submit (AC: 2)
  - [x]5.2 Test `StateInspectorModal` shows "No state available" on null API response (AC: 8)
  - [x]5.3 Test `StateDiffViewer` renders field change table with correct columns and data (AC: 6)
  - [x]5.4 Test `StateDiffViewer` shows old value with red tint and new value with green tint CSS classes (AC: 6)
  - [x]5.5 Test `StateDiffViewer` shows "State diff not available" on null diff response (AC: 8)
  - [x]5.6 Test "Compare" toggle enables checkboxes in timeline grid (AC: 4)
  - [x]5.7 Test "View Diff" button enabled only when exactly 2 rows selected (AC: 4)
  - [x]5.8 Test "Inspect State" button in EventDetailPanel opens modal (AC: 1)
  - **Recommended tests**:
  - [x]5.9 Test `StateDiffViewer` renders collapsible full state panels (AC: 7)
  - [x]5.10 Test large diff (>100 changes) shows "Show all" expansion (AC: 12)
  - [x]5.11 Test "Diff with Previous" button visible when SequenceNumber >= 1, hidden when SequenceNumber == 0 (AC: 9)
  - [x]5.12 Test deep linking: `?diff=5-15` param parsed correctly (AC: 10)
  - [x]5.13 Test timestamp mode toggle in StateInspectorModal (AC: 3)
  - [x]5.14 Test "Diff with Previous" at sequence 1 shows all fields as additions (AC: 9 edge case)
  - [x]5.15 Test empty diff (identical states) shows "No differences found" message (AC: 8)
  - [x]5.16 Test compare instruction banner shows progressive text (AC: 4)
  - [x]5.17 Test inspector modal stays open after submit and allows re-query (AC: 2)

- [x] **Task 6: Integration verification** (AC: 10, 11)
  - [x]6.1 Verify deep links work: `/streams/{t}/{d}/{id}?diff=5-15` and `?inspect=42` restore correct views
  - [x]6.2 Verify keyboard navigation: Tab through diff table cells, Escape closes modal/panel
  - [x]6.3 Verify `forced-colors` mode: diff colors use border/icon instead of background tint. **Fallback:** if `FluentDataGrid` cell borders conflict with Fluent UI built-in borders, use a leading icon column (minus-circle / plus-circle) instead of border-left — icons are color-independent and always accessible.
  - [x]6.4 Verify real-time: `DashboardRefreshService.OnDataChanged` does NOT reset diff view or modal state

## Dev Notes

### Architecture Compliance

- **ADR-P4**: Admin.UI communicates exclusively via HTTP REST API to Admin.Server. No DaprClient in UI. The `AdminStreamApiClient` wraps `HttpClient` calls.
- **ADR-P5**: No observability deep-links in this story. Deferred to story 15-7.
- **SEC-5**: Never log `StateJson`, `OldValue`, `NewValue` in any log statement. `AggregateStateSnapshot.ToString()` and `FieldChange.ToString()` already redact these fields.

### Existing Code to Reuse (DO NOT Recreate)

| What | Where | How |
|------|-------|-----|
| `AggregateStateSnapshot` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs` | Fields: TenantId, Domain, AggregateId, SequenceNumber, Timestamp, StateJson |
| `AggregateStateDiff` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateDiff.cs` | Fields: FromSequence, ToSequence, ChangedFields (IReadOnlyList\<FieldChange\>) |
| `FieldChange` record | `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs` | Fields: FieldPath, OldValue, NewValue. SEC-5 redaction in ToString(). |
| `IStreamQueryService` interface | `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` | Has `GetAggregateStateAtPositionAsync` and `DiffAggregateStateAsync` methods already |
| `AdminStreamsController` | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` | Has `GET .../state?sequenceNumber={}` and `GET .../diff?fromSequence={}&toSequence={}` endpoints |
| `AdminStreamApiClient` | `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` | Has `GetAggregateStateAtPositionAsync`. Add diff + timestamp methods. |
| `JsonViewer.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/JsonViewer.razor` | Reuse for state display in inspector modal and diff viewer full state panels |
| `EventDetailPanel.razor` | `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor` | Modify: add "Inspect State" and "Diff with Previous" buttons |
| `StreamTimelineGrid.razor` | `src/Hexalith.EventStore.Admin.UI/Components/StreamTimelineGrid.razor` | Modify: add compare mode with checkboxes |
| `TimelineFilterBar.razor` | `src/Hexalith.EventStore.Admin.UI/Components/TimelineFilterBar.razor` | Modify: add "Compare" toggle button |
| `StreamDetail.razor` | `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` | Modify: handle diff/inspect URL params, render StateDiffViewer |
| `StatusBadge.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatusBadge.razor` | Reuse for any status display |
| `StatCard.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor` | Reuse if needed for summary stats |
| `EmptyState.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor` | Reuse for 404/error states |
| `SkeletonCard.razor` | `src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor` | Reuse for loading states |
| `ViewportService` | `src/Hexalith.EventStore.Admin.UI/Services/ViewportService.cs` | Reuse for responsive layout decisions |
| `DashboardRefreshService` | `src/Hexalith.EventStore.Admin.UI/Services/DashboardRefreshService.cs` | Signal pattern for real-time — do NOT reset diff/inspect state on signal |

### API Endpoints Used

```
# State at position (existing from 15-3):
GET /api/v1/admin/streams/{t}/{d}/{id}/state?sequenceNumber={seq}
                                                → AggregateStateSnapshot (404 = not available)

# State diff between two positions (NEW API client method):
GET /api/v1/admin/streams/{t}/{d}/{id}/diff?fromSequence={}&toSequence={}
                                                → AggregateStateDiff (404 = not available)

# Timeline (existing — used by client-side timestamp resolution):
GET /api/v1/admin/streams/{t}/{d}/{id}/timeline?toSequence={}&count=1000
                                                → PagedResult<TimelineEntry>
```

**RESOLVED — Timestamp approach**: The server endpoint `AdminStreamsController.GetAggregateState` accepts `sequenceNumber` only — no `timestamp` param. Timestamp resolution is handled **client-side** in `GetAggregateStateAtTimestampAsync`: fetch timeline, find the last entry where `Timestamp <= requested`, then call state-at-position with that sequence. No Admin.Server modifications needed.

### SEC-5 Scope Clarification — UI Display vs Logging

`FieldChange.ToString()` and `AggregateStateSnapshot.ToString()` redact `OldValue`, `NewValue`, and `StateJson` — this is for **logging safety only**. The UI MUST display the actual property values (`.OldValue`, `.NewValue`, `.StateJson`) to be useful. Never call `.ToString()` on these records in the UI — always access the raw properties directly. SEC-5 applies to logs and console output, NOT to UI rendering.

### Large FieldChange Values in Diff Table

If `FieldChange.OldValue` or `NewValue` exceeds **1KB**, do not render inline in the table cell. Instead:
- Show truncated preview (first 80 chars) with "..." suffix in the table cell
- Clicking the cell opens a popover or expander with the full value rendered via `JsonViewer`
- This prevents the diff table from becoming unusable when a single field change involves a large nested object or array

### Tenant Context in Admin UI

`TenantId` flows from the page's route parameters (`/streams/{TenantId}/{Domain}/{AggregateId}`), not from user input fields. All API calls in the inspector modal and diff viewer inherit this route-level `TenantId`. There is no cross-tenant risk as long as the dev uses the route param consistently — do NOT accept tenant input inside the modal or diff viewer.

### FluentDataGrid Column Spec (Diff Table)

| Column | Property | Alignment | Font | Width | Notes |
|--------|----------|-----------|------|-------|-------|
| Field Path | `FieldPath` | Left | Monospace | Flex 1fr | Full JSON path |
| Old Value | `OldValue` | Left | Monospace | 200px min | Red background tint, truncate >80 chars |
| New Value | `NewValue` | Left | Monospace | 200px min | Green background tint, truncate >80 chars |

### Diff Color Scheme

| Change Type | Light Mode | Dark Mode | Icon |
|-------------|-----------|-----------|------|
| Removed (old value) | `rgba(207, 34, 46, 0.1)` bg | `rgba(248, 81, 73, 0.1)` bg | minus circle |
| Added (new value) | `rgba(26, 127, 55, 0.1)` bg | `rgba(46, 160, 67, 0.1)` bg | plus circle |

In `forced-colors` mode: replace background tint with `border-left: 3px solid` using system colors `LinkText` (old) and `Highlight` (new).

### Deep Linking URL Strategy

| View State | URL | Behavior on Load |
|------------|-----|------------------|
| Diff view | `/streams/{t}/{d}/{id}?diff=5-15` | Open StateDiffViewer with from=5, to=15 |
| Inspector | `/streams/{t}/{d}/{id}?inspect=42` | Open StateInspectorModal for seq 42 |
| Combined | `/streams/{t}/{d}/{id}?detail=42&diff=5-15` | Diff takes priority over detail |

### FluentDataGrid Checkbox Column — Verify Before Building

Fluent UI Blazor v4 provides `SelectColumn<T>` for row selection in `FluentDataGrid`. **Before implementing a custom checkbox column**, verify that `SelectColumn` supports:
- Multi-select mode (selecting exactly 2 rows)
- Programmatic enable/disable of individual row checkboxes
- `SelectedItems` binding for tracking selections

If `SelectColumn` doesn't support disabling individual checkboxes when 2 are selected, use a `TemplateColumn` with manual `FluentCheckbox` elements and `_compareSequences` state management instead.

### Timestamp Input UX

The timestamp input in `StateInspectorModal` uses `FluentTextField` with ISO 8601 format. Since this is a developer-facing tool:
- Add a **tooltip** on the timestamp input: "Enter ISO 8601 format: YYYY-MM-DDThh:mm:ssZ"
- Add a **helper text** below the input showing the current time in ISO 8601 as a reference
- Validate input format on submit — show inline validation error for malformed timestamps
- Accept both `Z` (UTC) and `+HH:mm` offset formats

### Code Patterns to Follow

1. **File-scoped namespaces** (`namespace Hexalith.EventStore.Admin.UI.Components;`)
2. **Allman brace style**
3. **Private fields**: `_camelCase`
4. **4-space indentation**, CRLF, UTF-8
5. **Nullable enabled**, **implicit usings enabled**
6. **Primary constructors** for services
7. **`ConfigureAwait(false)`** on all async calls
8. **`CancellationToken`** parameter on all async methods
9. **`IAsyncDisposable`** on components with subscriptions
10. **`[Parameter, EditorRequired]`** for required component parameters
11. **`EventCallback<T>`** for parent-child communication
12. **`virtual` methods** on service classes for NSubstitute mocking
13. **`ArgumentException`** with validation in record init accessors
14. **Monospace class** (`class="mono"`) for all technical identifiers

### File Structure (New/Modified Files)

```
src/Hexalith.EventStore.Admin.UI/
  Services/
    AdminStreamApiClient.cs                    (MODIFY: add GetAggregateStateDiffAsync, GetAggregateStateAtTimestampAsync)
  Components/
    StateInspectorModal.razor                  (NEW: modal dialog for state inspection at any position/timestamp)
    StateDiffViewer.razor                      (NEW: side-by-side diff with field change table)
    StateDiffViewer.razor.css                  (NEW: diff colors, field path monospace, value truncation)
    EventDetailPanel.razor                     (MODIFY: add "Inspect State" + "Diff with Previous" buttons)
    StreamTimelineGrid.razor                   (MODIFY: add compare mode with checkboxes)
    TimelineFilterBar.razor                    (MODIFY: add "Compare" toggle button)
  Pages/
    StreamDetail.razor                         (MODIFY: handle ?diff=, ?inspect= URL params, render StateDiffViewer/StateInspectorModal)
  wwwroot/
    css/
      app.css                                  (MODIFY: diff viewer colors, compare mode checkbox styles, forced-colors rules)

tests/Hexalith.EventStore.Admin.UI.Tests/
  Components/
    StateInspectorModalTests.cs                (NEW)
    StateDiffViewerTests.cs                    (NEW)
```

### Anti-Patterns to Avoid

- Do NOT add DaprClient to Admin.UI — use `AdminStreamApiClient` calling REST API only
- Do NOT reference Admin.Server project — only Admin.Abstractions for DTOs
- Do NOT create new DTO types — `AggregateStateDiff`, `AggregateStateSnapshot`, `FieldChange` already exist
- Do NOT modify existing DTO records — they are shared contracts used by CLI and MCP
- Do NOT create a custom diff algorithm — the server calculates diffs via `DiffAggregateStateAsync`
- Do NOT use external diff/JSON libraries — use existing `JsonViewer` component and Fluent UI
- Do NOT hardcode Admin.Server URL — use Aspire service discovery via HttpClientFactory
- Do NOT use `.sln` files — use `.slnx` only
- Do NOT skip `ConfigureAwait(false)` on async calls
- Do NOT log StateJson, OldValue, or NewValue (SEC-5)
- Do NOT poll the API — use `DashboardRefreshService` signal pattern (and preserve diff/inspect state on refresh)
- Do NOT implement blame view (per-field provenance showing which event set each field) — that's story 20-1
- Do NOT implement bisect tool (binary search through state history) — that's story 20-2
- Do NOT implement step-through debugger — that's story 20-3

### Out of Scope (Deferred)

| Deferred Item | Deferred To |
|---------------|-------------|
| `ChangedByEvent` field on `FieldChange` DTO — identifying which event caused each field change | Story 20-1 (Blame View) |
| Blame view (per-field provenance — which event last set each field) | Story 20-1 |
| Bisect tool (binary search through state history to find divergence) | Story 20-2 |
| Step-through event debugger (forward/backward with watch expressions) | Story 20-3 |
| Event replay sandbox (replay events, compare replayed vs stored state) | Story 20-3 |
| Projection dashboard | Story 15-5 |
| Event type catalog | Story 15-6 |
| Observability deep-links | Story 15-7 |

### Previous Story Intelligence (15-3)

Key learnings from story 15-3 implementation:
- **Razor compiler issues**: Switch expressions using `<` operator are interpreted as HTML tags. Use if-else chains instead.
- **RenderFragment with RenderTreeBuilder lambdas**: Not supported in .razor files. Extract complex rendering to separate components.
- **StatusDisplayConfig nested in @code block**: Requires `@using static` import in consuming components.
- **`IDisposable.Dispose()` explicit interface implementation**: Fails in Razor. Use `public void Dispose()` instead.
- **`SupplyParameterFromQuery`**: Parameters cannot be set via bUnit `.Add()`. Test child components directly.
- **ViewportService JS interop**: Must handle `JSDisconnectedException` gracefully — default to wide layout during prerender.
- **Event detail inline state**: Already shows state via `GetAggregateStateAtPositionAsync`. The inspector modal adds sequence/timestamp selection; the diff viewer adds comparison.
- **Master-detail slot**: The detail panel area in `StreamDetail.razor` uses conditional rendering based on `_selectedSequence`. Extend this pattern with `_diffMode` flag to show `StateDiffViewer` instead of `EventDetailPanel`/`CommandDetailPanel`.

### Git Intelligence

Recent commits (story 15-3 and review):
- `8b7ad94` chore: Update story 15-3 status to done after code review
- `cf7cf50` fix: Apply 23 code review patches for story 15-3 stream browser
- `a7a8473` feat: Add stream browser with command/event timeline view (story 15-3)
- `ca242c3` feat: Implement AdminStreamApiClient for interacting with Admin.Server API

Patterns: Feature-folder organization, bUnit test infrastructure established, AdminStreamApiClient extension pattern (add virtual methods + follow error handling conventions), code review yielding 23 patches per story.

### Project Structure Notes

- All new components go in `src/Hexalith.EventStore.Admin.UI/Components/` (feature-level) not in `Components/Shared/` (shared components are generic reusable ones like JsonViewer, StatusBadge)
- Tests go in `tests/Hexalith.EventStore.Admin.UI.Tests/Components/`
- No new packages needed — all dependencies already in `Directory.Packages.props`
- No new service registrations needed in `Program.cs` — all existing services suffice

### References

- [Source: _bmad-output/planning-artifacts/epics.md — Story 15.4, FR70, FR71]
- [Source: _bmad-output/planning-artifacts/architecture.md — ADR-P4, ADR-P5, state store key patterns, snapshot strategy]
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md — UX-DR45 (state diff viewer), UX-DR42 (deep linking), UX-DR48 (virtualized rendering), semantic color vocabulary]
- [Source: _bmad-output/planning-artifacts/prd.md — FR70 (aggregate state at any position/timestamp), FR71 (diff state between positions)]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateDiff.cs — Diff contract]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs — Snapshot contract]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs — Field change contract]
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs — DiffAggregateStateAsync, GetAggregateStateAtPositionAsync]
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs — GET .../diff, GET .../state endpoints]
- [Source: _bmad-output/implementation-artifacts/15-3-stream-browser-command-event-query-timeline.md — Previous story patterns, code conventions, anti-patterns]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

None — clean implementation with zero warnings.

### Completion Notes List

- Task 1: Added `GetAggregateStateDiffAsync` and `GetAggregateStateAtTimestampAsync` to `AdminStreamApiClient`. Diff method follows existing 404-returns-null pattern. Timestamp method uses client-side resolution with 3-call cap to avoid modifying Admin.Server.
- Task 2: Created `StateInspectorModal.razor` as a `FluentDialog` with sequence/timestamp toggle, +1/-1 navigation buttons, JsonViewer display, and EmptyState for 404. Modal stays open after submit. Added "Inspect State" button to `EventDetailPanel.razor`.
- Task 3: Added "Compare" toggle to `TimelineFilterBar.razor`. Extended `StreamTimelineGrid.razor` with checkbox column, progressive instruction banner (0/1/2 selected), View Diff button, and pagination clears selections. Updated `StreamDetail.razor` with compare mode state, `?diff=` and `?inspect=` deep linking with normalization.
- Task 4: Created `StateDiffViewer.razor` with parallel API calls (diff + 2 state fetches tracked independently), field change table with diff-colored columns, "Show all N changes" for large diffs, collapsible full state panels via FluentAccordion, and comprehensive error handling (404, timeout, empty diff, initial state at seq 0). Created scoped CSS with light/dark/forced-colors support.
- Task 4b: Added "Diff with Previous" button (Appearance.Accent) to EventDetailPanel, visible when SequenceNumber >= 1. Raises OnDiffRequested callback with (seq-1, seq).
- Task 5: Created 21 bUnit tests across 3 test files covering all merge-blocking and recommended test cases. All 100 Admin.UI tests pass. All 695 Tier 1 tests pass with zero regressions.
- Task 6: Full build passes with zero warnings. Deep linking ?diff= and ?inspect= params implemented with normalization. Forced-colors CSS uses border-left fallback. DashboardRefreshService does NOT reset diff/inspect state on refresh.

### Change Log

- 2026-03-23: Implemented aggregate state inspector modal, state diff viewer, compare mode, and deep linking (Story 15-4)

### File List

**New files:**
- src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor
- src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor
- src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor.css
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateDiffViewerTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/StreamTimelineCompareTests.cs

**Modified files:**
- src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs
- src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor
- src/Hexalith.EventStore.Admin.UI/Components/StreamTimelineGrid.razor
- src/Hexalith.EventStore.Admin.UI/Components/TimelineFilterBar.razor
- src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor
- src/Hexalith.EventStore.Admin.UI/wwwroot/css/app.css
- _bmad-output/implementation-artifacts/sprint-status.yaml
