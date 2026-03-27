# Story 20.4: Command Sandbox Test Harness

Status: done

Size: Large — 3 new models in Admin.Abstractions (SandboxCommandRequest, SandboxEvent, SandboxResult), extends `IStreamQueryService` with sandbox method, adds sandbox computation POST endpoint to CommandApi, adds REST facade POST to `AdminStreamsController`, extends `AdminStreamApiClient`, creates `CommandSandbox.razor` component integrated into `StreamDetail.razor` with deep linking. Creates ~5-6 test classes across 3 test projects (~30-40 tests). Fourth story in Epic 20's Advanced Debugging suite.

## Story

As a **platform operator or developer using the Hexalith EventStore Admin UI**,
I want **a command sandbox that lets me submit a command against any historical aggregate state and see what events would be produced — without persisting anything to the event store**,
so that **I can test command behavior against real aggregate state, preview replays before executing them, investigate "what-if" scenarios at any point in the aggregate's history, and safely validate command payloads before submission — like a dry-run mode for event-sourced command processing**.

**Dependency:** Stories 20-1 (Blame View), 20-2 (Bisect Tool), and 20-3 (Step-Through Event Debugger) should be implemented first — all stories modify `StreamDetail.razor` and the mutual exclusion precedence logic. No code dependency on blame/bisect/step models. Epic 15 must be complete (done).

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- Command sandbox renders correctly in `StreamDetail.razor` with command input form and result display
- POST endpoint accepts command details, invokes domain service against reconstructed state, returns structured result
- Sandbox invokes the same domain service Handle method with equivalent parameters (best-effort simulation — results may differ from real processing if domain services depend on actor-injected envelope extensions such as trace context or `actor:globalAdmin` flag)
- No events, status records, or archive entries are persisted by the sandbox — execution is fully ephemeral
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Deep linking via `?sandbox` or `?sandbox={seq}` opens sandbox at specified state position

## Acceptance Criteria

1. **AC1: SandboxCommandRequest model** — A new `SandboxCommandRequest` record in `Admin.Abstractions/Models/Streams/` captures the command input for sandbox execution. Properties: `CommandType` (string — the **fully qualified** command type name, e.g., `"Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter"`; must match the type name used in `CommandEnvelope.CommandType`), `PayloadJson` (string — the command payload as a JSON string; if null or empty, defaults to `"{}"`), `AtSequence` (long? — the state position to execute against; null = latest state; 0 = empty initial state before any events), `CorrelationId` (string? — optional correlation ID for tracing, auto-generated as `Ulid.NewUlid().ToString()` if null), `UserId` (string? — optional user ID override; defaults to the authenticated user's ID from JWT). All string properties use null-coalescing to `string.Empty` (following deserialization-safe pattern). `ToString()` redacts `PayloadJson` (SEC-5 compliance — command payloads may contain sensitive data).

2. **AC2: SandboxEvent model** — A new `SandboxEvent` record in `Admin.Abstractions/Models/Streams/` captures one event that would be produced by sandbox execution. Properties: `Index` (int — 0-based position in the produced events list), `EventTypeName` (string — the event type name), `PayloadJson` (string — the event payload as a JSON string), `IsRejection` (bool — true if this event is a domain rejection, false for state-change events). All string properties use null-coalescing to `string.Empty`. `ToString()` redacts `PayloadJson` (SEC-5).

3. **AC3: SandboxResult model** — A new `SandboxResult` record in `Admin.Abstractions/Models/Streams/` wraps the complete sandbox execution output. Properties: `TenantId` (string), `Domain` (string), `AggregateId` (string), `AtSequence` (long — the state position the command was executed against; 0 if tested against empty initial state), `CommandType` (string — the command type that was tested), `Outcome` (string — `"accepted"` when the domain service returned state-change events, `"accepted"` with empty `ProducedEvents` when the domain service returned a no-op (`DomainResult.IsNoOp` — command acknowledged but no state change), `"rejected"` when it returned rejection events (`DomainResult.IsRejection`), `"error"` when the domain service invocation failed), `ProducedEvents` (IReadOnlyList\<SandboxEvent\> — events that would be produced; empty list on error or no-op), `ResultingStateJson` (string — the aggregate state JSON after applying produced events to the input state; equals input state on no-op; empty on rejection or error), `StateChanges` (IReadOnlyList\<FieldChange\> — diff between input state and resulting state; reuses existing `FieldChange` model; empty on rejection, error, or no-op), `ErrorMessage` (string? — non-null only when Outcome = `"error"`, describes the domain service invocation failure), `ExecutionTimeMs` (long — elapsed time for sandbox execution in milliseconds). All string properties use null-coalescing to `string.Empty`. `ToString()` redacts `ResultingStateJson`, `PayloadJson` in events, and field values in `StateChanges` (SEC-5).

4. **AC4: IStreamQueryService extension** — Add `SandboxCommandAsync(string tenantId, string domain, string aggregateId, SandboxCommandRequest request, CancellationToken ct)` to `IStreamQueryService`. Returns `SandboxResult`. Throws `ArgumentException` if `request.CommandType` is empty. Throws `ArgumentException` if `request.AtSequence` is provided and < 0.

5. **AC5: Server-side sandbox computation (two-layer delegation)** — The sandbox computation follows the same two-layer pattern as existing `/blame`, `/bisect`, and `/step` endpoints.

   **(a) CommandApi layer (computation):** Add a new sandbox POST endpoint alongside the existing admin stream query endpoints in `AdminStreamQueryController` (same controller, same route prefix pattern). Route: `POST .../sandbox`. Request body: `SandboxCommandRequest` (JSON). The endpoint:

   - **Step 1: Validate request.** Verify `CommandType` is non-empty. If `PayloadJson` is null or empty, default it to `"{}"`. Verify `PayloadJson` is valid JSON (parse with `JsonDocument.Parse`, return 400 if malformed) — this is a **UX convenience** to catch obvious errors before a round-trip to the domain service; the EventStore is schema-ignorant and does not validate command payload shapes. Verify `AtSequence >= 0` if provided.
   - **Step 2: Reconstruct state.** If `AtSequence` is null, get total event count and use latest. If `AtSequence == 0`, use empty state `{}`. Otherwise, reconstruct state at `AtSequence` using existing `ReconstructState` logic. Return 404 if stream doesn't exist (unless `AtSequence == 0`, which is always valid). Return 400 if `AtSequence` exceeds stream length.
   - **Step 3: Invoke domain service.** This is the critical step. The sandbox MUST use `IDomainServiceInvoker` (from `Hexalith.EventStore.Server.DomainServices`) to invoke the domain service — this is the same interface the `AggregateActor` uses. Add `IDomainServiceInvoker` to the `AdminStreamQueryController` constructor (it is already registered in DI via `ServiceCollectionExtensions.AddHexalithEventStoreServer()`). The sandbox needs to:
     (i) **Build a `CommandEnvelope`**: Construct with `MessageId = Ulid.NewUlid().ToString()`, `Tenant = tenantId`, `Domain = domain`, `AggregateId = aggregateId`, `CommandType = request.CommandType` (fully qualified), `Payload = System.Text.Encoding.UTF8.GetBytes(request.PayloadJson)`, `CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()`, `UserId = request.UserId ?? "sandbox-user"`, `CausationId = null`, `Extensions = null`. Note: `CommandEnvelope` has a **throwing constructor** that validates `MessageId`, `CommandType`, `CorrelationId`, and `UserId` are non-empty.
     (ii) **Build the current state for the domain service**: The `IDomainServiceInvoker.InvokeAsync` method takes `(CommandEnvelope command, object? currentState, CancellationToken ct)`. The `currentState` parameter accepts the reconstructed state. Pass the `JsonObject` from `ReconstructState` — the invoker serializes it via `DomainServiceRequest(CommandEnvelope, object? CurrentState)` which is sent to the domain service as JSON. For `AtSequence == 0`, pass `null` as `currentState`.
     (iii) **Invoke**: Call `domainServiceInvoker.InvokeAsync(commandEnvelope, currentState, ct)`. This handles domain service resolution (`IDomainServiceResolver` maps tenant+domain to DAPR app ID + method, defaulting to version "v1"), DAPR `InvokeMethodAsync`, and response parsing. Catch `DomainServiceNotFoundException` for "no service registered" errors.
     (iv) **Parse the `DomainResult` response**: The returned `DomainResult` has properties `IsSuccess`, `IsRejection`, and `IsNoOp`. Events are in `DomainResult.Events` as `IReadOnlyList<IEventPayload>`. Cast each event to `ISerializedEventPayload` (from `Hexalith.EventStore.Contracts.Events`) to access `EventTypeName` (string) and `PayloadBytes` (byte[]). Convert payload bytes to JSON string: `Encoding.UTF8.GetString(eventPayload.PayloadBytes)`. Detect rejection events via `event is IRejectionEvent` (marker interface from `Hexalith.EventStore.Contracts.Events`).
     (v) **Determine outcome**: If `DomainResult.IsRejection` → `Outcome = "rejected"`. If `DomainResult.IsNoOp` (empty events) → `Outcome = "accepted"` with empty events. If `DomainResult.IsSuccess` → `Outcome = "accepted"` with produced events. Set `SandboxEvent.IsRejection = event is IRejectionEvent` for each event.
   - **Domain service version**: The sandbox always uses the default domain service version ("v1"). The `IDomainServiceInvoker` extracts version from `CommandEnvelope.Extensions["domain-service-version"]`, defaulting to "v1". Since the sandbox sets `Extensions = null`, it will always invoke the default version. This is a known limitation.
   - **Response size validation**: `IDomainServiceInvoker.InvokeAsync` validates `MaxEventsPerResult` and `MaxEventSizeBytes` from `DomainServiceOptions`. The sandbox inherits these limits. This is appropriate for diagnostic use.
   - **Step 4: Compute resulting state (accepted only).** Apply each produced event's payload to the input state using the existing JSON deep merge logic (same as `ReconstructState`). The resulting state is the input state with all produced events applied.
   - **Step 5: Compute state diff (accepted only).** Diff the input state and resulting state using existing JSON diff logic (same as `DiffAggregateStateAsync`). Returns `IReadOnlyList<FieldChange>`.
   - **Step 6: Return SandboxResult.** Package all data into `SandboxResult` with execution time.
   - **Error handling:** If the domain service is not registered (no registration found for tenant+domain), return `SandboxResult` with `Outcome = "error"` and `ErrorMessage = "No domain service registered for domain '{domain}' in tenant '{tenantId}'"`. If the domain service invocation fails (DAPR `InvokeMethodException`, timeout, connection error), return `SandboxResult` with `Outcome = "error"` and `ErrorMessage` describing the failure. Do NOT throw — always return a structured `SandboxResult`.
   - **Dispose** intermediate `JsonDocument` objects after extracting values to prevent memory leaks.

   **(b) Admin.Server layer (delegation):** `DaprStreamQueryService.SandboxCommandAsync()` delegates to CommandApi via DAPR POST. **Critical: the existing `InvokeCommandApiAsync` helper does NOT support POST request bodies** — it creates an `HttpRequestMessage` without setting `HttpContent`. The dev agent must either: (a) **Add a POST-body overload** of `InvokeCommandApiAsync` that accepts a `TRequest body` parameter and sets `request.Content = JsonContent.Create(body, options: jsonSerializerOptions)` on the `HttpRequestMessage` before calling `InvokeMethodAsync`, OR (b) **Call `_daprClient.InvokeMethodAsync<SandboxCommandRequest, SandboxResult>(HttpMethod.Post, commandApiAppId, endpoint, sandboxRequest, ct)` directly** — but this bypasses JWT forwarding from `InvokeCommandApiAsync`. Option (a) is preferred for consistency. Use a **30-second timeout** (same as step/blame). Uses the default `ServiceInvocationTimeoutSeconds` from `AdminServerOptions`.

6. **AC6: REST endpoint** — `POST /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/sandbox` on `AdminStreamsController`. Request body: `SandboxCommandRequest` (JSON). Requires `ReadOnly` authorization policy (the sandbox is a diagnostic tool — it invokes the domain service Handle pure function but persists nothing; if domain services have side effects in Handle, that is a domain service bug, not an EventStore concern). Returns `SandboxResult` as JSON. Returns 404 if stream doesn't exist and `AtSequence` is not 0. Returns 400 if `CommandType` is empty or `PayloadJson` is malformed.

7. **AC7: API client method** — Add `SandboxCommandAsync(string tenantId, string domain, string aggregateId, SandboxCommandRequest request, CancellationToken ct)` to `AdminStreamApiClient`. Uses `HttpClient.PostAsJsonAsync` for the POST body. Follows the same error handling pattern as `GetAggregateBlameAsync`: catches expected exceptions (Unauthorized, Forbidden, ServiceUnavailable, OperationCanceled) separately, returns null on unexpected errors with logged warning. URL-encodes all path segments with `Uri.EscapeDataString`.

8. **AC8: CommandSandbox component** — A new `CommandSandbox.razor` component in `Admin.UI/Components/` provides the command sandbox interface. Layout has two phases:

   **Input phase:**
   - **Header bar:** "Command Sandbox" title with "Close" button (invokes `OnClose` EventCallback).
   - **Info banner:** `IssueBanner` (info severity, not error) with: "Commands are executed as a dry run — no events are persisted. The domain service Handle method is invoked with reconstructed state to show what would happen." This sets clear expectations.
   - **Command type input:** `FluentTextField` with label "Command Type (fully qualified)" (required). Placeholder: "e.g., Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter". Validation: non-empty on submit.
   - **Payload editor:** `FluentTextArea` with label "Command Payload (JSON)" and monospace font (`font-family: var(--monospace-font, 'Cascadia Code', 'Consolas', monospace)`). Minimum 6 rows. Placeholder: `{ }`. Validation: must be valid JSON on submit (attempt `JsonDocument.Parse`, show inline error "Invalid JSON" below the field if malformed).
   - **Target sequence:** `FluentNumberField` with label "Test Against State at Sequence" (optional). Placeholder: "Latest". Min value: 0. Helper text: "Leave empty for latest state, 0 for empty initial state."
   - **Run button:** `FluentButton` with `Appearance.Accent` labeled "Run in Sandbox" with `Icon="@(new Icons.Regular.Size20.Play())"`. Disabled until `CommandType` is non-empty and payload is valid JSON (or empty — empty payload is valid as `{}`).

   **Result phase** (shown below the input form, not replacing it — so the user can modify and re-run):

   **Outcome banner:**
   - Accepted (with events): `IssueBanner` with success appearance: "Command accepted — {N} event(s) would be produced."
   - Accepted (no-op): `IssueBanner` with info appearance: "Command accepted (no-op) — no events would be produced. The aggregate state remains unchanged."
   - Rejected: `IssueBanner` with warning appearance: "Command rejected — {rejection event type}: {reason}."
   - Error: `IssueBanner` with error appearance: "Sandbox error — {error message}."

   **Result sections (visible when Outcome = "accepted"):**

   **Section 1: Produced Events**
   - Header: "Produced Events ({N})"
   - `FluentDataGrid<SandboxEvent>` with columns: Index (int, "#"), Event Type (string, bold), Rejection (bool, `FluentBadge` "Rejection" if true). Each row has a "View Payload" `FluentButton` with `Appearance.Lightweight` that opens the event payload JSON in a `FluentDialog` using `JsonViewer` shared component.

   **Section 2: State Changes**
   - Header: "State Changes ({N} fields changed)" — collapsed if empty.
   - `FluentDataGrid<FieldChange>` with columns: Field Path (monospace), Old Value (truncated to 60 chars, full in `title`), New Value (truncated to 60 chars, full in `title`).
   - `FluentSearch` filter for field paths (case-insensitive substring match).

   **Section 3: Resulting State**
   - Header: "Resulting State (after applying {N} events)"
   - `JsonViewer` shared component displaying `ResultingStateJson`.

   **Execution metadata:**
   - Footer line: "Executed in {ExecutionTimeMs}ms against state at sequence {AtSequence}."

   **Result sections (visible when Outcome = "rejected"):**

   **Rejection detail:**
   - If `ProducedEvents` contains rejection events, show them in the events table (same as accepted but with `IsRejection = true` flag highlighted).
   - No state changes or resulting state for rejections.

   Component implements `IAsyncDisposable` — cancels any in-flight API calls via `CancellationTokenSource` on disposal.

   Component parameters: `TenantId`, `Domain`, `AggregateId` (EditorRequired), `InitialSequence` (long? — pre-fill target sequence), `OnClose` (EventCallback), `OnNavigateToEvent` (EventCallback\<long\> — navigates to event detail in StreamDetail).

9. **AC9: StreamDetail.razor integration** — The existing `StreamDetail.razor` page adds a "Sandbox" `FluentButton` with `Appearance.Outline` and `Icon="@(new Icons.Regular.Size20.BeakerSettings())"` in the toolbar (near the existing "Inspect State", "Diff", "Blame", "Bisect", and "Step Through" buttons). Clicking opens the `CommandSandbox` component (replaces the detail panel area, same pattern as `BlameViewer`, `BisectTool`, and `EventDebugger`). If an event is currently selected (`?detail=N`), the initial sequence is pre-filled with that value. If no event is selected, the initial sequence field is left empty (defaults to latest).

10. **AC10: Deep linking** — `StreamDetail.razor` supports a new `?sandbox` query parameter via `[SupplyParameterFromQuery(Name = "sandbox")]`. When the parameter is present with no value or value `"true"`, the page auto-opens the CommandSandbox with no pre-filled sequence. When the parameter has a numeric value (e.g., `?sandbox=42`), the page auto-opens the CommandSandbox with that sequence pre-filled. Navigating to sandbox updates the URL via `Navigation.NavigateTo(url, forceLoad: false, replace: true)`. The `sandbox` parameter is mutually exclusive with `step`, `bisect`, `blame`, and `diff` — if multiple are present, precedence is: `sandbox` > `step` > `bisect` > `blame` > `diff` (rationale: sandbox is an active tool the user explicitly opens; it takes highest precedence to avoid losing form input state when deep linking).

11. **AC11: Loading, empty, and error states** — `SkeletonCard` shows during sandbox execution (between clicking "Run" and receiving the result). If API call fails with non-timeout error: `IssueBanner` with "Unable to run sandbox — check server connectivity." If API call times out (`OperationCanceledException`): `IssueBanner` with "Sandbox timed out — state reconstruction may be too slow for this aggregate (large stream without nearby snapshots). Try specifying a sequence closer to a known snapshot position, or use a smaller aggregate for testing." If domain service is not registered (error outcome): `IssueBanner` with the error message from `SandboxResult.ErrorMessage`. If stream doesn't exist and `AtSequence` is not 0: `IssueBanner` with "Stream not found — verify tenant, domain, and aggregate ID."

## Tasks / Subtasks

- [x] Task 1: Create new models in Admin.Abstractions (AC: #1, #2, #3)
  - [x] 1.1 Create `SandboxCommandRequest` record in `Models/Streams/SandboxCommandRequest.cs` with null-coalescing, `ToString()` redaction
  - [x] 1.2 Create `SandboxEvent` record in `Models/Streams/SandboxEvent.cs` with null-coalescing, `ToString()` redaction
  - [x] 1.3 Create `SandboxResult` record in `Models/Streams/SandboxResult.cs` with all properties, null-coalescing, `ToString()` redaction
- [x] Task 2: Add sandbox computation endpoint to CommandApi (AC: #5a) — **MOST COMPLEX TASK**
  - [x] 2.1 Add `IDomainServiceInvoker domainServiceInvoker` to the `AdminStreamQueryController` constructor. This service is registered in DI via `ServiceCollectionExtensions.AddHexalithEventStoreServer()`. **Verify** CommandApi's `Program.cs` calls this method (it should — CommandApi hosts actors). If DI fails at startup with unresolved `IDomainServiceInvoker`, ensure the registration call is present. The invoker transitively requires `IDomainServiceResolver`, `DaprClient`, and `IOptions<DomainServiceOptions>` — all should already be registered.
  - [x] 2.2 Add `POST .../sandbox` endpoint in `AdminStreamQueryController`: validate request (default empty payload to `"{}"`), reconstruct state at target sequence, build `CommandEnvelope` with `Payload = Encoding.UTF8.GetBytes(payloadJson)` and generated `MessageId`/`CorrelationId`
  - [x] 2.3 Call `domainServiceInvoker.InvokeAsync(commandEnvelope, currentState, ct)` — catch `DomainServiceNotFoundException` for error outcome
  - [x] 2.4 Parse `DomainResult`: cast events to `ISerializedEventPayload` for `EventTypeName` + `PayloadBytes`, detect rejections via `DomainResult.IsRejection` and `IRejectionEvent`, handle no-op via `DomainResult.IsNoOp`
  - [x] 2.5 For accepted outcomes: apply produced events to input state via `DeepMerge`, compute diff via `JsonDiff`, build `SandboxResult`
  - [x] 2.6 Handle all error paths: no domain service registered (`DomainServiceNotFoundException`), invocation failure (DAPR exceptions), malformed payload (400), stream not found (404), sequence out of range (400)
- [x] Task 3: Extend stream query service (AC: #4, #5b)
  - [x] 3.1 Add `SandboxCommandAsync` to `IStreamQueryService` interface
  - [x] 3.2 Implement `SandboxCommandAsync` in `DaprStreamQueryService` (delegates to CommandApi via POST `InvokeMethodAsync`, 30-second timeout)
- [x] Task 4: Add REST facade endpoint on Admin.Server (AC: #6)
  - [x] 4.1 Add `SandboxCommandAsync` POST endpoint to `AdminStreamsController` — validate command type non-empty, delegate to service
- [x] Task 5: Create UI API client method (AC: #7)
  - [x] 5.1 Add `SandboxCommandAsync` to `AdminStreamApiClient` — use `PostAsJsonAsync`, URL-encode path segments, standard error handling
- [x] Task 6: Create CommandSandbox component (AC: #8, #11)
  - [x] 6.1 Create `CommandSandbox.razor` in `Admin.UI/Components/` — implement input form with command type, payload editor, target sequence
  - [x] 6.2 Implement JSON validation on payload input (attempt `JsonDocument.Parse`, show inline error)
  - [x] 6.3 Implement result display: outcome banner, produced events table, state changes table, resulting state viewer
  - [x] 6.4 Implement `IAsyncDisposable` for in-flight API call cancellation
- [x] Task 7: Integrate into StreamDetail page (AC: #9, #10)
  - [x] 7.1 Add "Sandbox" button to `StreamDetail.razor` toolbar
  - [x] 7.2 Add `?sandbox` deep link support via `[SupplyParameterFromQuery(Name = "sandbox")]`
  - [x] 7.3 Wire CommandSandbox into the detail panel area, handle callbacks
  - [x] 7.4 Update mutual exclusion: add `_sandboxMode` boolean alongside existing `_stepMode`, `_bisectMode`, etc. Prepend `@if (_sandboxMode)` before the `_stepMode` check in the Razor if-else chain. Precedence: `sandbox` > `step` > `bisect` > `blame` > `diff`
- [x] Task 8: Write tests (all ACs)
  - [x] 8.1 Model tests in Admin.Abstractions.Tests (`Models/Streams/SandboxCommandRequestTests.cs`, `SandboxEventTests.cs`, `SandboxResultTests.cs`) — constructor with valid inputs, null-coalescing defaults, `ToString()` redaction, serialization round-trip
  - [x] 8.2 Service query tests in Admin.Server.Tests (`Services/`) — verify delegation to CommandApi via POST, timeout behavior, argument validation. **Critical:** add a test verifying the `InvokeCommandApiAsync` POST overload sets `HttpRequestMessage.Content` with the serialized `SandboxCommandRequest` body — if this is missing, CommandApi receives null and returns 400
  - [x] 8.3 Controller tests in Admin.Server.Tests (`Controllers/`) — parameter validation (empty command type → 400, malformed JSON → 400), authorization policy, response types
  - [x] 8.4 Edge case tests: (a) `AtSequence == 0` — sandbox against empty initial state (`currentState = null`), state changes show all fields as "added"; (b) `AtSequence == null` — uses latest state; (c) `AtSequence` exceeds stream length — 400 error; (d) empty/null payload defaults to `"{}"` — valid request, no validation failure; (e) domain service returns rejection (`DomainResult.IsRejection = true`) — verify `Outcome = "rejected"` and `SandboxEvent.IsRejection = true`; (f) domain service returns no-op (`DomainResult.IsNoOp = true`, empty events) — verify `Outcome = "accepted"` with empty `ProducedEvents` and empty `StateChanges`
  - [x] 8.5 Domain service error tests (mock `IDomainServiceInvoker` via NSubstitute): (a) throw `DomainServiceNotFoundException` → `Outcome = "error"` with descriptive message; (b) domain service timeout (`OperationCanceledException`) → `Outcome = "error"`; (c) domain service throws unexpected exception → `Outcome = "error"` with exception message
  - [x] 8.6 CommandSandbox component tests in Admin.UI.Tests (`Components/CommandSandboxTests.cs`) — form validation (empty command type disables Run button, malformed JSON shows error), result rendering (accepted outcome shows events + state changes, rejected outcome shows rejection banner, error outcome shows error banner), info banner always visible
  - [x] 8.7 StreamDetail sandbox integration tests in Admin.UI.Tests (`Pages/`) — `?sandbox` + `?step` + `?bisect` + `?blame` + `?diff` mutual exclusion test (sandbox takes precedence), button renders, CommandSandbox opens
  - [x] 8.8 Accepted outcome end-to-end test: simulate domain service returning 2 state-change events, verify `ProducedEvents` has 2 entries, `ResultingStateJson` reflects applied events, `StateChanges` shows field diffs
  - [x] 8.9 Timeout handling test: simulate exceeding 30-second timeout (via `OperationCanceledException`) — verify UI shows `IssueBanner` with "Sandbox timed out" message

### Review Findings

- [x] [Review][Patch] **HIGH** `.Milliseconds` returns 0-999 component, not total ms — FIXED: use `(long)Stopwatch.GetElapsedTime(sw).TotalMilliseconds` [AdminStreamQueryController.cs: 3 locations]
- [x] [Review][Patch] **HIGH** `ConfigureAwait(false)` in Blazor component breaks sync context — FIXED: removed ConfigureAwait(false) [CommandSandbox.razor]
- [x] [Review][Patch] **HIGH** Missing critical test: POST body set on HttpRequestMessage — FIXED: added test verifying POST method and JSON content [DaprStreamQueryServiceSandboxTests.cs]
- [x] [Review][Patch] **MED** `DaprStreamQueryService` missing `AtSequence < 0` validation per AC4 — FIXED: added validation [DaprStreamQueryService.cs]
- [x] [Review][Patch] **MED** Null request body → misleading "CommandType is required" — FIXED: added explicit null check at both controllers [AdminStreamsController.cs, AdminStreamQueryController.cs]
- [x] [Review][Patch] **MED** Rejection events not implementing `ISerializedEventPayload` → empty EventTypeName — FIXED: added fallback `evt.GetType().Name` via extracted helper [AdminStreamQueryController.cs]
- [x] [Review][Patch] **MED** API errors swallowed as null → UI shows "Stream not found" — FIXED: changed message to "No result returned" [CommandSandbox.razor]
- [x] [Review][Patch] **MED** Missing tests — FIXED: added null request body test, AtSequence==0 test, negative AtSequence test, POST body verification test [test files]
- [x] [Review][Patch] **LOW** Duplicate event-extraction logic — FIXED: extracted `ExtractSandboxEvents` helper method [AdminStreamQueryController.cs]
- [x] [Review][Patch] **LOW** FluentDialog dismiss button state leak — FIXED: added `@bind-Hidden:after` handler [CommandSandbox.razor]
- [x] [Review][Defer] IssueBanner lacks severity parameter — info/success/warning all render as warning style — deferred, pre-existing component limitation
- [x] [Review][Defer] IStreamQueryService.SandboxCommandAsync returns nullable vs spec non-nullable — deferred, pragmatic design choice for 404 handling

## Dev Notes

### Architecture Compliance

This story follows the **exact same architecture** as stories 15-3 (Stream Browser), 15-4 (State Inspector), 20-1 (Blame View), 20-2 (Bisect Tool), and 20-3 (Step-Through Event Debugger). No new architectural patterns are introduced beyond one key addition: **the sandbox endpoint invokes the domain service**, which is a new capability for `AdminStreamQueryController` but follows the existing DAPR service invocation pattern used by `AggregateActor`.

**Data flow:** UI `CommandSandbox.razor` -> `AdminStreamApiClient.SandboxCommandAsync()` -> HTTP POST -> `AdminStreamsController` (Admin.Server) -> `IStreamQueryService.SandboxCommandAsync()` -> `DaprStreamQueryService` invokes CommandApi via DAPR `InvokeMethodAsync` POST (30-second timeout) -> CommandApi `/sandbox` endpoint reconstructs state, invokes domain service via DAPR service invocation, computes resulting state and diff, returns `SandboxResult`.

**Architecture note:** The dev agent MUST trace the existing `AggregateActor.ProcessCommandAsync` to understand how the domain service is invoked. The sandbox reuses the same domain service invocation mechanism but without persisting events. Specifically:
1. Find how the actor resolves the domain service endpoint (tenant+domain → DAPR app ID + method)
2. Find the request DTO sent to the domain service (likely `CommandEnvelope` + state)
3. Find the response DTO received from the domain service (`DomainResult` or equivalent)
4. Find how the actor determines if the result is a rejection vs acceptance

The sandbox endpoint MUST reuse the actual domain service invocation — it must NOT simulate or mock the domain service call.

### Critical Patterns to Follow

1. **Model records**: Use the null-coalescing to `string.Empty` pattern (no throwing constructors) for deserialization safety. Redact `PayloadJson`, `ResultingStateJson`, and field values in `ToString()` (SEC-5).
2. **Reuse `FieldChange`**: The state changes in `SandboxResult.StateChanges` reuse the existing `FieldChange` record from `Admin.Abstractions/Models/Streams/FieldChange.cs`. Do NOT create a duplicate model.
3. **Service interface**: Add to existing `IStreamQueryService` — do NOT create a new interface.
4. **Controller**: Add to existing `AdminStreamsController` (Admin.Server facade) and `AdminStreamQueryController` (CommandApi computation) — do NOT create new controllers. Use `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]` on AdminStreamsController.
5. **API client**: Add to existing `AdminStreamApiClient` — do NOT create a new client class. This is the **first POST method** in the API client — use `HttpClient.PostAsJsonAsync` instead of `GetAsync`. Follow the `GetAggregateBlameAsync` error handling pattern for exception handling.
6. **Component**: Follow `BlameViewer.razor` / `BisectTool.razor` / `EventDebugger.razor` structure — parameters for identity, `OnClose` callback, `SkeletonCard` for loading, `IssueBanner` for errors.
7. **Deep linking**: Follow `StreamDetail.razor` existing pattern with `[SupplyParameterFromQuery]` and `NavigateTo(url, forceLoad: false, replace: true)`. Use the established `UpdateUrl()` method for URL updates.
8. **Tests**: Follow existing test patterns — xUnit + Shouldly for models, NSubstitute for service/controller mocks, bUnit for components.
9. **Timeout**: Use the default 30-second `ServiceInvocationTimeoutSeconds` from `AdminServerOptions` — no dedicated timeout override needed. The sandbox performs one state reconstruction + one domain service call.
10. **Shared components**: Reuse `JsonViewer`, `SkeletonCard`, `EmptyState`, `IssueBanner`, `StatCard` from `Admin.UI/Components/Shared/`. Never recreate these.

### Domain Service Invocation — Concrete Reference Guide

The sandbox's unique aspect vs other Epic 20 stories is **invoking the domain service**. The other tools (blame, bisect, step-through) only read and analyze existing events. The sandbox calls the domain service Handle method with a command and state.

**The invocation chain (concrete classes):**

```
AdminStreamQueryController (sandbox endpoint)
  → IDomainServiceInvoker.InvokeAsync(commandEnvelope, currentState, ct)
    → IDomainServiceResolver.ResolveAsync(tenantId, domain, version, ct)
      → returns DomainServiceRegistration { AppId, MethodName }
    → DaprClient.InvokeMethodAsync(appId, methodName, DomainServiceRequest)
      → DomainServiceRequest { Command: CommandEnvelope, CurrentState: object? }
      → Domain service returns DomainServiceWireResult { Events, IsRejection }
    → DaprDomainServiceInvoker.ToDomainResult(wireResult)
      → DomainResult { Events: IReadOnlyList<IEventPayload>, IsSuccess, IsRejection, IsNoOp }
```

**Key classes and their locations:**

| Class | Location | Purpose |
|-------|----------|---------|
| `IDomainServiceInvoker` | `Server/DomainServices/IDomainServiceInvoker.cs` | **Inject this** into `AdminStreamQueryController`. Already in DI. |
| `IDomainServiceResolver` | `Server/DomainServices/IDomainServiceResolver.cs` | Resolves tenant+domain → DAPR app ID + method. Used internally by invoker. |
| `DomainServiceRegistration` | `Server/DomainServices/DomainServiceRegistration.cs` | Registration record with AppId, MethodName |
| `DomainServiceNotFoundException` | `Server/DomainServices/DomainServiceNotFoundException.cs` | Thrown when no service is registered — catch this for `Outcome = "error"` |
| `DomainServiceRequest` | `Contracts/Commands/DomainServiceRequest.cs` | Wire DTO: `{ Command: CommandEnvelope, CurrentState: object? }` |
| `DomainServiceWireResult` | `Contracts/Results/DomainServiceWireResult.cs` | Wire response with `IsRejection` bool flag |
| `DomainResult` | `Contracts/Results/DomainResult.cs` | Parsed result: `IsSuccess`, `IsRejection`, `IsNoOp`, `Events` |
| `ISerializedEventPayload` | `Contracts/Events/ISerializedEventPayload.cs` | Cast `IEventPayload` to this to get `EventTypeName` (string) + `PayloadBytes` (byte[]) |
| `IRejectionEvent` | `Contracts/Events/IRejectionEvent.cs` | Marker interface — `event is IRejectionEvent` detects rejections |
| `CommandEnvelope` | `Contracts/Commands/CommandEnvelope.cs` | Command DTO. `Payload` is `byte[]` (not string). Has throwing constructor. |
| `DomainServiceOptions` | `Server/DomainServices/DomainServiceOptions.cs` | Limits: `MaxEventsPerResult`, `MaxEventSizeBytes` |

**CommandEnvelope construction (critical detail):**

`CommandEnvelope.Payload` is `byte[]`, not `string`. Convert from the sandbox request:
```csharp
var envelope = new CommandEnvelope(
    MessageId: Ulid.NewUlid().ToString(),
    Tenant: tenantId,
    Domain: domain,
    AggregateId: aggregateId,
    CommandType: request.CommandType,  // must be fully qualified
    Payload: Encoding.UTF8.GetBytes(request.PayloadJson ?? "{}"),
    CorrelationId: string.IsNullOrEmpty(request.CorrelationId) ? Guid.NewGuid().ToString() : request.CorrelationId,
    UserId: string.IsNullOrEmpty(request.UserId) ? "sandbox-user" : request.UserId,
    CausationId: null,
    Extensions: null);
```
**Critical:** Use `string.IsNullOrEmpty()`, NOT null-coalescing (`??`). The model's null-coalescing defaults convert `null` to `""` (empty string), and `"" ?? "sandbox-user"` evaluates to `""`, which causes `CommandEnvelope` to throw `ArgumentException: UserId cannot be empty`.

**Rejection detection (concrete answer):**

- `DomainResult.IsRejection` (bool property) — top-level rejection flag
- Individual events: cast to `ISerializedEventPayload`, then check `event is IRejectionEvent`
- `DomainResult.IsNoOp` — command accepted but no events produced (empty events list)
- `DomainResult.IsSuccess` — command accepted with state-change events

**Domain service version:**

`IDomainServiceInvoker` extracts version from `CommandEnvelope.Extensions["domain-service-version"]`, defaulting to `"v1"`. Since sandbox sets `Extensions = null`, it always invokes the default `"v1"` version.

**Contrast with ReplayController (DO NOT confuse):**

`ReplayController` sends commands through the full MediatR pipeline (`mediator.Send(command)` → `SubmitCommandHandler` → `CommandRouter` → `AggregateActor` → domain service → **persist events**). The sandbox MUST NOT use this path. Instead, it calls `IDomainServiceInvoker.InvokeAsync` **directly**, bypassing the entire persistence layer.

### POST Request Pattern — New for Admin Stream API

All existing admin stream endpoints use GET. The sandbox uses **POST** because:
- The command payload JSON can be arbitrarily large (not suitable for query parameters)
- POST is semantically correct for "create a computation" even when ephemeral
- The request body is `SandboxCommandRequest` serialized as JSON

**DaprStreamQueryService POST delegation:**

The existing `InvokeCommandApiAsync<TResponse>` helper creates an `HttpRequestMessage` but **never sets `HttpContent`** — it only supports GET with query parameters. For the sandbox POST, the dev agent MUST add a POST-body overload:

```csharp
// New overload to add to DaprStreamQueryService:
private async Task<TResponse?> InvokeCommandApiAsync<TRequest, TResponse>(
    string endpoint,
    TRequest body,
    CancellationToken ct,
    int? timeoutSeconds = null)
{
    var request = _daprClient.CreateInvokeMethodRequest(
        HttpMethod.Post, _commandApiAppId, endpoint);
    // Forward JWT from current request context (same as existing GET overload)
    ForwardAuthorizationHeader(request);
    // Set POST body — MUST use JsonContent.Create (NOT StringContent) to auto-set Content-Type: application/json
    request.Content = JsonContent.Create(body);
    // Apply timeout
    using var cts = CreateTimeoutCts(ct, timeoutSeconds);
    return await _daprClient.InvokeMethodAsync<TResponse>(request, cts.Token);
}
```

Then call it from `SandboxCommandAsync`:
```csharp
var endpoint = $"api/v1/admin/streams/{Uri.EscapeDataString(tenantId)}/{Uri.EscapeDataString(domain)}/{Uri.EscapeDataString(aggregateId)}/sandbox";
return await InvokeCommandApiAsync<SandboxCommandRequest, SandboxResult>(endpoint, request, ct);
```

### Existing Code to Reuse (DO NOT Reinvent)

| What | Where | Why |
|------|-------|-----|
| State reconstruction | `ReconstructState` in `AdminStreamQueryController` | Sandbox reconstructs state at target sequence |
| JSON diff | `JsonDiff` helper in `AdminStreamQueryController` | Sandbox diffs input state vs resulting state |
| Deep merge | `DeepMerge` helper in `AdminStreamQueryController` | Sandbox applies produced events to state |
| `FieldChange` model | `Admin.Abstractions/Models/Streams/FieldChange.cs` | Reuse for state changes |
| `IDomainServiceInvoker` | `Server/DomainServices/IDomainServiceInvoker.cs` | **Inject into controller** — invokes domain service via DAPR |
| `IDomainServiceResolver` | `Server/DomainServices/IDomainServiceResolver.cs` | Resolves tenant+domain → DAPR app ID (used internally by invoker) |
| `DomainServiceRegistration` | `Server/DomainServices/DomainServiceRegistration.cs` | Registration record with AppId, MethodName |
| `DomainServiceNotFoundException` | `Server/DomainServices/DomainServiceNotFoundException.cs` | Catch for "no domain service registered" error path |
| `DomainServiceRequest` | `Contracts/Commands/DomainServiceRequest.cs` | Wire DTO sent to domain service: `{ Command, CurrentState }` |
| `DomainResult` | `Contracts/Results/DomainResult.cs` | Parsed response: `IsSuccess`, `IsRejection`, `IsNoOp`, `Events` |
| `DomainServiceWireResult` | `Contracts/Results/DomainServiceWireResult.cs` | Wire response format with `IsRejection` flag |
| `ISerializedEventPayload` | `Contracts/Events/ISerializedEventPayload.cs` | Cast `IEventPayload` to get `EventTypeName` + `PayloadBytes` |
| `IRejectionEvent` | `Contracts/Events/IRejectionEvent.cs` | Marker interface for rejection detection: `event is IRejectionEvent` |
| `CommandEnvelope` | `Contracts/Commands/CommandEnvelope.cs` | Build command envelope — `Payload` is `byte[]`, has throwing constructor |
| `DomainServiceOptions` | `Server/DomainServices/DomainServiceOptions.cs` | Limits: `MaxEventsPerResult`, `MaxEventSizeBytes` (inherited by sandbox) |
| `JsonViewer` | `Admin.UI/Components/Shared/JsonViewer.razor` | Display state and event payloads |
| `SkeletonCard`, `EmptyState`, `IssueBanner` | `Admin.UI/Components/Shared/` | Loading/error/empty states |
| `StatCard` | `Admin.UI/Components/Shared/StatCard.razor` | Result summary |
| URL building helpers | `AdminStreamApiClient` static methods | `Uri.EscapeDataString` patterns |
| `InvokeCommandApiAsync` | `DaprStreamQueryService` | DAPR service invocation with JWT forwarding — **needs POST overload** |
| URL update pattern | `StreamDetail.razor` `UpdateUrl()` method | Building query params and NavigateTo |
| FluentUI components | `FluentButton`, `FluentTextField`, `FluentTextArea`, `FluentNumberField`, `FluentDataGrid`, `FluentSearch`, `FluentDialog` | Standard UI toolkit |

### File Locations

**New files:**
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxCommandRequest.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxEvent.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxResult.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/SandboxCommandRequestTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/SandboxEventTests.cs`
- `tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/SandboxResultTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs`

**Modified files:**
- `src/Hexalith.EventStore.CommandApi/Controllers/AdminStreamQueryController.cs` — add POST sandbox computation endpoint with domain service invocation
- `src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs` — add `SandboxCommandAsync`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` — implement `SandboxCommandAsync` (delegates to CommandApi via POST, 30s timeout)
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` — add sandbox POST facade endpoint
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs` — add `SandboxCommandAsync` (first POST method)
- `src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor` — add Sandbox button, `?sandbox` query param, CommandSandbox integration, update mutual exclusion precedence

### Anti-Patterns to Avoid

- **Do NOT** create a separate `ISandboxService` — extend `IStreamQueryService`
- **Do NOT** create a new `AdminSandboxController` — extend existing `AdminStreamsController` and `AdminStreamQueryController`
- **Do NOT** create a new `AdminSandboxApiClient` — extend `AdminStreamApiClient`
- **Do NOT** duplicate `FieldChange` — reuse from `Admin.Abstractions/Models/Streams/FieldChange.cs`
- **Do NOT** persist ANY data from sandbox execution — no events, no status records, no archive entries. The sandbox is fully ephemeral.
- **Do NOT** route the sandbox through the real command processing pipeline (SubmitCommandHandler → CommandRouter → AggregateActor) — this would persist events. The sandbox must bypass the persistence layer entirely and call the domain service directly.
- **Do NOT** call `ICommandStatusStore` or `ICommandArchiveStore` from the sandbox endpoint — the sandbox is stateless and ephemeral, it must not write to any store. The only external call is to the domain service via `IDomainServiceInvoker`.
- **Do NOT** use `<FluentTooltip>` for field values in tables — use native `title` attribute (performance, consistent with all Epic 20 components)
- **Do NOT** add a NavMenu entry for sandbox — it's accessed via StreamDetail page only
- **Do NOT** simulate the domain service — the sandbox MUST call the real domain service's Handle method via DAPR to ensure functional equivalence
- **Do NOT** add new `AdminServerOptions` properties — the sandbox uses the default `ServiceInvocationTimeoutSeconds` (30s) which is already configured and validated
- **Do NOT** use GET for the sandbox endpoint — use POST because the request body contains a potentially large JSON payload

### CurrentState Serialization Constraint

The sandbox passes a `JsonObject` (from `ReconstructState`) as the `currentState` parameter to `IDomainServiceInvoker.InvokeAsync`. In normal operation, the actor passes a typed domain object. Both serialize to JSON over the DAPR wire — but the domain service deserializes `CurrentState` on the other end. For well-behaved domain services that accept JSON-deserialized state (like the Counter sample), this works correctly. Domain services with custom state deserialization logic may produce different results in sandbox vs real execution. This is a known constraint — document it in the sandbox info banner if needed, but do not block on it.

Additionally, `DomainResult` does not support mixed event types — a result is either all state-change events (`IsSuccess`) or all rejection events (`IsRejection`). The sandbox can rely on this invariant without defensive mixed-type checks.

### State at Sequence 0

When `AtSequence == 0`, the sandbox tests the command against empty initial state (before any events). This is valid and useful for testing aggregate creation commands. The state is `{}` (empty JSON object). No stream lookup is needed. The resulting `StateChanges` will show all fields as "added" (empty → new values).

### Concurrent Write Staleness

Sandbox results reflect state at the moment of reconstruction. Concurrent writes are not blocked — between state reconstruction and domain service invocation, a real command could modify the aggregate. For `AtSequence = null` (latest), this means the sandbox may test against a state that is already stale by the time the result is displayed. This is inherent to all point-in-time diagnostic tools (blame, bisect, step-through share the same property) and is not a defect.

### Sandbox vs Replay

The sandbox and replay features are distinct:
- **Sandbox** (this story): Executes a command against ANY historical state, does NOT persist events, returns what WOULD happen. Diagnostic/testing tool.
- **Replay** (existing `ReplayController`): Re-submits a previously failed command through the FULL pipeline, DOES persist events. Operational recovery tool.

The sandbox can be used as a "preview before replay" — test a command in the sandbox first, then replay for real if the results look correct. But the sandbox itself never modifies state.

### Previous Story Intelligence

**From story 20-1 (Blame View — done):**
- `BlameViewer.razor` component pattern — sandbox follows same component parameter structure
- `?blame` deep link — `?sandbox` follows same pattern
- 30-second timeout for API calls — sandbox uses same timeout
- `ToString()` redaction for SEC-5 — follow same pattern
- Null-coalescing in record constructors — follow same pattern
- `FluentDataGrid` with search filter — sandbox reuses same pattern for state changes table

**From story 20-2 (Bisect Tool — done):**
- `BisectTool.razor` with three-phase UI (setup/loading/result) — sandbox uses similar two-phase UI (input form always visible, result appended below)
- `?bisect` deep link with mutual exclusion — extend to five-way: `sandbox` > `step` > `bisect` > `blame` > `diff`
- `IAsyncDisposable` for canceling in-flight calls — sandbox follows same pattern
- `FluentNumberField` for sequence input — sandbox reuses for target sequence
- POST body pattern — sandbox introduces POST to the admin API, bisect uses GET with query params

**From story 20-3 (Step-Through Event Debugger — ready-for-dev):**
- `EventDebugger.razor` with VCR controls — sandbox is simpler (form + result, no stepping)
- `?step` deep link — `?sandbox` follows same pattern
- Mutual exclusion precedence: `step` > `bisect` > `blame` > `diff` — sandbox adds itself at highest precedence

**From story 20-1/20-2 review findings (deferred work):**
- D1: CommandApi controller uses `[AllowAnonymous]` — sandbox endpoint follows same pattern (DAPR trust boundary)
- D3: `ConfigureAwait(false)` in Blazor components — sandbox follows same project-wide pattern
- D4: DeepMerge doesn't handle field deletion — sandbox state reconstruction inherits same limitation

### Git Intelligence

Recent commits show Epic 20 stories 20-1 (Blame View) and 20-2 (Bisect Tool) completed successfully. Story 20-3 (Step-Through Event Debugger) is ready-for-dev. All follow the identical architecture: models in Admin.Abstractions, service extension in `IStreamQueryService`, DAPR delegation in `DaprStreamQueryService`, CommandApi computation endpoint in `AdminStreamQueryController`, `AdminStreamsController` facade, `AdminStreamApiClient`, Blazor component, `StreamDetail` integration with deep linking. Story 20-4 follows this exact pattern with one addition: domain service invocation.

### Project Structure Notes

- Alignment with unified project structure confirmed — all paths follow existing `src/Hexalith.EventStore.Admin.*` conventions
- No new projects needed — sandbox fits entirely within existing Admin.Abstractions, Admin.Server, CommandApi, and Admin.UI projects
- Test projects already exist for all affected projects
- No new NuGet dependencies needed — all UI components (FluentUI) and patterns already available
- The CommandApi project already references the Server project (for actor hosting), so it has access to domain service invocation infrastructure

### Performance Considerations

Each sandbox execution requires:
1. State reconstruction at target sequence — O(snapshot_interval) with snapshots
2. Domain service invocation — depends on domain service complexity, typically <1s
3. JSON deep merge for resulting state — O(event_count * field_count) where event_count is the number of produced events (typically 1-5)
4. JSON diff for state changes — O(field_count)

The 30-second timeout provides adequate headroom. State reconstruction is the same performance profile as blame/step-through endpoints.

**Memory safety:** Dispose intermediate `JsonDocument` objects after extracting values. The sandbox holds at most two state copies (input state and resulting state) plus the produced events — bounded memory.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 20] Epic definition — "command sandbox" listed as story 20-4
- [Source: _bmad-output/planning-artifacts/prd.md#FR70] Point-in-time state exploration — sandbox extends this with "what would happen if I sent a command at this point"
- [Source: _bmad-output/planning-artifacts/prd.md#Journey 9] MCP event replay sandbox — "replayed in sandbox" confirms concept
- [Source: _bmad-output/planning-artifacts/prd.md#Command API] POST /api/v1/commands — existing command submission pattern
- [Source: _bmad-output/planning-artifacts/prd.md#Command API] POST /api/v1/commands/replay/{correlationId} — existing replay pattern (sandbox is the dry-run complement)
- [Source: _bmad-output/planning-artifacts/architecture.md#ADR-P4] Admin Three-Interface Architecture — single Admin.Server hosts API + Blazor UI
- [Source: _bmad-output/planning-artifacts/architecture.md#D7] Domain Service Invocation — DAPR service invocation pattern
- [Source: _bmad-output/planning-artifacts/architecture.md#SEC-5] Event payload data never appears in logs
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md#UX-DR42] Deep linking — every view has unique shareable URL
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs] Service interface to extend
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs] Existing model to reuse for state changes
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs] DAPR implementation to extend — trace URL construction for CommandApi
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs] Controller to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs] Options (no changes needed — uses existing ServiceInvocationTimeoutSeconds)
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs] API client to extend (first POST method)
- [Source: src/Hexalith.EventStore.Admin.UI/Components/BlameViewer.razor] Component pattern reference
- [Source: src/Hexalith.EventStore.Admin.UI/Components/BisectTool.razor] Component pattern reference
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor] Page to integrate sandbox into — follow existing deep link and mutual exclusion patterns
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/AdminStreamQueryController.cs] Controller for computation endpoint — add sandbox POST here
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandsController.cs] Existing command submission — reference for CommandEnvelope construction
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/ReplayController.cs] Existing replay — reference for command re-packaging
- [Source: src/Hexalith.EventStore.CommandApi/Controllers/CommandValidationController.cs] Pre-flight validation — reference for dry-run concept
- [Source: src/Hexalith.EventStore.Server/Commands/CommandRouter.cs] Command routing — traces to actor invocation
- [Source: src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs] Command handler — shows full processing pipeline (sandbox bypasses persistence)
- [Source: src/Hexalith.EventStore.Server/Commands/SubmitCommandExtensions.cs] Command envelope construction — reuse for building sandbox invocation request
- [Source: src/Hexalith.EventStore.Contracts/Commands/CommandEnvelope.cs] Command envelope DTO — Payload is byte[], has throwing constructor
- [Source: src/Hexalith.EventStore.Contracts/Commands/DomainServiceRequest.cs] Wire DTO sent to domain service: { Command, CurrentState }
- [Source: src/Hexalith.EventStore.Contracts/Results/DomainResult.cs] Parsed result: IsSuccess, IsRejection, IsNoOp, Events
- [Source: src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs] Wire response with IsRejection flag
- [Source: src/Hexalith.EventStore.Contracts/Events/ISerializedEventPayload.cs] Interface: EventTypeName + PayloadBytes for event extraction
- [Source: src/Hexalith.EventStore.Contracts/Events/IRejectionEvent.cs] Marker interface for rejection detection
- [Source: src/Hexalith.EventStore.Server/DomainServices/IDomainServiceInvoker.cs] Inject into AdminStreamQueryController for domain service invocation
- [Source: src/Hexalith.EventStore.Server/DomainServices/IDomainServiceResolver.cs] Resolves tenant+domain → DAPR app ID (used by invoker)
- [Source: src/Hexalith.EventStore.Server/DomainServices/DomainServiceNotFoundException.cs] Exception for "no domain service registered"
- [Source: src/Hexalith.EventStore.Server/DomainServices/DomainServiceOptions.cs] MaxEventsPerResult, MaxEventSizeBytes limits
- [Source: _bmad-output/implementation-artifacts/20-1-blame-view-per-field-provenance.md] Predecessor story — patterns and learnings
- [Source: _bmad-output/implementation-artifacts/20-2-bisect-tool-binary-search-state-divergence.md] Predecessor story — patterns, mutual exclusion, and callback wiring
- [Source: _bmad-output/implementation-artifacts/20-3-step-through-event-debugger.md] Predecessor story — patterns and mutual exclusion precedence

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6 (1M context)

### Debug Log References

- Build error: `Ulid` type not available in CommandApi project — resolved by using `Guid.NewGuid()` instead for MessageId generation
- Build error: CA1062 null check on `request` parameter — resolved by adding `ArgumentNullException.ThrowIfNull(request)`
- Pre-existing IntegrationTests CS0433 `Program` type collision — not related to sandbox changes

### Completion Notes List

- Created 3 new model records (SandboxCommandRequest, SandboxEvent, SandboxResult) following existing null-coalescing and SEC-5 redaction patterns
- Added POST sandbox computation endpoint to AdminStreamQueryController with full domain service invocation via IDomainServiceInvoker — handles accepted, rejected, no-op, and error outcomes
- Extended IStreamQueryService with SandboxCommandAsync, implemented in DaprStreamQueryService with new POST-body InvokeCommandApiAsync overload for JWT forwarding
- Added POST facade endpoint to AdminStreamsController with ReadOnly authorization policy
- Added first POST method to AdminStreamApiClient using PostAsJsonAsync
- Created CommandSandbox.razor component with input form, JSON validation, result display (outcome banners, events table, state changes table, resulting state viewer), event payload dialog, and IAsyncDisposable
- Integrated sandbox into StreamDetail.razor with highest precedence in mutual exclusion chain (sandbox > step > bisect > blame > diff), deep linking via ?sandbox query parameter
- All 1,923 Tier 1 tests pass with zero failures and zero regressions

### File List

**New files:**
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxCommandRequest.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxEvent.cs
- src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/SandboxResult.cs
- src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/SandboxCommandRequestTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/SandboxEventTests.cs
- tests/Hexalith.EventStore.Admin.Abstractions.Tests/Models/Streams/SandboxResultTests.cs
- tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerSandboxTests.cs
- tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceSandboxTests.cs
- tests/Hexalith.EventStore.Admin.UI.Tests/Components/CommandSandboxTests.cs

**Modified files:**
- src/Hexalith.EventStore.CommandApi/Controllers/AdminStreamQueryController.cs
- src/Hexalith.EventStore.Admin.Abstractions/Services/IStreamQueryService.cs
- src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs
- src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs
- src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs
- src/Hexalith.EventStore.Admin.UI/Pages/StreamDetail.razor

### Change Log

- 2026-03-27: Implemented command sandbox test harness (story 20-4) — 3 models, POST endpoint with domain service invocation, service delegation, facade endpoint, API client, Blazor component, StreamDetail integration, deep linking, 10 new test files. All 1,923 Tier 1 tests pass.
