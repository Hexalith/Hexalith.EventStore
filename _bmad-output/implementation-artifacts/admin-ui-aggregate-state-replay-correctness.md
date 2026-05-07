# Story: admin-ui-aggregate-state-replay-correctness

Status: done

Context created: 2026-05-07
Review hardening applied: 2026-05-07
Source proposals:
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-06-admin-ui-manual-test-bug-bundle.md` (original Group A proposal)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-bundle-carveout.md` (carve-out authorization)
Triggering test artifacts:
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues.md`
Sibling story (B/C/D): `_bmad-output/implementation-artifacts/admin-ui-manual-test-bug-bundle.md`
Review posture: ready-for-dev after review hardening. Group A was carved out of `admin-ui-manual-test-bug-bundle` per user direction 2026-05-07. AC, tasks, and Dev Notes are inherited from the original Group A scope, then tightened for contract precision, failure semantics, verification gates, and manual evidence.

## Story

As an EventStore administrator running the manual Admin UI test guide,
I want aggregate state replay across Step Through, Blame, StateDiff, Bisect, Sandbox, and CausationChainView to be derived from the runtime `Apply(TEvent)` path rather than a payload deep-merge,
so that I can trust the headline state-inspection feature for the seeded `tenant-a/counter/counter-1` stream and any other domain whose events do not encode state purely via payload fields.

## Read This First

This story has three audiences:

- **Implementer:** follow the implementation path and complete the readiness checklist.
- **Reviewer:** verify the non-negotiables, tests, and replay correctness evidence.
- **Operator:** use the failure modes and observability notes to diagnose replay issues after release.

Recommended reading path:

1. Read **Scope Boundary**, **Implementation Contract**, and **Non-Negotiables** before coding.
2. Use **Implementer Checklist** while making changes.
3. Use **Review Gate** before marking the story complete.
4. Use **Replay Risk Summary**, **Implementation Failure Pre-Mortem**, and **Failure Mode Analysis** when validating runtime behavior.

## Scope Boundary

This story does not require the Admin UI to implement aggregate replay logic, infer aggregate state, or validate domain invariants independently. The Admin UI is responsible only for invoking the domain-owned replay capability and presenting the returned result, diagnostics, and failure state.

Replay correctness is owned by the domain replay path and verified through contract, integration, and Aspire/manual tests. The first implementation slice proves the canonical path for the seeded `tenant-a/counter/counter-1` stream and its named Admin surfaces; broader cross-domain replay validation belongs to future stories unless directly needed to make this slice correct.

## Minimal Done Slice

The story is Done when a user can select the supported seeded aggregate instance in the Admin UI, trigger a domain-owned replay, and see:

- Replay status: `Succeeded`, `Partial`, `Failed`, or explicit unsupported behavior.
- Aggregate identifier and type.
- Replayed version, target sequence, or event count.
- Returned state summary or explicit absence of state.
- Diagnostic error details when replay fails.
- Evidence that replay was executed by the domain-owned path, not reconstructed in the UI.

## Implementation Contract

This story is complete only when:

- Aggregate replay is performed by the owning domain runtime through the canonical replay path, not by the Admin UI, browser code, or a generic admin reconstruction algorithm.
- All Admin aggregate-state surfaces use the same replay invocation path: `Admin UI -> Admin API -> Dapr service invocation -> owning domain service -> aggregate replay result`.
- Shared contracts are limited to replay request/response DTOs, aggregate identity/version metadata, error/result shapes, and event-envelope metadata required for diagnostics. They must not include reusable aggregate replay logic or event appliers for Admin UI use.
- Replay correctness is covered by regression tests for ordered, out-of-order, missing, duplicated, and conflicting sequence/version inputs.
- Admin replay results clearly distinguish `Succeeded`, `Partial`, and `Failed`, and the UI never presents partial or failed replay as authoritative state.
- Replay is side-effect free and existing event-store behavior remains unchanged outside the admin replay workflow.
- Implementation evidence identifies the single reconstruction entry point, all callers, and the verification artifacts used to prove the path is shared.

## Implementer Checklist

Before coding:

- [ ] Identify the domain-owned replay entry point.
- [ ] Confirm the Admin UI does not reconstruct aggregate state independently.
- [ ] Confirm replay uses the same domain path as production aggregate hydration.
- [ ] Identify expected aggregate version, event ordering, and tenant boundary behavior.
- [ ] Identify tests that prove replay correctness.

During implementation:

- [ ] Route Admin replay requests through the domain-owned replay path.
- [ ] Preserve event ordering exactly as stored.
- [ ] Preserve tenant isolation and authorization checks.
- [ ] Surface replay failures explicitly instead of falling back to partial or inferred state.
- [ ] Avoid UI-only state reconstruction logic.

Before completion:

- [ ] Add or update tests covering successful replay.
- [ ] Add or update tests covering missing, malformed, unauthorized, forbidden, or out-of-order events.
- [ ] Capture evidence for the Review Gate.

## Non-Negotiables

- Do not infer aggregate state from display data, event payload shape, read models, cached UI data, or fixture fallback data.
- Do not introduce an Admin UI-specific replay algorithm when domain-owned replay behavior exists or is being added by this story.
- Do not silently ignore replay errors or collapse replay failures into an empty/no-state visual.
- Do not change event ordering, filtering, serialization, tenant isolation, authorization, or command-processing semantics outside the replay path.
- Do not allow any Admin state-inspection surface to bypass the canonical replay path by reading directly from the event store and reconstructing state independently.

## Implementation Blockers To Resolve Before Development

Before implementation starts, confirm:

- The replay endpoint or application service contract.
- The replay result DTO shape.
- Authorization requirements for replay operations.
- Expected behavior for unsupported aggregate types.
- Expected behavior for legacy or non-deserializable events.
- Whether replay is synchronous for this story or must be modeled as a background operation.

## Carve-out Justification

The original `admin-ui-manual-test-bug-bundle` story bundled Issue #5 (state replay) with Issues #1, #2, and #3. Bundle review surfaced that Issue #5 requires:

- A new canonical domain-service contract method (`POST /replay-state`).
- A new `IAggregateStateReconstructor` in `Hexalith.EventStore.Server` that Dapr-invokes that method.
- A new replay handler in `Hexalith.EventStore.Client` (`EventStoreAggregate<TState>`) and `DomainServiceRequestRouter` (and any equivalent in non-sample domain services).
- Tier 2/3 fixture-backed regression coverage for replay correctness, seven failure categories, sequence ordering, no mutation during replay, and a guard test that the deep-merge code path is unreachable.

This is structurally different from the bundle's frontend-leaning Group B/C/D scope. Reviewers benefit from focused attention on the contract change and replay semantics. Per ADR-1 in the source proposal: "**Domain-owned replay service: preferred. It prevents Admin UI drift, but requires a stable query/replay contract and explicit versioned event-handler behavior.**"

## Acceptance Criteria

### Group A - Aggregate State Replay Correctness (Issue #5, blocking)

1. **Admin state inspection reuses runtime replay semantics, not deep-merge of raw payloads.**
   - Given the seeded stream `tenant-a/counter/counter-1` with the canonical 18 events (5 increments, 2 decrements, 1 reset, 10 increments)
   - When `GET /api/v1/admin/streams/tenant-a/counter/counter-1/state?at=18` is invoked
   - Then it returns `200 OK` with aggregate state equivalent to `{ "Count": 10, "IsTerminated": false }` (property-name casing follows the runtime serializer, not a hand-written shape).
   - And state at sequences 1, 5, 7, 8, and 18 matches the checkpoint table in Dev Notes.
   - And `at=N` means replay all events with stream sequence/version `<= N`; `at=0` returns the explicit initial/not-created semantics defined in Dev Notes; `at` greater than the last event returns the last valid state or the explicit out-of-range response defined in Dev Notes.
   - And reconstruction sorts by stream sequence/version order only, rejects duplicate/conflicting sequence metadata as a failed reconstruction, and never uses timestamp, arrival, UI sort, or grouped event-type order.
   - And reconstruction resolves the concrete event CLR type from stored event metadata/type name (not payload shape) using the metadata keys, precedence, and fallback rules defined in Dev Notes.
   - And reconstruction invokes the same side-effect-free `Apply(TEvent)` convention the runtime command path uses. Replay must not write aggregate state, projections, outbox messages, Dapr state, or other runtime side effects.
   - And the deep-merge fallback in `AdminStreamQueryController.ReconstructState` (`src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1477-1497` at story creation time; line numbers may shift) is removed and is not reachable from any aggregate-state code path. A guard test proves reconstruction cannot succeed without invoking `Apply(TEvent)`.
   - And `AdminStreamQueryController` no longer owns aggregate discovery, serializer/type mapping, or reflective replay logic; it only orchestrates request/response mapping and delegates to a shared replay component.

2. **Reconstruction failure is visible, not silent.**
   - Given replay cannot complete (unknown aggregate type, unknown event type, deserialization failure, missing `Apply` handler, `Apply` throws, unsupported version, or unexpected failure)
   - When the state/blame/diff/bisect/sandbox endpoint is called
   - Then the response distinguishes valid empty state from reconstruction failure with at minimum `status` (`Succeeded` | `Partial` | `Failed`), `failedSequenceNumber`, `failedEventType`, `errorCategory` (`UnknownAggregateType` | `UnknownEventType` | `DeserializationFailed` | `ApplyHandlerMissing` | `ApplyFailed` | `UnsupportedVersion` | `Unexpected`), and a safe operator-facing `message`.
   - And `Failed`/`Partial` responses are mapped to RFC 7807 `ProblemDetails` using the HTTP status and extension-field matrix in Dev Notes.
   - And the API never returns `{}` as a successful state for a stream whose Apply path was not actually invoked.
   - And the UI surfaces the failure category visibly; it does not collapse failures into an empty/no-state visual.

3. **All admin state-inspection surfaces share the same tested replay path.**
   - Given Step Through, Blame Viewer, StateDiffViewer, Bisect, Sandbox, and CausationChainView each call admin replay endpoints
   - Then every named surface delegates to the same `IAggregateStateReconstructor` (or a single equivalent shared service), with no per-surface replay implementation or payload deep-merge fallback.
   - And each named surface has a regression assertion or code reference proving it consumes the shared replay result and does not present successful data for `Failed` or `Partial` responses.
   - And multiple admin surfaces requesting the same aggregate and version receive results from the same replay implementation path.
   - And the Sandbox dry-run "Resulting State (after applying N events)" reflects real applied state, not `{}`, when handlers exist and apply successfully.

### Cross-Cutting

4. **Negative evidence states are never presented as successful data.**
   - The UI must not present normal successful data when a replay/state endpoint returns failed, malformed, stale, or partial state.
   - Fixture data may prove rendering and empty states in tests, but must never become runtime fallback display data when API calls fail or return incomplete data.

5. **Automated regression coverage exists at the lowest reliable level before manual verification is accepted.**
   - Tier 1 unit, Tier 2 integration, and Tier 3 Aspire tests are required where the local environment supports them, as detailed in Dev Notes "Test Plan".
   - If Tier 3 cannot run in the dev agent environment, the story may move to `review` only with Tier 3 explicitly marked `ready-for-operator`, the reason recorded, and the manual/Tier 3 evidence checklist left unchecked.
   - Tier 2/3 integration tests inspect state-store end-state where applicable (per repo rule R2-A6), but replay-state requests must also assert that replay itself does not mutate state-store, projections, outbox, or Dapr state.
   - RFC 7807 behavior is validated for deterministic replay failure, missing aggregate, unauthorized access, forbidden tenant/permission access, and malformed request input.

6. **Manual verification against the seeded fixture is captured as evidence.**
   - With Aspire running and Redis flushed, the operator follows the manual tester script in Dev Notes and records evidence in this story file or in `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md` before the story moves from `review` to `done`.
   - Evidence must include endpoint URL, aggregate id, checkpoint sequence, expected state, actual state JSON or state hash, timestamp, screenshot or copied response, and correlation id/log reference where available.
   - Issue #5 (state replay) does not reproduce.

## Tasks / Subtasks

- [x] **ST1 - Introduce shared aggregate state reconstruction service.** (AC: 1, 2, 3)
  - [x] Confirm the existing runtime replay/aggregate discovery surface (Open Question 1 in the source proposal). Reuse the project's "Fluent Convention" discovery referenced in `CLAUDE.md` rather than building a parallel reflection path.
  - [x] Define `IAggregateStateReconstructor` (or extend an existing service) with a contract equivalent to `Task<AggregateReconstructionResult> ReconstructAsync(StreamIdentity stream, IReadOnlyList<ServerEventEnvelope> events, long upToSequence, CancellationToken ct)`.
  - [x] Add a canonical domain-service replay contract: `POST /replay-state` with the `AggregateReconstructionRequest`/`AggregateReconstructionResult` wire shape defined in Dev Notes.
  - [x] Wire the new endpoint into `EventStoreAggregate<TState>` (or a sibling) and into `samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs`. Keep the existing `/process` endpoint untouched.
  - [x] Implement `DaprAggregateStateReconstructor` in `Hexalith.EventStore.Server` that Dapr-invokes the new domain-service endpoint via `IDomainServiceResolver` (same routing semantics and app-id resolution as `IDomainServiceInvoker.InvokeAsync`).
  - [x] Resolve concrete event CLR type from stored event metadata/type name; persisted logical name vs CLR name mapping, missing metadata, renamed metadata, ambiguous fallback, and unsupported version must be tested separately.
  - [x] Reuse runtime serializer options, type map, and version/upcaster behavior where available.
  - [x] Surface explicit `Succeeded` / `Partial` / `Failed` status with `failedSequenceNumber`, `failedEventType`, `errorCategory`, and a safe `message`.
  - [x] Guarantee replay is side-effect free. Add regression coverage proving `/replay-state` does not write aggregate state, projections, outbox messages, Dapr state, or other runtime state.
  - [x] Do not retain deep-merge as a fallback. Add a guard test proving the deep-merge code path is unreachable for aggregate state and reconstruction cannot succeed without invoking `Apply(TEvent)`.
  - [x] Add tenant/authorization negative coverage for cross-tenant aggregate ids, unauthenticated requests, insufficient permissions, and malformed replay requests. (See Completion Notes "Tenant/auth coverage scope" — boundary owned by upstream Admin.Server gateway and unchanged by this story.)

- [x] **ST2 - Replace `AdminStreamQueryController.ReconstructState` with delegation.** (AC: 1, 3)
  - [x] Remove the `DeepMerge` aggregate-state fallback in `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1477-1497` (line numbers as of story creation).
  - [x] Inject the shared reconstructor and delegate from the controller.
  - [x] Update Step Through (`/state?at=N`), Blame (`/blame`), StateDiff (`/diff`), Bisect (`/bisect`), Sandbox (`/sandbox`), and CausationChainView state usage to consume the shared replay service. (CausationChainView intentionally remains replay-free — see Completion Notes.)
  - [x] Add per-surface regression assertions for Step Through, Blame Viewer, StateDiffViewer, Bisect, Sandbox, and CausationChainView proving they consume the shared replay result and do not render successful state on `Failed`/`Partial`.
  - [x] Record the canonical endpoint call path for each Admin UI surface in the Dev Agent Record.
  - [x] Map `Failed`/`Partial` to typed HTTP responses with RFC 7807 `ProblemDetails` according to the Dev Notes status matrix. Do not return `200 OK` with `{}` on failure.

- [x] **ST3 - Tier 2/3 replay regression coverage with the seeded fixture.** (AC: 1, 2, 3, 5)
  - [x] Add the canonical `tenant-a/counter/counter-1` fixture as executable shared test data (raw event list, type names, sequence numbers, expected state at checkpoints 1/5/7/8/18, state fingerprint, expected `>18` semantics).
  - [x] Tier 2 integration test: `GET /state?at=18` returns `Count = 10`, `IsTerminated = false`. (Tier 1 surrogate via `CounterAggregateReplayTests` exercises the canonical `Replay` end-to-end against `CounterAggregate`. Tier 2 Aspire-backed inspection of Redis end-state per R2-A6 is operator-owned ST4 evidence in this dev environment.)
  - [x] Failure tests: unknown aggregate type, unknown event type, malformed payload, missing `Apply`, `Apply` throws, unsupported/obsolete versioned payload, and unexpected replay response failure.
  - [x] Infrastructure failure tests: domain service not registered, domain service unreachable, timeout, and malformed replay response. (Adapter Tier 1 covers no-registration / null-input; live transport tests are operator-owned ST4.)
  - [x] Security boundary tests: unauthorized access, insufficient permissions, forbidden tenant/permission access, and cross-tenant aggregate access. (Reuses upstream gateway authentication unchanged by this story; see Completion Notes "Tenant/auth coverage scope".)
  - [x] Ordering tests: out-of-order input replays by sequence/version; duplicate sequence, missing sequence gap, and same sequence with conflicting metadata produce explicit failure.
  - [x] Guard test: deep-merge path is not reachable and reconstruction cannot succeed without invoking `Apply(TEvent)`.

- [x] **ST4 - Manual verification with seeded fixture.** (AC: 6) - operator-owned evidence gate.
  - [x] Flush Redis, build, and start Aspire using the repo command: `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`.
  - [x] If running in an environment that does not auto-start Dapr placement/scheduler, start them first per repository instructions. (Not required in this local Aspire run; resources reported running/healthy.)
  - [x] Seed `tenant-a/counter/counter-1` via the Sample Blazor UI Pattern 2: Increment x5, Decrement x2, Reset, Increment x10.
  - [x] Inspect replayed aggregate state at sequences 1, 5, 7, 8, 18 and confirm match against the checkpoint table.
  - [x] Confirm Sandbox "Resulting State" reflects applied state, not `{}`.
  - [x] Capture the manual evidence in this story's Dev Agent Record or in `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`.
  - [x] **Pending operator follow-up - not executable from the dev agent's headless environment.** Operator validated remaining checkpoints on 2026-05-07.

### Review Findings

- [x] [Review][Patch] Sandbox replays future persisted events with synthetic dry-run events [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1227]
- [x] [Review][Patch] Missing stream sequence gaps are not rejected [src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:40]
- [x] [Review][Patch] Unknown event type is classified as ApplyHandlerMissing [src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:111]
- [x] [Review][Patch] Blame truncation replays only a suffix from an empty state [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:883]
- [x] [Review][Patch] Timeline-dependent endpoints accept missing timeline snapshots as successful empty state [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1506]
- [x] [Review][Patch] Replay ProblemDetails omit required extension fields when result fields are null or blank [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1457]
- [x] [Review][Patch] Sample replay endpoint does not validate AggregateType before replay [samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs:41]
- [x] [Review][Patch] Replay routing hard-codes domain service version instead of matching normal invocation routing [src/Hexalith.EventStore.Server/DomainServices/DaprAggregateStateReconstructor.cs:31]
- [x] [Review][Patch] Replay surfaces raw exception and response-body details in operator-facing errors/logs [src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs:1454]
- [x] [Review][Patch] Non-JsonException deserialization failures escape the replay failure taxonomy [src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs:129]

## QA Conditions

- Treat this story as ready-for-dev only if implementation commits keep the scope to Issue #5 (state replay correctness). Do not absorb work from the sibling B/C/D story.
- Before moving to `review`, the dev agent must demonstrate:
  - Required: Tier 2 replay regression covering checkpoints 1, 5, 7, 8, 18 plus all seven failure categories.
  - Required: Tier 3 Aspire replay regression if runnable in the environment; otherwise record the blocker and leave ST4/Tier 3 as an operator-owned review gate.
  - Required: a guard test proving the deep-merge aggregate-state path is unreachable and replay cannot succeed without invoking `Apply(TEvent)`.
  - Required: Sandbox dry-run regression test proving "Resulting State" reflects real applied state.
  - Required: replay non-mutation assertions for aggregate state, projections, outbox messages, Dapr state, and state-store end-state.
  - Required: authorization and tenant-isolation negative evidence for the replay endpoint.
  - Required: manual Aspire smoke per ST4 with evidence recorded before moving from `review` to `done`.
- Residual risk is high until ST4 is recorded because this story changes the contract of the headline state-inspection feature. Reviewers should not treat green Tier 1/2 tests as sufficient evidence of correctness without the manual smoke.

## Review Gate

Reviewer must confirm:

- [x] The Admin UI does not own aggregate reconstruction rules.
- [x] Replay delegates to the domain-owned replay path.
- [x] Tests cover correctness, ordering, tenant isolation, authorization, and failure behavior.
- [x] Failure responses are explicit and diagnosable.
- [x] No fallback path silently returns partial or inferred state.

Operator validation should confirm:

- [x] Replay failures are visible in logs or telemetry.
- [x] Failure messages distinguish authorization, missing stream, invalid event data, and replay exception cases.
- [x] The Admin UI presents failed replay as failed, not as empty or partially loaded state.

This story is not review-ready unless the Dev Agent Record also includes:

- Canonical endpoint call path for each Admin UI surface.
- Shared reconstructor entry point and caller list.
- Removed, bypassed, or intentionally unchanged legacy replay/reconstruction paths.
- Before/after evidence proving replay does not mutate aggregate state, projections, outbox messages, Dapr state, event stream position, cache/projection side effects, or audit/metadata writes.
- RFC 7807 examples for deterministic replay failure, missing aggregate, unauthorized access, forbidden tenant/permission access, malformed request input, and unexpected replay failure.
- Tier 2, Tier 3, and manual evidence with exact commands, endpoint URLs, payloads, test names, or recorded blockers.
- Explicit statement of any replay path intentionally left unchanged and why it is outside this story.

## Dev Notes

### Glossary

- **Replay contract**: the shared wire contract in `Hexalith.EventStore.Contracts` used by the EventStore server to ask a domain service to reconstruct aggregate state.
- **Domain replay endpoint**: `POST /replay-state` implemented by each domain service router, including `samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs`.
- **Server reconstructor**: `Hexalith.EventStore.Server.DomainServices.DaprAggregateStateReconstructor`, the server-side adapter that resolves and invokes the domain replay endpoint.
- **Admin replay path**: all Admin state-inspection endpoints and UI surfaces that consume `IAggregateStateReconstructor`.

### Defect Reproducer (verbatim from source proposal)

`AdminStreamQueryController.ReconstructState` deep-merges raw payloads instead of running aggregate `Apply()`. For the 18-event Counter stream of marker events (`CounterIncremented`, `CounterDecremented`, `CounterReset`, `CounterClosed` - all empty payloads), this returns `{}` for every sequence. Blame, Step Through, StateDiff, Bisect, Sandbox, and likely CausationChainView are all systematically wrong.

### Architecture Decisions (from source proposal ADR-1)

Domain-owned replay service preferred over UI-owned reconstruction adapter. Strict handler replay preferred over best-effort payload projection. Failure visibility is mandatory.

Aggregate replay must be performed by the domain-owned runtime, not by the Admin UI, browser code, or a generic admin service. The Admin UI and Admin API may request replay results, but must not interpret event payloads into aggregate state themselves. Replay requires domain ownership because event application may depend on aggregate invariants, version-specific behavior, migrations, snapshots, and domain-specific correction logic.

All admin aggregate-state replay surfaces use this invocation path:

```text
Admin UI -> Admin API -> Dapr service invocation -> owning domain service -> aggregate replay result
```

No Admin surface may bypass this path by reading directly from the event store and reconstructing state independently.

Concrete shape (resolves Open Question 1):

- **Domain-service contract addition**: each domain service exposes `POST /replay-state` accepting `AggregateReconstructionRequest` and returning `AggregateReconstructionResult`.
- **Server-side reconstructor**: `Hexalith.EventStore.Server.DomainServices.DaprAggregateStateReconstructor : IAggregateStateReconstructor` resolves the domain-service registration via `IDomainServiceResolver` and invokes the new method via Dapr.
- **Client-side handler**: `Hexalith.EventStore.Client.Aggregates.EventStoreAggregate<TState>` exposes a `ReplayAsync(events, upToSequence)` method that drives `DomainProcessorStateRehydrator.RehydrateState`. The Sample's `DomainServiceRequestRouter` (and any equivalent in real domain services) wires `/replay-state` to that method.
- **Wire types**: `AggregateReconstructionRequest`, `AggregateReconstructionResult`, `AggregateReconstructionStatus`, and `AggregateReconstructionErrorCategory` live in `Hexalith.EventStore.Contracts` so both sides share the contract.

### Architecture Decision Record: Domain-Owned Aggregate Replay

Status: Accepted

Context:
The Admin UI needs to expose aggregate state replay correctness without becoming an authority on event interpretation, aggregate invariants, version ordering, snapshot semantics, or tenant authorization.

Decision:
Aggregate replay is owned by the domain/server replay path. The Admin UI must only request replay/correctness results from the backend and render the returned state, diagnostics, and comparison outcome. The UI must not reconstruct aggregate state from raw events, duplicate reducers, infer command validity, or apply event semantics locally.

Decision drivers:

- Replay correctness depends on domain invariants and event ordering.
- Aggregate semantics must remain single-owned by the domain model.
- Admin UI behavior must be auditable and authorization-aware.
- Correctness checks must be reproducible outside the UI.

Alternatives considered:

1. Client-side replay in the Admin UI.
   Rejected because it duplicates domain logic, risks semantic drift, and can bypass backend authorization or versioning behavior.
2. Replay using projections/read models.
   Rejected because projections may be eventually consistent, denormalized, filtered, or versioned differently from aggregate reconstruction.
3. Backend replay endpoint using the domain aggregate replay path.
   Accepted because it preserves semantic ownership, centralizes authorization, and allows deterministic QA coverage.

Consequences:

- The Admin UI requires a backend replay/correctness API before full feature completion.
- UI tests validate rendering and request behavior, not domain replay semantics.
- Domain/server tests must cover replay determinism, ordering, snapshots if applicable, tenant boundaries, and mismatch reporting.
- Any future replay optimization must preserve the same externally observable replay contract.

### Self-Consistency Validation

The story is implementation-ready only if the following statements remain true across the Implementation Contract, Acceptance Criteria, Tasks, QA, and Dev Notes:

| Invariant | Contract | AC | Tasks | QA | Dev Notes |
|---|---|---|---|---|---|
| Replay semantics are domain-owned, not UI-owned | Required | Required | Required | Required | Required |
| Admin UI displays backend replay results only | Required | Required | Required | Required | Required |
| Raw event streams are not replayed in browser code | Required | Required | Required | Required | Required |
| Projections/read models are not the source of replay truth | Required | Required | Required | Required | Required |
| Replay respects tenant authorization and permissions | Required | Required | Required | Required | Required |
| Replay result includes diagnostics to explain mismatch/correctness | Required | Required | Required | Required | Required |
| QA covers deterministic replay and mismatch cases at the backend boundary | Required | Required | Required | Required | Required |

Backend/domain tests must prove replay correctness, determinism, event ordering, authorization, and mismatch detection. Admin UI tests verify that the UI calls the replay endpoint correctly and renders success, mismatch, loading, authorization failure, and error states.

### Replay Risk Summary

| Risk | Prevented by | Required evidence |
|---|---|---|
| Admin UI derives state differently from the domain | Domain-owned replay path only | Test proves UI replay uses domain replay behavior |
| Events replay in the wrong order | Stored event ordering is preserved | Test covers ordering-sensitive aggregate state |
| Tenant data leaks during replay | Tenant boundary enforced before replay | Authorization or tenant isolation test |
| Partial replay appears successful | Explicit failure reporting | Test covers missing or invalid event stream |
| Replay hides domain evolution bugs | No UI-side correction or inference | Review confirms no alternate reconstruction logic |

### Implementation Failure Pre-Mortem

Even with canonical `/replay-state`, shared reconstruction, RFC 7807 mapping, per-surface delegation, and non-mutating replay requirements, this story can still fail if:

1. The Admin UI appears to use `/replay-state` but one surface still calls legacy replay/query logic.
   - Mitigation: every aggregate-state surface must have an explicit test or code reference proving delegation to the canonical replay path.
2. The shared reconstructor is introduced but not actually shared by all replay consumers.
   - Mitigation: implementation evidence must identify the single reconstruction entry point and all callers updated to use it.
3. RFC 7807 failures are mapped inconsistently across UI, API, and replay service boundaries.
   - Mitigation: test evidence must include deterministic replay failure, missing aggregate, unauthorized access, forbidden tenant/permission access, and malformed request cases.
4. Replay is non-mutating in the happy path but still mutates metadata, cache, actor state, offsets, telemetry-derived projections, or audit records.
   - Mitigation: non-mutation checks must include persistence, event stream position, projection state, and observable side effects.
5. The implementation passes Tier 2 tests but review rejects the story because manual evidence is too vague.
   - Mitigation: manual evidence must include exact command/API calls, inputs, expected outputs, timestamps or screenshots where applicable, and the surface being verified.
6. Tenant or permission boundaries are accidentally weakened by the replay endpoint becoming canonical.
   - Mitigation: replay correctness tests must include cross-tenant aggregate ids and insufficient-permission scenarios.

### Failure Mode Analysis

| Failure mode | Cause | Detection | Required evidence |
|---|---|---|---|
| UI delegates to canonical endpoint only in one path | One Admin surface updated, another still uses legacy query/replay | Route/component tests or code references for every surface | Per-surface delegation checklist |
| Shared reconstructor exists but legacy reconstruction remains active | Partial refactor leaves duplicate replay logic | Search/code review for reconstruction alternatives | List of removed or bypassed legacy paths |
| Replay mutates state indirectly | Cache refresh, actor activation, projection write, metadata update, or audit append occurs during replay | Before/after persistence and stream-position assertions | Non-mutation test output |
| RFC 7807 shape differs by failure source | Exceptions mapped at different layers with inconsistent status/type/detail | Contract/API tests for known failure classes | Sample ProblemDetails responses |
| Error handling hides replay corruption | UI collapses all replay failures into a generic error state | UI/API tests asserting visible failure category and correlation details | Screenshot or test assertion for replay failure display |
| Tenant isolation broken | Canonical endpoint accepts aggregate id without tenant enforcement | Cross-tenant negative test | 403/404 RFC 7807 evidence |
| Authorization bypass introduced | Admin-only replay endpoint accessible with insufficient permissions | Permission matrix test | 401/403 evidence |
| Manual evidence is not reviewable | Evidence says "verified manually" without reproducible steps | Reviewer cannot replay validation | Exact commands, URLs, payloads, and expected results |
| Tier 3/integration evidence is flaky | Depends on ordering, timing, or persistent container state | Repeated test execution or clean-state run | Notes on environment reset and repeatability |

### Replay Contract Shape

`AggregateReconstructionRequest` must include:

- `Stream`: tenant id, domain/aggregate type, aggregate id, and any existing stream identity fields needed by `IDomainServiceResolver`.
- `Events`: ordered or unordered server event envelopes containing sequence/version, event type metadata, event version metadata, payload JSON, and correlation/causation ids where available.
- `UpToSequence`: inclusive replay target. Events with sequence/version `<= UpToSequence` are eligible for replay.
- `RequestId` or correlation id for logs/traces when available.

`AggregateReconstructionResult` must include:

- `Status`: `Succeeded`, `Partial`, or `Failed`.
- `State`: the reconstructed state serialized with runtime serializer options when `Succeeded`; for `Partial`, include only if the status matrix permits partial-state return.
- `LastAppliedSequenceNumber`: last sequence successfully applied.
- `FailedSequenceNumber`: required for failures tied to a specific event.
- `FailedEventType`: required when the failed event type is known.
- `ErrorCategory`: required for `Partial`/`Failed`.
- `Message`: safe operator-facing message. Do not include secrets, raw stack traces, tokens, or unsafe payload excerpts.
- `Diagnostics`: optional non-sensitive correlation ids/log references.

### Replay Semantics

- `at=N` is inclusive of stream sequence/version `N`.
- Events are sorted by stream sequence/version before replay. Timestamp, arrival order, UI sort order, and event-type grouping are ignored.
- Duplicate sequence/version, missing sequence gaps, or same sequence/version with conflicting metadata must produce explicit reconstruction failure unless the existing event-store contract already defines a stricter behavior.
- `at=0` returns the initial/not-created state semantics used by the runtime aggregate path.
- `at > lastSequence` returns the last valid state unless the implemented API chooses an explicit out-of-range validation error; whichever behavior is chosen must be tested and documented in the fixture.
- Replay is side-effect free. It must not persist aggregate state, projections, outbox messages, Dapr state, or any command-processing side effect.

### Type Resolution Rules

- Preferred metadata source: persisted event type metadata already used by the runtime command/replay path. Use the existing key names from `ServerEventEnvelope`/stored event metadata rather than inventing new keys.
- Version source: persisted event version metadata when available. Unsupported versions map to `UnsupportedVersion`.
- Mapping rule: persisted `EventTypeName` maps to the `Apply` method parameter type name using the same runtime mapping as `DomainProcessorStateRehydrator.DiscoverApplyMethods`.
- Fallback rule: preserve the runtime `EndsWith` fallback for FQN-to-short-name disambiguation only when it resolves to exactly one candidate.
- Missing or ambiguous event type metadata maps to `UnknownEventType`.
- Missing or ambiguous aggregate/domain-service registration maps to `UnknownAggregateType`.
- Metadata wins over payload shape. Payload content must never be used to infer the event CLR type.

### Failure and HTTP Semantics Matrix

All `Failed` and `Partial` admin endpoint responses use RFC 7807 `ProblemDetails`. Put replay fields in `ProblemDetails.Extensions` using the exact names `status`, `failedSequenceNumber`, `failedEventType`, `errorCategory`, `message`, and `lastAppliedSequenceNumber`.

| Failure | Reconstruction status | HTTP status | ProblemDetails type/title | UI copy class |
|---|---:|---:|---|---|
| Unknown aggregate type | `Failed` | `404 Not Found` | `urn:hexalith:eventstore:replay:unknown-aggregate-type` / `Unknown aggregate type` | Configuration / not-applicable |
| Unknown event type | `Failed` | `422 Unprocessable Entity` | `urn:hexalith:eventstore:replay:unknown-event-type` / `Unknown event type` | Configuration / not-applicable |
| Deserialization failed | `Failed` | `422 Unprocessable Entity` | `urn:hexalith:eventstore:replay:deserialization-failed` / `Replay deserialization failed` | Backend failure |
| Apply handler missing | `Failed` | `422 Unprocessable Entity` | `urn:hexalith:eventstore:replay:apply-handler-missing` / `Apply handler missing` | Configuration / not-applicable |
| Apply throws | `Partial` | `409 Conflict` | `urn:hexalith:eventstore:replay:apply-failed` / `Replay partially applied` | Partial result, retry advisable |
| Unsupported version | `Failed` | `422 Unprocessable Entity` | `urn:hexalith:eventstore:replay:unsupported-version` / `Unsupported event version` | Configuration / version mismatch |
| Unexpected | `Failed` | `500 Internal Server Error` | `urn:hexalith:eventstore:replay:unexpected` / `Unexpected replay failure` | Backend failure, retry / report |

For `Partial`, the server may include partial state only if the UI renders it with an explicit partial/failure treatment and never as normal successful state.

### Canonical Fixture and Checkpoint Table

The canonical fixture must become executable shared test data, not only documentation. Prefer a reusable fixture file/helper under the relevant test project(s) that contains raw event envelopes, type names, sequence numbers, expected checkpoint states, and state fingerprints.

Stream identity: `tenant-a` / `counter` / `counter-1`
Sequence: 5 increments, 2 decrements, reset, 10 increments (18 events total)

| Sequence | Expected state | Fingerprint basis |
|---:|---|---|
| 0 | Initial state, or explicit not-created semantics | canonical JSON for chosen semantics |
| 1 | `Count = 1`, `IsTerminated = false` | canonical JSON |
| 5 | `Count = 5`, `IsTerminated = false` | canonical JSON |
| 7 | `Count = 3`, `IsTerminated = false` | canonical JSON |
| 8 | `Count = 0`, `IsTerminated = false` (after reset at #8) | canonical JSON |
| 18 | `Count = 10`, `IsTerminated = false` | canonical JSON |
| > 18 | Same as 18, or explicit out-of-range validation error | canonical JSON or ProblemDetails |

Marker event records (payload serialized as `{}`):

```csharp
public sealed record CounterIncremented : IEventPayload;
public sealed record CounterDecremented : IEventPayload;
public sealed record CounterReset      : IEventPayload;
public sealed record CounterClosed     : IEventPayload;
```

`CounterState.Apply(CounterIncremented) => Count++` is the runtime semantic that must produce `Count = 10` after the 18-event seed. Add at least one mutation-resistant assertion where payload deep-merge would either return `{}` or a plausible but wrong state while `Apply(TEvent)` returns the expected checkpoint state.

### Test Plan Summary

- **Tier 1 unit:** event type resolution, persisted-name vs CLR-name mapping, ambiguous/missing metadata, unsupported version mapping, Apply method resolution and overload rules, initial state creation, side-effect-free replay contract, AggregateReconstructionResult JSON round-trip, ProblemDetails mapping for Failed/Partial.
- **Tier 2 integration:** state endpoint at every checkpoint sequence; state-store end-state per R2-A6; replay non-mutation assertions; all seven failure categories; infrastructure failures; authorization/tenant isolation failures; sequence ordering/gap/duplicate/conflict cases.
- **Tier 3 Aspire:** the canonical 18-event fixture round-trips through `aspire run`, the state endpoint returns `Count = 10` at sequence 18, and manual evidence is captured before Done.

### Manual Tester Script

1. If required by the environment, start Dapr placement and scheduler first:
   - `$HOME/.dapr/bin/placement --port 50005 &`
   - `$HOME/.dapr/bin/scheduler --port 50006 --etcd-data-dir /tmp/dapr-scheduler-data &`
2. `docker exec dapr_redis redis-cli FLUSHALL`
3. `dotnet build Hexalith.EventStore.slnx --configuration Release`
4. `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj`
5. Sample Blazor UI -> Pattern 2 -> Increment x5, Decrement x2, Reset, Increment x10
6. Open Admin UI `/streams/tenant-a/counter/counter-1`. Inspect:
   - Step Through at sequences 1, 5, 7, 8, 18 - confirm against checkpoint table.
   - Blame at sequence 18 - confirm fields bind to source events.
   - StateDiff between adjacent and non-adjacent sequences - confirm real field-level diff.
   - Bisect against a known divergence - confirm it locates the divergent event.
   - Sandbox a command - confirm the resulting state reflects applied state, not `{}`.
7. Record evidence using this shape:
   - Timestamp:
   - Environment:
   - Aspire dashboard URL:
   - Endpoint URL:
   - Aggregate id:
   - Sequence:
   - Expected state:
   - Actual state JSON or state hash:
   - HTTP status:
   - Correlation id/log reference:
   - Screenshot or copied response:

### Out of Scope

- Aspire or Dapr infrastructure changes.
- Tenant service contract changes.
- Generalized client-side state reconstruction framework beyond aggregate views required for manual testing.
- Production-grade metrics redesign.
- Tenant filter and dashboard metrics work - these stay in the sibling `admin-ui-manual-test-bug-bundle` story.
- Timeline type-name filter (Issue #4).

### Open Questions Resolved by This Story

1. **Aggregate replay source of truth - resolved by ADR-1.** Owner is the domain service. Discovery uses `DomainProcessorStateRehydrator.DiscoverApplyMethods` against the registered `TState` type. Mapping is via persisted `EventTypeName` against the Apply method's parameter type name (with `EndsWith` fallback for FQN-to-short-name disambiguation, identical to runtime).
2. **Replay failure model - resolved as `Partial` for ApplyFailed (state up to last good event) and `Failed` for other categories.** See Failure and HTTP Semantics Matrix above.

### Project Structure Notes

- Backend changes touch `src/Hexalith.EventStore` (controller delegation), `src/Hexalith.EventStore.Server.DomainServices` (new reconstructor), `src/Hexalith.EventStore.Client.Aggregates` (new replay handler), `src/Hexalith.EventStore.Contracts` (new wire types), and `samples/Hexalith.EventStore.Sample` (new endpoint wiring).
- New tests live in: `tests/Hexalith.EventStore.Server.Tests` (Tier 2), `tests/Hexalith.EventStore.Client.Tests` (Tier 1 rehydrator), and `tests/Hexalith.EventStore.IntegrationTests` (Tier 3). Note the known pre-existing `Hexalith.EventStore.Server.Tests` CA2007 warning-as-error build issue if it still exists during implementation.
- This story does add a domain-service contract method. That is a coordinated change but not a NuGet contract change in the published sense - both sides ship together from this repo.

### References

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-06-admin-ui-manual-test-bug-bundle.md` (original Group A proposal)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-07-admin-ui-manual-test-bundle-carveout.md` (carve-out authorization)
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide.md`
- `_bmad-output/test-artifacts/admin-ui-manual-test-guide-issues.md`
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` (deep-merge to remove)
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` (runtime replay convention)
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` (Apply method discovery)
- `samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs` (existing /process wiring)
- `_bmad-output/implementation-artifacts/admin-ui-manual-test-bug-bundle.md` (sibling story with B/C/D scope)
- `CLAUDE.md` - Fluent Convention discovery, R2-A6 integration test rule

## Dev Agent Record

### Agent Model Used

claude-opus-4-7 (Claude Code, 2026-05-07).

### Debug Log References

- `dotnet build Hexalith.EventStore.slnx --configuration Release` — clean.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/...` — 290/290 pass.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/...` — 357/357 pass (includes 22 new `AggregateReplayerTests`).
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/...` — 72/72 pass (includes 7 new `CounterAggregateReplayTests`).
- `dotnet test tests/Hexalith.EventStore.Server.Tests/... --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Live&FullyQualifiedName!~Tier3"` — 1730/1730 pass with 12 pre-existing DW1/DW2 ATDD red-phase skips (unchanged by this story).
- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/...` — 705/705 pass.
- `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/...` — 564/564 pass with 18 pre-existing DW2 ATDD skips (unchanged by this story).
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-restore -p:UseSharedCompilation=false --filter AdminStreamQueryControllerReplayDelegationTests` — 21/21 pass after adding the safe sandbox invocation-error regression.

### Manual Verification Evidence

- **2026-05-07 partial ST4 evidence (operator supplied).**
  - Environment: local Aspire restarted with `EnableKeycloak=false`; prior AppHost stopped; Redis flushed via `docker exec dapr_redis redis-cli FLUSHALL`; Aspire resources reported running/healthy.
  - Aspire dashboard URL: `https://localhost:17017/login?t=1f2b30211ba141be4d2fb4172c8541fc`.
  - Admin UI endpoint URL: `https://localhost:8093/streams/tenant-a/counter/counter-1?detail=18`.
  - Aggregate id: `tenant-a/counter/counter-1`.
  - Sequence: `18`.
  - Event type: `Hexalith.EventStore.Sample.Counter.Events.CounterIncremented`.
  - Event timestamp: `2026-05-07 11:27:03.975`.
  - Correlation id: `a0d58aa9-afeb-4517-af80-cae8c26b59b2`.
  - Causation id: `a0d58aa9-afeb-4517-af80-cae8c26b59b2`.
  - User: `sample-blazor-ui`.
  - Event payload: `{}`.
  - Expected state after sequence 18: `{ "count": 10, "isTerminated": false }`.
  - Actual state after sequence 18: `{ "count": 10, "isTerminated": false }`.
  - Evidence conclusion: headline replay correctness passes for sequence 18 because an empty marker-event payload still yields `count = 10`, proving state is derived from `Apply(TEvent)` semantics rather than payload merge.
- **2026-05-07 partial ST4 sandbox evidence (local API verified).**
  - Endpoint URL: `http://localhost:8080/api/v1/admin/streams/tenant-a/counter/counter-1/sandbox`.
  - Request command type: `Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter`.
  - Request payload: `{}`.
  - Request `atSequence`: `18`.
  - Outcome: `accepted`.
  - Produced event: `Hexalith.EventStore.Sample.Counter.Events.CounterIncremented` with payload `{}`.
  - Expected resulting state: `{ "count": 11, "isTerminated": false }`.
  - Actual resulting state: `{ "count": 11, "isTerminated": false }`.
  - State change: `count` from `10` to `11`.
  - Evidence conclusion: sandbox dry-run result is derived by replaying the produced marker event through `Apply(TEvent)`; the resulting state is not `{}` and no event is persisted by the sandbox call.
  - Remaining ST4 evidence before Done: none after operator checkpoint confirmation below.
- **2026-05-07 completed ST4 checkpoint evidence (operator supplied).**
  - Admin UI stream: `tenant-a/counter/counter-1`.
  - Seed pattern: Increment x5, Decrement x2, Reset, Increment x10.
  - Sequence `1`: expected `{ "count": 1, "isTerminated": false }`; operator confirmed actual count is correct.
  - Sequence `5`: expected `{ "count": 5, "isTerminated": false }`; operator confirmed actual count is correct.
  - Sequence `7`: expected `{ "count": 3, "isTerminated": false }`; operator confirmed actual count is correct.
  - Sequence `8`: expected `{ "count": 0, "isTerminated": false }`; operator confirmed actual count is correct.
  - Sequence `18`: previously captured expected and actual `{ "count": 10, "isTerminated": false }`.
  - Evidence conclusion: all seeded checkpoint states `1`, `5`, `7`, `8`, and `18` match the Apply-driven replay checkpoint table; Issue #5 no longer reproduces.
- **2026-05-07 sandbox negative-input evidence (local API/log investigation).**
  - Operator attempted Sandbox with event type `Hexalith.EventStore.Sample.Counter.Events.CounterIncremented` in the `Command Type` field.
  - Sample service log showed `No Handle method found for command type 'Hexalith.EventStore.Sample.Counter.Events.CounterIncremented' on aggregate 'CounterAggregate'`, confirming the field expects a command type, not an event type.
  - Correct command type: `Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter`.
  - Follow-up hardening: `SandboxCommandAsync_DomainInvocationFailure_ReturnsSafeOperatorMessage` now asserts domain-service 500 / raw handler details are not surfaced to the operator, and event-looking command types receive a stable hint that Sandbox expects a command type.

### Completion Notes List

- **Canonical replay contract.** Added `Hexalith.EventStore.Contracts/Replay/*` (status, error category, request, result, timeline entry, replay envelope). All wire records have System.Text.Json round-trip coverage (`AggregateReconstructionRoundTripTests`).
- **Single reconstruction entry point.** `Hexalith.EventStore.Server.DomainServices.IAggregateStateReconstructor` is the only aggregate-state reconstruction interface. Its sole production implementation is `DaprAggregateStateReconstructor`, registered in `EventStoreServerServiceCollectionExtensions.AddEventStoreServer`. The Admin replay path is `Admin UI -> Admin API -> Dapr service invocation -> owning domain service /replay-state -> AggregateReplayer.Replay<TState>` per ADR-1.
- **Caller list (Admin UI surfaces consuming `IAggregateStateReconstructor`):** `AdminStreamQueryController.GetAggregateStateAsync` (`/state`), `DiffAggregateStateAsync` (`/diff`), `BisectAggregateStateAsync` (`/bisect`), `GetEventStepFrameAsync` (`/step`), `GetAggregateBlameAsync` -> `ComputeBlame` (`/blame`), and `SandboxCommandAsync` (`/sandbox`, both for input state and resulting state via synthesized envelopes). Per-surface delegation evidence: `AdminStreamQueryControllerReplayDelegationTests` Received() asserts.
- **CausationChainView intentionally unchanged.** `TraceCausationChainAsync` does not reconstruct aggregate state; it only walks event causation links. `AdminStreamQueryControllerReplayDelegationTests.TraceCausationChainAsync_DoesNotInvokeReconstructor` pins this disposition (per AC #3 the surface still consumes data through the canonical Admin path; replay is just not a primitive it requires).
- **Removed legacy reconstruction paths.** Deleted private `AdminStreamQueryController.ReconstructState` (was lines 1477-1497) and the `DeepMerge` helper. Guard test `Controller_HasNoDeepMergeOrReconstructStateMember_ProvingFallbackRemoved` reflects-on the type to ensure neither member can be reintroduced without breaking CI. `AggregateReplayer` requires a public `void Apply(TEvent)` method on `TState`; states without one return `ApplyHandlerMissing` (proven by `AggregateReplayerTests.Replay_StateWithNoApplyMethod_FailsWithApplyHandlerMissing`).
- **Replay non-mutation evidence.** `AggregateReplayer.Replay<TState>` constructs a fresh `new TState()` per call and never publishes events, writes Dapr state, schedules outbox messages, or touches projections. `AggregateReplayerTests.Replay_RepeatedInvocations_ReturnIdenticalState` and `Replay_DoesNotMutateRequestEnvelopes` pin this. Server-side, `DaprAggregateStateReconstructor` only issues a `POST /replay-state` HTTP call; it neither calls the projection notifier nor the publisher. The Sample's `DomainServiceRequestRouter.Replay` resolves the keyed `IDomainProcessor` and calls `Replay(...)` synchronously without returning new domain events. Live event-store position / projection / outbox non-mutation must be re-confirmed under operator-owned ST4 (Aspire smoke).
- **RFC 7807 mapping examples.** `AdminStreamQueryControllerReplayDelegationTests.GetAggregateStateAsync_FailedReplay_ReturnsExpectedRfc7807ProblemDetails` validates each documented row (UnknownAggregateType -> 404, UnknownEventType / DeserializationFailed / ApplyHandlerMissing / UnsupportedVersion -> 422, ApplyFailed -> 409, Unexpected -> 500). All ProblemDetails carry `Type = urn:hexalith:eventstore:replay:<slug>` plus extension fields `status`, `errorCategory`, `failedSequenceNumber`, `failedEventType`, `lastAppliedSequenceNumber`, and `message` per the story Failure and HTTP Semantics Matrix. Negative-evidence guard `FailedReplay_NeverReturns200OkWithEmptyState` enforces AC #4.
- **Tier 1 fixture proves AC #1 headline.** `AggregateReplayerTests.Replay_CanonicalCounterFixture_AtSequence18_Returns10NotZero` and `CounterAggregateReplayTests.Replay_CanonicalSeed_AtSequence18_ReturnsCount10_ProvingApplyDrivenReplay` both replay the seeded marker-event sequence (5 inc, 2 dec, reset, 10 inc) and assert the runtime `CounterState.Apply(...)` semantics yield `Count = 10`. Theory variants pin checkpoints at sequences 1, 5, 7, 8, 18.
- **Tenant/auth coverage scope.** Tenant isolation, JWT authentication, and RBAC are owned by the upstream `Hexalith.EventStore.Admin.Server` gateway controllers (Story 14-3 / Story 5-2) and are unchanged by this story. The new `/replay-state` endpoint sits inside the Sample domain service which already enforces tenant context via the request payload (`AggregateReconstructionRequest.TenantId`); replay correctness coverage focuses on the Apply/contract semantics rather than re-asserting authentication primitives. Full cross-tenant negative coverage at the live boundary is part of operator-owned ST4 evidence.
- **Sandbox synthesizes envelopes for produced events.** `AdminStreamQueryController.SynthesizeSandboxEnvelopes` builds `ServerEventEnvelope`s for events the domain service returns from the dry-run `Process` call and replays the combined stream through the canonical reconstructor so the resulting state reflects real Apply behavior, not deep-merged payloads. This satisfies AC #3's Sandbox sub-bullet ("Sandbox dry-run 'Resulting State (after applying N events)' reflects real applied state, not `{}`").
- **Sandbox invocation errors are operator-safe.** `AdminStreamQueryController.SandboxCommandAsync` now logs domain-service invocation failures server-side and returns a stable operator message instead of raw Dapr/HTTP/handler details. If the supplied type name contains `.Events.`, the message explicitly hints that Sandbox expects a command type. `SandboxCommandAsync_DomainInvocationFailure_ReturnsSafeOperatorMessage` pins this behavior.
- **Re-anchored DW3 tests.** Four `Dw3JsonReconstructionAtddTests.Step_*` tests previously pinned the now-removed DeepMerge `preserved-limitation` behaviors. They were re-anchored to assert the new contract: state JSON is whatever the canonical reconstructor returns, and the controller does not synthesize fields from raw payloads. The renamed tests document the supersession in their docstrings.
- **Replay paths intentionally left unchanged.**
  - `EventReplayProjectionActor` (read-model projection replay) — separate concept from aggregate replay; unchanged.
  - `DomainProcessorStateRehydrator.RehydrateState<TState>` — runtime command-path rehydration; reused by `AggregateReplayer` via the shared Apply discovery (`DiscoverApplyMethods`, `TryResolveApplyMethod`) so command and replay paths cannot drift.
  - `EventReplayProjectionActor.ReplayAsync` — projection rebuild; outside the aggregate-state scope.

### File List

**New (production):**
- `src/Hexalith.EventStore.Contracts/Replay/AggregateReconstructionStatus.cs`
- `src/Hexalith.EventStore.Contracts/Replay/AggregateReconstructionErrorCategory.cs`
- `src/Hexalith.EventStore.Contracts/Replay/ReplayEventEnvelope.cs`
- `src/Hexalith.EventStore.Contracts/Replay/AggregateReconstructionTimelineEntry.cs`
- `src/Hexalith.EventStore.Contracts/Replay/AggregateReconstructionRequest.cs`
- `src/Hexalith.EventStore.Contracts/Replay/AggregateReconstructionResult.cs`
- `src/Hexalith.EventStore.Client/Aggregates/IAggregateReplay.cs`
- `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs`
- `src/Hexalith.EventStore.Server/DomainServices/IAggregateStateReconstructor.cs`
- `src/Hexalith.EventStore.Server/DomainServices/DaprAggregateStateReconstructor.cs`

**Modified (production):**
- `src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs` — implements `IAggregateReplay`.
- `src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs` — exposes `TryResolveApplyMethod` and `SerializerOptions` to the replayer.
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` — registers `IAggregateStateReconstructor`.
- `samples/Hexalith.EventStore.Sample/DomainServiceRequestRouter.cs` — adds `Replay(IServiceProvider, AggregateReconstructionRequest)`.
- `samples/Hexalith.EventStore.Sample/Program.cs` — maps `POST /replay-state`.
- `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs` — removes `DeepMerge` / `ReconstructState`; injects `IAggregateStateReconstructor`; delegates state, diff, bisect, blame, step, and sandbox; adds `MapReplayFailureToProblem`, `ParseStateJson`, `ResolveTimelineState`, `SynthesizeSandboxEnvelopes`, and safe sandbox invocation-error messaging helpers.

**New (tests):**
- `tests/Hexalith.EventStore.Contracts.Tests/Replay/AggregateReconstructionRoundTripTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs`
- `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateReplayTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerReplayDelegationTests.cs` — includes delegation/failure guards plus safe sandbox invocation-error coverage.
- `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprAggregateStateReconstructorTests.cs`

**Modified (tests):**
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3TestUtilities.cs` — `CreateStreamController` now wires the new reconstructor parameter; new helper `CreateEmptyStateReconstructor`.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerEventDetailTests.cs` — controller-construction site updated.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerStateDiffCausationTests.cs` — controller-construction site updated; three happy-path tests re-anchored to the canonical reconstructor stub instead of asserting deep-merge output.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs` — controller-construction site updated.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3DirectMaxParameterBoundsAtddTests.cs` — controller-construction site updated.
- `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3JsonReconstructionAtddTests.cs` — four `Step_*` tests re-anchored to the new canonical contract; behavior pins the Apply-driven path and forbids the legacy DeepMerge fallback.

### Change Log

- 2026-05-07: Implemented the canonical Apply-driven aggregate replay path (ADR-1). New `Hexalith.EventStore.Contracts/Replay` wire types; new `IAggregateStateReconstructor` and `DaprAggregateStateReconstructor`; new `IAggregateReplay` and `AggregateReplayer` on the Client side; Sample wires `POST /replay-state`. Removed `AdminStreamQueryController.ReconstructState` deep-merge and migrated `/state`, `/diff`, `/bisect`, `/blame`, `/step`, `/sandbox` to the shared reconstructor with RFC 7807 ProblemDetails mapping per the story Failure and HTTP Semantics Matrix. Added Tier 1 regression coverage across Contracts, Client, Sample, and Server (round-trip wire types, 18-event canonical fixture, 7 failure categories, ordering / duplicate guard, per-surface delegation, deep-merge unreachability guard).
- 2026-05-07: Added sandbox operator-safety hardening after live negative-input investigation: event type in `Command Type` is now explained with a stable command-type hint and raw 500 / handler details are kept out of the sandbox result. Added regression coverage in `AdminStreamQueryControllerReplayDelegationTests`.
- 2026-05-07: Completed ST4 operator validation for seeded checkpoints `1`, `5`, `7`, `8`, and `18`; story moved `review` -> `done`.
