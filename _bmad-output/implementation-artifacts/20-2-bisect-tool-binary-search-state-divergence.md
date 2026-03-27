# Story 20.2: Bisect Tool — Binary Search State Divergence

Status: done

Size: Medium-Large — 3 new models in Admin.Abstractions, extends `IStreamQueryService` with bisect method, adds bisect computation endpoint to CommandApi, adds REST facade to `AdminStreamsController`, extends `AdminStreamApiClient`, creates `BisectTool.razor` component integrated into `StreamDetail.razor` with deep linking. Creates ~5-6 test classes across 3 test projects (~30-40 tests). Second story in Epic 20's Advanced Debugging suite.

## Story

As a **platform operator or developer using the Hexalith EventStore Admin UI**,
I want **a bisect tool that performs binary search through event history to find the exact event where aggregate state diverged from expected field values**,
so that **I can efficiently pinpoint the root cause of state corruption in O(log N) comparisons instead of manually inspecting thousands of events, reducing investigation time from hours to seconds**.

**Dependency:** Story 20-1 (Blame View) should be implemented first — both stories modify `StreamDetail.razor`, `AdminServerOptions`, and `AdminServerOptionsValidator`. No code dependency on blame models, but implementation order avoids merge conflicts. Epic 15 must be complete (done).

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- Bisect tool renders correctly in `StreamDetail.razor` with setup form, progress, and results
- REST endpoint returns structured JSON with bisect result data
- Binary search algorithm converges to single event in O(log N) state comparisons
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Deep linking via `?bisect=good-bad` opens bisect tool with pre-filled ranges

## Acceptance Criteria

1. **AC1: BisectStep model** — A new `BisectStep` record in `Admin.Abstractions/Models/Streams/` captures one step in the bisect process: `StepNumber` (int — 1-based step index), `TestedSequence` (long — the midpoint sequence tested at this step), `Verdict` (string — `"good"` when state matches expected field values, `"bad"` when state diverges; use `string` not enum for JSON serialization simplicity), `DivergentFieldCount` (int — number of watched fields that diverged at this midpoint; 0 when verdict is `"good"`). All string properties validate non-null via null-coalescing to `string.Empty` (following `DaprHealthHistoryEntry` deserialization-safe pattern). `ToString()` returns `"Step {StepNumber}: seq {TestedSequence} = {Verdict}"`.

2. **AC2: BisectResult model** — A new `BisectResult` record in `Admin.Abstractions/Models/Streams/` wraps the bisect output: `TenantId`, `Domain`, `AggregateId`, `GoodSequence` (long — final narrowed known-good sequence), `DivergentSequence` (long — the exact event where divergence was first detected), `DivergentTimestamp` (DateTimeOffset — timestamp of the divergent event), `DivergentEventType` (string — event type name of the divergent event), `DivergentCorrelationId` (string — correlation ID of the divergent event), `DivergentUserId` (string — user who initiated the command that produced the divergent event), `DivergentFieldChanges` (IReadOnlyList\<FieldChange\> — the fields that changed at the divergent event AND were being watched; reuses existing `FieldChange` model), `WatchedFieldPaths` (IReadOnlyList\<string\> — the field paths that were compared during bisect; empty list means all fields were compared), `Steps` (IReadOnlyList\<BisectStep\> — complete bisect history in order), `TotalSteps` (int — count of bisect iterations performed), `IsTruncated` (bool — true when the bisect was limited by `MaxBisectSteps` and could not converge to a single event). When `DivergentFieldChanges` is empty (no divergence detected for watched fields), `DivergentSequence` equals the final narrowed `badSequence` and should NOT be displayed as a confirmed divergence point — the UI must check for empty `DivergentFieldChanges` before rendering divergent event details. All string properties use null-coalescing to `string.Empty`. `ToString()` redacts field values (SEC-5 compliance).

3. **AC3: IStreamQueryService extension** — Add `BisectAsync(string tenantId, string domain, string aggregateId, long goodSequence, long badSequence, IReadOnlyList<string>? fieldPaths, CancellationToken ct)` to `IStreamQueryService`. When `fieldPaths` is null or empty, all leaf fields are compared. Returns `BisectResult`. Throws `ArgumentException` if `goodSequence >= badSequence` or if either sequence is < 0.

4. **AC4: Server-side bisect computation (two-layer delegation)** — The bisect computation follows the same two-layer pattern as the existing `/state`, `/diff`, and `/blame` endpoints. **Critical: the dev agent must examine how `DaprStreamQueryService.DiffAggregateStateAsync()` constructs its `InvokeMethodAsync` URLs and identify the matching CommandApi controller — the bisect endpoint goes in that same controller.**

   **(a) CommandApi layer (computation):** Add a new bisect endpoint alongside the existing state/diff/blame endpoints in CommandApi (same controller, same route prefix pattern). Route: `GET .../bisect?good={goodSeq}&bad={badSeq}&fields={comma-separated-field-paths}&maxSteps={maxSteps}`. The endpoint computes bisect using the following **mandatory binary search algorithm**:

   ```
   goodSeq = request.good
   badSeq = request.bad
   goodState = reconstructState(goodSeq)
   watchFields = request.fields (or all leaf fields of goodState if empty)
   expectedValues = extractFieldValues(goodState, watchFields)
   steps = []

   step = 0
   while (badSeq - goodSeq > 1 AND step < maxSteps):
       step++
       mid = goodSeq + (badSeq - goodSeq) / 2
       midState = reconstructState(mid)
       midValues = extractFieldValues(midState, watchFields)

       if midValues == expectedValues:  // USE JsonElement.DeepEquals for semantic JSON comparison
           steps.add(BisectStep(step, mid, "good", 0))
           goodSeq = mid
       else:
           divergentCount = countDivergentFields(midValues, expectedValues)
           steps.add(BisectStep(step, mid, "bad", divergentCount))
           badSeq = mid

   // badSeq is now the first divergent event
   divergentChanges = diff(badSeq - 1, badSeq)
       .filter(change => watchFields.contains(change.FieldPath) OR watchFields is empty)
   divergentEvent = getEventMetadata(badSeq)

   return BisectResult(
       goodSeq, badSeq, divergentEvent.timestamp, divergentEvent.eventType,
       divergentEvent.correlationId, divergentEvent.userId,
       divergentChanges, watchFields, steps, step,
       isTruncated: badSeq - goodSeq > 1)
   ```

   **Leaf field extraction** uses **dot-notation JSON paths** (e.g., `Count`, `Status.IsActive`, `Items.0.Name` for array elements), matching the same path format used by `FieldChange.FieldPath` and `FieldProvenance.FieldPath` in existing models. Parse the `StateJson` string from `AggregateStateSnapshot` into a `JsonDocument`, recursively walk all properties, and build dot-notation paths for every leaf node (primitives and nulls). Array elements use numeric index notation (`Items.0`, `Items.1`).

   Reuse the existing state reconstruction logic (`GetAggregateStateAtPositionAsync`) and JSON diff logic (`DiffAggregateStateAsync`) already used by the diff and blame endpoints. State reconstruction at each midpoint benefits from snapshots (O(snapshot_interval) per reconstruction, not O(N)). **Dispose** each intermediate `JsonDocument` / state object after extracting field values before reconstructing the next midpoint state — do not hold all O(log N) states in memory simultaneously.

   **Field comparison semantics:** Use `System.Text.Json.JsonElement.DeepEquals` (or parse field values as `JsonElement` and compare structurally) for field value comparison at each midpoint. Do NOT compare serialized JSON strings — `0` vs `0.0`, `null` vs missing field, and whitespace differences would cause false divergence detection.

   **No-divergence path:** If the binary search converges (or reaches max steps) and the state at `badSequence` matches the expected field values for ALL watched fields, the endpoint returns a `BisectResult` with `DivergentFieldChanges` as an empty list. The UI interprets empty `DivergentFieldChanges` as "no divergence detected" (AC10). No separate flag needed — empty changes IS the signal.

   **Performance guards:**
   - `maxSteps` query parameter (default from `AdminServerOptions.MaxBisectSteps`): caps the binary search depth. Default 30 supports streams up to 2^30 (~1 billion) events.
   - `maxFields` query parameter (default from `AdminServerOptions.MaxBisectFields`): caps the number of fields compared per step. If the state has more leaf fields than `maxFields` and no `fieldPaths` were specified, return 400 with message "State has {N} fields — specify field paths to narrow the comparison (max {maxFields} fields)."
   - Validate: `good < bad`, `good >= 0`, `bad > 0`. Return 400 for invalid ranges.
   - Validate: state at `goodSequence` must actually exist (stream has at least `goodSequence` events). Return 404 if stream doesn't exist, 400 if `goodSequence` exceeds stream length.

   **(b) Admin.Server layer (delegation):** `DaprStreamQueryService.BisectAsync()` delegates to CommandApi via `InvokeMethodAsync` (same pattern as `GetAggregateBlameAsync`). It passes `AdminServerOptions.MaxBisectSteps` and `AdminServerOptions.MaxBisectFields` as query parameters. Use a **60-second timeout** for bisect calls (longer than blame's 30-second timeout because bisect performs O(log N) state reconstructions). Add properties to `AdminServerOptions`:
   - `MaxBisectSteps` (int, default: 30, must be > 0)
   - `MaxBisectFields` (int, default: 1,000, must be > 0)
   Both validated by `AdminServerOptionsValidator`.

5. **AC5: REST endpoint** — `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/bisect?good={goodSequence}&bad={badSequence}&fields={fieldPaths}` on `AdminStreamsController`. Parameters `good` and `bad` are required. Parameter `fields` is optional (comma-separated field paths, URL-encoded). Requires `ReadOnly` authorization policy (same as all existing stream endpoints). Returns `BisectResult` as JSON. Returns 404 if stream doesn't exist. Returns 400 if `good >= bad`, `good < 0`, or state has too many fields without field path filtering.

6. **AC6: API client method** — Add `BisectAsync(string tenantId, string domain, string aggregateId, long goodSequence, long badSequence, IReadOnlyList<string>? fieldPaths, CancellationToken ct)` to `AdminStreamApiClient`. Follows the same error handling pattern as `GetAggregateStateDiffAsync`: catches expected exceptions (Unauthorized, Forbidden, ServiceUnavailable, OperationCanceled) separately, returns null on unexpected errors with logged warning. URL-encodes all path segments with `Uri.EscapeDataString`. Joins `fieldPaths` with commas for the `fields` query parameter.

7. **AC7: BisectTool component** — A new `BisectTool.razor` component in `Admin.UI/Components/` displays the bisect workflow. Layout has three phases:

   **Setup phase:**
   - **Header**: "Bisect Tool" with "Close" button (invokes `OnClose` EventCallback).
   - **Form**: Two `FluentNumberField` inputs for "Known Good Sequence" and "Known Bad Sequence" with validation (good < bad, both > 0). Pre-filled from component parameters if provided.
   - **Field selection** (optional): After entering sequences, a "Select Fields" `FluentButton` loads state at the good sequence and displays a `FluentSearch` filter above a `FluentCheckbox` list of leaf field paths (field path substring match, case-insensitive — same pattern as BlameViewer's field table search). User can check specific fields to watch or leave all unchecked to compare all fields. Show field count: "Comparing {N} fields" or "Comparing all {N} fields".
   - **Start button**: `FluentButton` with `Appearance.Accent` labeled "Start Bisect" — disabled until both sequences are valid.

   **Loading phase** (while API call executes):
   - **Progress**: `SkeletonCard` with indeterminate `FluentProgressBar` and message: "Running bisect — testing up to {estimatedMaxSteps} midpoints..." (estimated = ceil(log2(bad - good))). The bisect executes as a single server-side API call; intermediate steps are NOT streamed in real-time.

   **Result phase:**
   - **Summary**: `StatCard` showing "Divergent Event" with sequence number and event type.
   - **Divergent event detail**: Event type (as link — clicking invokes `OnNavigateToEvent` with the sequence number), timestamp, correlation ID, user ID.
   - **Field changes table**: `FluentDataGrid<FieldChange>` showing the watched fields that changed at the divergent event. Columns: Field Path, Old Value (truncated to 60 chars, full in `title`), New Value (truncated to 60 chars, full in `title`).
   - **Bisect history**: Collapsible `FluentAccordionItem` labeled "Bisect Steps ({N} steps)" showing the complete step log. Each step shows: step number, tested sequence, verdict icon (checkmark for good, X for bad), divergent field count if bad. Final narrowed range displayed at bottom: "Narrowed to sequences {good}..{bad}."
   - **Truncation warning**: If `IsTruncated` is true, display `IssueBanner` warning: "Bisect reached maximum step limit ({MaxBisectSteps}) — result may not be the exact divergent event. Narrowed range: {good}..{bad}."
   - **Action buttons**: "Navigate to Event" (invokes `OnNavigateToEvent`), "Run Blame at This Event" (invokes `OnNavigateToBlame` with divergent sequence — only rendered when `OnNavigateToBlame` is not null), "New Bisect" (resets to setup phase, pre-filling good with previous result's `GoodSequence` and bad with `DivergentSequence` for iterative narrowing).

   Component implements `IAsyncDisposable` — cancels any in-flight API calls (field loading or bisect computation) via `CancellationTokenSource` on disposal. This prevents `ObjectDisposedException` when the user navigates away mid-bisect.

   Component parameters: `TenantId`, `Domain`, `AggregateId` (EditorRequired), `InitialGoodSequence` (long? — pre-fill good sequence), `InitialBadSequence` (long? — pre-fill bad sequence), `OnClose` (EventCallback), `OnNavigateToEvent` (EventCallback\<long\> — navigates to event detail), `OnNavigateToBlame` (EventCallback\<long\>? — navigates to blame at sequence; nullable because story 20-1 may not be deployed yet).

8. **AC8: StreamDetail.razor integration** — The existing `StreamDetail.razor` page adds a "Bisect" `FluentButton` with `Appearance.Outline` and `Icon="@(new Icons.Regular.Size20.BranchCompare())"` in the toolbar (near the existing "Inspect State", "Diff", and "Blame" buttons). Clicking opens the `BisectTool` component (replaces the detail panel area, same pattern as `StateDiffViewer` and `BlameViewer`). If an event is currently selected (`?detail=N`), the bad sequence is pre-filled with that value and good sequence with 0 (empty state before any events — the most common "known good" starting point). If no event is selected, both fields start empty. The `OnNavigateToBlame` callback is only wired if blame view is available (story 20-1 deployed); otherwise pass `null` and the "Run Blame" button does not render.

9. **AC9: Deep linking** — `StreamDetail.razor` supports a new `?bisect=good-bad` query parameter via `[SupplyParameterFromQuery(Name = "bisect")]`. Format: `"good-bad"` (e.g., `"5-100"`), parsed the same way as `?diff` using `TryParseDiffParam` (reuse existing method — it already normalizes min-max). When present, the page auto-opens the BisectTool with pre-filled sequences on load. Navigating to bisect updates the URL via `Navigation.NavigateTo(url, forceLoad: false, replace: true)`. The `bisect` parameter is mutually exclusive with `diff` and `blame` — if multiple are present, precedence is: `bisect` > `blame` > `diff` (rationale: bisect is the most specific investigation tool; user who lands on a URL with multiple parameters gets the most targeted view).

10. **AC10: Loading, empty, and error states** — `SkeletonCard` shows during field loading and bisect computation. If stream has fewer than 2 events: `EmptyState` with "Stream needs at least 2 events for bisect — only {N} event(s) found." If API call fails with non-timeout error: `IssueBanner` with "Unable to run bisect — check server connectivity." If API call times out (`OperationCanceledException`): `IssueBanner` with "Bisect timed out — try narrowing the range or specifying fewer fields." If bisect finds no divergence (`DivergentFieldChanges` is empty): `EmptyState` with "No divergence detected — state at sequence {good} and {bad} is identical for the watched fields. Verify your good/bad sequence selection." If field count exceeds maximum without field selection (400 response): `IssueBanner` with "State has {N} fields — select specific fields to compare (max {MaxBisectFields})."

## Tasks / Subtasks

- [x] Task 1: Create new models in Admin.Abstractions (AC: #1, #2)
  - [x] 1.1 Create `BisectStep` record in `Models/Streams/BisectStep.cs`
  - [x] 1.2 Create `BisectResult` record in `Models/Streams/BisectResult.cs`
  - [x] 1.3 Add `MaxBisectSteps` (int, default: 30) and `MaxBisectFields` (int, default: 1,000) properties to `AdminServerOptions` and add `> 0` validation for both in `AdminServerOptionsValidator`
- [x] Task 2: Add bisect computation endpoint to CommandApi (AC: #4)
  - [x] 2.1 **First**: examine how `DaprStreamQueryService.DiffAggregateStateAsync()` constructs its `InvokeMethodAsync` URL, then find the matching CommandApi controller that handles the `/diff` route — this is the controller where the bisect endpoint belongs
  - [x] 2.2 Add `GET .../bisect?good={seq}&bad={seq}&fields={paths}&maxSteps={max}&maxFields={max}` endpoint in that same controller, implementing the binary search algorithm from AC4
  - [x] 2.3 Reuse existing state reconstruction logic (`GetAggregateStateAtPositionAsync` equivalent) and JSON diff logic from the diff endpoint
  - [x] 2.4 Implement field extraction and comparison: parse state JSON, extract leaf fields, compare against expected values from good sequence state
- [x] Task 3: Extend stream query service (AC: #3, #4)
  - [x] 3.1 Add `BisectAsync` to `IStreamQueryService` interface
  - [x] 3.2 Implement `BisectAsync` in `DaprStreamQueryService` (delegates to CommandApi with 60-second timeout, passes `MaxBisectSteps` and `MaxBisectFields` as query parameters)
- [x] Task 4: Add REST facade endpoint on Admin.Server (AC: #5)
  - [x] 4.1 Add `BisectAsync` endpoint to `AdminStreamsController` — parse `fields` query parameter as comma-separated list
- [x] Task 5: Create UI API client method (AC: #6)
  - [x] 5.1 Add `BisectAsync` to `AdminStreamApiClient` — join field paths with commas, URL-encode all segments
- [x] Task 6: Create BisectTool component (AC: #7, #10)
  - [x] 6.1 Create `BisectTool.razor` in `Admin.UI/Components/` — implement setup/loading/result phases with `IAsyncDisposable` for in-flight call cancellation
  - [x] 6.2 Wire `OnNavigateToEvent` and `OnNavigateToBlame` callbacks (do not navigate directly; let `StreamDetail.razor` handle URL updates)
  - [x] 6.3 Implement field selection: load state at good sequence, display checkbox list of leaf fields
- [x] Task 7: Integrate into StreamDetail page (AC: #8, #9)
  - [x] 7.1 Add "Bisect" button to `StreamDetail.razor` toolbar
  - [x] 7.2 Add `?bisect=good-bad` deep link support — reuse `TryParseDiffParam` for parsing
  - [x] 7.3 Wire BisectTool into the detail panel area, handle `OnNavigateToEvent` and `OnNavigateToBlame`
  - [x] 7.4 Implement mutual exclusion: bisect > blame > diff precedence
- [x] Task 8: Write tests (all ACs)
  - [x] 8.1 Model tests in Admin.Abstractions.Tests (`Models/Streams/BisectStepTests.cs`, `BisectResultTests.cs`) — include serialization round-trip test for `BisectResult` with `IReadOnlyList<BisectStep>` and `IReadOnlyList<FieldChange>`
  - [x] 8.2 `AdminServerOptionsValidator` tests for `MaxBisectSteps > 0` and `MaxBisectFields > 0` validation
  - [x] 8.3 Service bisect query tests in Admin.Server.Tests (`Services/`)
  - [x] 8.4 Controller tests in Admin.Server.Tests (`Controllers/`)
  - [x] 8.5 Binary search convergence test: given stream with 1,024 events where field X diverges at sequence 500, verify bisect converges in exactly ceil(log2(1024)) = 10 steps and identifies sequence 500 as the divergent event
  - [x] 8.6 Edge case tests: (a) good and bad are adjacent (bad = good + 1) — converges in 0 search steps, immediately returns divergent event; (b) field diverges at the very first event after good — verify bisect finds it; (c) good=0 (empty initial state) with bad=N — verify bisect handles sequence 0 as valid known-good state; (d) stream exceeds max steps — verify `IsTruncated = true` and narrowed range returned
  - [x] 8.7 Field selection tests: (a) bisect with specific field paths — only watched fields trigger divergence; (b) bisect with all fields — any field change triggers divergence; (c) too many fields without selection — verify 400 error
  - [x] 8.8 BisectTool component tests in Admin.UI.Tests (`Components/BisectToolTests.cs`) — include tests for: setup form validation (good >= bad shows error), field selection UI, result rendering, truncation banner, "no divergence" empty state
  - [x] 8.9 StreamDetail bisect integration tests in Admin.UI.Tests (`Pages/`) — include `?bisect` + `?blame` + `?diff` mutual exclusion test (bisect takes precedence)
  - [x] 8.10 Timeout handling test: simulate bisect exceeding 60-second timeout (via `OperationCanceledException`) — verify UI shows `IssueBanner` with "Bisect timed out — try narrowing the range or specifying fewer fields" and verify service re-throws `OperationCanceledException` (not swallowed)
  - [x] 8.11 No-divergence test: given stream where state at good and bad sequences is identical for watched fields, verify `BisectResult.DivergentFieldChanges` is empty and UI shows "No divergence detected" `EmptyState`
  - [x] 8.12 Snapshot invariance test: given identical stream with and without snapshots, verify bisect produces identical `DivergentSequence` and `DivergentFieldChanges` — confirms snapshot acceleration doesn't affect correctness

## Dev Notes

### Architecture Compliance

This story follows the **exact same architecture** as stories 15-3 (Stream Browser), 15-4 (Aggregate State Inspector & Diff Viewer), 20-1 (Blame View), and all Epic 14/15/19/20 patterns. No new architectural patterns are introduced.

**Data flow:** UI `BisectTool.razor` -> `AdminStreamApiClient.BisectAsync()` -> HTTP GET -> `AdminStreamsController` (Admin.Server) -> `IStreamQueryService.BisectAsync()` -> `DaprStreamQueryService` invokes CommandApi via DAPR `InvokeMethodAsync` (60-second timeout) -> CommandApi `/bisect` endpoint runs binary search algorithm (reconstructing state at O(log N) midpoints, comparing field values against known-good state), returns `BisectResult`.

**Architecture note:** The dev agent MUST trace the existing `DaprStreamQueryService.DiffAggregateStateAsync()` to identify how it constructs the CommandApi URL and which CommandApi controller receives it. The bisect endpoint goes in that same controller. Do NOT guess — follow the existing pattern.

### Critical Patterns to Follow

1. **Model records**: Use the `DaprHealthHistoryEntry` pattern (null-coalescing to `string.Empty`, no throwing constructors) for deserialization safety. Redact sensitive values in `ToString()` (SEC-5).
2. **Reuse `FieldChange`**: The divergent field changes in `BisectResult.DivergentFieldChanges` reuse the existing `FieldChange` record from `Admin.Abstractions/Models/Streams/FieldChange.cs`. Do NOT create a duplicate model.
3. **Service interface**: Add to existing `IStreamQueryService` — do NOT create a new interface.
4. **Controller**: Add to existing `AdminStreamsController` — do NOT create a new controller. Use `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]`.
5. **API client**: Add to existing `AdminStreamApiClient` — do NOT create a new client class. Follow the `GetAggregateStateDiffAsync` error handling pattern exactly.
6. **Component**: Follow `StateDiffViewer.razor` structure — parameters for identity, `OnClose` callback, `SkeletonCard` for loading, `EmptyState` for no data, `IssueBanner` for errors.
7. **Deep linking**: Follow `StreamDetail.razor` existing pattern with `[SupplyParameterFromQuery]` and `NavigateTo(url, forceLoad: false, replace: true)`. Reuse `TryParseDiffParam` for parsing the `good-bad` format.
8. **Tests**: Follow existing test patterns — xUnit + Shouldly for models, bUnit + NSubstitute for components.
9. **Timeout**: Use 60-second timeout for bisect (longer than blame's 30s, because bisect performs O(log N) state reconstructions). Set via `ServiceInvocationTimeoutSeconds` override or dedicated timeout in the `DaprStreamQueryService.BisectAsync` implementation — follow how blame timeout is handled.

### Existing Code to Reuse (DO NOT Reinvent)

| What | Where | Why |
|------|-------|-----|
| State reconstruction | `GetAggregateStateAtPositionAsync` in CommandApi | Bisect reconstructs state at O(log N) midpoints |
| JSON diff | `DiffAggregateStateAsync` in CommandApi | Bisect diffs at the divergent event to get field changes |
| `FieldChange` model | `Admin.Abstractions/Models/Streams/FieldChange.cs` | Bisect reuses same model for divergent field changes |
| `AggregateStateSnapshot` | `Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs` | State at each midpoint |
| `TryParseDiffParam` | `StreamDetail.razor` (lines 535-548) | Parsing `good-bad` query parameter format |
| `StatCard`, `EmptyState`, `IssueBanner`, `SkeletonCard` | `Admin.UI/Components/Shared/` | Shared UI components — never recreate |
| `FluentDataGrid` patterns | `StateDiffViewer.razor`, `BlameViewer.razor` | Table rendering with sorting |
| `AdminServerOptions` | `Admin.Server/Configuration/AdminServerOptions.cs` | Add `MaxBisectSteps` and `MaxBisectFields` here |
| URL building helpers | `AdminStreamApiClient` static methods | Reuse `Uri.EscapeDataString` patterns |
| `InvokeCommandApiAsync` | `DaprStreamQueryService` (lines 259-278) | DAPR service invocation with JWT forwarding |
| URL update pattern | `StreamDetail.razor` `UpdateUrl()` method | Building query params and NavigateTo |

### File Locations

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/BisectStep.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/BisectResult.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/BisectStepTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/BisectResultTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/BisectToolTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Controllers/` — add bisect computation endpoint in the **same controller** that handles the existing `/diff`, `/state`, and `/blame` admin stream routes (find by tracing `DaprStreamQueryService.DiffAggregateStateAsync` URL construction)
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` — add `BisectAsync`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — implement `BisectAsync` (delegates to CommandApi, 60s timeout)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — add bisect facade endpoint
- `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs` — add `MaxBisectSteps` and `MaxBisectFields`
- `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptionsValidator.cs` — add `MaxBisectSteps > 0` and `MaxBisectFields > 0` validation
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` — add `BisectAsync`
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` — add Bisect button, `?bisect` query param, BisectTool integration, update mutual exclusion precedence

### Anti-Patterns to Avoid

- **Do NOT** create a separate `IBisectService` — extend `IStreamQueryService`
- **Do NOT** create a new `AdminBisectController` — extend `AdminStreamsController`
- **Do NOT** create a new `AdminBisectApiClient` — extend `AdminStreamApiClient`
- **Do NOT** create a new `BisectFieldChange` model — reuse existing `FieldChange` from `Admin.Abstractions/Models/Streams/FieldChange.cs`
- **Do NOT** use `<FluentTooltip>` for field values in tables — use native `title` attribute (performance, same rationale as health heatmap in 19-5 and blame in 20-1)
- **Do NOT** add a NavMenu entry for bisect — it's accessed via StreamDetail page only
- **Do NOT** implement client-side bisect logic — the binary search runs server-side in CommandApi where it has direct access to event stream and snapshots
- **Do NOT** reconstruct state from scratch at each midpoint — leverage snapshot acceleration (state reconstruction should be O(snapshot_interval), not O(N))
- **Do NOT** hardcode timeout values — use `AdminServerOptions` configuration
- **Do NOT** hold all O(log N) intermediate state reconstructions in memory simultaneously — dispose each `JsonDocument`/state object after extracting field values before reconstructing the next midpoint (prevents O(log N * state_size) memory pressure for large aggregates)
- **Do NOT** compare field values with string equality on serialized JSON — use `System.Text.Json.JsonElement.DeepEquals` for semantic comparison (handles `0` vs `0.0`, whitespace differences, property ordering). This is the **highest-risk area** for false positives/negatives in bisect

### Binary Search Algorithm Detail

The algorithm is conceptually identical to `git bisect`:
1. User provides a "known good" sequence (state was correct) and "known bad" sequence (state is wrong)
2. Reconstruct state at the good sequence, extract field values for watched fields
3. Binary search between good and bad: at each midpoint, reconstruct state and compare watched fields against good-state values
4. If midpoint matches good state → narrow right (goodSeq = mid)
5. If midpoint diverges → narrow left (badSeq = mid)
6. Converge to `badSeq - goodSeq == 1` — `badSeq` is the first divergent event
7. Diff at the divergent event to get the exact field changes

**Complexity:** O(log N) state reconstructions. Each reconstruction benefits from snapshots, making it O(snapshot_interval) per reconstruction. Total: O(log N * snapshot_interval). For N=100,000 events with 100-event snapshots: ~17 * 100 = 1,700 event replays. Well within the 60-second timeout.

**Monotonicity assumption:** Binary search finds the **most recent** point of divergence, not the first. If a watched field oscillates (e.g., changes from expected to unexpected at event 50, back to expected at event 70, then to a different unexpected value at event 90), bisect identifies event 90 — the last transition away from the expected value. This is the correct behavior for the primary use case ("why does the current state look wrong?"), but users expecting "when did things first go wrong" should use the blame view (story 20-1) instead, which shows the full provenance history of each field.

### Previous Story Intelligence

**From story 20-1 (predecessor in same epic):**
- Two new `AdminServerOptions` properties added: `MaxBlameEvents` and `MaxBlameFields` — bisect adds `MaxBisectSteps` and `MaxBisectFields` following the same pattern
- `BlameViewer.razor` added to StreamDetail toolbar — bisect button goes next to it
- `?blame` deep link added to StreamDetail — `?bisect` follows the same pattern
- Mutual exclusion between `?diff` and `?blame` (blame takes precedence) — extend to three-way: bisect > blame > diff
- 30-second timeout for blame calls — bisect uses 60-second timeout (more state reconstructions)
- `ToString()` redaction pattern for SEC-5 compliance — follow same pattern
- Null-coalescing (`?? string.Empty`) in record constructors for deserialization safety — follow same pattern
- `AdminServerOptionsValidator` validation pattern for > 0 checks — follow same pattern

**From story 15-4 review findings:**
- StateDiffViewer uses P12 fallback for FromSequence==0 (initial state) — bisect should handle sequence 0 as valid good sequence
- 5-second timeout on diff API calls (P9) — bisect needs longer 60-second timeout
- Parallel API calls with independent result tracking — bisect is a single API call (server handles the iteration)

### Git Intelligence

Recent commits show Epic 19 (DAPR Infrastructure Visibility) is complete. Story 20-1 (Blame View) is ready-for-dev. All recent stories followed the same pattern: models in Admin.Abstractions, service in Admin.Server, controller endpoint, API client, Blazor page/component, tests. Story 20-2 follows the identical pattern.

### Project Structure Notes

- Alignment with unified project structure confirmed — all paths follow existing `src/Hexalith.EventStore.Admin.*` conventions
- No new projects needed — bisect tool fits entirely within existing Admin.Abstractions, Admin.Server, and Admin.UI projects
- Test projects already exist for all affected projects

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 20] Epic definition and FR coverage
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-21-admin-tooling.md#Epic 20] Story descriptions — "20.2: Bisect tool — binary search through event history to find state divergence"
- [Source: _bmad-output/planning-artifacts/prd.md#FR70] Point-in-time state exploration
- [Source: _bmad-output/planning-artifacts/prd.md#FR71] Aggregate state diff
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR44] Blame view UX requirement (related — bisect extends investigation)
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR45] State diff viewer requirement (related — bisect uses diff)
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR42] Deep linking — every view has unique shareable URL
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR48] Virtualized rendering for large data sets
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P4] Admin Three-Interface Architecture
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5] Event payload data never appears in logs
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs] Service interface to extend
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs] Existing model to reuse for divergent changes
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs] DAPR implementation to extend — trace URL construction for CommandApi
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs] Controller to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs] Options to extend
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs] API client to extend
- [Source: src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor] Component pattern to follow
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor] Page to integrate bisect into — reuse `TryParseDiffParam` for `?bisect` parsing
- [Source: _bmad-output/implementation-artifacts/20-1-blame-view-per-field-provenance.md] Previous story in this epic — patterns and implementation order

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Fixed IssueBanner usage: component uses `Visible`/`Description` parameters, not `Message` (pre-existing inconsistency in BlameViewer.razor)
- EventCallback<long> cannot be nullable in Blazor — used non-nullable with `.HasDelegate` check instead

### Completion Notes List

- Implemented full bisect tool: binary search through event history to find exact state divergence event in O(log N) comparisons
- BisectStep and BisectResult models follow DaprHealthHistoryEntry pattern (null-coalescing to string.Empty, SEC-5 compliant ToString)
- Binary search algorithm in AdminStreamQueryController reuses existing DeepMerge, JsonDiff, and FlattenJson helpers
- Field comparison uses JsonElement.DeepEquals for semantic JSON comparison (handles 0 vs 0.0, whitespace, property ordering)
- DaprStreamQueryService.BisectAsync delegates to CommandApi with 60-second timeout (longer than blame's 30s)
- BisectTool.razor implements three phases (setup/loading/result) with IAsyncDisposable, field selection, and proper error handling
- StreamDetail.razor integration: Bisect button in toolbar, ?bisect=good-bad deep link, mutual exclusion (bisect > blame > diff)
- All 22 new tests pass, 1,830 total tests pass across all Tier 1 test projects with zero regressions

### File List

**New files:**
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/BisectStep.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/BisectResult.cs
- src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/BisectStepTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/BisectResultTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/BisectToolTests.cs

**Modified files:**
- src/Hexalith.EventStore.CommandApi/Controllers/AdminStreamQueryController.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs
- src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs
- src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs
- src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs
- src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptionsValidator.cs
- src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs
- src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor
- tests/Hexalith.EventStore.Admin.Server.Tests/Configuration/AdminServerOptionsValidatorTests.cs

### Review Findings

- [x] [Review][Patch] P1: JsonDocument leak in ExtractFieldValues catch block [AdminStreamQueryController.cs:518] — FIXED: added `using` to `JsonDocument.Parse` in catch block
- [x] [Review][Patch] P2: AdminStreamApiClient swallows 400 BadRequest [AdminStreamApiClient.cs:369-376] — FIXED: added re-throw for `HttpRequestException` with `BadRequest` status
- [x] [Review][Patch] P3: Double timeout wrapping — effective 30s not 60s [DaprStreamQueryService.cs:264-266] — FIXED: added optional `timeoutSeconds` parameter to `InvokeCommandApiAsync`, bisect passes 60s directly
- [x] [Review][Dismiss] P4: FluentProgressRing vs FluentProgressBar [BisectTool.razor:102] — DISMISSED: `FluentProgressBar` does not exist in Fluent UI Blazor; `FluentProgressRing` is the correct component (spec error)
- [x] [Review][Patch] P5: Missing "Stream < 2 events" EmptyState [BisectTool.razor] — FIXED: added pre-check via GetStreamTimelineAsync before bisect; shows EmptyState with event count
- [x] [Review][Patch] P6: Missing explicit `badSequence < 0` validation [DaprStreamQueryService.cs:236-244] — FIXED: added `badSequence < 0` guard
- [x] [Review][Patch] P7: FlattenJson/ExtractLeafFieldPaths array handling mismatch [BisectTool.razor:492-520] — FIXED: removed array recursion from `ExtractLeafFieldPaths` to match server's `FlattenJson`
- [x] [Review][Patch] P8: goodSequence=0 empty state — vacuous bisect with no fieldPaths [AdminStreamQueryController.cs:236-256] — FIXED: added fallback to extract watch fields from bad state when good state is empty
- [x] [Review][Defer] D1: O(N*logN) state reconstruction performance — deferred, pre-existing pattern shared by blame/diff
- [x] [Review][Defer] D2: DeepMerge doesn't handle field deletion — deferred, pre-existing in blame
- [x] [Review][Defer] D3: JsonDiff uses string comparison (not DeepEquals) — deferred, pre-existing in blame/diff
- [x] [Review][Defer] D4: ConfigureAwait(false) in Blazor component [BisectTool.razor:317,376] — deferred, project-wide pattern (D3 from 20-1 review)
- [x] [Review][Defer] D5: No upper bound on maxSteps/maxFields query params [AdminStreamQueryController.cs:190-197] — deferred, defense-in-depth gated by DAPR isolation (D6 from 20-1 review)
