# Story: admin-ui-state-inspection-cluster-fix

Status: ready-for-dev

Context created: 2026-05-05
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-05-admin-ui-state-inspection-cluster.md`
Review posture: ready-for-dev with QA conditions documented below.

## Story

As an EventStore administrator,
I want aggregate state, state diffs, and causation inspection to work from the Admin UI stream detail page,
so that I can diagnose stream behavior from the UI without silent empty panels, broken dialogs, or misleading timeout messages.

## Acceptance Criteria

1. EventStore exposes the missing aggregate state endpoint used by Admin Server.
   - Given a stream exists for `{tenantId}/{domain}/{aggregateId}`
   - When `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/state?at={sequence}` is invoked on the EventStore service
   - Then it returns `200 OK` with `AggregateStateSnapshot(TenantId, Domain, AggregateId, SequenceNumber, Timestamp, StateJson)`.
   - And `at == 0` returns the initial empty state snapshot.
   - And `at < 0` returns `400 Bad Request`.
   - And an empty or missing stream returns `404 Not Found`.
   - And all non-200 responses use typed HTTP status codes plus RFC 7807 ProblemDetails; the UI must not need to parse `detail` strings to classify the failure.
   - And reconstruction reuses the existing `ReconstructState` JSON-merge behavior in `AdminStreamQueryController`; do not create a second state-replay algorithm.

2. EventStore exposes the missing aggregate state diff endpoint used by Admin Server and the Admin UI diff page.
   - Given a stream exists and `0 <= from < to`
   - When `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/diff?from={from}&to={to}` is invoked on the EventStore service
   - Then it reconstructs both positions, calls the existing `JsonDiff` helper, and returns `200 OK` with `AggregateStateDiff(from, to, changedFields)`.
   - And unchanged states return `200 OK` with an empty `ChangedFields` list.
   - And invalid ranges (`from < 0`, `to <= from`) return `400 Bad Request`.
   - And an empty or missing stream returns `404 Not Found`.
   - And all non-200 responses use typed HTTP status codes plus RFC 7807 ProblemDetails; the UI must not need to parse `detail` strings to distinguish invalid range, missing stream, or backend failure.

3. EventStore exposes the missing causation endpoint used by Admin Server and the Event Detail panel.
   - Given a stream exists and `at` points at an event in that stream
   - When `GET /api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/causation?at={sequence}` is invoked on the EventStore service
   - Then it returns a `CausationChain` built from the target event and related `CorrelationId` / `CausationId` metadata available in the stream.
   - And the chain includes target event metadata so the UI can show what event the chain starts from.
   - And blank/null `CausationId` values do not fabricate links.
   - And the required output is the target event plus direct causation lineage proven from IDs; same-correlation grouping is optional and must not block AC completion.
   - And same-correlation events must not be presented as direct causation unless `CausationId` / message id linkage proves it.
   - And invalid sequence values return `400 Bad Request`.
   - And a missing stream or missing target event returns `404 Not Found`.
   - And all non-200 responses use typed HTTP status codes plus RFC 7807 ProblemDetails; the UI must not need to parse `detail` strings to distinguish invalid request from missing causation data.
   - And the implementation uses the existing `CausationChain` / `CausationNode` DTO contract from `Hexalith.EventStore.Admin.Abstractions.Models.Streams`.

4. Admin Server keeps its existing stream facade contract intact.
   - Given the Admin UI calls Admin Server with `state?sequenceNumber=...`, `diff?fromSequence=...&toSequence=...`, and `causation?sequenceNumber=...`
   - When Admin Server proxies to EventStore through `DaprStreamQueryService`
   - Then the EventStore-side calls use `state?at=...`, `diff?from=...&to=...`, and `causation?at=...` exactly as already implemented.
   - And Admin Server maps upstream `400` to client-visible bad request behavior instead of generic `503` where controller/service patterns already support semantic exceptions.
   - And Admin Server preserves typed `401`, `403`, `404`, timeout, and backend-unavailable semantics so the UI can render distinct states without string matching.
   - And existing `AdminStreamsController` authorization and `AdminTenantAuthorizationFilter` behavior is preserved.

5. The Event Detail panel distinguishes real no-state from error states.
   - Given the selected event state request returns a legitimate null/no-state result
   - Then the panel may show the current "State reconstruction not available at this position." message.
   - Given authentication, authorization, backend availability, cancellation, or unexpected failures occur
   - Then `EventDetailPanel.razor` does not swallow them through a bare `catch`.
   - And it shows a categorized, retryable, user-facing message for sign-in/permission/backend/unexpected failures.
   - And unexpected failures are logged through an injected `ILogger<EventDetailPanel>` without logging event payload data.
   - And event metadata and payload remain visible when only the state-inspection request fails.
   - And when an operator without backend knowledge sees an inspection failure, the UI makes the next action clear: retry, sign in, change range, or report backend/backend-unavailable failure.

6. The Inspect State dialog renders correctly under Fluent UI Blazor v5.
   - Given the user clicks Inspect State from a stream detail event
   - Then the dialog shows the title row, close button, mode switch, sequence/timestamp input, step buttons, Inspect button, loading state, and result/error region without clipped content.
   - And the dialog keeps stream identity visible (`tenantId`, `domain`, `aggregateId`, and selected sequence/timestamp context) across initial, loading, success, empty, and failed inspection states.
   - And `StateInspectorModal.razor` no longer nests `TitleTemplate` or `ChildContent` inside `FluentDialogBody`.
   - And sizing is applied through the v5-compatible local pattern used by working dialogs in this repo, either direct flat children under `FluentDialogBody` plus CSS wrapper, or the existing `IDialogService` pattern if implementation confirms that is the better v5 fit.
   - And timestamp mode still keeps the existing three-page timeline cap behavior and cap-exceeded copy.

7. The Diff with Previous page stops misclassifying upstream failure as "range too large".
   - Given a tiny stream with adjacent sequences
   - When the user opens Diff with Previous
   - Then the UI does not display "Diff range too large" because the missing upstream endpoint or server-side exception delayed the response.
   - And the client timeout is raised from 5 seconds to a value aligned with the admin service invocation path (minimum 15 seconds unless implementation chooses the configured service invocation timeout).
   - And `OperationCanceledException` from a true client timeout shows "Diff timed out - try a smaller range."
   - And `HttpRequestException`, backend failures, and missing-state results have distinct messages that do not imply the user chose an invalid range.
   - And unchanged states use explicit empty-diff copy such as "No state changes detected for this event" instead of an error-style empty state.

8. Regression tests cover the server endpoints, Admin Server proxy, UI dialog, panel, and diff behavior.
   - EventStore controller/API contract tests cover `/state`, `/diff`, and `/causation` route existence, query names, DTO body shape, happy path, bad request path, and not-found/empty-stream path.
   - Admin Server tests cover external authorization, tenant filter behavior, proxy route/query mapping, and semantic 400/401/403/404/timeout/unavailable preservation where applicable.
   - Causation tests cover traversal boundaries: target event present, missing target event, missing causation id, and a linked correlation/causation chain.
   - Admin UI bUnit tests assert the corrected `StateInspectorModal` DOM structure, no clipped-template-only rendering, open/load/switch/close behavior, and stable state across mode switches.
   - Admin UI tests cover `EventDetailPanel` categorized state-load errors and `StateDiffViewer` timeout/server/missing-state messages.
   - Tests assert end-state/payload behavior, not only mock invocation counts, per the repo's R2-A6 testing lesson.

9. Live verification proves the actual Admin UI workflow.
   - With Aspire running and `eventstore`, `eventstore-admin`, and `eventstore-admin-ui` healthy, open a stream such as `/streams/system/tenants/acme-corp`.
   - Click at least two events and verify State After This Event shows JSON state when state exists.
   - Open Inspect State and verify sequence stepping and timestamp lookup render and fetch successfully.
   - Open Diff with Previous on adjacent events and verify changed fields or a truthful no-diff/no-state message.
   - Click Trace Causation and verify the response renders or returns a truthful not-found message.
   - Record a manual Aspire smoke result in the Dev Agent Record before moving the story to review.

## Tasks / Subtasks

- [ ] ST1 - Add EventStore `/state` endpoint in `AdminStreamQueryController`. (AC: 1, 4)
  - [ ] Add `[HttpGet("{tenantId}/{domain}/{aggregateId}/state")]`.
  - [ ] Accept `[FromQuery] long at`; do not use Admin Server's `sequenceNumber` query name on the EventStore route.
  - [ ] Reuse `AggregateIdentity`, `IActorProxyFactory`, `IAggregateActor.GetEventsAsync(0)`, and `ReconstructState`.
  - [ ] Set the snapshot timestamp to the event timestamp at `at` when `at > 0`; use a deterministic empty timestamp for `at == 0`.
  - [ ] Return RFC 7807 `ProblemDetails` for 400/404, matching the controller's existing style.

- [ ] ST2 - Add EventStore `/diff` endpoint in `AdminStreamQueryController`. (AC: 2, 4)
  - [ ] Add `[HttpGet("{tenantId}/{domain}/{aggregateId}/diff")]`.
  - [ ] Accept `[FromQuery] long from`, `[FromQuery] long to`.
  - [ ] Validate `0 <= from < to` before actor work.
  - [ ] Reconstruct both states with existing helpers and map `JsonDiff` output to `FieldChange` records.
  - [ ] Preserve empty-change success instead of treating it as not found.

- [ ] ST3 - Add EventStore `/causation` endpoint in `AdminStreamQueryController`. (AC: 3, 4)
  - [ ] Add `[HttpGet("{tenantId}/{domain}/{aggregateId}/causation")]`.
  - [ ] Accept `[FromQuery] long at`; validate `at >= 1`.
  - [ ] Build `CausationChain` from stream metadata without logging payload content.
  - [ ] Confirm `CausationChain` constructor semantics before coding; do not invent a new DTO.
  - [ ] Cover causation traversal limits so a missing or blank causation id does not fabricate links.
  - [ ] Include target event metadata in the chain response and distinguish related same-correlation events from direct causation links.

- [ ] ST4 - Preserve or refine Admin Server semantic behavior. (AC: 4)
  - [ ] Review `DaprStreamQueryService` `GetAggregateStateAtPositionAsync`, `DiffAggregateStateAsync`, and `TraceCausationChainAsync` after ST1-ST3 exist.
  - [ ] Keep existing EventStore query names (`at`, `from`, `to`) and Admin Server facade query names (`sequenceNumber`, `fromSequence`, `toSequence`) distinct.
  - [ ] If upstream 400/404 currently becomes a misleading 503 in `AdminStreamsController.IsServiceUnavailable`, add tight method-level mappings similar to the recently fixed event-detail path. Do not broaden unrelated controllers.
  - [ ] Preserve typed failures for UI classification; do not make the UI parse ProblemDetails `Detail` text.
  - [ ] Do not add custom retries; architecture rule 4 leaves retries to DAPR resiliency.

- [ ] ST5 - Replace bare state-load catch in `EventDetailPanel.razor`. (AC: 5)
  - [ ] Inject `ILogger<EventDetailPanel>`.
  - [ ] Add a separate `_stateError` or equivalent display state so error messages are not collapsed into `_stateSnapshot = null`.
  - [ ] Handle `UnauthorizedAccessException`, `ForbiddenAccessException`, `ServiceUnavailableException`, `OperationCanceledException`, and unexpected `Exception` separately.
  - [ ] Keep event detail metadata/payload visible when only the state snapshot request fails.
  - [ ] Preserve event detail loading, payload rendering, copy-correlation callback, Inspect State callback, Diff callback, and Trace Causation behavior.

- [ ] ST6 - Repair `StateInspectorModal.razor` for Fluent UI Blazor v5. (AC: 6)
  - [ ] Flatten dialog markup so title and body controls are direct content under `FluentDialogBody`, or migrate to the existing dialog-service pattern after verifying the local v5 API.
  - [ ] Move sizing/scrolling to an inner wrapper class or v5 dialog parameters; do not rely on `Style` on `FluentDialog` if it does not affect the actual wrapper.
  - [ ] Keep tenant/domain/aggregate/selected sequence context visible in the dialog so the operator never loses place while switching modes or retrying.
  - [ ] Preserve `OnAfterRenderAsync` show lifecycle and `OnDialogStateChangeAsync` close callback unless switching to `IDialogService` makes those obsolete.
  - [ ] Keep timestamp validation outside the try block so invalid timestamps do not set `_fetched = true`.

- [ ] ST7 - Fix `StateDiffViewer.razor` timeout and message branches. (AC: 7)
  - [ ] Replace the hardcoded 5-second CTS with at least 15 seconds or a shared configured value.
  - [ ] Do not treat a timeout increase as the fix by itself; the EventStore `/diff` endpoint and typed error classification remain required.
  - [ ] Keep parallel diff/from/to state calls, but avoid one failure masking all outcomes as a range problem.
  - [ ] Make timeout, HTTP/backend failure, missing-state, and no-diff messages distinct.
  - [ ] Use non-error empty-diff copy for unchanged adjacent states.
  - [ ] Preserve the `FromSequence == 0` initial-state fallback and `MaxDisplayChanges` behavior.

- [ ] ST8 - Add targeted tests. (AC: 8)
  - [ ] Add/extend EventStore tests in `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTests*.cs`.
  - [ ] Add/extend Admin Server tests in `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests*.cs` and `Controllers/AdminStreamsControllerTests*.cs`.
  - [ ] Add/extend Admin UI tests in `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs`, `StateDiffViewerTests.cs`, and page/panel coverage near `StreamDetailPageTests.cs`.
  - [ ] Add at least one API-client classification test for state/diff/causation failures so the UI is not forced into string matching.
  - [ ] Prefer existing test seams (`TestHttpMessageHandler`, bUnit, Shouldly, NSubstitute) over new infrastructure.

- [ ] ST9 - Verify and record evidence. (AC: 9)
  - [ ] Run `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release`.
  - [ ] Run `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release`.
  - [ ] Run targeted EventStore controller tests; if `Hexalith.EventStore.Server.Tests` as a whole is blocked by known baseline warnings/failures, record the exact targeted command, result, and baseline blocker.
  - [ ] Run `dotnet build Hexalith.EventStore.slnx --configuration Release`.
  - [ ] With Aspire running, perform the live stream detail smoke and capture the resource state plus UI/API evidence in Dev Agent Record. **Pending operator follow-up — not executable from the dev agent's headless environment; ready for the user to run through the Admin UI.**

## QA Conditions

- Treat this story as ready-for-dev only if implementation commits keep the scope to Admin UI state inspection and do not mix in the tenant-management debug cluster.
- Before moving to `review`, the dev agent must demonstrate:
  - Required: API contract tests for `/state`, `/diff`, and `/causation`.
  - Required: UI/component regression coverage for `StateInspectorModal`.
  - Required: negative-path coverage for `EventDetailPanel` state-inspection failure.
  - Required: error-classification coverage for `StateDiffViewer`.
  - Required: manual Aspire smoke through the Admin UI against a multi-event stream with state, diff, and causation checks.
  - Required: browser or screenshot evidence that the `StateInspectorModal` body is not clipped after the Fluent UI v5 repair; bUnit markup checks alone are not sufficient for this visual regression.
  - Nice-to-have: broader browser automation or screenshot automation beyond one visual proof, but do not build new infrastructure if the required evidence is already captured.
- Residual risk is moderate until the manual Aspire smoke is recorded because this feature crosses EventStore endpoints, Admin Server proxy behavior, API client classification, and Blazor component rendering.

## Dev Notes

### Current Baseline

- This story is backlog-to-ready context only. It should produce a new implementation pass and must not mix into the existing tenant-management debug cluster unless the user explicitly asks.
- This story artifact was restored on `main` to repair sprint status/artifact drift after the sprint-status row existed without its matching story file.

### Defect Chain

The user-visible cluster has two root causes:

1. Admin Server and Admin UI already call state/diff/causation surfaces, but the EventStore controller does not yet expose `state`, `diff`, or `causation` routes. The Admin Server facade is therefore proxying to missing upstream endpoints.
2. `StateInspectorModal.razor` retained a pre-v5/nested dialog structure: `TitleTemplate` and `ChildContent` are nested inside `FluentDialogBody`, and sizing is applied on `FluentDialog` where it does not produce the intended visible body.

There are two compounding UI defects:

- `EventDetailPanel.razor` catches every state-load exception and sets `_stateSnapshot = null`, making auth, forbidden, unavailable, timeout, and endpoint-missing failures indistinguishable from true no-state.
- `StateDiffViewer.razor` uses a 5-second local timeout for three parallel calls and maps `OperationCanceledException` to "Diff range too large", even when the real cause is a missing/slow upstream endpoint.

### Diagnostic Contract Rules

- The diagnostic endpoints are read-only operational contracts, not domain/write-side models.
- DTOs should remain boring and version-tolerant: use existing Admin Abstractions stream DTOs, keep response shapes stable, and avoid exposing storage implementation details.
- The UI must classify failures from status/exception type, not text parsing. Backend `ProblemDetails.Detail` is human copy, not a machine contract.
- Bad diagnostic output is worse than no output. When state/diff/causation cannot be computed truthfully, return a typed failure or an explicit empty/no-data result rather than fabricated state.
- Diagnostic truth beats diagnostic completeness. If the system cannot reconstruct state, diff, or causation confidently, it must say why rather than return guessed state.

### ADR-Lite Decisions

- EventStore owns diagnostic computation for state, diff, and causation because it has direct actor/event access and already hosts the stream computation helpers.
- Admin Server owns external authorization, tenant filtering, DAPR proxying, and UI-facing error semantics.
- Admin UI owns rendering distinct inspection states without parsing human ProblemDetails text.
- Diagnostic endpoints are read-only and truth-preserving. They may reveal authorized state for inspection, but they must not mutate event streams, projection state, or command status.
- If implementation discovers existing helper abstractions already cover state/diff/causation, extend those helpers rather than adding parallel controller-local helper paths.

### Failure Semantics Matrix

| Failure | Server Contract | UI Copy Class |
|---------|-----------------|---------------|
| Invalid sequence or range | `400` ProblemDetails | Invalid request |
| Missing stream or target event | `404` ProblemDetails | Not found / no data |
| Valid diff with no changes | `200` with empty `ChangedFields` | No changes |
| Authentication or authorization denied | `401` / `403` from Admin Server | Sign in / access denied |
| True timeout or cancellation | Timeout/cancellation exception or typed timeout response | Timed out, retry |
| Backend unavailable | `503` ProblemDetails | Backend unavailable |

### Auth Ownership

- `AdminStreamQueryController` in the EventStore service is currently an internal diagnostic computation controller and is marked `[AllowAnonymous]`; do not force external auth tests onto that layer unless the implementation changes the controller contract.
- `AdminStreamsController` in Admin Server owns external read authorization and tenant filtering for the Admin UI surface. Authorization, forbidden, and tenant-scope tests belong primarily at the Admin Server facade.
- EventStore endpoint tests should still prove route/query/DTO/400/404 behavior so the DAPR proxy has a stable computation target.

### Files To Preserve Or Update

`src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`

- Current state: contains routes for `bisect`, `timeline`, `events/{sequenceNumber:long}`, `blame`, `step`, and `sandbox`; missing `state`, `diff`, `causation`.
- Existing helpers to reuse: `ReconstructState`, `JsonDiff`, `DeepMerge`, `FlattenJson`, `ExtractFieldValues`, `ComputeBlame`.
- Preserve: `AggregateIdentity` actor id construction, `IAggregateActor.GetEventsAsync(0)` pattern, `OperationCanceledException` rethrow, controller-level logging without event payload data, and ProblemDetails style.
- Do not parse aggregate ids as GUIDs. URL aggregate ids are opaque/ULID-compatible strings.

`src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`

- Current state: already proxies to EventStore paths:
  - `state?at={sequenceNumber}`
  - `diff?from={fromSequence}&to={toSequence}`
  - `causation?at={sequenceNumber}`
- Preserve: JWT forwarding through `_authContext.GetToken()`, `DaprClient.CreateInvokeMethodRequest`, `EnsureSuccessStatusCode`, `_options.ServiceInvocationTimeoutSeconds`, and existing 30/60-second special cases for expensive routes.
- Required only if tests reveal semantic 400/404 is misclassified: add narrow exception translation; do not redesign the service.

`src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`

- Current state: already exposes Admin Server facade routes:
  - `state?sequenceNumber=...`
  - `diff?fromSequence=...&toSequence=...`
  - `causation?sequenceNumber=...`
- Preserve: `AdminAuthorizationPolicies.ReadOnly`, `AdminTenantAuthorizationFilter`, `ResolveTenantScope`, and ProblemDetails `correlationId`.
- Watch-out: `IsServiceUnavailable` treats all `HttpRequestException` values as 503. The prior event-detail fix avoided misleading 503s for 400/404 in one path. Apply the same local pattern here if endpoint tests prove this cluster needs it.

`src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs`

- Current state: UI facade calls Admin Server with `sequenceNumber`, `fromSequence`, `toSequence`; this is correct for the Admin Server controller and must not be changed to EventStore's `at/from/to` names.
- Current state-load and diff methods return `null` for generic exceptions but rethrow auth/forbidden/service-unavailable/invalid-operation/cancellation.
- Preserve: `EnsureSuccessAsync` mapping to `UnauthorizedAccessException`, `ForbiddenAccessException`, and `ServiceUnavailableException`.
- If adding richer UI behavior, prefer surfacing exceptions from API client to components rather than swallowing them earlier.

`src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor`

- Current state: event detail loading has an `_error` path; state loading has only `_stateSnapshot` and `_stateLoading`.
- Required change: introduce explicit state error display. The state panel should not reuse the general event-detail `_error` unless doing so keeps rendering simple and scoped.
- Preserve: `OnInspectState`, `OnDiffRequested`, `TraceCausation`, `CausationChainView`, and copy-correlation behavior.

`src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor`

- Current state: shows a `FluentDialog` manually via `@ref` and `OnAfterRenderAsync`, but nests `TitleTemplate` and `ChildContent` inside `FluentDialogBody`.
- Compare working repo patterns before editing:
  - `src/Hexalith.EventStore.Admin.UI/Components/EventDebugger.razor`
  - `src/Hexalith.EventStore.Admin.UI/Components/CommandSandbox.razor`
  - `src/Hexalith.EventStore.Admin.UI/Pages/Tenants.razor`
- Preserve: sequence stepping, timestamp lookup, cap-exceeded copy, close callback, and invalid timestamp behavior.

`src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor`

- Current state: launches diff/from/to state calls in parallel under a 5-second CTS, then maps any `OperationCanceledException` to range-too-large.
- Required change: distinct UI outcomes for timeout, upstream/server failure, missing state, and legitimate no-diff.
- Preserve: `FromSequence == 0` initial state fallback, `MaxDisplayChanges`, full-state accordion, and "Show all" behavior.

### Testing Guidance

- Admin UI tests use bUnit 2.7.2, Shouldly, and NSubstitute. Extend existing component tests rather than adding browser-only coverage.
- Admin Server tests already have stream controller/service fixtures; use the existing HTTP/DAPR seams.
- EventStore Server tests have a known repo caveat around `Hexalith.EventStore.Server.Tests` build warnings treated as errors. Do not hide a real regression, but record the baseline if the whole project cannot run.
- For endpoint tests, assert DTO bodies and route/query behavior. Mock-count-only tests are not sufficient.
- For EventStore endpoint contract tests, include route existence, query names, typed 400/404 handling, empty-stream behavior, and DTO body shape.
- For Admin Server tests, include external 401/403 authorization behavior, tenant filtering, DAPR proxy route/query mapping, and typed propagation of invalid request/not found/timeout/unavailable states.
- For causation tests, include at least a chain with matching correlation/causation values and a missing target event.
- For UI error tests, prefer typed exceptions from `AdminStreamApiClient` over asserting exact internal exception text.
- For modal tests, assert interactive render behavior across open, load, mode switch, failed load, and close. A static lifecycle-only render test is not enough for this Fluent UI v5 regression.
- For visual layout risk, capture a browser/screenshot smoke after implementation because bUnit cannot prove the Fluent dialog body is not clipped in the actual browser.

### Architecture And UX Guardrails

- Follow architecture rule 4: no custom retry loops; DAPR resiliency owns retries.
- Follow architecture rule 5 / SEC-5: never log event payload data. Returning payload/state JSON in admin responses is allowed for authorized inspection; logs must stay metadata-only.
- Follow architecture rule 7 and UX error rules: server-side HTTP errors should use RFC 7807 ProblemDetails.
- State inspection is an operational/admin surface, so precise diagnostic copy is acceptable. Avoid implying user fault for backend failures.
- Do not remove Inspect State. It is the free-form version of "State After This Event" and supports sequence stepping and timestamp lookup for incident forensics.

### External Technical Notes Checked

- Repo package pins: `Microsoft.FluentUI.AspNetCore.Components` is `5.0.0-rc.2-26098.1`; `Dapr.Client` is `1.17.7`; bUnit is `2.7.2`; xUnit v3 is `3.2.2`.
- NuGet currently lists `Microsoft.FluentUI.AspNetCore.Components` stable `4.14.0` and prerelease `5.0.0-rc.2-26098.1`; keep the repo's v5 RC package and do not downgrade as part of this story. Source: https://www.nuget.org/packages/Microsoft.FluentUI.AspNetCore.Components
- NuGet currently lists `Dapr.Client` `1.17.9` newer than the repo's `1.17.7`; do not upgrade DAPR packages as part of this story. Source: https://www.nuget.org/packages/Dapr.Client
- Fluent UI Blazor documentation/source is hosted from the Microsoft `fluentui-blazor` project and demo site; use local working v5 patterns first, then official docs/source only to confirm API details. Source: https://github.com/microsoft/fluentui-blazor

### Anti-Reinvention Guardrails

- Do not create a new state reconstruction service unless the existing private helpers become impossible to reuse cleanly. A small private helper extraction inside `AdminStreamQueryController` is acceptable if it removes duplication among `state`, `diff`, `step`, `blame`, and `bisect`.
- If existing helper abstractions already cover state/diff/causation, extend those rather than forking implementation paths with new similarly named helpers.
- Do not create duplicate DTOs for state, diff, field changes, or causation. Use `Hexalith.EventStore.Admin.Abstractions.Models.Streams`.
- Do not change Admin UI facade query names unless you also intentionally update `AdminStreamsController`; the UI talks to Admin Server, not directly to EventStore.
- Do not solve the diff problem by only changing the message. The upstream `/diff` endpoint must exist and return data.
- Do not solve the state panel by hiding all failures behind "not available"; this story exists specifically because that hid the real defect.
- Do not mix this with tenant-management debug cluster ST3/ST4 work.

### Maintainability Warning

- Future maintainers should be able to identify this story's intent from the failure semantics and tests. Prefer helper names and UI copy that say what they classify or compute.
- Avoid opaque helper names such as `HandleThingAsync`, `ProcessStateAsync`, or catch-all copy such as "Something went wrong" on this diagnostic surface.
- Generic fallback copy is acceptable only when typed classification is unavailable, and it should still tell the operator whether retrying is reasonable.

### Manual Smoke Evidence Checklist

Before moving the story to `review`, record the minimum live evidence in the Dev Agent Record:

- Admin UI URL visited.
- Stream identity: tenant id, domain, aggregate id.
- Selected sequence numbers.
- State result observed, or typed state no-data/error result observed.
- Diff result observed, including changed fields, no changes, or typed no-data/error result.
- Causation result observed, or typed not-found result observed.
- Inspect State modal evidence: screenshot or explicit browser observation that title, controls, body, loading/result/error region, and close action are visible and not clipped.
- Aspire resource state at the time of smoke.

## References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-05-admin-ui-state-inspection-cluster.md`
- `_bmad-output/planning-artifacts/epics.md` - Epic 15 and follow-up notes for admin stream/event detail surfaces.
- `_bmad-output/planning-artifacts/architecture.md` - rules 4, 5, 7, 14 and SEC-5; DAPR state/event-store architecture.
- `_bmad-output/planning-artifacts/ux-design-specification.md` - UX-DR44/45, admin dashboard state/diff requirements, error clarity principles.
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Services/AdminStreamApiClient.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateSnapshot.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/AggregateStateDiff.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/CausationChain.cs`
- `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/FieldChange.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateDiffViewerTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/StreamDetailPageTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Services/AdminStreamApiClientTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTests.cs`

## Dev Agent Record

### Agent Model Used

TBD by implementing dev agent.

### Debug Log References

- Story context restored on `main` after preflight reported `admin-ui-state-inspection-cluster-fix` as `ready-for-dev` with missing artifact.

### Completion Notes List

- Pending implementation.

### Verification Evidence Captured

- Pending implementation.

### File List

- `_bmad-output/implementation-artifacts/admin-ui-state-inspection-cluster-fix.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs`
- `src/Hexalith.EventStore.Admin.UI/Components/EventDetailPanel.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateInspectorModal.razor`
- `src/Hexalith.EventStore.Admin.UI/Components/StateDiffViewer.razor`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerStateDiffCausationTests.cs` (new)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Controllers/AdminStreamsControllerStateDiffCausationTests.cs` (new)
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceStateMappingTests.cs` (new)
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateInspectorModalTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Components/StateDiffViewerTests.cs`

### Change Log

| Date | Change | Notes |
|------|--------|-------|
| 2026-05-05 | Story context created | Backlog story moved to ready-for-dev with implementation guardrails and verification plan. |
| 2026-05-05 | Artifact restored on main | Repaired sprint status/artifact drift; implementation remains pending. |
