# Story 20.3: Step-Through Event Debugger

Status: review

Size: Medium-Large — 1 new model in Admin.Abstractions, extends `IStreamQueryService` with step-frame method, adds step-frame computation endpoint to CommandApi, adds REST facade to `AdminStreamsController`, extends `AdminStreamApiClient`, creates `EventDebugger.razor` component integrated into `StreamDetail.razor` with deep linking and keyboard navigation. Creates ~5-6 test classes across 3 test projects (~30-40 tests). Third story in Epic 20's Advanced Debugging suite.

## Story

As a **platform operator or developer using the Hexalith EventStore Admin UI**,
I want **a step-through event debugger that lets me navigate forward and backward through an aggregate's event history while seeing the state after each event and the field changes it caused**,
so that **I can understand exactly how aggregate state evolved event-by-event, observe cause-and-effect relationships between events and state, and quickly locate events that changed specific fields — like a time-travel debugger for event-sourced aggregates**.

**Dependency:** Stories 20-1 (Blame View) and 20-2 (Bisect Tool) should be implemented first — all three stories modify `StreamDetail.razor` and the mutual exclusion precedence logic. No code dependency on blame or bisect models. Epic 15 must be complete (done).

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- Event debugger renders correctly in `StreamDetail.razor` with VCR controls, state display, and field changes
- REST endpoint returns structured JSON with combined event + state + diff data
- Forward/backward stepping works with immediate response using snapshot-accelerated state reconstruction
- Auto-play mode animates through events with configurable speed and field-watch pause
- Keyboard navigation (arrow keys, space, Home/End) works when debugger is focused
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Deep linking via `?step={seq}` opens debugger at specified sequence

## Acceptance Criteria

1. **AC1: EventStepFrame model** — A new `EventStepFrame` record in `Admin.Abstractions/Models/Streams/` combines event metadata, aggregate state, and field changes in a single response for one debugging step. Properties: `TenantId` (string), `Domain` (string), `AggregateId` (string), `SequenceNumber` (long — current position), `EventTypeName` (string — type name of the event at this position), `Timestamp` (DateTimeOffset — when this event was recorded), `CorrelationId` (string), `CausationId` (string), `UserId` (string — who initiated the command that produced this event), `EventPayloadJson` (string — raw event payload JSON for inspection), `StateJson` (string — aggregate state JSON after applying this event), `FieldChanges` (IReadOnlyList\<FieldChange\> — fields that changed from the previous state to the state after this event; reuses existing `FieldChange` model), `TotalEvents` (long — total event count in the stream for position display). All string properties validate non-null via null-coalescing to `string.Empty` (following `FieldProvenance` / `BisectStep` deserialization-safe pattern). `ToString()` redacts `EventPayloadJson`, `StateJson`, and field values in `FieldChanges` (SEC-5 compliance). `HasPrevious` is a computed property: `SequenceNumber > 1`. `HasNext` is a computed property: `SequenceNumber < TotalEvents`.

2. **AC2: IStreamQueryService extension** — Add `GetEventStepFrameAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct)` to `IStreamQueryService`. Returns `EventStepFrame`. Throws `ArgumentException` if `sequenceNumber < 1`. Returns the combined event detail, reconstructed state at that sequence, and diff from the previous sequence (or initial field changes for sequence 1).

3. **AC3: Server-side step-frame computation (two-layer delegation)** — The step-frame computation follows the same two-layer pattern as existing `/state`, `/diff`, `/blame`, and `/bisect` endpoints.

   **(a) CommandApi layer (computation):** Add a new step endpoint alongside the existing state/diff/blame/bisect endpoints in CommandApi (same controller, same route prefix pattern). Route: `GET .../step?at={sequenceNumber}`. The endpoint:
   - Validates `at >= 1`. Returns 400 for invalid values.
   - Gets total event count for the stream (from metadata or by probing). Returns 404 if stream doesn't exist, 400 if `at` exceeds total events.
   - Retrieves event detail at sequence `at` (reuses existing `GetEventDetailAsync` equivalent logic — event type, timestamp, correlation/causation IDs, user ID, payload JSON).
   - Reconstructs aggregate state at sequence `at` (reuses existing `GetAggregateStateAtPositionAsync` equivalent logic — benefits from snapshots, O(snapshot_interval) per reconstruction).
   - Computes field changes: if `at == 1`, diff from empty state `{}` to state at 1 (all fields are "added"). If `at > 1`, reconstructs state at `at - 1` and diffs to state at `at` (reuses existing diff logic from `DiffAggregateStateAsync` equivalent). **Optimization:** since state at `at` is already computed, only reconstruct state at `at - 1` for the diff.
   - Returns `EventStepFrame` with all fields populated.
   - **Dispose** intermediate `JsonDocument` / state objects after extracting values to prevent memory leaks.

   **(b) Admin.Server layer (delegation):** `DaprStreamQueryService.GetEventStepFrameAsync()` delegates to CommandApi via `InvokeMethodAsync` (same pattern as `GetAggregateBlameAsync` and `BisectAsync`). Use a **30-second timeout** (same as blame — single state reconstruction + diff is comparable workload). This uses the default `ServiceInvocationTimeoutSeconds` from `AdminServerOptions` — no dedicated timeout override needed since 30s is already the default.

4. **AC4: REST endpoint** — `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/step?at={sequenceNumber}` on `AdminStreamsController`. Parameter `at` is required (long). Requires `ReadOnly` authorization policy (same as all existing stream endpoints). Returns `EventStepFrame` as JSON. Returns 404 if stream doesn't exist. Returns 400 if `at < 1` or `at` exceeds stream length.

5. **AC5: API client method** — Add `GetEventStepFrameAsync(string tenantId, string domain, string aggregateId, long sequenceNumber, CancellationToken ct)` to `AdminStreamApiClient`. Follows the same error handling pattern as `GetAggregateBlameAsync`: catches expected exceptions (Unauthorized, Forbidden, ServiceUnavailable, OperationCanceled) separately, returns null on unexpected errors with logged warning. URL-encodes all path segments with `Uri.EscapeDataString`.

6. **AC6: EventDebugger component** — A new `EventDebugger.razor` component in `Admin.UI/Components/` provides a step-through debugging experience with VCR-style controls, state visualization, and field change highlighting. Layout:

   **Header bar:**
   - "Event Debugger" title with "Close" button (invokes `OnClose` EventCallback).
   - Position display: "Event {N} of {Total} — {EventTypeName}" centered. Including the event type name in the position bar gives immediate context without scanning the info panel below.

   **Navigation controls:**
   - `FluentButton` toolbar with five controls: First (`|<` — `Icons.Regular.Size20.Previous()`), Previous (`<` — `Icons.Regular.Size20.ChevronLeft()`), Play/Pause (toggle — `Icons.Regular.Size20.Play()` / `Icons.Regular.Size20.Pause()`), Next (`>` — `Icons.Regular.Size20.ChevronRight()`), Last (`>|` — `Icons.Regular.Size20.Next()`). Buttons disabled appropriately: First/Previous disabled at sequence 1, Next/Last disabled at last event.
   - **Speed selector** (visible when auto-play is active or ready): `FluentSelect<int>` with options: Slow (2000ms), Normal (1000ms), Fast (500ms), Fastest (200ms). Default: Normal.
   - **Jump-to input**: `FluentNumberField` for entering a specific sequence number with "Go" button. Validates 1 <= value <= TotalEvents.

   **Event info panel:**
   - Compact single row: Event type name (bold), timestamp (relative format using `TimeFormatHelper`), correlation ID (monospace, truncated with `title`), user ID.
   - "View Full Event" `FluentButton` with `Appearance.Lightweight` opens the event payload JSON in a `FluentDialog` using `JsonViewer` shared component.

   **Main display — two sections stacked vertically:**

   **Section 1: Field Changes** (top, collapsed if empty)
   - Header: "Changes at This Event ({N} fields changed)" or "Initial State ({N} fields set)" for sequence 1.
   - `FluentDataGrid<FieldChange>` with columns: Field Path (monospace), Old Value (truncated to 60 chars, full in `title`; shows "—" for sequence 1), New Value (truncated to 60 chars, full in `title`).
   - Watched fields (if any) highlighted with a distinct background color and sorted to top.
   - `FluentSearch` filter for field paths (case-insensitive substring match).

   **Section 2: Current State** (bottom)
   - Header: "Aggregate State at Sequence {N}"
   - `JsonViewer` shared component displaying the full `StateJson`.

   **Watch fields feature (optional but valuable):**
   - "Watch Fields" `FluentButton` opens a popover or inline panel with a `FluentSearch` input.
   - User types field path substrings to add to the watch list (semicolon-separated or one per line).
   - Watched fields appear as `FluentBadge` chips that can be dismissed.
   - **Two distinct match semantics:** (1) **Display highlighting** in the field changes table uses **case-insensitive substring match** (same as BlameViewer's search — watching `Count` highlights `Count`, `ItemCount`, `Items.0.Count`). (2) **Auto-pause trigger** uses **exact field path match** only (watching `Count` pauses only when the field `Count` changes, not `ItemCount`). This prevents false pauses from partial matches while keeping the visual highlighting broad for discovery.
   - During auto-play, the debugger **pauses automatically** when any watched field's **exact path** appears in `FieldChanges` and shows a brief `IssueBanner` info: "Paused — watched field '{fieldPath}' changed."
   - Watched field paths are stored in component state (not persisted across sessions).

   **Keyboard navigation** (active when the component container `<div>` has focus via `tabindex="0"` and `@onkeydown`). The container must call `FocusAsync()` in `OnAfterRenderAsync(firstRender: true)` so keyboard shortcuts work immediately when the debugger opens — without auto-focus the user must click the container first, making keyboard nav appear broken:
   - `ArrowLeft` or `ArrowUp`: Previous event (same as Previous button)
   - `ArrowRight` or `ArrowDown`: Next event (same as Next button)
   - `Space`: Toggle auto-play
   - `Home`: Jump to first event
   - `End`: Jump to last event
   - `Escape`: Close debugger (invoke `OnClose`)

   **Auto-play behavior:**
   - When Play is pressed, the debugger fetches the next frame, renders it, then waits the configured delay before fetching the next. The speed interval is measured from the **completion of one step's render** to the **start of the next fetch** — it is a minimum pause between steps, not a fixed wall-clock timer. Actual pace depends on API latency + configured delay.
   - Auto-play stops when: reaching the last event, user presses Pause/any nav button, a watched field triggers a pause, an API error occurs, the component is disposed.
   - During auto-play, the Play button icon switches to Pause.
   - Speed changes take effect on the next step (do not restart the current delay).

   Component implements `IAsyncDisposable` — cancels any in-flight API calls and auto-play timer via `CancellationTokenSource` on disposal. Uses `PeriodicTimer` or `Task.Delay` for auto-play intervals.

   Component parameters: `TenantId`, `Domain`, `AggregateId` (EditorRequired), `InitialSequence` (long? — starting position; default: 1), `OnClose` (EventCallback), `OnNavigateToEvent` (EventCallback\<long\> — navigates to event detail view in StreamDetail), `OnNavigateToBlame` (EventCallback\<long\>? — opens blame at sequence; nullable for graceful fallback if story 20-1 not deployed), `OnNavigateToBisect` (EventCallback\<(long, long)\>? — opens bisect with good/bad range; nullable for graceful fallback if story 20-2 not deployed).

7. **AC7: StreamDetail.razor integration** — The existing `StreamDetail.razor` page adds a "Step Through" `FluentButton` with `Appearance.Outline` and `Icon="@(new Icons.Regular.Size20.PlayCircle())"` in the toolbar (near the existing "Inspect State", "Diff", "Blame", and "Bisect" buttons). Clicking opens the `EventDebugger` component (replaces the detail panel area, same pattern as `BlameViewer`, `StateDiffViewer`, and `BisectTool`). If an event is currently selected (`?detail=N`), the initial sequence is pre-filled with that value. If no event is selected, the initial sequence defaults to 1 (first event). The `OnNavigateToBlame` callback is wired if story 20-1 is deployed (always, since it is done); `OnNavigateToBisect` callback is wired if story 20-2 is deployed (always, since it precedes 20-3).

8. **AC8: Deep linking** — `StreamDetail.razor` supports a new `?step={seq}` query parameter via `[SupplyParameterFromQuery(Name = "step")]`. When present, the page auto-opens the EventDebugger at the specified sequence on load. Navigating within the debugger updates the URL via `Navigation.NavigateTo(url, forceLoad: false, replace: true)` (update URL as the user steps through events so the current position is always shareable). The `step` parameter is mutually exclusive with `bisect`, `blame`, and `diff` — if multiple are present, precedence is: `step` > `bisect` > `blame` > `diff` (rationale: step-through is the most interactive and specific debugging tool).

9. **AC9: Loading, empty, and error states** — `SkeletonCard` shows during frame loading (initial load and each step). If stream has 0 events: `EmptyState` with "Stream has no events — nothing to step through." If API call fails with non-timeout error: `IssueBanner` with "Unable to load event frame — check server connectivity." If API call times out (`OperationCanceledException`): `IssueBanner` with "Frame load timed out — the aggregate state may be too large to reconstruct." If sequence exceeds stream length (400 response): `IssueBanner` with "Sequence {N} is beyond the stream's {Total} events."

## Tasks / Subtasks

- [x] Task 1: Create EventStepFrame model in Admin.Abstractions (AC: #1)
  - [x] 1.1 Create `EventStepFrame` record in `Models/Streams/EventStepFrame.cs` with all properties, null-coalescing, `HasPrevious`/`HasNext` computed properties, and `ToString()` redaction
- [x] Task 2: Add step-frame computation endpoint to CommandApi (AC: #3a)
  - [x] 2.1 **First**: examine how `DaprStreamQueryService.GetAggregateBlameAsync()` constructs its `InvokeMethodAsync` URL, then find the matching CommandApi controller — the step endpoint goes in that same controller
  - [x] 2.2 Add `GET .../step?at={seq}` endpoint in that same controller, composing existing state reconstruction + event detail + diff logic
  - [x] 2.3 Handle sequence 1 edge case: diff from empty state `{}` to state at 1
  - [x] 2.4 Retrieve total event count from stream metadata
- [x] Task 3: Extend stream query service (AC: #2, #3b)
  - [x] 3.1 Add `GetEventStepFrameAsync` to `IStreamQueryService` interface
  - [x] 3.2 Implement `GetEventStepFrameAsync` in `DaprStreamQueryService` (delegates to CommandApi, uses default 30-second timeout from `ServiceInvocationTimeoutSeconds`)
- [x] Task 4: Add REST facade endpoint on Admin.Server (AC: #4)
  - [x] 4.1 Add `GetEventStepFrameAsync` endpoint to `AdminStreamsController` — validate `at >= 1`, delegate to service
- [x] Task 5: Create UI API client method (AC: #5)
  - [x] 5.1 Add `GetEventStepFrameAsync` to `AdminStreamApiClient` — URL-encode segments, standard error handling
- [x] Task 6: Create EventDebugger component (AC: #6, #9)
  - [x] 6.1 Create `EventDebugger.razor` in `Admin.UI/Components/` — implement navigation controls, position display, event info panel
  - [x] 6.2 Implement state display section with `JsonViewer` and field changes table with `FluentDataGrid<FieldChange>`
  - [x] 6.3 Implement keyboard navigation via `@onkeydown` on focused container
  - [x] 6.4 Implement auto-play with configurable speed, using `Task.Delay` loop with `CancellationTokenSource`
  - [x] 6.5 Implement watch fields feature — field path input, badge chips, auto-pause on watched field change
  - [x] 6.6 Wire `OnNavigateToEvent`, `OnNavigateToBlame`, `OnNavigateToBisect` callbacks
  - [x] 6.7 Implement `IAsyncDisposable` for in-flight API call and auto-play timer cancellation
- [x] Task 7: Integrate into StreamDetail page (AC: #7, #8)
  - [x] 7.1 Add "Step Through" button to `StreamDetail.razor` toolbar
  - [x] 7.2 Add `?step={seq}` deep link support via `[SupplyParameterFromQuery(Name = "step")]`
  - [x] 7.3 Wire EventDebugger into the detail panel area, handle callbacks
  - [x] 7.4 Update mutual exclusion: `step` > `bisect` > `blame` > `diff` precedence
  - [x] 7.5 Update URL as user steps through events (call `UpdateUrl` on each step navigation)
- [x] Task 8: Write tests (all ACs)
  - [x] 8.1 Model tests in Admin.Abstractions.Tests (`Models/Streams/EventStepFrameTests.cs`) — constructor with valid inputs, null-coalescing defaults, `HasPrevious`/`HasNext` computed properties, `ToString()` redaction, serialization round-trip
  - [x] 8.2 Service query tests in Admin.Server.Tests (`Services/`) — verify delegation to CommandApi, timeout behavior, argument validation
  - [x] 8.3 Controller tests in Admin.Server.Tests (`Controllers/`) — parameter validation (`at < 1` → 400), authorization policy, response types
  - [x] 8.4 Edge case tests: (a) sequence 1 (first event) — `FieldChanges` represent initial state population, `HasPrevious` is false; (b) last event — `HasNext` is false; (c) single-event stream — `HasPrevious` and `HasNext` both false; (d) sequence exceeds stream length — 400 error; (e) snapshot invariance — verify identical `FieldChanges` and `StateJson` with and without snapshots at the step position, confirms snapshot acceleration doesn't affect correctness
  - [x] 8.5 EventDebugger component tests in Admin.UI.Tests (`Components/EventDebuggerTests.cs`) — VCR button states (disabled at boundaries), position display with event type name, keyboard navigation (verify auto-focus on open via `FocusAsync`), field changes rendering
  - [x] 8.6 Auto-play tests: play starts advancing, pause stops, speed change takes effect, stops at last event, pauses on watched field change, concurrent step cancellation — rapid-fire Next clicks while auto-play is fetching verify that pending requests are cancelled via `CancellationTokenSource` without race conditions or duplicate renders
  - [x] 8.7 StreamDetail step integration tests in Admin.UI.Tests (`Pages/`) — `?step` + `?bisect` + `?blame` + `?diff` mutual exclusion test (step takes precedence), button renders, EventDebugger opens
  - [x] 8.8 Timeout handling test: simulate exceeding 30-second timeout (via `OperationCanceledException`) — verify UI shows `IssueBanner` with "Frame load timed out" message
  - [x] 8.9 Watch fields test: adding/removing watch fields, substring highlighting in changes table, exact-match auto-pause trigger (watching `Count` pauses on `Count` change but NOT on `ItemCount` change)

## Dev Notes

### Architecture Compliance

This story follows the **exact same architecture** as stories 15-3 (Stream Browser), 15-4 (Aggregate State Inspector & Diff Viewer), 20-1 (Blame View), and 20-2 (Bisect Tool). No new architectural patterns are introduced.

**Data flow:** UI `EventDebugger.razor` -> `AdminStreamApiClient.GetEventStepFrameAsync()` -> HTTP GET -> `AdminStreamsController` (Admin.Server) -> `IStreamQueryService.GetEventStepFrameAsync()` -> `DaprStreamQueryService` invokes CommandApi via DAPR `InvokeMethodAsync` (30-second timeout) -> CommandApi `/step` endpoint reconstructs state, retrieves event detail, computes diff, returns `EventStepFrame`.

**Architecture note:** The dev agent MUST trace the existing `DaprStreamQueryService.GetAggregateBlameAsync()` (or `DiffAggregateStateAsync`) to identify how it constructs the CommandApi URL and which CommandApi controller receives it. The step endpoint goes in that same controller. Do NOT guess — follow the existing pattern.

### Critical Patterns to Follow

1. **Model records**: Use the null-coalescing to `string.Empty` pattern (no throwing constructors) for deserialization safety. Redact `EventPayloadJson`, `StateJson`, and field values in `ToString()` (SEC-5).
2. **Reuse `FieldChange`**: The field changes in `EventStepFrame.FieldChanges` reuse the existing `FieldChange` record from `Admin.Abstractions/Models/Streams/FieldChange.cs`. Do NOT create a duplicate model.
3. **Service interface**: Add to existing `IStreamQueryService` — do NOT create a new interface.
4. **Controller**: Add to existing `AdminStreamsController` — do NOT create a new controller. Use `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]`.
5. **API client**: Add to existing `AdminStreamApiClient` — do NOT create a new client class. Follow the `GetAggregateBlameAsync` error handling pattern exactly.
6. **Component**: Follow `BlameViewer.razor` / `BisectTool.razor` structure — parameters for identity, `OnClose` callback, `SkeletonCard` for loading, `EmptyState` for no data, `IssueBanner` for errors.
7. **Deep linking**: Follow `StreamDetail.razor` existing pattern with `[SupplyParameterFromQuery]` and `NavigateTo(url, forceLoad: false, replace: true)`. Use the established `UpdateUrl()` method for URL updates.
8. **Tests**: Follow existing test patterns — xUnit + Shouldly for models, NSubstitute for service/controller mocks.
9. **Timeout**: Use the default 30-second `ServiceInvocationTimeoutSeconds` from `AdminServerOptions` — no dedicated timeout override needed. The step endpoint performs one state reconstruction + one diff, comparable to blame workload.
10. **Shared components**: Reuse `JsonViewer`, `SkeletonCard`, `EmptyState`, `IssueBanner`, `StatCard`, `TimeFormatHelper` from `Admin.UI/Components/Shared/`. Never recreate these.

### Existing Code to Reuse (DO NOT Reinvent)

| What | Where | Why |
|------|-------|-----|
| State reconstruction | `GetAggregateStateAtPositionAsync` in CommandApi | Step frame reconstructs state at sequence N |
| Event detail retrieval | `GetEventDetailAsync` in CommandApi | Step frame retrieves event metadata and payload |
| JSON diff | `DiffAggregateStateAsync` in CommandApi | Step frame diffs state at N-1 vs N for field changes |
| `FieldChange` model | `Admin.Abstractions/Models/Streams/FieldChange.cs` | Reuse for field changes at each step |
| `AggregateStateSnapshot` | `Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs` | State reconstruction result type |
| `EventDetail` model | `Admin.Abstractions/Models/Streams/EventDetail.cs` | Event metadata source |
| `JsonViewer` | `Admin.UI/Components/Shared/JsonViewer.razor` | State JSON display |
| `SkeletonCard`, `EmptyState`, `IssueBanner` | `Admin.UI/Components/Shared/` | Loading/error/empty states |
| `TimeFormatHelper` | `Admin.UI/Components/Shared/TimeFormatHelper.cs` | Timestamp formatting |
| `StatCard` | `Admin.UI/Components/Shared/StatCard.razor` | Position display |
| URL building helpers | `AdminStreamApiClient` static methods | `Uri.EscapeDataString` patterns |
| `InvokeCommandApiAsync` | `DaprStreamQueryService` | DAPR service invocation with JWT forwarding |
| URL update pattern | `StreamDetail.razor` `UpdateUrl()` method | Building query params and NavigateTo |
| FluentUI components | `FluentButton`, `FluentNumberField`, `FluentSelect`, `FluentDataGrid`, `FluentSearch`, `FluentBadge`, `FluentDialog` | Standard UI toolkit |

### File Locations

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventStepFrame.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/EventStepFrameTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Controllers/` — add step computation endpoint in the **same controller** that handles the existing `/diff`, `/state`, `/blame`, and `/bisect` admin stream routes (find by tracing `DaprStreamQueryService.DiffAggregateStateAsync` URL construction)
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` — add `GetEventStepFrameAsync`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — implement `GetEventStepFrameAsync` (delegates to CommandApi, 30s timeout)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — add step facade endpoint
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` — add `GetEventStepFrameAsync`
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` — add Step Through button, `?step` query param, EventDebugger integration, update mutual exclusion precedence

### Anti-Patterns to Avoid

- **Do NOT** create a separate `IEventDebuggerService` — extend `IStreamQueryService`
- **Do NOT** create a new `AdminDebuggerController` — extend `AdminStreamsController`
- **Do NOT** create a new `AdminDebuggerApiClient` — extend `AdminStreamApiClient`
- **Do NOT** duplicate `FieldChange`, `EventDetail`, or `AggregateStateSnapshot` models — reuse from `Admin.Abstractions/Models/Streams/`
- **Do NOT** use `<FluentTooltip>` for field values in tables — use native `title` attribute (performance, same as health heatmap in 19-5, blame in 20-1, bisect in 20-2)
- **Do NOT** add a NavMenu entry for the debugger — it's accessed via StreamDetail page only
- **Do NOT** make multiple client-side API calls per step (state + event detail + diff separately) — use the single combined `/step` endpoint that returns `EventStepFrame`
- **Do NOT** reconstruct state from scratch at each step — leverage snapshot acceleration (state reconstruction should be O(snapshot_interval), not O(N))
- **Do NOT** hold both state-at-N and state-at-N-1 in memory after the diff is computed — dispose intermediate `JsonDocument` objects promptly
- **Do NOT** use `setInterval` or JavaScript interop for auto-play — use C# `Task.Delay` with `CancellationTokenSource` in the Blazor component
- **Do NOT** hardcode auto-play speeds — use the speed selector options defined in AC6
- **Do NOT** add new `AdminServerOptions` properties — the step endpoint uses the default `ServiceInvocationTimeoutSeconds` (30s) which is already configured and validated

### Sequence 0 Is Not Supported

The debugger starts at sequence 1 (the first real event). Sequence 0 is not a valid step position — there is no event at sequence 0, no event type, no payload, no correlation ID. The "state before any events" question is answered by sequence 1's `FieldChanges`: when `SequenceNumber == 1`, all fields are shown as "added" from empty state `{}`, making the initial state fully visible. This is consistent with how `StateDiffViewer` handles `FromSequence == 0`. Note that story 20-2 (bisect) allows `goodSequence = 0` because bisect compares *state* not *events* — the debugger navigates *events*.

### URL Update Throttling During Auto-Play

During auto-play, each step would normally call `Navigation.NavigateTo` to update `?step={seq}`. At "Fast" speed this generates multiple NavigateTo calls per second. To avoid excessive browser history manipulation and potential performance impact, **throttle URL updates during auto-play to at most once per second**. Buffer the latest sequence number and flush to the URL on a 1-second timer. When auto-play stops (pause, watched field, end of stream), immediately flush the current sequence to the URL. Manual stepping (button clicks, keyboard) should update the URL immediately on each step — throttling only applies during auto-play.

### Step-Through vs Existing StateInspectorModal

The existing `StateInspectorModal.razor` already has +1/-1 sequence stepping with state JSON display. The `EventDebugger` differs in key ways:
1. **Shows field changes** at each step (not just state) — the core "what did this event do?" question
2. **Shows event metadata** — type, timestamp, correlation ID, user (the "who caused this?" question)
3. **Auto-play mode** — animate through event history to observe state evolution
4. **Watch fields** — pause auto-play when specific fields change (efficient alternative to manual scanning)
5. **Keyboard navigation** — rapid stepping without clicking
6. **Deep linking** — each position is a shareable URL
7. **Integrated in panel area** (not a modal) — more space for data display, works alongside the timeline

The `EventDebugger` is a debugging power tool; the `StateInspectorModal` is a quick inspection utility. They complement each other.

### Performance Considerations

Each step requires the server to:
1. Reconstruct state at sequence N (O(snapshot_interval) with snapshots at every 100 events)
2. Reconstruct state at sequence N-1 (also O(snapshot_interval); often shares the same snapshot as N)
3. Diff the two states (O(field_count))
4. Retrieve event detail at N (single key lookup)

With 100-event snapshot intervals, each step reconstructs ~200 events max. This should complete well within the 30-second timeout. For adjacent steps (N, then N+1), the server cannot cache state between requests (stateless), but snapshots ensure each step is independently fast.

**Single-pass optimization opportunity:** When the CommandApi reconstructs state at sequence N by replaying events from the nearest snapshot, the intermediate state at sequence N-1 is computed as part of that replay. A smart implementation can capture both states in a single pass (record state *before* applying the last event, then apply it) rather than reconstructing twice independently. This is not mandated — the naive approach (two independent reconstructions) is correct and within timeout — but if the dev agent sees a clean way to implement single-pass capture, it halves the event replay cost per step.

Auto-play at "Fastest" speed (200ms intervals) generates ~5 requests/second. The 30-second timeout provides natural backpressure — if a step takes longer than expected, the next auto-play step waits for it to complete before advancing.

**Concurrent step safety:** When the user clicks Next/Previous while an auto-play fetch is in-flight, the component must cancel the pending request via `CancellationTokenSource` before issuing the new one. Use `InvokeAsync(() => StateHasChanged())` inside the auto-play loop to ensure UI updates happen on the Blazor synchronization context — direct `StateHasChanged()` calls from a `Task.Delay` loop risk cross-thread exceptions.

### Previous Story Intelligence

**From story 20-1 (Blame View — done):**
- `BlameViewer.razor` component pattern — debugger follows same component parameter structure
- `?blame` deep link — `?step` follows same pattern
- 30-second timeout for API calls — step uses same timeout (default `ServiceInvocationTimeoutSeconds`)
- `ToString()` redaction for SEC-5 — follow same pattern
- Null-coalescing (`?? string.Empty`) in record constructors — follow same pattern
- `FluentDataGrid` with search filter — debugger reuses same pattern for field changes table

**From story 20-2 (Bisect Tool — predecessor):**
- `BisectTool.razor` with three-phase UI (setup/loading/result) — debugger uses a simpler single-phase UI with VCR controls
- `?bisect` deep link with mutual exclusion — extend to four-way: `step` > `bisect` > `blame` > `diff`
- `IAsyncDisposable` for canceling in-flight calls — debugger follows same pattern for both API calls and auto-play timer
- `OnNavigateToEvent` / `OnNavigateToBlame` callback pattern — debugger adds `OnNavigateToBisect` too
- `FluentNumberField` for sequence input — debugger reuses for jump-to-sequence

**From story 15-4 (State Inspector — foundation):**
- `StateInspectorModal.razor` has +1/-1 stepping — debugger is the enhanced version with diff + event detail + auto-play
- `StateDiffViewer.razor` shows field changes — debugger integrates this inline per step

### Git Intelligence

Recent commits show Epic 20 stories 20-1 (Blame View) merged successfully. Story 20-2 (Bisect Tool) is in-progress. Both follow the identical architecture: models in Admin.Abstractions, service extension in IStreamQueryService, DAPR delegation in DaprStreamQueryService, CommandApi computation endpoint, AdminStreamsController facade, AdminStreamApiClient, Blazor component, StreamDetail integration with deep linking. Story 20-3 follows this exact same pattern.

### Project Structure Notes

- Alignment with unified project structure confirmed — all paths follow existing `src/Hexalith.EventStore.Admin.*` conventions
- No new projects needed — step-through debugger fits entirely within existing Admin.Abstractions, Admin.Server, CommandApi, and Admin.UI projects
- Test projects already exist for all affected projects
- No new NuGet dependencies needed — all UI components (FluentUI) and patterns already available

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 20] Epic definition — "step-through event debugger" listed as story 20-3
- [Source: _bmad-output/planning-artifacts/prd.md#FR70] Point-in-time state exploration — "show aggregate state at any historical event position"
- [Source: _bmad-output/planning-artifacts/prd.md#FR71] State diff viewer — "diff aggregate state between any two event positions, highlighting changed fields"
- [Source: _bmad-output/planning-artifacts/prd.md#Executive Summary] "Event stream as time machine — enabling 'what was the state at time T?' queries"
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR42] Deep linking — every view has unique shareable URL
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR44] Blame view (related — debugger provides complementary navigation)
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR45] State diff viewer (related — debugger shows per-step diffs)
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR48] Virtualized rendering for large data sets
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR49] Keyboard navigation — Vim-style shortcuts and accessibility
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P4] Admin Three-Interface Architecture — single Admin.Server hosts API + Blazor UI
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5] Event payload data never appears in logs
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs] Service interface to extend
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs] Existing model to reuse for field changes
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventDetail.cs] Existing event detail model
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs] Existing state snapshot model
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs] DAPR implementation to extend — trace URL construction for CommandApi
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs] Controller to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs] Options (no changes needed — uses existing ServiceInvocationTimeoutSeconds)
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs] API client to extend
- [Source: src/Hexalith.EventStore.Admin.UI/Components/BlameViewer.razor] Component pattern reference
- [Source: src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor] Component pattern reference (once 20-2 is implemented)
- [Source: src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor] Diff display pattern reference
- [Source: src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor] Existing +1/-1 stepping (debugger extends this concept)
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/JsonViewer.razor] JSON display component to reuse
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor] Page to integrate debugger into — follow existing deep link and mutual exclusion patterns
- [Source: _bmad-output/implementation-artifacts/20-1-blame-view-per-field-provenance.md] Predecessor story — patterns and learnings
- [Source: _bmad-output/implementation-artifacts/20-2-bisect-tool-binary-search-state-divergence.md] Predecessor story — patterns, mutual exclusion, and callback wiring

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Fixed `IssueBanner` property mismatch (`Message` -> `Title`/`Description`/`Visible`)
- Fixed `FluentSelect` generic type (`int` -> `string` with parse)
- Fixed `FluentNumberField` binding (`long` -> `long?`)
- Fixed nullable `EventCallback<T>` (struct, can't be nullable — use `HasDelegate`)
- Removed `ConfigureAwait(false)` from Blazor component (breaks sync context)
- Replaced `FluentBadge.OnDismissClick` (not available) with nested `FluentButton`
- Fixed bUnit test exception handling (`Task.FromException` instead of `ThrowsAsync`)

### Completion Notes List

- All 8 tasks completed
- 383 model tests pass (including 8 new EventStepFrame tests)
- 344 server tests pass (including 5 new controller tests)
- 432 UI tests pass (including 12 new EventDebugger tests)
- Single-pass optimization implemented in CommandApi (captures state at N-1 during replay to N)
- Auto-play with URL throttling (1-second buffer during playback)
- Watch fields with exact-match pause during auto-play, substring-match highlighting in display
- Keyboard navigation: Arrow keys, Home/End, Space (play/pause), Escape (close)
- Deep linking: `?step={seq}` with mutual exclusion precedence `step > bisect > blame > diff`

### File List

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/EventStepFrame.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/EventStepFrameTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/EventDebuggerTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Controllers/AdminStreamQueryController.cs` — added `/step` endpoint
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` — added `GetEventStepFrameAsync`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — implemented `GetEventStepFrameAsync`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — added step facade endpoint
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` — added `GetEventStepFrameAsync`
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` — added Step Through button, `?step` deep link, EventDebugger integration
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs` — added 5 step endpoint tests
