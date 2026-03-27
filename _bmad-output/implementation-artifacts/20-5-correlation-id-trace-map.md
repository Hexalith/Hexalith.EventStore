# Story 20.5: Correlation ID Trace Map

Status: ready-for-dev

Size: Medium-Large — 3 new models in Admin.Abstractions (CorrelationTraceMap, TraceMapEvent, TraceMapProjection), extends `IStreamQueryService` with trace map method, adds 2 new controller files (AdminTracesController in Admin.Server, AdminTraceQueryController in CommandApi), extends `DaprStreamQueryService` and `AdminStreamApiClient`, creates `CorrelationTraceMap.razor` component integrated into `StreamDetail.razor` with deep linking. Creates ~5-6 test classes across 3 test projects (~30-40 tests). Fifth and final story in Epic 20's Advanced Debugging suite.

## Story

As a **platform operator or developer using the Hexalith EventStore Admin UI**,
I want **a correlation ID trace map that shows the complete lifecycle of a command — from submission through event production to projection consumption — as a visual flow, starting from any correlation ID**,
so that **I can understand the full impact of a single command across the system, trace why events were produced, verify projections consumed them, and deep-link to external observability tools for distributed trace details — answering "what happened for this correlation?" in one view**.

**Dependency:** Stories 20-1 (Blame View), 20-2 (Bisect Tool), 20-3 (Step-Through Event Debugger), and 20-4 (Command Sandbox) should be implemented first — all stories modify `StreamDetail.razor` and the mutual exclusion precedence logic. No code dependency on previous story models. Epic 15 must be complete (done).

**Functional Requirement:** FR72 — "The admin tool can trace the full causation chain for any event — originating command, sender identity, correlation ID, and downstream projections affected."

**UX Requirements:** UX-DR42 (deep linking), UX-DR43 (context-aware breadcrumbs), UX-DR47 (observability deep links). Every correlation ID in the admin UI is a hyperlink to the trace map (UX-DR: "Correlation ID as hyperlink").

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- Trace map renders correctly in `StreamDetail.razor` showing command lifecycle pipeline, produced events, affected projections, and timing
- GET endpoint accepts a tenant ID and correlation ID, queries command status store and event stream, returns structured trace map
- Clicking any correlation ID in the admin UI opens the trace map for that correlation
- Deep link to external observability tool (Zipkin/Jaeger/Aspire) is shown when `ADMIN_TRACE_URL` is configured
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Deep linking via `?trace={correlationId}` opens the trace map in StreamDetail

## Acceptance Criteria

1. **AC1: CorrelationTraceMap model** — A new `CorrelationTraceMap` record in `Admin.Abstractions/Models/Streams/` captures the full trace for a correlation ID. Properties: `CorrelationId` (string), `TenantId` (string), `Domain` (string — the domain of the aggregate that processed the command; empty if command status not found), `AggregateId` (string — the aggregate that processed the command; empty if command status not found), `CommandType` (string — fully qualified command type name), `CommandStatus` (string — terminal status: `"Completed"`, `"Rejected"`, `"PublishFailed"`, `"TimedOut"`, `"Processing"`, or `"Unknown"` if command status expired/not found), `UserId` (string? — who submitted the command), `CommandReceivedAt` (DateTimeOffset? — when the command entered the pipeline), `CommandCompletedAt` (DateTimeOffset? — when the command reached terminal status), `DurationMs` (long? — elapsed time from received to completed; null if either timestamp is missing), `ProducedEvents` (IReadOnlyList\<TraceMapEvent\> — events produced by this command in the aggregate stream; empty list if no events found or command status expired), `AffectedProjections` (IReadOnlyList\<TraceMapProjection\> — projections that consume events from this domain with their processing status relative to the trace events), `RejectionEventType` (string? — non-null when CommandStatus = `"Rejected"`, the rejection event type name), `ErrorMessage` (string? — non-null when CommandStatus = `"PublishFailed"` or `"TimedOut"` or computation failed), `ExternalTraceUrl` (string? — deep link to external observability tool, null if `ADMIN_TRACE_URL` not configured), `TotalStreamEvents` (long — total events in the aggregate stream for context), `ScanCapped` (bool — true if the event scan hit the 10,000-event cap before finding all expected events; false otherwise), `ScanCapMessage` (string? — non-null when `ScanCapped` is true, e.g., "Event scan was limited to the most recent 10,000 events. Older events for this correlation may exist but were not included."). All string properties use null-coalescing to `string.Empty` (following deserialization-safe pattern). `ToString()` omits event payloads (SEC-5 — the trace map itself does not carry event payloads, but redact for safety).

2. **AC2: TraceMapEvent model** — A new `TraceMapEvent` record in `Admin.Abstractions/Models/Streams/` captures one event in the trace. Properties: `SequenceNumber` (long), `EventTypeName` (string — fully qualified event type name), `Timestamp` (DateTimeOffset — when the event was recorded), `CausationId` (string? — the causation identifier linking this event to its cause), `IsRejection` (bool — true if this is a rejection event). All string properties use null-coalescing to `string.Empty`.

3. **AC3: TraceMapProjection model** — A new `TraceMapProjection` record in `Admin.Abstractions/Models/Streams/` captures a projection's processing status relative to the trace. Properties: `ProjectionName` (string — the projection's registered name), `Status` (string — `"processed"` if projection has consumed past all trace events, `"pending"` if still catching up, `"faulted"` if projection is in error state, `"unknown"` if projection status unavailable), `LastProcessedSequence` (long? — the projection's last processed sequence number, null if unknown). All string properties use null-coalescing to `string.Empty`.

4. **AC4: IStreamQueryService extension** — Add `GetCorrelationTraceMapAsync(string tenantId, string correlationId, string? domain, string? aggregateId, CancellationToken ct)` to `IStreamQueryService`. Returns `CorrelationTraceMap`. Throws `ArgumentException` if `correlationId` is empty. The `domain` and `aggregateId` parameters are **optional stream context hints** — when provided (e.g., when the user opens the trace from StreamDetail where the stream is already known), the endpoint can scan events directly without depending on command status. When omitted, the endpoint discovers the aggregate from the command status store. This "events-first" design ensures the trace map works even when command status has expired (24-hour TTL).

5. **AC5: Server-side trace map computation (two-layer delegation)** — The trace map computation follows the same two-layer delegation pattern as existing stream endpoints, but uses a **different route prefix** because the trace operates on a correlation ID (cross-stream concern), not a specific aggregate stream.

   **(a) CommandApi layer (computation):** Add a new `AdminTraceQueryController` in CommandApi. Route prefix: `api/v1/admin/traces`. Single endpoint: `GET api/v1/admin/traces/{tenantId}/{correlationId}?domain={domain}&aggregateId={aggregateId}`. The `domain` and `aggregateId` query parameters are optional. Uses `[AllowAnonymous]` (DAPR trust boundary, same as `AdminStreamQueryController`). The endpoint:

   - **Step 0: Discover event envelope and state store patterns.** Before implementing, the dev agent MUST trace existing code to find: (a) The command status record C# type — find it by examining `CommandStatusController.cs` in CommandApi, identify what type `DaprClient.GetStateAsync<T>` deserializes into, note its exact property names. (b) The DAPR state store name — trace how `CommandStatusController` or `AggregateActor` resolves the store name (may be a constant in `CommandStatusConstants`, an options property, or a DAPR component name). (c) The event envelope type — trace how `AdminStreamQueryController.GetAggregateBlameAsync` reads individual events from state store keys and deserializes them, use the same type and extraction pattern for `CorrelationId`.

   - **Step 1: Read command status.** Query the DAPR state store for key `{tenantId}:{correlationId}:status` using `DaprClient.GetStateAsync<TCommandStatus>(stateStoreName, key)` — do NOT use `IActorStateManager` (which is only available inside actor turns; this is a regular ASP.NET controller). The command status record contains: `CommandType` (string), `Status` (string — pipeline stage), `AggregateId` (string), `Domain` (string), `UserId` (string?), `Timestamp` (DateTimeOffset), and terminal-state-specific fields: `EventCount` (int, for Completed), `RejectionEventTypeName` (string, for Rejected), `FailureReason` (string, for PublishFailed), `TimeoutDuration` (string, for TimedOut). If the key doesn't exist (TTL expired or invalid correlation ID) AND no `domain`/`aggregateId` query params were provided, return a `CorrelationTraceMap` with `CommandStatus = "Unknown"`, empty events and projections, and `ErrorMessage = "Command status not found — the status record may have expired (default 24-hour TTL) or the correlation ID is invalid. Try opening the trace from a stream view where the aggregate context is known."`. If the key doesn't exist BUT `domain` and `aggregateId` query params ARE provided, proceed to Step 2 using those values (events-first fallback — enables tracing even when command status has expired).

   - **Step 2: Read events from aggregate stream.** Determine the target aggregate: use command status fields if available, otherwise use the `domain`/`aggregateId` query parameters. If neither source provides aggregate context, return the "Unknown" result. Read events from the identified aggregate. Use the existing **sequential key-read pattern** (same as `GetAggregateBlameAsync` in `AdminStreamQueryController`) — read individual event keys `{tenantId}:{domain}:{aggregateId}:events:{seq}` via `DaprClient.GetStateAsync` in a loop, not a DAPR query. DAPR state stores do not support secondary index queries on correlation ID; the only way to find matching events is to iterate through the event keys and check each event envelope's `CorrelationId` field. The events from a single command are stored as a contiguous block (atomic actor turn), so scan backward from the latest event and collect matches. Stop scanning once all expected events are found (use `EventCount` from Completed status as a hint) or after scanning the entire stream. For each matching event, extract: sequence number, event type name, timestamp, causation ID, and whether it's a rejection event (check `IsRejection` on the domain result or event type conventions). **Cap the scan** at 10,000 events from the tail to prevent unbounded reads on very large streams. If the scan hits the cap without finding all expected events (compare found count vs `EventCount` from command status), set `ScanCapped = true` and `ScanCapMessage = "Event scan was limited to the most recent 10,000 events. Older events for this correlation may exist but were not included. The command produced {EventCount} events but only {foundCount} were found within the scan window."`.

   - **Step 3: Determine affected projections.** Query projection status for projections that process events in the relevant domain. Use the existing projection status infrastructure (from Epic 11 story 11-4, convention-based projection discovery). For each projection: compare its `LastProcessedSequence` against the max sequence number in the trace's events. If `LastProcessedSequence >= maxTraceSequence` → status `"processed"`. If `LastProcessedSequence < minTraceSequence` → status `"pending"`. If projection is in error state → status `"faulted"`. If projection status is unavailable → status `"unknown"`. **Graceful degradation:** If projection status queries fail (service unavailable, no projections registered), return empty `AffectedProjections` list — do not fail the entire trace map.

   - **Step 4: Build external trace URL.** Read `ADMIN_TRACE_URL` from DAPR configuration store or environment variable. If configured, format as: `{ADMIN_TRACE_URL}?correlationId={correlationId}`. If not configured, set `ExternalTraceUrl = null`.

   - **Step 5: Compute timing.** If command status includes timestamps for `Received` and terminal stage, compute `DurationMs = (CommandCompletedAt - CommandReceivedAt).TotalMilliseconds`. If timestamps are partial, set `DurationMs = null`.

   - **Step 6: Return CorrelationTraceMap.** Package all data. Always return a structured response — never throw. If any step fails partially, include what was gathered with appropriate null/empty values.

   **(b) Admin.Server layer (delegation):** New `AdminTracesController` in Admin.Server. Route prefix: `api/v1/admin/traces`. Single endpoint: `GET api/v1/admin/traces/{tenantId}/{correlationId}`. Uses `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]` and `[ServiceFilter(typeof(AdminTenantAuthorizationFilter))]` (same as `AdminStreamsController`). Delegates to `IStreamQueryService.GetCorrelationTraceMapAsync(tenantId, correlationId, ct)`. Returns `CorrelationTraceMap` as JSON. Returns 400 if `correlationId` is empty.

   `DaprStreamQueryService.GetCorrelationTraceMapAsync()` delegates to CommandApi via DAPR `InvokeMethodAsync` (GET). Use the existing `InvokeCommandApiAsync<TResponse>` helper. URL: `api/v1/admin/traces/{tenantId}/{correlationId}` with optional `?domain={domain}&aggregateId={aggregateId}` query params when provided. Use a **30-second timeout** (default `ServiceInvocationTimeoutSeconds` from `AdminServerOptions`). On failure, return a `CorrelationTraceMap` with `CommandStatus = "Unknown"` and `ErrorMessage` describing the failure (same graceful degradation as `GetAggregateBlameAsync`).

6. **AC6: REST endpoint** — `GET /api/v1/admin/traces/{tenantId}/{correlationId}?domain={domain}&aggregateId={aggregateId}` on `AdminTracesController` (Admin.Server). The `domain` and `aggregateId` query parameters are optional stream context hints. Requires `ReadOnly` authorization policy. Returns `CorrelationTraceMap` as JSON. Returns 400 if `correlationId` is empty.

7. **AC7: API client method** — Add `GetCorrelationTraceMapAsync(string tenantId, string correlationId, string? domain, string? aggregateId, CancellationToken ct)` to `AdminStreamApiClient`. URL: `api/v1/admin/traces/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(correlationId)}` with optional query params `?domain={Uri.EscapeDataString(domain)}&aggregateId={Uri.EscapeDataString(aggregateId)}` appended when non-null. Follows the same error handling pattern as `GetAggregateBlameAsync`: catches expected exceptions (Unauthorized, Forbidden, ServiceUnavailable, OperationCanceled) separately, returns null on unexpected errors with logged warning.

8. **AC8: CorrelationTraceMap component** — A new `CorrelationTraceMap.razor` component in `Admin.UI/Components/` provides the correlation trace map interface. Layout:

   **Header bar:**
   - "Correlation Trace Map" title with "Close" button (invokes `OnClose` EventCallback).
   - Correlation ID display: full ID in monospace font with copy-to-clipboard `FluentButton` (Appearance.Lightweight, use `navigator.clipboard.writeText` via JS interop). The copy button is essential — operators paste correlation IDs into other tools constantly.

   **Command lifecycle pipeline (top section):**
   - Horizontal bar showing command processing stages: **Received** -> **Processing** -> **Events Stored** -> **Events Published** -> **Completed** (or terminal: Rejected/PublishFailed/TimedOut).
   - Implement as a `FluentStack` with `Orientation.Horizontal`. Each stage is a `FluentBadge`:
     - Completed stages: `Appearance.Accent` (filled).
     - Current/terminal stage: `Appearance.Accent` with bold text.
     - Future stages (not reached): `Appearance.Outline` (muted).
     - Failed terminal stages (Rejected/PublishFailed/TimedOut): `Appearance.Accent` with red/warning color via custom CSS class.
   - Between stages: a connector line (CSS `border-top` or `::after` pseudo-element).
   - Below the pipeline: command type (bold, full qualified name), user ID, duration ("Completed in {N}ms" or "Rejected after {N}ms" or "Status: Processing (in-flight)" or "Status: Unknown (expired)").
   - If `CommandStatus == "Unknown"`: Show `IssueBanner` (info severity): "Command status not found — the status record may have expired or the correlation ID is invalid. Events below were found by scanning the aggregate stream."

   **Events section (middle):**
   - Header: "Produced Events ({N})" or "No Events Found" if empty.
   - `FluentDataGrid<TraceMapEvent>` with columns: Sequence (long, "#"), Event Type (string, bold, monospace), Timestamp (relative format using `TimeFormatHelper`), Causation ID (monospace, truncated to 8 chars with full in `title`), Rejection (`FluentBadge` "Rejection" if `IsRejection` is true).
   - Each row is clickable — clicking navigates to the event in the stream timeline via `OnNavigateToEvent` callback: `Navigation.NavigateTo($"/admin/streams/{tenantId}/{domain}/{aggregateId}?detail={sequenceNumber}")`.
   - If events list is empty and status is not "Unknown": `EmptyState` with "No events found in the aggregate stream for this correlation ID."

   **Projections section (bottom):**
   - Header: "Affected Projections ({N})" — collapsed if empty.
   - `FluentDataGrid<TraceMapProjection>` with columns: Projection Name (string), Status (`FluentBadge` — green "Processed" / yellow "Pending" / red "Faulted" / grey "Unknown"), Last Processed Seq (long, monospace).
   - If projections list is empty: no section rendered (not an error — projections may not be configured).

   **External observability section (footer):**
   - If `ExternalTraceUrl` is non-null: `FluentButton` with `Appearance.Outline` labeled "View in External Tracer" with `Icon="@(new Icons.Regular.Size20.Open())"`. Opens URL in new tab (`target="_blank"`).
   - If `ExternalTraceUrl` is null: no button rendered (graceful degradation per ADR-P5).

   **Timing summary footer:**
   - "Command received {relative time} — {CommandType}" if timestamps available.
   - "Duration: {DurationMs}ms" if available.
   - "Aggregate: {Domain}/{AggregateId} ({TotalStreamEvents} total events)" for context.

   Component implements `IAsyncDisposable` — cancels any in-flight API calls via `CancellationTokenSource` on disposal.

   Component parameters: `TenantId` (EditorRequired), `CorrelationId` (EditorRequired), `Domain` (string? — pre-populated from the stream context if opened from StreamDetail; passed to the API as optional hint for events-first scanning), `AggregateId` (string? — pre-populated from stream context; same purpose), `OnClose` (EventCallback), `OnNavigateToEvent` (EventCallback\<(string tenantId, string domain, string aggregateId, long seq)\> — navigates to event in StreamDetail). The component passes `Domain` and `AggregateId` to the API client when non-null, enabling trace map results even when command status has expired.

9. **AC9: StreamDetail.razor integration** — The existing `StreamDetail.razor` page adds trace map support:

   **Correlation ID clickability:** Every correlation ID displayed in the stream timeline, event detail panel, and causation chain view becomes a clickable link that opens the `CorrelationTraceMap` component. Replace plain `<span class="monospace">` correlation ID displays with a `FluentButton` (Appearance.Lightweight, monospace font) that sets `_traceMode = true` and `_traceCorrelationId = correlationId`.

   **Toolbar button:** Add a "Trace Map" `FluentButton` with `Appearance.Outline` and `Icon="@(new Icons.Regular.Size20.BranchFork())"` in the toolbar (after "Sandbox" button). The button is **only enabled when an event is selected** (`_selectedEventSequence.HasValue`) — clicking opens the trace map for the selected event's correlation ID. If no event is selected, the button is disabled with `title="Select an event to trace its correlation ID"`.

   **Mode integration:** Add `_traceMode` boolean and `_traceCorrelationId` string alongside existing `_stepMode`, `_bisectMode`, etc. Insert `@else if (_traceMode)` **after** the `_sandboxMode` check but **before** the `_stepMode` check in the Razor if-else chain. Precedence: `sandbox` > `trace` > `step` > `bisect` > `blame` > `diff` (sandbox has higher precedence than trace because it holds ephemeral user-entered form state — command type, payload JSON, target sequence — that cannot be recovered if accidentally overridden by a correlation ID click).

10. **AC10: Deep linking** — `StreamDetail.razor` supports a new `?trace={correlationId}` query parameter via `[SupplyParameterFromQuery(Name = "trace")]`. When the parameter is present with a non-empty string value, the page auto-opens the CorrelationTraceMap with that correlation ID. Navigating to trace mode updates the URL via `Navigation.NavigateTo(url, forceLoad: false, replace: true)`. The `trace` parameter is mutually exclusive with `sandbox`, `step`, `bisect`, `blame`, and `diff` — if multiple are present, precedence is: `sandbox` > `trace` > `step` > `bisect` > `blame` > `diff`.

11. **AC11: Loading, empty, error, and scan-capped states** — `SkeletonCard` shows while the trace map is loading (between opening trace mode and receiving the API response). If API call fails with non-timeout error: `IssueBanner` (error severity) with "Unable to load trace map — check server connectivity." If API call times out (`OperationCanceledException`): `IssueBanner` (error severity) with "Trace map timed out — the aggregate stream may be very large." If correlation ID not found (status = "Unknown" with no events): `IssueBanner` (warning severity) with "No trace data found for this correlation ID. The command status may have expired (24-hour default TTL). **Alternative investigation paths:** (1) Filter the stream timeline by this correlation ID using the correlation filter. (2) Check the external observability tool (Zipkin/Jaeger/Aspire) for distributed trace details. (3) Search structured logs by correlation ID." If `ScanCapped` is true: `IssueBanner` (warning severity) with the `ScanCapMessage` text — shown above the events table to alert the user that results may be incomplete.

## Tasks / Subtasks

- [ ] Task 1: Create new models in Admin.Abstractions (AC: #1, #2, #3)
  - [ ] 1.1 Create `CorrelationTraceMap` record in `Models/Streams/CorrelationTraceMap.cs` with null-coalescing, `ToString()` safe representation, including `ScanCapped` bool and `ScanCapMessage` string? properties
  - [ ] 1.2 Create `TraceMapEvent` record in `Models/Streams/TraceMapEvent.cs` with null-coalescing
  - [ ] 1.3 Create `TraceMapProjection` record in `Models/Streams/TraceMapProjection.cs` with null-coalescing
- [ ] Task 2: Add trace map computation endpoint to CommandApi (AC: #5a) — **START WITH STEP 0**
  - [ ] 2.0 **Before implementing:** trace existing code to find (a) command status DTO type from `CommandStatusController.cs`, (b) DAPR state store name from `CommandStatusConstants` or options, (c) event envelope deserialization type from `AdminStreamQueryController`'s blame/step endpoints. Record exact type names and namespaces.
  - [ ] 2.1 Create `AdminTraceQueryController` in CommandApi with route `api/v1/admin/traces` and `[AllowAnonymous]`
  - [ ] 2.2 Add `GET {tenantId}/{correlationId}?domain={d}&aggregateId={a}` endpoint: read command status from DAPR state store via `DaprClient.GetStateAsync` using key `{tenantId}:{correlationId}:status`; fall back to query params if status expired
  - [ ] 2.3 If status found: read aggregate events, filter by correlation ID, scan backward from latest with 10,000-event cap
  - [ ] 2.4 Query projection status for domain projections — gracefully degrade if unavailable
  - [ ] 2.5 Build external trace URL from `ADMIN_TRACE_URL` configuration (DAPR config store or environment variable)
  - [ ] 2.6 Return `CorrelationTraceMap` — always return structured response, never throw
- [ ] Task 3: Extend stream query service (AC: #4, #5b)
  - [ ] 3.1 Add `GetCorrelationTraceMapAsync` to `IStreamQueryService` interface
  - [ ] 3.2 Implement `GetCorrelationTraceMapAsync` in `DaprStreamQueryService` (delegates to CommandApi via GET `InvokeCommandApiAsync`, 30-second timeout, graceful error fallback)
- [ ] Task 4: Add REST facade controller on Admin.Server (AC: #6)
  - [ ] 4.1 Create `AdminTracesController` in Admin.Server with route `api/v1/admin/traces`, ReadOnly authorization, tenant authorization filter
  - [ ] 4.2 Add `GET {tenantId}/{correlationId}` endpoint — validate correlationId non-empty, delegate to service
- [ ] Task 5: Create UI API client method (AC: #7)
  - [ ] 5.1 Add `GetCorrelationTraceMapAsync` to `AdminStreamApiClient` — URL-encode path segments, standard error handling pattern
- [ ] Task 6: Create CorrelationTraceMap component (AC: #8, #11)
  - [ ] 6.1 Create `CorrelationTraceMap.razor` in `Admin.UI/Components/` — implement header with correlation ID display and copy button
  - [ ] 6.2 Implement command lifecycle pipeline visualization (horizontal FluentStack with FluentBadge stages and connector lines)
  - [ ] 6.3 Implement events table with `FluentDataGrid<TraceMapEvent>`, clickable rows for navigation
  - [ ] 6.4 Implement projections table with `FluentDataGrid<TraceMapProjection>` and status badges
  - [ ] 6.5 Implement external trace URL button (shown only when configured)
  - [ ] 6.6 Implement `IAsyncDisposable` for in-flight API call cancellation
- [ ] Task 7: Integrate into StreamDetail page (AC: #9, #10)
  - [ ] 7.1 Make correlation IDs clickable throughout StreamDetail — replace plain spans with FluentButton (Appearance.Lightweight) that opens trace map
  - [ ] 7.2 Add "Trace Map" toolbar button (enabled when event is selected)
  - [ ] 7.3 Add `?trace={correlationId}` deep link support via `[SupplyParameterFromQuery(Name = "trace")]`
  - [ ] 7.4 Wire CorrelationTraceMap into the detail panel area, handle callbacks
  - [ ] 7.5 Update mutual exclusion: add `_traceMode` boolean alongside existing modes. Prepend `@if (_traceMode)` before `_sandboxMode` check. Precedence: `sandbox` > `trace` > `step` > `bisect` > `blame` > `diff`
- [ ] Task 8: Write tests (all ACs)
  - [ ] 8.1 Model tests in Admin.Abstractions.Tests (`Models/Streams/CorrelationTraceMapTests.cs`, `TraceMapEventTests.cs`, `TraceMapProjectionTests.cs`) — constructor with valid inputs, null-coalescing defaults, `ToString()` representation, serialization round-trip
  - [ ] 8.2 Service query tests in Admin.Server.Tests (`Services/`) — verify delegation to CommandApi via GET, timeout behavior, argument validation (empty correlationId throws)
  - [ ] 8.3 Controller tests in Admin.Server.Tests (`Controllers/AdminTracesControllerTests.cs`) — parameter validation (empty correlationId -> 400), authorization policy, response types
  - [ ] 8.4 CommandApi controller tests (`Controllers/AdminTraceQueryControllerTests.cs`) — mock DaprClient.GetStateAsync, verify key pattern `{tenantId}:{correlationId}:status`, verify graceful fallback when status not found
  - [ ] 8.5 Edge case tests: (a) command status expired, no stream context — `CommandStatus = "Unknown"`, empty events, actionable error message; (b) command status expired WITH stream context (domain+aggregateId provided) — events found by scanning using stream context fallback; (c) correlation ID has no matching events in stream — empty `ProducedEvents`; (d) command still processing (non-terminal status) — `CommandStatus = "Processing"`, no events yet; (e) rejected command — `CommandStatus = "Rejected"`, `RejectionEventType` populated, events contain rejection event with `IsRejection = true`; (f) very large stream (>10,000 events) — scan cap triggered, `ScanCapped = true`, `ScanCapMessage` populated; (g) projection service unavailable — empty `AffectedProjections`, no error; (h) scan finds all expected events within cap — `ScanCapped = false`
  - [ ] 8.6 CorrelationTraceMap component tests in Admin.UI.Tests (`Components/CorrelationTraceMapTests.cs`) — pipeline stage rendering (completed stages filled, future stages muted, failed stages highlighted), events table renders with clickable rows, projection status badges, external trace URL button visibility (shown when configured, hidden when null), copy button for correlation ID
  - [ ] 8.7 StreamDetail trace integration tests in Admin.UI.Tests (`Pages/`) — `?trace` + `?sandbox` + `?step` + `?bisect` + `?blame` + `?diff` mutual exclusion test (trace takes precedence), toolbar button renders and enables when event is selected, correlation ID click opens trace map
  - [ ] 8.8 Timeout handling test: simulate exceeding 30-second timeout (via `OperationCanceledException`) — verify UI shows `IssueBanner` with "Trace map timed out" message
  - [ ] 8.9 Graceful degradation test: simulate partial data — command status found but no events (TTL scenario), events found but projections unavailable, all data found (happy path)

## Dev Notes

### Architecture Compliance

This story follows the **same admin data flow architecture** as stories 15-3 through 20-4 (UI component -> API client -> Admin.Server controller -> DaprStreamQueryService -> CommandApi computation), but introduces **two new controller files** because the trace map operates on a different URL namespace (`/traces/{tenantId}/{correlationId}` instead of `/streams/{tenantId}/{domain}/{aggregateId}/...`). This is justified because:

1. The trace map takes a **correlation ID** as its primary identifier, not a stream identity (tenant+domain+aggregate)
2. The domain and aggregate are **discovered** from the command status store, not provided by the caller
3. The route semantically represents traces, not streams — routing conflicts would occur with `{domain}` path segment

**Data flow:** UI `CorrelationTraceMap.razor` -> `AdminStreamApiClient.GetCorrelationTraceMapAsync()` -> HTTP GET -> `AdminTracesController` (Admin.Server) -> `IStreamQueryService.GetCorrelationTraceMapAsync()` -> `DaprStreamQueryService` invokes CommandApi via DAPR `InvokeMethodAsync` GET (30-second timeout) -> CommandApi `AdminTraceQueryController` reads command status from DAPR state store, scans aggregate events by correlation ID, queries projection status, builds external trace URL, returns `CorrelationTraceMap`.

### Distinction from Existing CausationChainView

The existing `CausationChainView.razor` (from story 15-3) and `TraceCausationChainAsync` trace **backward** from a specific event to its originating command — answering "what caused this event?". The correlation ID trace map traces **forward** from a correlation ID to all its effects — answering "what happened because of this command?". They are complementary:

| Feature | CausationChainView | CorrelationTraceMap (this story) |
|---------|-------------------|----------------------------------|
| Input | Event (tenant, domain, aggregateId, sequence) | Correlation ID (tenant, correlationId) |
| Direction | Backward (event → command) | Forward (command → events → projections) |
| Scope | Single event's ancestry | All events + projections for a correlation |
| Data source | Event metadata (causation chain) | Command status store + event stream scan + projection status |
| Visualization | Vertical chain (command → events) | Horizontal pipeline + events table + projections table |
| Integration | Inline in event detail panel | Full panel replacing detail area (like blame/bisect/step/sandbox) |

### Command Status Store Key Pattern

The trace map reads from the same state store that `CommandStatusController` writes to. The key pattern is:

```
{tenantId}:{correlationId}:status
```

The command status is stored as a JSON object. The dev agent MUST find the actual command status record structure by examining:
1. `CommandStatusController.cs` in CommandApi — to see what's written
2. `CommandStatusConstants.cs` in Server (or Contracts) — for key derivation
3. The status record DTO/class — for deserializable properties

**Critical:** The command status has a **24-hour TTL** by default (configurable per-tenant via DAPR config store). For old correlations, the status will be gone. The trace map MUST handle this gracefully — return `CommandStatus = "Unknown"` and still attempt to find events by scanning the aggregate (if domain/aggregateId can be inferred from event correlation metadata, though this is a harder fallback).

### Event Scanning Strategy

Events from a single command are stored as a **contiguous block** in the aggregate stream (atomic actor turn via `IActorStateManager.SaveStateAsync`). The scanning algorithm:

1. Read aggregate metadata → get `TotalEvents`
2. Start from `TotalEvents` and scan backward
3. For each event, check if `CorrelationId` matches target
4. Collect matching events
5. Stop when: (a) found `EventCount` matches (from command status), OR (b) scanned 10,000 events from tail (cap), OR (c) reached sequence 1

Events are stored at keys `{tenantId}:{domain}:{aggregateId}:events:{seq}`. Each event contains correlation ID in its envelope metadata.

**Optimization:** If command status includes `EventCount`, the scan can stop early once that many matching events are found. Since events are contiguous, finding one matching event means adjacent events likely also match — scan both directions from the first match.

### Projection Status Querying

The projection system (Epic 11) stores projection state including `LastProcessedSequence`. The admin API (story 15-5) has `ProjectionDashboard` endpoints. The trace map computation can:

1. Query registered projections for the relevant domain
2. For each projection, read `LastProcessedSequence`
3. Compare against `maxTraceSequence` (highest sequence in trace events)

**Graceful degradation is critical:** If projection queries fail (service unavailable, no projections registered, or the admin API doesn't have a direct projection query method in CommandApi), return an empty `AffectedProjections` list. The trace map's primary value is the command lifecycle + events — projections are supplementary.

The dev agent should check what projection infrastructure is available in CommandApi. **Concrete fallback chain:** (1) Check if projection status is queryable from CommandApi via a projection actor or service — if yes, use it. (2) If not, reuse the same logic that populates `CausationChain.AffectedProjections` — trace how `AdminStreamQueryController`'s causation endpoint determines affected projection names and replicate that logic. (3) If neither is available, return empty `AffectedProjections` list.

### External Trace URL Configuration

From ADR-P5 (Admin Web UI — Observability Deep-Link Strategy): "Observability tool URLs are configured via DAPR configuration store or environment variables (`ADMIN_TRACE_URL`, `ADMIN_METRICS_URL`, `ADMIN_LOGS_URL`). Missing URLs disable the corresponding deep-link buttons gracefully."

The trace map reads `ADMIN_TRACE_URL` and formats it:
- If `ADMIN_TRACE_URL` contains `{correlationId}` placeholder: replace with actual correlation ID
- Otherwise: append `?correlationId={correlationId}` as query parameter
- If `ADMIN_TRACE_URL` is not configured: `ExternalTraceUrl = null`

Check how other admin components read `ADMIN_TRACE_URL` — search the codebase for `ADMIN_TRACE_URL`, `ADMIN_METRICS_URL`, or `ADMIN_LOGS_URL` to find the existing config-reading pattern. The health dashboard (story 15-7) may implement this. If no existing pattern is found, use `IConfiguration["ADMIN_TRACE_URL"]` (reads from environment variables and DAPR config) as the simplest approach.

### Critical Patterns to Follow

1. **Model records**: Use the null-coalescing to `string.Empty` pattern (no throwing constructors) for deserialization safety. `ToString()` should not expose sensitive data (SEC-5).
2. **Reuse existing models**: `TraceMapEvent` is new (distinct from `CausationEvent` because it includes `IsRejection` and is designed for the trace map context). Do NOT reuse `CausationEvent` — the models serve different purposes and may diverge.
3. **Service interface**: Add to existing `IStreamQueryService` — do NOT create a new interface. Despite the new controllers, the service abstraction remains unified.
4. **Controllers**: NEW `AdminTracesController` (Admin.Server) and `AdminTraceQueryController` (CommandApi). These are justified by the different route namespace. Follow the exact same patterns as `AdminStreamsController` / `AdminStreamQueryController` — same DI, same auth, same error handling.
5. **API client**: Add to existing `AdminStreamApiClient` — do NOT create a new client class. Follow the `GetAggregateBlameAsync` error handling pattern.
6. **Component**: Follow `BlameViewer.razor` / `BisectTool.razor` / `EventDebugger.razor` / `CommandSandbox.razor` structure — parameters for identity, `OnClose` callback, `SkeletonCard` for loading, `IssueBanner` for errors.
7. **Deep linking**: Follow `StreamDetail.razor` existing pattern with `[SupplyParameterFromQuery]` and `NavigateTo(url, forceLoad: false, replace: true)`. Use the established `UpdateUrl()` method.
8. **Tests**: Follow existing test patterns — xUnit + Shouldly for models, NSubstitute for service/controller mocks, bUnit for components.
9. **Timeout**: Use the default 30-second `ServiceInvocationTimeoutSeconds` from `AdminServerOptions`.
10. **Shared components**: Reuse `JsonViewer`, `SkeletonCard`, `EmptyState`, `IssueBanner`, `StatCard`, `TimeFormatHelper` from `Admin.UI/Components/Shared/`. Never recreate these.

### Existing Code to Reuse (DO NOT Reinvent)

| What | Where | Why |
|------|-------|-----|
| Command status key derivation | `CommandStatusConstants` in Server/Contracts | Key pattern for state store lookup |
| Command status reading | `CommandStatusController.cs` in CommandApi | Existing pattern for reading status from state store |
| Event reading | `AdminStreamQueryController` existing methods | Reading events from aggregate stream |
| Projection status | `ProjectionDashboard` / existing projection endpoints | Projection LastProcessedSequence |
| `CausationChain.AffectedProjections` logic | `AdminStreamQueryController` causation endpoint | How affected projections are determined |
| External URL configuration | Health dashboard (story 15-7) | Reading `ADMIN_TRACE_URL` from config |
| `FieldChange` model | Not used in this story | Trace map doesn't diff state |
| `SkeletonCard`, `EmptyState`, `IssueBanner` | `Admin.UI/Components/Shared/` | Loading/error/empty states |
| `TimeFormatHelper` | `Admin.UI/Components/Shared/TimeFormatHelper.cs` | Relative timestamp formatting |
| URL building helpers | `AdminStreamApiClient` static methods | `Uri.EscapeDataString` patterns |
| `InvokeCommandApiAsync` | `DaprStreamQueryService` | DAPR service invocation with JWT forwarding |
| URL update pattern | `StreamDetail.razor` `UpdateUrl()` method | Building query params and NavigateTo |
| FluentUI components | `FluentButton`, `FluentBadge`, `FluentStack`, `FluentDataGrid` | Standard UI toolkit |
| `CausationChainView.razor` | `Admin.UI/Components/` | Reference for correlation ID display + copy pattern |

### File Locations

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/CorrelationTraceMap.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/TraceMapEvent.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/TraceMapProjection.cs`
- `src/Hexalith.EventStore.CommandApi/Controllers/AdminTraceQueryController.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminTracesController.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/CorrelationTraceMap.razor`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/CorrelationTraceMapTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/TraceMapEventTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/TraceMapProjectionTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminTracesControllerTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CorrelationTraceMapTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` — add `GetCorrelationTraceMapAsync`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — implement `GetCorrelationTraceMapAsync` (delegates to CommandApi, 30s timeout)
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` — add `GetCorrelationTraceMapAsync`
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` — add trace map button, `?trace` query param, CorrelationTraceMap integration, make correlation IDs clickable, update mutual exclusion precedence

### Anti-Patterns to Avoid

- **YES, DO create** `AdminTracesController` (Admin.Server) and `AdminTraceQueryController` (CommandApi) — this is an **intentional pattern break** from stories 20-1 through 20-4 which all say "do NOT create new controllers." The trace map operates on a different route namespace (`/traces/{tenantId}/{correlationId}`) that cannot fit under the existing `/streams/{tenantId}/{domain}/{aggregateId}/...` prefix without routing conflicts. Two new controller files are correct here.
- **Do NOT** create a separate `ITraceQueryService` — extend `IStreamQueryService`
- **Do NOT** create a new `AdminTraceApiClient` — extend `AdminStreamApiClient`
- **Do NOT** duplicate `CausationEvent` — `TraceMapEvent` is a new model with different properties (has `IsRejection`, designed for the trace map context)
- **Do NOT** reuse `CausationChainView.razor` for the trace map — the trace map is a full interactive panel, not an inline chain display. They serve different purposes.
- **Do NOT** use `<FluentTooltip>` for truncated values — use native `title` attribute (performance, consistent with all Epic 20 components)
- **Do NOT** add a NavMenu entry for trace map — it's accessed from StreamDetail page via correlation ID links
- **Do NOT** call the trace map from within the CausationChainView — they are independent tools. The CausationChainView's `OnCorrelationClick` callback is already wired in StreamDetail for copy/filter behavior; the trace map is triggered by the new clickable correlation ID links.
- **Do NOT** add new `AdminServerOptions` properties — use the default `ServiceInvocationTimeoutSeconds` (30s)
- **Do NOT** fail the entire trace if projections are unavailable — gracefully degrade to empty projections list
- **Do NOT** scan more than 10,000 events from the stream tail — cap to prevent timeout on very large streams
- **Do NOT** block the trace map result on the external trace URL — if DAPR config is unavailable, just set `ExternalTraceUrl = null`
- **Do NOT** assume command status always exists — 24-hour TTL means old correlations have no status. Always handle the "Unknown" case.

### Clipboard Copy Pattern

The CorrelationTraceMap component needs to copy the correlation ID to clipboard. **Before implementing, check which pattern the codebase uses:**

1. **Check if a shared `ClipboardHelper` or `ClipboardService` exists** in `Admin.UI/Services/` or `Admin.UI/Components/Shared/` — if so, reuse it.
2. **Check how `CausationChainView.razor` handles it** — it uses an `OnCorrelationClick` EventCallback that delegates to the parent (`StreamDetail.razor`). If StreamDetail handles clipboard via JS interop, follow that same pattern for consistency.
3. **Only if no shared pattern exists**, implement directly:

```csharp
[Inject] private IJSRuntime JS { get; set; } = default!;

private async Task CopyCorrelationId()
{
    await JS.InvokeVoidAsync("navigator.clipboard.writeText", CorrelationId);
}
```

**Consistency is critical** — all correlation ID copy interactions across the admin UI must use the same mechanism. Do NOT introduce a second clipboard approach if one already exists.

### Command Pipeline Stage Mapping

The command status `Status` field uses the pipeline stage names from the architecture. Map them to display labels:

| Status Value | Display Label | Pipeline Position | Badge Style |
|-------------|---------------|-------------------|-------------|
| `"Received"` | "Received" | 1/5 | Completed |
| `"Processing"` | "Processing" | 2/5 | Current (in-flight) |
| `"EventsStored"` | "Events Stored" | 3/5 | Completed |
| `"EventsPublished"` | "Events Published" | 4/5 | Completed |
| `"Completed"` | "Completed" | 5/5 (terminal) | Success |
| `"Rejected"` | "Rejected" | 5/5 (terminal) | Warning |
| `"PublishFailed"` | "Publish Failed" | 4/5 (terminal) | Error |
| `"TimedOut"` | "Timed Out" | 2/5 (terminal) | Error |
| `"Unknown"` | "Unknown" | N/A | Muted |

For non-terminal statuses (Received, Processing, EventsStored, EventsPublished), all stages up to and including the current one are "completed", and stages after are "future" (muted).

### Previous Story Intelligence

**From story 20-1 (Blame View — done):**
- `BlameViewer.razor` component pattern — trace map follows same component parameter structure
- `?blame` deep link — `?trace` follows same pattern
- 30-second timeout for API calls — trace map uses same timeout
- `ToString()` redaction for SEC-5 — follow same pattern
- `FluentDataGrid` with monospace columns — trace map reuses for events table

**From story 20-2 (Bisect Tool — done):**
- `BisectTool.razor` with loading/result phases — trace map uses same pattern
- `IAsyncDisposable` for canceling in-flight calls — trace map follows same pattern
- `FluentBadge` for status indicators — trace map uses for pipeline stages and projection status

**From story 20-3 (Step-Through Event Debugger — done):**
- `EventDebugger.razor` with VCR controls — trace map is simpler (no stepping, just display)
- Mutual exclusion precedence pattern — trace map extends the chain
- `TimeFormatHelper` for relative timestamps — trace map reuses

**From story 20-4 (Command Sandbox — ready-for-dev):**
- `CommandSandbox.razor` with input/result phases — trace map is simpler (just result display)
- `?sandbox` deep link — `?trace` follows same pattern
- Mutual exclusion: sandbox is just below trace in precedence

**From story 20-1/20-2 review findings (deferred work):**
- D1: CommandApi controllers use `[AllowAnonymous]` — trace computation controller follows same pattern (DAPR trust boundary)

### Project Structure Notes

- New controllers are at different route prefixes than existing stream controllers — no routing conflicts
- `AdminTracesController` follows exact same DI/auth/filter pattern as `AdminStreamsController`
- `AdminTraceQueryController` follows exact same pattern as `AdminStreamQueryController`
- Models in `Models/Streams/` keep the flat model organization consistent
- The service interface stays unified (`IStreamQueryService`) despite the new controllers

### References

- [Source: architecture.md — D2: Command Status Storage (key pattern, TTL, lifecycle)]
- [Source: architecture.md — Cross-Cutting Concern #4: Error Propagation (correlation ID traceability)]
- [Source: architecture.md — ADR-P5: Observability Deep-Link Strategy (ADMIN_TRACE_URL)]
- [Source: architecture.md — Structured Logging Pattern (correlationId in every log entry)]
- [Source: prd.md — FR72: Trace full causation chain for any event]
- [Source: ux-design-specification.md — Correlation ID Display (monospace, hyperlink, copy-to-clipboard)]
- [Source: ux-design-specification.md — Journey 2: Jerome's Command Investigation (correlation ID as hyperlink)]
- [Source: ux-design-specification.md — UX-DR42 (deep linking), UX-DR47 (observability deep links)]
- [Source: 20-3-step-through-event-debugger.md — mutual exclusion, deep link, component patterns]
- [Source: 20-4-command-sandbox-test-harness.md — mutual exclusion, POST pattern, component patterns]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
