# Post-Epic Deferred DW3: Admin Debugging JSON and Large-Stream Hardening

Status: ready-for-dev

<!-- Source: sprint-change-proposal-2026-05-04-deferred-work-triage.md - Proposal D / DW3 -->
<!-- Source: deferred-work.md - Epic 20 JSON reconstruction, direct CommandApi bounds, and large-stream deferrals through 2026-05-04 -->
<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an EventStore maintainer responsible for Admin debugging reliability,
I want the JSON reconstruction and large-stream limits behind blame, bisect, step-through, sandbox, diff, and trace-map tools to be explicit, bounded, and tested,
so that developers can trust debugging output without hidden memory, correctness, or trust-boundary assumptions.

## Story Context

`deferred-work.md` has accumulated repeated Epic 20 review deferrals around `DeepMerge`, `JsonDiff`, nested removals, array treatment, `GetEventsAsync(0)`, direct CommandApi query parameters, and `[AllowAnonymous]` internal admin endpoints. The deferred-work triage proposal groups those items into DW3 because they form one system-level decision: Admin debugging currently reconstructs state from event payload JSON, not from domain `Apply` methods or a snapshot-aware actor API.

This story is a hardening and decision story. It should make the current behavior falsifiable, add missing bounds where they are local and low-risk, and document accepted degradation where the correct fix needs actor API or product architecture work. Do not absorb DW2 live-evidence work, DW4 evidence-template validation, DW5 Admin UI runtime polish, or DW6 deferred-work governance into this story.

Current HEAD at story creation: `28275e7a`.

## Acceptance Criteria

1. **JSON reconstruction semantics are documented as an architecture decision.** Given Admin debugging endpoints reconstruct state by JSON-merging event payloads, when DW3 closes, then a durable architecture note records what the reconstruction model can and cannot prove. The note must cover object merge, missing delete semantics, nested removal behavior, arrays as opaque values, malformed JSON handling, event payload privacy, and the difference between JSON-level reconstruction and domain `Apply`-method reconstruction.

2. **Deletion and nested-removal behavior is either fixed narrowly or explicitly accepted.** Given an event payload removes a property, sets a property to `null`, removes a nested property, or omits a previously present property, when blame, bisect, step-through, sandbox, and diff derive `FieldChange` or provenance output, then the behavior must be tested and consistent. If actual delete semantics cannot be inferred from the event payload model, record that as accepted degradation instead of inventing deletion behavior.

3. **Array treatment is bounded and visible.** Given state or event payloads contain arrays, when JSON diffing runs, then the implementation must either preserve current opaque-leaf behavior with tests and documentation or add a tested element-level diff only if it is narrow and does not change existing UI contracts. The story must not introduce a broad JSON Patch engine or complex array-move detection unless a product/architecture decision is recorded first.

4. **Recursion and malformed-path failure modes are guarded.** Given deeply nested JSON, malformed property names, empty field paths, or non-object JSON payloads are encountered, when reconstruction and diff helpers run, then they must not produce uncontrolled `StackOverflowException`, uncaught `ArgumentException`, or payload-adjacent diagnostic leaks. Add limits, validation, or accepted-debt records with exact triggers.

5. **Direct CommandApi max parameters are bounded.** Given DAPR-isolated CommandApi admin endpoints accept direct query parameters such as `maxEvents`, `maxFields`, `maxSteps`, and timeline `count`, when callers pass zero, negative, or extremely large values, then the endpoint response must enforce a server-side maximum or return a clear 400. Admin.Server configured caps must remain the normal facade defaults, and direct CommandApi access must not bypass memory or CPU protection.

6. **Large-stream behavior is explicit for every debugging surface.** Given an aggregate stream is larger than configured limits, when blame, bisect, step-through, sandbox, diff, timeline, or trace-map tools run, then each surface must have an explicit behavior: bounded scan, truncation flag/message, 400 with guidance, accepted degradation, or deferred actor API requirement. No endpoint may claim complete analysis after silently truncating or loading the full stream without a documented limit.

7. **`GetEventsAsync(0)` usage is dispositioned per endpoint.** Given current CommandApi admin debugging paths call `IAggregateActor.GetEventsAsync(0)` for full-stream reads, when DW3 closes, then each usage is classified as `patch-now`, `accepted-debt`, or `future actor API`. If a range or snapshot-aware actor API is required, define the proposed contract shape and migration path, but do not implement a broad actor API unless the change is small, tested, and does not disturb production command processing.

8. **Trace-map tail scanning remains honest.** Given `AdminTraceQueryController` scans up to `MaxEventScan` most recent events while looking for a correlation id, when the scan cap hides older events, then the returned `CorrelationTraceMap` must expose the cap and avoid implying complete correlation coverage. If the current `ScanCapped` condition is too narrow, fix it with focused tests.

9. **Internal admin trust boundary is recorded.** Given `AdminStreamQueryController` and `AdminTraceQueryController` use `[AllowAnonymous]` because they are intended to be reached only through DAPR service invocation from Admin.Server, when DW3 closes, then an architecture note records this trust boundary, why Admin.Server remains the authorized facade, what DAPR or network isolation is required, and what would trigger service-to-service auth work. Do not silently add public authentication changes to CommandApi in this story.

10. **Facade and MCP behavior stays non-regressive.** Given Admin.Server, Admin UI, CLI, and MCP tools depend on the existing stream/debugging response models, when DW3 adds bounds or clarifies degradation, then public response model shape and route names remain compatible unless a failing test proves a defect. Any new error responses must be documented for Admin UI, CLI, and MCP callers.

11. **Tests cover helper behavior and endpoint behavior.** Add or update the smallest focused tests. Expected areas include `AdminStreamQueryController` JSON merge/diff/reconstruction tests, `AdminTraceQueryController` scan-cap tests, `DaprStreamQueryService` timeout/error propagation tests only if facade behavior changes, and Admin UI/MCP tests only if visible behavior or response handling changes.

12. **Deferred-work dispositions are updated narrowly.** Given DW3 closes or routes relevant bullets in `_bmad-output/implementation-artifacts/deferred-work.md`, when the story moves to review, then each touched bullet must receive a disposition marker such as `STORY:post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening`, `RESOLVED`, `ACCEPTED-DEBT`, `DUPLICATE`, or `FUTURE-ACTOR-API`. Do not rewrite unrelated deferred-work sections.

13. **Scope boundaries stay intact.** DW3 must not implement DW2 runtime smoke evidence, DW4 evidence schema validation, broad Admin UI visual polish, new MCP tools, DAPR component changes, public query contract changes, nested submodule initialization, or a large event-stream storage redesign. Pressure to do those things must be recorded as deferred product or architecture work.

14. **Bookkeeping is closed.** At dev handoff, update this story's Dev Agent Record, File List, Change Log, Verification Status, and deferred-work dispositions. Move this story and its sprint-status row to `review` only after targeted tests and documentation updates are recorded. Move both to `done` only after code review signoff.

## Scope Boundaries

- Do not replace the event-store persistence model or actor state layout.
- Do not add a broad JSON Patch, JSON Schema, or domain-specific state reconstruction engine unless a recorded architecture decision approves it.
- Do not change Admin.Server facade authorization policies or public Admin API route names.
- Do not add new MCP, CLI, or Admin UI features except narrow error/degradation display required by bounded behavior changes.
- Do not change DAPR component YAML, access-control YAML, AppHost topology, or deployment overlays.
- Do not make CommandApi admin endpoints externally public; document the internal DAPR trust boundary instead.
- Do not edit generated preflight JSON audit files.

## Implementation Inventory

| Area | File / artifact | Expected use |
|---|---|---|
| Planning source | `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md` | Proposal D scope and acceptance direction |
| Deferred source | `_bmad-output/implementation-artifacts/deferred-work.md` | Raw Epic 20 JSON, large-stream, and trust-boundary deferrals |
| CommandApi stream debugging | `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` | blame, bisect, timeline, step, sandbox, `DeepMerge`, `JsonDiff`, `ReconstructState`, max query params |
| CommandApi trace map | `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs` | correlation trace scan cap and `[AllowAnonymous]` trust-boundary note |
| Admin facade delegation | `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs` | DAPR invocation timeouts, fallback behavior, JWT forwarding |
| Admin facade controller | `src/Hexalith.EventStore.Admin.Server/Controllers/AdminStreamsController.cs` | public authorized facade behavior for stream debugging |
| Admin options | `src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs` | configured facade caps and timeout defaults |
| Stream models | `src/Hexalith.EventStore.Admin.Abstractions/Models/Streams/*.cs` | response contracts, `FieldChange`, blame, bisect, step, sandbox, trace-map models |
| Server tests | `tests/Hexalith.EventStore.Server.Tests/Controllers/*.cs` | focused CommandApi helper and endpoint tests |
| Admin.Server tests | `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs` | facade delegation tests if behavior changes |
| Documentation target | `docs/operations/` or `docs/concepts/` | architecture/trust-boundary and large-stream limitation note |

## Current Code Intelligence

- `AdminStreamQueryController` is in `src/Hexalith.EventStore/Controllers/`, not the Admin.Server project. It is the CommandApi computation layer for admin stream debugging and is marked `[AllowAnonymous]`.
- `DaprStreamQueryService` is the Admin.Server facade implementation. It forwards bearer tokens to CommandApi via DAPR service invocation and applies facade defaults such as `MaxTimelineEvents`, `MaxBlameEvents`, `MaxBlameFields`, `MaxBisectSteps`, `MaxBisectFields`, and timeout choices.
- `AdminStreamQueryController.GetAggregateBlameAsync`, `BisectAggregateStateAsync`, `GetEventStepFrameAsync`, and `SandboxCommandAsync` all depend on JSON-level `DeepMerge`, `JsonDiff`, `FlattenJson`, or `ReconstructState` helpers.
- `DeepMerge` recursively merges object properties and overwrites leaf nodes. It does not infer delete semantics from omitted fields. A property explicitly set to `null` is stored as a JSON null node and may be treated differently from omission.
- `JsonDiff` currently recurses into nested objects and checks fields removed from the current object level, but delete visibility is gated by `DeepMerge` because reconstruction never removes omitted properties.
- `FlattenJson` treats arrays as leaf values because only nested `JsonObject` nodes recurse. This matches the existing UI contracts that present one field path per JSON leaf/object path, not element-level JSON Patch paths.
- Several CommandApi endpoints normalize non-positive max parameters to defaults, but the direct endpoint layer must still defend against extremely large values because Admin.Server caps are only facade defaults.
- `GetEventsAsync(0)` loads full aggregate streams before truncation in several debugging paths. Trace map uses a hard `MaxEventScan = 10_000` tail scan after the full stream is loaded.
- `AdminTraceQueryController` sets `ScanCapped` only when command status supplies an expected event count and fewer events are found inside the scan window. Unknown expected counts may still be partial if the stream exceeds the tail window.

## Latest Technical Notes

- `System.Text.Json.Nodes.JsonNode.DeepClone()` creates a recursively cloned mutable JSON node. Keep using clone semantics where the code needs before/after snapshots; avoid aliasing mutable `JsonNode` instances across diff steps. Source: <https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode.deepclone?view=net-10.0>
- Microsoft's ASP.NET Core MVC authorization guidance states that `[AllowAnonymous]` bypasses authorization statements on MVC controllers/actions. DW3 must therefore treat CommandApi `[AllowAnonymous]` as a deliberate internal trust-boundary decision, not as ordinary public endpoint authorization. Source: <https://learn.microsoft.com/aspnet/core/mvc/security/authorization/simple?view=aspnetcore-10.0>
- DAPR service invocation returns the called service status code to the caller for normal service responses. Admin.Server should preserve useful upstream problem details where safe, but CommandApi remains responsible for bounding direct internal query work. Source: <https://docs.dapr.io/reference/api/service_invocation_api/>

## Tasks / Subtasks

- [ ] Task 0: Baseline and classify DW3 deferrals (AC: #1, #7, #9, #12, #13)
  - [ ] 0.1 Re-read Proposal D / DW3 and the Epic 20 sections in `deferred-work.md`.
  - [ ] 0.2 Classify each relevant deferred item as `patch-now`, `accepted-debt`, `future-actor-api`, `duplicate`, or `not-DW3`.
  - [ ] 0.3 Record any product or architecture decisions before editing production code.
  - [ ] 0.4 Confirm the story will not pull in DW2 live-evidence, DW4 validators, DW5 UI polish, or DW6 governance.

- [ ] Task 1: Document reconstruction semantics and trust boundary (AC: #1, #2, #3, #7, #9, #13)
  - [ ] 1.1 Add a durable architecture/operations note for Admin debugging JSON reconstruction and large-stream behavior.
  - [ ] 1.2 Document JSON object merge, explicit `null`, omitted fields, nested removals, arrays, malformed JSON, and non-object payload handling.
  - [ ] 1.3 Document `[AllowAnonymous]` CommandApi admin controllers as internal DAPR-invoked endpoints behind the authorized Admin.Server facade.
  - [ ] 1.4 Define future actor API shape if snapshot/range-aware debugging is deferred, including sequence range, snapshot anchor, and event-count metadata needs.

- [ ] Task 2: Bound direct CommandApi parameters (AC: #5, #6, #10, #11)
  - [ ] 2.1 Identify every direct max/count parameter on `AdminStreamQueryController` and `AdminTraceQueryController`.
  - [ ] 2.2 Add upper-bound validation or clamping with clear 400 responses where direct callers can exceed configured protection.
  - [ ] 2.3 Keep Admin.Server facade defaults compatible with existing `AdminServerOptions`.
  - [ ] 2.4 Add endpoint tests for zero, negative, default, at-limit, and above-limit values.

- [ ] Task 3: Harden JSON diff and reconstruction failure modes (AC: #2, #3, #4, #11)
  - [ ] 3.1 Add tests for explicit null, omitted property, nested property removal, empty property name, non-object payload, malformed JSON, and array payload behavior.
  - [ ] 3.2 Patch only behaviors that are coherent from the event payload model and preserve existing public response shape.
  - [ ] 3.3 Add recursion/depth protection or record accepted debt with exact trigger thresholds if safe implementation is not local.
  - [ ] 3.4 Verify no logs or problem details expose event payload values.

- [ ] Task 4: Make large-stream behavior honest per debugging surface (AC: #6, #7, #8, #10, #11)
  - [ ] 4.1 Produce a matrix for blame, bisect, step-through, sandbox, diff, timeline, event detail, and trace map with input size, current read pattern, bound, truncation signal, and remaining debt.
  - [ ] 4.2 Fix trace-map scan-cap reporting if older events can be hidden without `ScanCapped = true`.
  - [ ] 4.3 Decide whether any `GetEventsAsync(0)` call can be replaced with a narrow range read under the current `IAggregateActor.GetEventsAsync(long fromSequence)` contract.
  - [ ] 4.4 If broader actor API work is required, record it as `future-actor-api` rather than mixing it into DW3.

- [ ] Task 5: Validate facade and client compatibility (AC: #10, #11)
  - [ ] 5.1 Run targeted Admin.Server tests if facade error propagation or timeout behavior changes.
  - [ ] 5.2 Run Admin UI, CLI, or MCP tests only when visible responses or client error handling changes.
  - [ ] 5.3 Ensure new 400/problem responses remain safe for Admin UI, CLI, and MCP consumers.

- [ ] Task 6: Close deferred-work and bookkeeping (AC: #12, #14)
  - [ ] 6.1 Update only DW3-relevant `deferred-work.md` bullets with disposition markers.
  - [ ] 6.2 Update this story's Dev Agent Record, File List, Change Log, Verification Status, and any deferred architecture decisions.
  - [ ] 6.3 Run markdown validation and targeted tests individually.
  - [ ] 6.4 Move this story and sprint-status row to `review` only after documentation, tests, and dispositions are complete.

## Dev Notes

### Architecture Guardrails

- Admin.Server remains the public authorized facade for Admin UI, CLI, and MCP. CommandApi admin debugging controllers are internal computation endpoints reached through DAPR service invocation.
- Event payload data and aggregate state JSON are debugging data, not operational log data. Do not emit payload or state values into logs, activities, or problem-details messages.
- The event stream remains append-only. DW3 may read and classify event/debugging behavior, but must not mutate events, snapshots, actor state, or projection state.
- JSON-level reconstruction is a diagnostic approximation unless it is proven equivalent to domain `Apply` behavior for a given aggregate. Documentation and UI text must not overclaim.
- Add hard bounds at the CommandApi computation layer when direct parameters can amplify CPU or memory work. Facade options are not enough by themselves.
- Prefer focused tests around `AdminStreamQueryController` helper behavior before broad UI/MCP changes.

### Previous Story Intelligence

- Story 20-1 deferred `GetEventsAsync(0)`, `DeepMerge` delete limitations, nested removal detection, direct max-parameter bypass, array-as-leaf behavior, and missing truncation/value-proposition tests.
- Story 20-2 improved semantic field comparison with `JsonElement.DeepEquals`, but still deferred O(N*logN) reconstruction, delete behavior, string-based final diffs, and direct max-parameter bounds.
- Story 20-3 added step-through debugger behavior and cancellation checks, but still deferred full-stream reads, recursion-depth risk, non-contiguous sequence count behavior, and empty `FieldPath` handling.
- DW2 is expected to prove runtime behavior, not patch JSON/large-stream limitations. If DW2 evidence finds these issues, route them back to DW3 rather than merging scopes.

### Testing Guidance

- Start with `tests/Hexalith.EventStore.Server.Tests/Controllers` because the risky logic lives in CommandApi controller helpers and endpoints.
- Prefer deterministic JSON payload tests over full Aspire/DAPR integration unless the behavior depends on runtime service invocation or actor placement.
- If Admin.Server facade behavior changes, add tests in `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprStreamQueryServiceTests.cs`.
- If problem responses or error classifications change in a way visible to UI/CLI/MCP, update the narrow client tests that parse those responses.
- Run test projects individually per repository guidance. Do not use solution-level `dotnet test` as the validation gate.

### References

- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-04-deferred-work-triage.md#Proposal-D-DW3-Admin-Debugging-JSON-and-Large-Stream-Hardening`] - DW3 scope and acceptance direction.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`] - raw Epic 20 JSON, large-stream, direct-bound, and trust-boundary deferrals.
- [Source: `_bmad-output/implementation-artifacts/20-1-blame-view-per-field-provenance.md`] - blame implementation and deferred JSON/large-stream review findings.
- [Source: `_bmad-output/implementation-artifacts/20-2-bisect-tool-binary-search-state-divergence.md`] - bisect implementation and semantic-comparison learnings.
- [Source: `_bmad-output/implementation-artifacts/20-3-step-through-event-debugger.md`] - step-through implementation and remaining reconstruction deferrals.
- [Source: `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`] - current CommandApi debugging computation and JSON helpers.
- [Source: `src/Hexalith.EventStore/Controllers/AdminTraceQueryController.cs`] - trace-map scan cap and internal endpoint trust boundary.
- [Source: `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`] - Admin.Server facade delegation and configured timeout/cap behavior.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Pre-dev hardening preflight: `_bmad-output/process-notes/predev-preflight-latest.json`, timestamp `2026-05-04T18:10:04Z`, result `pass`.

### Completion Notes List

- Created ready-for-dev story from first backlog row after DW2 in the Post-Epic Deferred Work Cleanup package.
- No implementation work has been performed for this story.
- No `project-context.md` file was present in the repository at story creation.

### File List

- `_bmad-output/implementation-artifacts/post-epic-deferred-dw3-admin-debugging-json-large-stream-hardening.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/process-notes/predev-hardening-runs.log`

## Verification Status

- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Markdown and YAML validation should be run before dev handoff if local tooling is available.

## Change Log

| Date | Version | Description | Author |
|---|---:|---|---|
| 2026-05-04 | 0.1 | Created ready-for-dev DW3 Admin debugging JSON and large-stream hardening story. | Codex automation |
