# Story 22.6: Stream Replay/Read APIs and Projection Rebuild Checkpoints

Status: review

Context created: 2026-05-13
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
Epic: Epic 22 - Public Gateway and Downstream Integration Contracts
Scope: FR99-FR101, with dependency awareness for Stories 22.1-22.5 public gateway contracts, projection adapter, gateway authorization, query policy, and publish guarantees.

## Story

As a projection owner,
I want per-tenant resumable stream replay APIs,
so that Parties projections can rebuild from EventStore streams safely.

## Stream Replay Contract

- EventStore must expose public, tenant-scoped stream read/replay APIs through `Hexalith.EventStore.Contracts` and high-level `Hexalith.EventStore.Client` methods; downstream services must not depend on `Hexalith.EventStore.Server`, admin-only controllers, or raw DAPR state keys.
- Replay reads are scoped by tenant, domain, aggregate, sequence range, checkpoint, and continuation token. The API must make aggregate-specific reads and domain-wide projection rebuild reads explicit rather than overloading the existing admin stream debugging endpoints.
- Checkpoints are monotonic and idempotent: a rebuild can advance from sequence N to sequence M, retries must never lower a persisted checkpoint, and duplicate delivery remains allowed.
- Reading a replay page must not itself imply checkpoint advancement. Rebuild progress advances only after the page has been accepted by the projection apply path; partial page failure, cancellation, corrupt events, unreadable protected data, or domain projection rejection must leave the checkpoint at the last safely applied sequence.
- Operator-safe rebuild flow includes start, progress, pause, resume, cancel, retry, terminal success, and terminal failure reason visibility. The story can use existing admin projection commands as a starting point but must make their EventStore-side behavior real and observable.
- Stream replay must preserve tenant isolation before any actor state access. Do not read event state by correlation ID alone, do not query the state store directly from downstream services, and do not bypass actor state boundaries.
- Replay APIs must not expose raw state-store keys, protected payload material, connection strings, DAPR addresses, or untrusted user display names in logs, ProblemDetails, docs examples, or tests.
- Story 22.7a-22.7d own payload/snapshot protection and unreadable protected data policy. This story may define replay metadata and safe failure placeholders, but protection policy changes must be deferred unless required to keep replay safe.

## Public API and Lifecycle Guardrails

- Public stream read/replay APIs and DTOs are downstream-facing. They belong in `Hexalith.EventStore.Contracts`, `Hexalith.EventStore.Client`, and `Hexalith.EventStore.Testing`; runtime adapters may wrap `IAggregateActor`, `EventStreamReader`, and `ProjectionCheckpointTracker` behind that public boundary.
- Admin stream debugging routes, admin projection controls, DAPR actor IDs, and state-store keys are not the downstream replay contract. Operator rebuild lifecycle APIs may adapt existing admin services, but public docs and examples must use Contracts/Client APIs only.
- Candidate public contract names are `StreamReadRequest`, `StreamReplayRequest`, `StreamReplayPage`, `ReplayContinuationToken`, `ProjectionRebuildOperation`, and `ProjectionRebuildCheckpoint`. Final names may differ, but ST0 must record the chosen equivalents before production code edits.
- Continuation tokens must be opaque, tenant-bound, scope-bound, tamper-safe or fail-closed, non-key-bearing, and safe to log only as redacted identifiers. They must not expose DAPR actor IDs, state-store keys, raw JSON state, sequence payload offsets, protected metadata, or user-controlled display values.
- Continuation validation must bind the token to a canonical request shape: tenant, domain, optional aggregate, projection or rebuild scope, route/API version, page-size constraints, and sequence cursor. If token expiry is deferred, the public taxonomy and tests must not advertise an `expired` reason until expiry behavior exists.
- Tenant/domain/aggregate authorization and replay scope validation must complete before continuation-token decoding can resolve actor identity, checkpoint identity, or actor state access. Tests must prove denied or cross-tenant requests do not call `IAggregateActor.GetEventsAsync`, projection services, checkpoint stores, or DAPR state APIs.
- Checkpoint ownership is scoped by tenant, domain, projection name, optional aggregate scope, and rebuild operation ID. Checkpoint writes must use optimistic concurrency or equivalent ETag/compare-and-set semantics, never blind last-write-wins.
- Checkpoint advancement is monotonic: duplicate page application is idempotent, stale or out-of-order advancement cannot lower progress, and retry after checkpoint-store failure must not falsely report rebuild success.
- Operator-triggered rebuild and normal polling must have an explicit coordination rule before code review: one may pause, lease, reject, or serialize the other, but they must not race silently against the same projection checkpoint.
- Operator rebuild lifecycle states are bounded to `not-started`, `running`, `pausing`, `paused`, `resuming`, `canceling`, `canceled`, `retrying`, `succeeded`, and `failed`, or a documented equivalent set. Legal transitions, idempotent retry behavior, terminal states, and safe failure reason exposure must be recorded before review.

## Current Implementation Intelligence

- `IAggregateActor.GetEventsAsync(long fromSequence)` already returns persisted server `EventEnvelope[]` ordered after the exclusive lower bound and throws on missing or corrupt event state.
- `AggregateActor.GetEventsAsync` currently derives the aggregate identity from the actor ID and reads event keys through the actor `StateManager`; it does not provide a public HTTP or client-package stream API.
- `EventStreamReader.RehydrateAsync` implements snapshot-aware command-time rehydration and reads actor event keys sequentially. Reuse its invariants where appropriate, but do not use command rehydration as a public replay API by accident.
- `ProjectionUpdateOrchestrator.DeliverProjectionAsync` currently calls `GetEventsAsync(0)` for full replay, sends `ProjectionRequest` to the domain service `/project` endpoint, stores projection state in `EventReplayProjectionActor`, then advances `ProjectionCheckpointTracker`.
- `ProjectionCheckpointTracker.SaveDeliveredSequenceAsync` already uses DAPR state ETags with a bounded retry loop and max-sequence semantics. This is the closest existing checkpoint behavior and should be extended or wrapped instead of duplicating checkpoint storage.
- `ProjectionCheckpointTracker.TrackIdentityAsync` registers aggregate identities by tenant/domain pages for polling. This can support domain-scoped rebuild enumeration, but the story must cover corrupted index behavior and per-tick limits honestly.
- `ProjectionPollerService` processes tracked identities with a per-tick cap and in-process de-duplication. Rebuild APIs must avoid conflicting semantics between normal polling and an operator-triggered rebuild.
- Admin stream endpoints under `AdminStreamQueryController` use `GetEventsAsync(0)` for state, diff, causation, timeline, blame, step, bisect, sandbox, and trace-map debugging. These are admin/debug surfaces, not downstream public replay contracts.
- `DaprStreamQueryService` delegates stream reads back to EventStore through DAPR service invocation because actor state keys are not safe to access directly. Preserve this boundary for new public APIs.
- `DaprProjectionCommandService` and `AdminProjectionsController` expose pause/resume/reset/replay facades today, but their backing service currently forwards to EventStore endpoints that must exist and return meaningful operation results before docs can claim operator-safe rebuild.
- `Hexalith.EventStore.Contracts.Replay` already contains aggregate reconstruction DTOs for the domain service `/replay-state` path. They are for aggregate state reconstruction, not a complete public stream read/rebuild checkpoint API.
- `Hexalith.EventStore.Client.Gateway.EventStoreGatewayClient` currently has command and query methods only. FR84 requires validation, command status, replay, and stream-read client methods; Story 22.6 should add the replay/stream slice without reopening command/query behavior from Story 22.1.
- `Hexalith.EventStore.Testing` already has event envelope builders, fake gateway client patterns, fake aggregate actors, and in-memory state helpers. Reuse those before creating new test infrastructure.

## Acceptance Criteria

1. **Public stream read contracts exist in Contracts, Client, and Testing.**
   - Given a downstream bounded context references EventStore packages
   - When it needs historical events for projection rebuild
   - Then `Hexalith.EventStore.Contracts` exposes stable request/response DTOs for stream reads and replay pages with tenant, domain, optional aggregate, sequence range, checkpoint, continuation token, page size, and metadata fields.
   - And `Hexalith.EventStore.Client` exposes typed stream read/replay methods that map HTTP success, continuation, cancellation, and ProblemDetails failures.
   - And `Hexalith.EventStore.Testing` exposes deterministic fakes/builders for success, empty stream, continuation, invalid range, unauthorized tenant, missing stream, checkpoint conflict, paused rebuild, cancelled rebuild, and unavailable EventStore paths.
   - And ST0 records the selected DTO/client method names and route shapes, including any intentionally deferred naming or version-prefix decision.

2. **Stream reads are tenant/domain/aggregate isolated before state access.**
   - Given a caller requests stream events
   - When tenant, domain, aggregate, sequence range, checkpoint, or continuation token input is malformed, unauthorized, stale, or cross-tenant
   - Then EventStore rejects with stable ProblemDetails before reading actor state.
   - And every actor proxy or state access path is derived from a validated `AggregateIdentity`, never from raw state-store keys or caller-supplied actor IDs.
   - And invalid, unauthorized, cross-tenant, or forbidden-scope requests are proven not to decode continuation tokens into actor identities, create actor proxies, access checkpoints, or call `IAggregateActor.GetEventsAsync`.

3. **Read paging and continuation semantics are explicit.**
   - Given a stream contains more events than one response can safely return
   - When the caller reads by sequence range or checkpoint
   - Then EventStore returns a deterministic page ordered by sequence number, a bounded `nextContinuationToken` when more data exists, and metadata for `fromSequence`, `toSequence`, `lastSequenceReturned`, `latestSequence`, `eventCount`, and truncation status.
   - And continuation tokens are opaque, tenant/domain scoped, tamper-resistant or validated fail-closed, and never reveal state-store key material.
   - And continuation tokens are rejected when the tenant, domain, aggregate, projection scope, route/API version, page size, or original query shape does not match the token-bound request.
   - And invalid, tampered, expired, stale, wrong-tenant, wrong-domain, wrong-aggregate, and changed-query-shape continuation tokens map to stable ProblemDetails reason codes.

4. **Projection rebuild checkpoint advancement is monotonic and idempotent.**
   - Given a projection rebuild advances after applying a page
   - When the same page is retried, applied out of order, paused, resumed, cancelled, or races with another worker
   - Then checkpoint state never regresses, duplicate delivery is safe, and conflicts return stable reason codes instead of corrupting progress.
   - And checkpoint records include tenant, domain, projection name or aggregate scope, last applied sequence, status, updated timestamp, optional operation ID, and sanitized failure reason.
   - And checkpoint advancement occurs only after the projection apply path accepts the page; partial page failure, projection rejection, protected payload unreadability, cancellation, or timeout records safe status/failure metadata without advancing beyond the last applied sequence.
   - And stale checkpoint writes, concurrent rebuild workers, duplicate page retries, pause/resume/cancel races, and checkpoint-store unavailability are covered by focused tests.

5. **Operator rebuild lifecycle is observable and controllable.**
   - Given an operator starts a projection rebuild
   - When it progresses, pauses, resumes, cancels, retries, completes, or fails
   - Then admin APIs, CLI/MCP service layers, and docs expose consistent status, progress, and failure reason behavior.
   - And pause/resume/cancel transitions are idempotent, safe under retries, and do not require domain services to read EventStore state-store internals.
   - And operator rebuild and background projection polling have a tested conflict policy such as pause, lease, serialize, or reject with a stable reason code.
   - And the story must not claim a full production rebuild control plane unless the public/operator lifecycle states, transition rules, and evidence are implemented or explicitly deferred.

6. **Domain-service rebuild documentation uses only public APIs.**
   - Given a domain service rebuilds projections
   - When implementation guidance is followed
   - Then docs show EventStore client usage for paged stream reads and checkpoint advancement, plus the domain `/project` or projection apply path.
   - And docs explicitly forbid reading DAPR actor state keys, EventStore internal server types, admin-only debug endpoints, or state-store indexes directly.

7. **Failure taxonomy is stable and safe.**
   - Given stream read or rebuild fails
   - When the cause is malformed range, invalid continuation token, tenant/RBAC denial, missing stream, missing event, corrupt event, unknown event type, protected payload unreadable, checkpoint conflict, checkpoint store unavailable, domain projection failure, timeout, pause, cancel, or service unavailable
   - Then API, client, docs, logs, and tests use stable ProblemDetails type URIs/reason codes without payload disclosure.
   - And the taxonomy includes unauthorized tenant, forbidden replay scope, invalid range, invalid continuation, token/request mismatch, expired continuation only if token expiry is implemented, missing stream, missing event, corrupt event, protected payload unavailable, projection apply rejected, checkpoint conflict, stale checkpoint, checkpoint unavailable, poller/rebuild conflict, rebuild operation not found, rebuild canceled, retryable transient failure, and service unavailable.

8. **Replay and rebuild proof is covered by focused tests and docs.**
   - Unit tests pin DTO serialization, client mapping, continuation validation, ProblemDetails mapping, checkpoint monotonicity, and operator lifecycle transitions.
   - Server/controller tests prove state access is blocked on invalid or unauthorized input before actor calls.
   - Integration or documented manual proof covers paged stream reads and rebuild progress through the Aspire/DAPR topology.
   - Test evidence names the Contracts, Client, Testing, and Server/controller/checkpoint scenarios that prove auth-before-state, no-leak behavior, continuation handling, checkpoint races, operator lifecycle, and client/fake parity.
   - Dev Agent Record, File List, Verification Status, and Change Log are updated before moving the story to review.

## Tasks / Subtasks

- [x] **ST0 - Baseline current replay/read and checkpoint behavior.** (AC: 1, 2, 3, 4, 5, 6, 7)
    - [x] Read this story, Epic 22, PRD FR99-FR101, ADR-P9, Stories 22.1-22.5, and `_bmad-output/project-context.md` before code edits.
    - [x] Inventory `IAggregateActor.GetEventsAsync`, `AggregateActor.GetEventsAsync`, `ReadEventsRangeAsync`, `EventStreamReader`, `ProjectionUpdateOrchestrator`, `ProjectionCheckpointTracker`, `ProjectionPollerService`, `AdminStreamQueryController`, `DaprStreamQueryService`, `AdminProjectionsController`, and `DaprProjectionCommandService`.
    - [x] Inventory public package surfaces: `Hexalith.EventStore.Contracts.Replay`, command/query DTOs, `IEventStoreGatewayClient`, `EventStoreGatewayClient`, and Testing fakes/builders.
    - [x] Record a decision table for public stream API shape, admin/debug endpoint boundaries, checkpoint key ownership, continuation token format, operator status model, and failure reason codes.
    - [x] Record selected names or equivalents for `StreamReadRequest`, `StreamReplayRequest`, `StreamReplayPage`, `ReplayContinuationToken`, `ProjectionRebuildOperation`, and `ProjectionRebuildCheckpoint`.
    - [x] Record which exact route(s) are public downstream replay routes versus operator/admin rebuild routes, and which existing admin/debug endpoints remain out of the downstream contract.
    - [x] Record whether checkpoint advancement is client-driven, operator-service-driven, or projection-apply-driven, and name the exact acceptance point after which progress may be persisted.
    - [x] Record the normal-poller versus operator-rebuild coordination policy before implementing checkpoint writes.

- [x] **ST1 - Add public stream read/replay DTOs and client methods.** (AC: 1, 3, 6, 7)
    - [x] Add Contracts DTOs for stream page requests/responses, event page items, checkpoint/progress metadata, and stable reason-code constants.
    - [x] Define continuation tokens as opaque, tenant-bound, scope-bound, tamper-safe or fail-closed, non-key-bearing values; document unsupported or deferred token-expiry behavior explicitly.
    - [x] Include a stable token/request mismatch reason for tokens bound to a different tenant, domain, aggregate, projection scope, route/API version, page-size constraint, or original query shape.
    - [x] Keep existing aggregate reconstruction DTOs intact; do not rename or repurpose `AggregateReconstructionRequest`, `AggregateReconstructionResult`, or `ReplayEventEnvelope` unless compatibility is explicitly handled.
    - [x] Add `IEventStoreGatewayClient` and `EventStoreGatewayClient` methods for stream reads/replay pages using the same JSON options, ProblemDetails mapping, typed cancellation, and strong validation style as command/query methods.
    - [x] Add Testing builders/fakes so downstream modules can test continuation, empty pages, authorization failures, and checkpoint conflicts without a live EventStore.

- [x] **ST2 - Implement EventStore HTTP endpoints for stream reads.** (AC: 2, 3, 7)
    - [x] Add public non-admin route(s) for stream reads; keep admin stream debugging routes under `api/v1/admin/streams`.
    - [x] Validate tenant/domain/aggregate/range/page-size/continuation inputs before creating actor proxies.
    - [x] Enforce gateway-owned tenant/RBAC checks consistent with Story 22.3 before state access.
    - [x] Prove denied, cross-tenant, invalid-continuation, invalid-range, and forbidden-scope requests do not call actor proxies, projection services, checkpoint stores, or direct DAPR state APIs.
    - [x] Verify continuation token metadata without resolving actor IDs, checkpoint keys, or projection identities until tenant/domain/scope authorization succeeds.
    - [x] Use `AggregateIdentity` and `IAggregateActor.GetEventsAsync` for aggregate-specific reads; do not use direct `DaprClient.GetStateAsync` against actor event keys.
    - [x] Return deterministic sequence ordering, bounded pages, continuation metadata, and safe empty-stream behavior.
    - [x] Map missing/corrupt events and unavailable actors to stable ProblemDetails without leaking payload bytes or state keys.

- [x] **ST3 - Define and implement checkpoint/progress storage.** (AC: 4, 5, 7)
    - [x] Decide whether to extend `ProjectionCheckpointTracker` or add a small rebuild-specific checkpoint service that reuses its ETag/max-sequence pattern.
    - [x] Store checkpoint records under a validated tenant/domain/projection scope with operation ID, status, last applied sequence, updated timestamp, and sanitized failure reason.
    - [x] Use ETag-based optimistic concurrency for checkpoint writes; never perform blind last-write-wins updates for checkpoint advancement.
    - [x] Ensure duplicate or stale checkpoint advancement is idempotent and cannot lower progress.
    - [x] Advance checkpoints only after the projection apply path returns an accepted/success outcome for the page; record failure status without advancement for partial page failure, projection rejection, timeout, cancellation, corrupt stream, or protected payload unreadability.
    - [x] Add race tests for duplicate progress, out-of-order checkpoint writes, stale ETags, concurrent workers, pause/resume/cancel interaction, retry after failure, and checkpoint-store unavailable behavior.
    - [x] Add reason codes for checkpoint conflict, checkpoint drift, checkpoint unavailable, paused, cancelled, domain failure, and corrupt stream.

- [x] **ST4 - Wire operator rebuild lifecycle APIs.** (AC: 5, 7)
    - [x] Make EventStore-side endpoints behind admin projection pause/resume/reset/replay return meaningful `AdminOperationResult` values and stable HTTP semantics.
    - [x] Ensure `DaprProjectionCommandService` forwards JWT context and maps failures without hiding operator-relevant reason codes.
    - [x] Define status responses for queued/running/paused/cancelled/completed/failed rebuild operations.
    - [x] Record legal lifecycle transitions and idempotent behavior for start, progress, pause, resume, cancel, retry, success, failure, and repeated terminal-state calls.
    - [x] Keep normal projection polling and operator-triggered rebuild from racing silently; document and test the conflict behavior.
    - [x] Implement or explicitly defer a single-writer/lease/pause/reject rule for rebuild operations that target a projection already being updated by normal polling.

- [x] **ST5 - Document downstream rebuild usage.** (AC: 6, 8)
    - [x] Add or update `docs/reference/stream-replay-api.md` with route shapes, DTO fields, continuation behavior, checkpoint semantics, examples, and ProblemDetails reason codes.
    - [x] Update projection/domain-service guidance to show rebuilds using EventStore public client APIs rather than DAPR state-store reads.
    - [x] Update configuration/admin docs for rebuild lifecycle commands, progress inspection, pause/resume/cancel, and failure recovery.
    - [x] Add explicit "do not use" guidance for Server/admin/debug types, DAPR actor IDs, DAPR state keys, raw checkpoint keys, and protected payload material in downstream projection rebuilds.
    - [x] Cross-link docs from package ownership guidance created by Story 22.1 and publishing guarantees from Story 22.5.

- [x] **ST6 - Add focused test coverage.** (AC: 1, 2, 3, 4, 5, 7, 8)
    - [x] Add Contracts tests for DTO serialization, validation assumptions, and reason-code constants.
    - [x] Add Client tests for success, continuation, not found, malformed continuation, unauthorized, unavailable, typed cancellation, and ProblemDetails mapping.
    - [x] Add Testing tests for new fake/builder behavior.
    - [x] Add Server controller/service tests proving invalid requests never call `IAggregateActor.GetEventsAsync`.
    - [x] Add checkpoint service tests for monotonic advancement, retry idempotency, stale write conflict, pause/resume/cancel, and store-unavailable behavior.
    - [x] Add negative advancement tests proving projection apply rejection, partial page failure, cancel, timeout, corrupt event, protected payload unavailable, and checkpoint-store failure do not advance beyond the last safely applied sequence.
    - [x] Add no-leak tests for DTO serialization, ProblemDetails, logs/activity tags, continuation tokens, docs examples, and test artifacts so state-store keys, actor IDs, payload bytes, protected data, DAPR addresses, tokens, stack traces, and user-controlled display names are absent.
    - [x] Add client/fake parity tests for success page, empty page, continuation, malformed continuation, expired/stale continuation, invalid range, unauthorized tenant, forbidden scope, missing stream, checkpoint conflict, canceled rebuild, paused rebuild, and transient unavailable paths.
    - [x] Add integration/manual proof for paged replay and rebuild progress only when Docker and Aspire/DAPR resources are running; runtime proof was recorded as blocked by the local Aspire CLI/AppHost version mismatch.

- [x] **ST7 - Validate and record evidence.** (AC: 8)
    - [x] Run `dotnet test tests/Hexalith.EventStore.Contracts.Tests`.
    - [x] Run `dotnet test tests/Hexalith.EventStore.Client.Tests`.
    - [x] Run `dotnet test tests/Hexalith.EventStore.Testing.Tests` if Testing fakes/builders change.
    - [x] Run focused Server tests with filters around stream/replay/projection/checkpoint behavior; avoid broad Server.Tests first because this repo has known CA2007 warning-as-error risk.
    - [x] Run integration tests only with Docker and a running Aspire/DAPR environment; not run because the apphost could not start with the installed Aspire CLI.
    - [x] Run generated API docs and markdown validation if public XML docs or reference docs change.
    - [x] Record exact test names proving auth-before-state, continuation token safety, checkpoint monotonicity/concurrency, operator lifecycle transitions, no-leak assertions, and Contracts/Client/Testing parity.
    - [x] Update Dev Agent Record, File List, Verification Status, and Change Log.

### Review Findings

#### Code review run on 2026-05-16 (Opus 4.7 1M — fifth pass against `fb36737c..HEAD`, 17 source/test files, +1399/-188)

_Three adversarial layers ran in parallel: Blind Hunter (diff-only), Edge Case Hunter (diff + project), Acceptance Auditor (diff + spec + CLAUDE.md). After dedup and triage: **10 decision-needed (all resolved per recommendation), 40 patch (30 original + 10 from resolved decisions), 4 defer, 6 dismiss**. **Headline: of the 30 outstanding pass-4 items, only 2 landed (M1-4P empty-stream short-circuit, H9-4P EventId renumber). Two items were actively WORSENED by commit `7a077da2`:** H2-4P (active-index write reordered to BEFORE checkpoint save) and H4-4P (no-op idempotent path now propagates active-index transient failures). C1-4P remains the dominant defect — Pause/Resume/Cancel against ANY operator-owned active rebuild still returns 409 OperationInFlight in production. **AC5 remains UNMET; story is not review-ready.**_

##### Pass-4 verification table (from Acceptance Auditor)

| Pass-4 ID | Verified at HEAD | Notes |
|---|---|---|
| C1-4P | **NOT-APPLIED** | `AdminProjectionRebuildController.cs:233,258-260` `TransitionExistingAsync` still passes `scope` (null OperationId) to `SaveAsync` for Pause/Resume/Cancel. Only Retry got the fresh-ULID fix. |
| H1-4P | **NOT-APPLIED** | Only `OperationCanceledException` caught at orchestrator boundary; transient `DaprException` still escapes, leaves `Running` + active-index polluted. |
| H2-4P | **NOT-APPLIED (WORSENED)** | Commit `7a077da2` explicitly moved active-index write BEFORE `TrySaveStateAsync` for active statuses (opposite of pass-4 recommendation). Phantom index entry on ETag failure. |
| H3-4P | **NOT-APPLIED** | `UpdateActiveIndexForLifecycleAsync` still inside the ETag retry loop; up to 9 index writes per state transition. |
| H4-4P | **NOT-APPLIED (WORSENED)** | Commit `7a077da2` added `UpdateActiveIndexForLifecycleAsync` invocation INSIDE the idempotent no-op branch and propagates its failure code as the result. |
| H5-4P | **NOT-APPLIED** | `MatchesRebuildScope` still uses `IsNullOrWhiteSpace` wildcard predicate. |
| H6-4P | **NOT-APPLIED** | `ToSequence > int.MaxValue` still surfaces as 500 InternalError. |
| H7-4P | **NOT-APPLIED** | `Succeeded → Canceled` / `Succeeded → Paused` still bypass terminal protection. |
| H8-4P | **NOT-APPLIED** | Test still seeds `FailureReasonCode = null` hardcoded; assertion happens to pass. |
| H9-4P | **APPLIED** | `RebuildCancelCleanupRejected` now uses `EventId = 1147`. |
| M1-4P | **APPLIED** | Empty-stream short-circuit (`currentSequence <= fromSequence → return []`) now runs BEFORE the overflow guard. |
| M2-4P | **NOT-APPLIED** | `IsDeserializationFailure` still recurses InnerException without depth bound. |
| M3-4P | **NOT-APPLIED** | `DaprException` still classified as data-corruption with `-1` sentinel. |
| M4-4P | **NOT-APPLIED** | `IsStateStoreUnavailable` still accepts `TimeoutException && depth > 0` regardless of parent frame. |
| M5-4P | **NOT-APPLIED** | Rebuild loop missing `Log.CheckpointDriftDetected` mirror. |
| M6-4P | **NOT-APPLIED** | Admin controller tests still have no `AssertNoForbiddenLeakage` equivalent. |
| L1-4P | **NOT-APPLIED** | Completion Notes line 873 still claims "strict uppercase ULID operation IDs". |
| L2-4P | **NOT-APPLIED** | `RebuildDeliveryResult.Complete(pageComplete = true)` default unchanged. |
| L3-4P | N/A | Auto-generated CHANGELOG via semantic-release; no manual entry expected. |
| L4-4P | **NOT-APPLIED** | `no-domain-service` not in `docs/reference/stream-replay-api.md`. |
| L5-4P | **NOT-APPLIED** | No multi-aggregate `PageComplete` mix regression test. |
| L6-4P | **NOT-APPLIED** | `ReadEventsRangeAsync` throws for `currentSequence == 0 + metadata.HasValue`; `GetCurrentSequenceAsync` returns 0. Asymmetry preserved. |
| DEC1-4P | **NOT-APPLIED** | Retry endpoint still flips status to `Retrying` without invoking the orchestrator. |
| DEC2-4P | **NOT-APPLIED** | `ReplayProjection` still returns 202+Success=true on partial-page exhaustion. |
| DEC3-4P | **NOT-APPLIED** | `IsDifferentOperation` still excludes `NotStarted`. |
| DEC4-4P | **NOT-APPLIED** | `ResetAsync` still allows terminal-record OperationId overwrite; no XML doc note added. |
| DEC5-4P | **NOT-APPLIED** | Tests still assert mock-call-counts; no `Arg.Do` capture for OperationInFlight/StaleCheckpoint/CheckpointUnavailable. |
| DEC6-4P | **NOT-APPLIED** | Projection state still written via `UpdateProjectionAsync` BEFORE per-aggregate `SaveAsync`. |
| DEC7-4P | **NOT-APPLIED** | Per-aggregate scope still inherits operator OperationId; Reset+Replay wedge intact. |
| DEC8-4P | **NOT-APPLIED** | No `ITenantValidator`/`IRbacValidator` injection; spec not amended to GlobalAdmin-only. |

##### Decisions resolved (2026-05-16 pass-5) — all 10 accepted per recommendations → routed to patches

- [x] [Review][Decision] **DEC1-5P → P-DEC1-5P** — Retry endpoint invokes `rebuildOrchestrator.RebuildProjectionAsync(retryScope, ct)` synchronously like Replay.
- [x] [Review][Decision] **DEC2-5P → P-DEC2-5P** — ReplayProjection treats terminal `Running` as 202 + `Success=false` + "incomplete; re-invoke" message until D2a scheduler lands.
- [x] [Review][Decision] **DEC3-5P → P-DEC3-5P** — Extend `IsDifferentOperation` guard to cover `NotStarted` rows (defense-in-depth). **APPLIED 2026-05-17** at `ProjectionRebuildCheckpointStore.cs:117-121`. The status filter (`IsLifecycleActive ∪ IsTerminal`) is removed; the guard now fires on any status when `IsDifferentOperation` returns true and `isPerAggregateProgress=false`. Effectively covers NotStarted as well as the active/terminal sets. Build clean.
- [x] [Review][Decision] **DEC4-5P → P-DEC4-5P** — Keep current `ResetAsync` terminal-overwrite behavior + add XML doc `<remarks>` note documenting trust boundary on `IProjectionRebuildCheckpointStore.ResetAsync` (also closes L7-5P).
- [x] [Review][Decision] **DEC5-5P → P-DEC5-5P** — Keep current ordering (projection state before checkpoint) + document strict-idempotency requirement on domain `/project` in `docs/reference/stream-replay-api.md`.
- [x] [Review][Decision] **DEC6-5P → P-DEC6-5P** — Per-aggregate `SaveAsync` rows bypass the `IsDifferentOperation` check; operator-scope row is the single source of operator identity (carve-out in store). **APPLIED 2026-05-17.** Added `bool isPerAggregateProgress = false` parameter to `IProjectionRebuildCheckpointStore.SaveAsync` and impl at `ProjectionRebuildCheckpointStore.cs:90-97, 117-121`. Orchestrator passes `isPerAggregateProgress: true` at the per-aggregate `SaveAsync` call (`ProjectionUpdateOrchestrator.cs:578-589`). The guard skips when true. Build clean.
- [x] [Review][Decision] **DEC7-5P → P-DEC7-5P** — Update spec/docs to mark D2b as **accepted-with-GlobalAdmin-only**; no validator injection required (cross-tenant role gate is sufficient for current threat model).
- [x] [Review][Decision] **DEC8-5P → P-DEC8-5P** — When `operatorScope.AggregateId != null`, do not write per-aggregate progress separately (single-row mode for aggregate-scoped rebuilds).
- [x] [Review][Decision] **DEC9-5P → P-DEC9-5P** — Map `NoDomainService` to 503 with `Retry-After: 30` (transient configuration classification).
- [x] [Review][Decision] **DEC10-5P → P-DEC10-5P** — At terminal-Succeeded write of a domain-wide rebuild, delete per-aggregate rows for the scope (explicit cleanup).

##### Patches (30) — fixable without input

CRITICAL:

- [x] [Review][Patch] **C1-5P (carry C1-4P) — Pause/Resume/Cancel endpoints non-functional against active operator rebuilds** [`AdminProjectionRebuildController.cs:264-273`] — **APPLIED 2026-05-17.** Non-retry branch in `TransitionExistingAsync` now constructs `lifecycleScope = scope with { OperationId = existing.OperationId }` and passes it to `SaveAsync`. Build clean.
- [x] [Review][Patch] **C2-5P — `Failed`/`NoDomainService` audit writes silently dropped as `StaleCheckpoint`** [`ProjectionUpdateOrchestrator.cs:413-422 (NoDomainService), :493-509 (ProjectionApplyRejected)`] — **APPLIED 2026-05-17.** Both Failed audit writes routed through `ResetAsync` (which per its XML doc explicitly bypasses monotonic guards) instead of `SaveAsync`. The StaleCheckpoint guard at `ProjectionRebuildCheckpointStore.cs:165-167` no longer applies to terminal-Failed lifecycle writes from the orchestrator. Build clean.
- [x] [Review][Patch] **C3-5P — Cancel-cleanup `ReadAsync` exception swallows the OperationCanceledException** [`ProjectionUpdateOrchestrator.cs:325-361`] — **APPLIED 2026-05-17.** Cancel-cleanup block wrapped in inner `try { ReadAsync + ResetAsync } catch (Exception cleanupEx) when (cleanupEx is not OperationCanceledException) { Log.RebuildCancelCleanupFailed }`. The OCE always reaches `throw;` at the end. New `LoggerMessage` EventId 1148. Build clean.
- [x] [Review][Patch] **C4-5P (worsened H2-4P + EH-H1) — Active-index write reordering creates phantom index entries on checkpoint save failure** [`ProjectionRebuildCheckpointStore.cs:175-195`] — **APPLIED 2026-05-17.** Reverted pass-5-worsening: checkpoint is written FIRST for ALL statuses via `TrySaveStateAsync`; active-index update runs only on save success. The bifurcated active/non-active path is collapsed to a single post-save index update. Build clean.

HIGH:

- [x] [Review][Patch] **H1-5P (carry H1-4P) — Rebuild loop catches only OCE; transient DaprException leaves status `Running` and pollutes active-index** [`ProjectionUpdateOrchestrator.cs:362-381`] — **APPLIED 2026-05-17.** Added `catch (Exception ex) when (ex is not OperationCanceledException)` after the OCE catch that calls `ResetAsync(operatorScope, initial.LastAppliedSequence, Failed, ex.GetType().Name, CT.None, initial.ToPosition)` then rethrows. New `LoggerMessage` EventId 1149. Build clean.
- [x] [Review][Patch] **H2-5P (worsened H4-4P + EH-H7) — No-op idempotent path propagates active-index transient failures** [`ProjectionRebuildCheckpointStore.cs:131-141`] — **APPLIED 2026-05-17.** No-op idempotent branch now logs `CheckpointNoOpIndexUpdateFailed` (new EventId 1198) on transient index-update failure and returns `Success(existing)` regardless. Operator polling for confirmation no longer flips a semantically-successful no-op into 503. Build clean.
- [x] [Review][Patch] **H3-5P — Terminal `Succeeded` write uses caller's `cancellationToken`** [`ProjectionUpdateOrchestrator.cs:404-419`] — **APPLIED 2026-05-17.** Terminal `Succeeded` `SaveAsync` now uses `CancellationToken.None`. Mirrors the documented C2-5P / NoDomainService / ProjectionApplyRejected pattern. Build clean.
- [x] [Review][Patch] **H4-5P — `StreamReadPageBuilder` ID factory invoked 3x with same seed produces identical messageId/correlationId/causationId** [`src/Hexalith.EventStore.Testing/Builders/StreamReadPageBuilder.cs:62-75`] — `_idFactory?.Invoke(sequenceNumber)` called three times with same arg. Deterministic factory `seq => $"id-{seq}"` yields identical values; tests asserting field distinctness silently pass. Fix: factory signature `Func<long, IdKind, string>` or pass three distinct seeds/roles. **APPLIED 2026-05-17.** Added role-aware deterministic ID generation plus regression tests.
- [x] [Review][Patch] **H5-5P — `IsValidOperationId` narrow catch list lets non-`ArgumentException`/`FormatException` escape a bool validator** [`ProjectionRebuildCheckpointStore.cs:594-600`] — `UniqueIdHelper.ToGuid` could throw other types (e.g., `OverflowException`, `IndexOutOfRangeException`); these escape and abort the SaveAsync call. Fix: catch all except `OperationCanceledException`/`OutOfMemoryException`, return false. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **H6-5P — `ReadEventsRangeAsync` toSequence overflow guard fires before empty-stream short-circuit** [`AggregateActor.cs:419-421`] — New `if (toSequence is long ts && ts > int.MaxValue) throw` runs BEFORE the empty-stream return-`[]` short-circuit (M1-4P fixed the fromSequence ordering but not toSequence). Caller passing `toSequence = long.MaxValue` against an empty stream throws — contract regression on the toSequence axis. Fix: move toSequence bound check below empty-stream short-circuit. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **H7-5P (carry H6-4P) — `ToSequence > int.MaxValue` not validated; surfaces as 500 InternalError** [`StreamsController.cs:277-286` + `AggregateActor.cs:639-641`] — Fix unchanged from pass-4: validator rejects `request.ToSequence.HasValue && request.ToSequence.Value > int.MaxValue` with 400 `invalid-range`. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **H8-5P (carry H7-4P) — `SaveAsync` allows transitions OUT of terminal status (Succeeded → Canceled / Succeeded → Paused)** [`ProjectionRebuildCheckpointStore.cs:148-156`] — **APPLIED 2026-05-17.** Added `IsTerminal(existing.Status)` short-circuit AFTER the idempotent no-op check and BEFORE the lifecycle-protected check. Any status change against a terminal record (other than idempotent same-status no-op) returns `Failure(CheckpointConflict)`. Operators wanting to start a fresh rebuild after a terminal record must route through `ResetAsync`. Build clean.
- [x] [Review][Patch] **H9-5P (carry H5-4P) — `MatchesRebuildScope` widens via empty/whitespace `AggregateId`; inconsistent with `ValidateScope`** [`ProjectionRebuildCheckpointStore.cs:852-861` vs `ProjectionUpdateOrchestrator.cs:1342-1346`] — Fix unchanged: align matcher to only treat `null` (not empty/whitespace) as wildcard. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **H10-5P (carry H8-4P) — Retry-FailureReasonCode test passes only because seed is hardcoded null** [`AdminProjectionRebuildControllerTests.cs:241-266` vs `:310-325`] — Production passes `existing.FailureReasonCode` to ResetAsync; assertion hardcodes `null`. If seeded `existing` had any non-null reason, assertion breaks. Fix: seed `existing` with `FailureReasonCode = "domain-failure"` and assert that specific value in the ResetAsync received call. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **H11-5P (carry H3-4P) — Active-index update inside ETag-retry loop (3× cost on conflict)** [`ProjectionRebuildCheckpointStore.cs:171-177`] — Fix unchanged: move active-index write OUTSIDE the ETag retry loop. **APPLIED 2026-05-17.** `ResetAsync` now mirrors save-first/index-after-success ordering.
- [x] [Review][Patch] **H12-5P — Conventional Commits violation: commit `b802f4d5` typed "Refactor" but adds features** [`git log fb36737c..HEAD`] — Diff includes new interface method (`HasActiveOperatorRebuildForDomainAsync`), new public reason codes (`OperationInFlight`, `StaleCheckpoint`, `ForbiddenRole`, `NoDomainService`), and new controller behavior — should be `feat:` per CLAUDE.md "Commit Messages" section and semantic-release contract. Fix: amend via `git commit --allow-empty -m "feat: …"` or document the version impact in spec Change Log. **DOCUMENTED 2026-05-17** in Change Log v1.4.
- [x] [Review][Patch] **H13-5P — `TaskCanceledException` misclassification: `OperationCanceledException` catch fires before `IsStateStoreUnavailable` check** [`ProjectionRebuildCheckpointStore.cs:200,513`] — `HttpClient.Timeout`-driven `TaskCanceledException` (with `IsCancellationRequested == false`) is caught by `catch (OperationCanceledException) { throw; }` and propagates as if user-canceled. Fix: check `TaskCanceledException && !cancellationToken.IsCancellationRequested` separately and re-raise as transient (or wrap in `DaprException`). **APPLIED 2026-05-17.**
- [x] [Review][Patch] **H14-5P — `GetCurrentSequenceAsync` then `ReadEventsRangeAsync` race makes 404 unreliable and conflates "actor not activated" with "actor exists, zero events"** [`StreamsController.cs:104-116` + `AggregateActor.cs:686-710`] — Distinguish `metadata.HasValue == false` (404 MissingStream) from `metadata.CurrentSequence == 0` (200 with empty page); propagate `metadataResult.HasValue` to controller. **APPLIED 2026-05-17.** Added `AggregateStreamMetadata` and controller tests.
- [x] [Review][Patch] **H15-5P — Reserved char `*` not mentioned in `ValidateKeyPart` error message** [`ProjectionRebuildCheckpointStore.cs:430-441`] — Error message lists `':'`, `'\0'`, `'|'`, `'\r'`, `'\n'` but not `*` (added per W1-CONT). Operator gets misleading 400 saying `*` is not reserved when it actually is. Fix: include `*` in the message. **APPLIED 2026-05-17.**

MEDIUM:

- [x] [Review][Patch] **M1-5P (carry DEC5-4P, promoted from decision) — R2-A6 violation: `OperationInFlight`/`StaleCheckpoint`/`CheckpointUnavailable` tests assert mock-call-counts not state-store end-state** [`ProjectionRebuildCheckpointStoreTests.cs:297-304, 322-329, 384-391`] — CLAUDE.md R2-A6 forbids mock-call-count as sole integration-test evidence. Fix: add three `Arg.Do<ProjectionRebuildCheckpoint>(captured.Add)` capture tests verifying persisted row fields after the failure path. **APPLIED 2026-05-17.** Added checkpoint capture assertions for operation-in-flight, stale-checkpoint, and active-index-unavailable paths.
- [x] [Review][Patch] **M2-5P (carry M2-4P) — `IsDeserializationFailure` recurses InnerException without depth bound** [`AggregateActor.cs:714-720`] — Fix: mirror `MaxExceptionFrames = 8` pattern from `ProjectionRebuildCheckpointStore`. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M3-5P (carry M3-4P) — `IsDeserializationFailure` includes `DaprException` — transient outage mis-classified as data corruption** [`AggregateActor.cs:719`] — Fix: narrow to `JsonException`/`InvalidDataException` only; let `DaprException` propagate to `IsServiceUnavailable` path. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M4-5P (carry M4-4P) — `IsStateStoreUnavailable` accepts `TimeoutException && depth > 0` under ANY wrapper** [`ProjectionRebuildCheckpointStore.cs:521-523` + `StreamsController.cs:362-364`] — Fix: only return true when immediate parent frame is `DaprException`/`HttpRequestException`/`SocketException`/`IOException`. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M5-5P (carry M5-4P) — Drift detection log missing in rebuild loop** [`ProjectionUpdateOrchestrator.cs:296-298`] — Fix: mirror `Log.CheckpointDriftDetected` at per-aggregate read site. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M6-5P (carry M6-4P) — Admin controller tests have no `AssertNoForbiddenLeakage` equivalent** [`AdminProjectionRebuildControllerTests.cs`] — Fix: extract `AssertNoForbiddenLeakage` to shared helper, apply across both controller test files. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M7-5P — `Math.Max(finalSnapshot.LastAppliedSequence, highestMatchedProgress)` saved as operator-scope `Succeeded` is cross-aggregate inflated** [`ProjectionUpdateOrchestrator.cs:1117-1126`] — `highestMatchedProgress` takes max across heterogeneous per-aggregate sequence spaces. For domain-wide rebuild covering aggregates A `{0..100}` and B `{0..1000}`, operator-scope `Succeeded.LastAppliedSequence = 1000` is artifact of B's larger space, not progress. Fix: write `Succeeded` with `LastAppliedSequence = 0` (or operator's `existing.LastAppliedSequence`) for domain-wide rebuilds; per-aggregate rows already carry truthful progress. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M8-5P — `HasActiveOperatorRebuildForDomainAsync` fail-closed stalls poller without bounded escalation** [`ProjectionRebuildCheckpointStore.cs:766-772`] — Single transient DAPR failure on index store disables ALL poller deliveries for `(tenant, domain)`. Log spam on tight polling cadence. Fix: bounded retry inside the read (same `s_retryDelays` pattern); throttle warning log to one-per-minute per `(tenant, domain)`; consider fail-open after retries exhausted with elevated WARN. **APPLIED 2026-05-17.** Added bounded retries and logs only after exhaustion.
- [x] [Review][Patch] **M9-5P — `CheckpointConflict` 409 missing `Retry-After` header** [`AdminProjectionRebuildController.cs:392-415`] — Existing condition `(statusCode == 503) || (statusCode == 409 && reasonCode == OperationInFlight)` excludes `CheckpointConflict` and `StaleCheckpoint`. Both are transient lock-contention-style 409s. Fix: add to Retry-After branch (or document why each is omitted). **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M10-5P — Cancel-cleanup uses stale operator-scope `LastAppliedSequence`** [`ProjectionUpdateOrchestrator.cs:329-345`] — Cleanup reads operator-scope and writes `Canceled, LastApplied=50` while per-aggregate row at 100 is orphaned. Subsequent `Retry` starts from 50, re-applying events 51–100. Fix: aggregate per-aggregate progress (use `Math.Max(canceledSnapshot.LastAppliedSequence, highestMatchedProgress)` from enclosing scope). **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M11-5P — Pre-write OperationId guard reads operator scope but writes per-aggregate scope** [`ProjectionUpdateOrchestrator.cs:1270-1281, 1287-1297`] — `preSaveOperator = ReadAsync(operatorScope)` checks operator OperationId. Next write is `SaveAsync(perAggregateScope, ...)` with no parallel per-aggregate pre-check. Concurrent operator B that overwrote per-aggregate row (but not operator row yet) passes operator check and gets re-overwritten on per-aggregate write. Fix: re-check per-aggregate row's OperationId too, or rely on store-side `IsDifferentOperation` guard. **APPLIED 2026-05-17.** `DeliverProjectionForRebuildAsync` now compares the per-aggregate row read at page start with the row immediately before projection-state write and interrupts on OperationId/progress/status/toPosition drift.
- [x] [Review][Patch] **M12-5P — `IsDifferentOperation` legacy-row asymmetry: migration-era rows with null OperationId are unsafe** [`ProjectionRebuildCheckpointStore.cs:895-904`] — `if (string.IsNullOrWhiteSpace(existing.OperationId)) return false;` lets ANY new operator overwrite legacy rows without conflict, even racing operators. Fix: write default sentinel OperationId during read-after-migration, OR document the trust assumption. **DOCUMENTED 2026-05-17** as legacy progress-only ownership takeover.
- [x] [Review][Patch] **M13-5P — `IsServiceUnavailable` recursion through non-transport intermediate exception** [`StreamsController.cs:1747-1771`] — `ActorMethodInvocationException(inner: InvalidOperationException(inner: HttpRequestException))` returns true via the catchall recursion arm. Application bug becomes 503. Fix: only recurse through `AggregateException`/`ActorMethodInvocationException`; stop on other intermediate frames. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M14-5P — `latestSequence = currentSequence` regresses against in-page actor inserts** [`StreamsController.cs:1660, 1683`] — `currentSequence` read BEFORE `ReadEventsRangeAsync`. If actor receives append between calls, response reports `LatestSequence < orderedEvents.Last().SequenceNumber` — internally contradictory. Fix: `Math.Max(currentSequence, orderedEvents[^1].SequenceNumber)`, OR read `currentSequence` AFTER `ReadEventsRangeAsync`. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **M15-5P — `ResetAsync` 6-arg positional call sites fragile** [`AdminProjectionRebuildController.cs:1531-1534`] — Future signature reorder will silently bind wrong values. Fix: use named args (`failureReasonCode:`, `cancellationToken:`, `toPosition:`). **APPLIED 2026-05-17.**

LOW:

- [x] [Review][Patch] **L1-5P (carry L1-4P) — Spec drift "strict uppercase ULID operation IDs"** [spec line 873] — Fix: update bullet to "case-insensitive ULID operation IDs via `UniqueIdHelper.ToGuid`". **ALREADY APPLIED; VERIFIED 2026-05-17.**
- [x] [Review][Patch] **L2-5P (carry L2-4P) — `RebuildDeliveryResult.Complete` `pageComplete = true` default is P-C4 hazard** [`ProjectionUpdateOrchestrator.cs:589`] — Fix: remove default; require explicit parameter. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **L3-5P (carry L4-4P) — `no-domain-service` not in `docs/reference/stream-replay-api.md`** — AC7 docs+tests parity. Fix: add taxonomy entry. **ALREADY APPLIED; VERIFIED 2026-05-17.**
- [x] [Review][Patch] **L4-5P (carry L5-4P) — Multi-aggregate `PageComplete` mix regression test missing** [`ProjectionUpdateOrchestratorTests.cs`] — Fix: add two-aggregate test where one returns `PageComplete=true` and other `PageComplete=false`; assert `Succeeded` is NOT written. **APPLIED 2026-05-17.** Added two-aggregate regression coverage with one full page and one complete page; terminal `Succeeded` is not written while any aggregate page remains incomplete.
- [x] [Review][Patch] **L5-5P (carry L6-4P) — Asymmetric `CurrentSequence == 0` invariants across actor methods** [`AggregateActor.cs:665-668` vs `:703-707`] — Fix: align both methods. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **L6-5P — Committed `22-6-current-review.diff` (1465 lines) in `_bmad-output/implementation-artifacts/`** [repository hygiene] — Patch dumps in committed implementation artifacts balloon repo size and confuse semantic-release scope. Fix: gitignore review diffs or store outside committed tree. **APPLIED 2026-05-17.** Removed story-specific review diff snapshots; `.gitignore` already blocks future ones.
- [x] [Review][Patch] **L7-5P — DEC4-4P doc note not added to `IProjectionRebuildCheckpointStore.ResetAsync` XML** [`IProjectionRebuildCheckpointStore.cs:30-34`] — XML doc does not document the audit-trail-overwrite policy for terminal-record OperationId mismatch. Fix: add `<remarks>` block. **ALREADY APPLIED; VERIFIED 2026-05-17.**
- [x] [Review][Patch] **L8-5P — `EventStoreGatewayClient` ctor silently honors smaller existing `HttpClient.MaxResponseContentBufferSize`** [`EventStoreGatewayClient.cs:345-347`] — Cap becomes "at most options, possibly smaller". Fix: log when option cap is overridden, OR unconditionally assign and document HttpClient ownership. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **L9-5P — Crockford `I/L/O/U` rejection error message gives no hint of forbidden chars** [`ProjectionRebuildCheckpointStore.cs:66-82`] — Common operator typo. Fix: include Crockford guidance in exception message. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **L10-5P — `MissingStream` test does not pin Type URI** [`StreamsControllerTests.cs:1962-1968`] — Asserts `reasonCode == MissingStream` but not `problem.Type == ProblemTypeUris.NotFound`. Regression to BadRequest type would pass. Fix: add Type assertion. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **L11-5P — `AssertNoForbiddenLeakage` substring `"ETag"`/`"AggregateActor"` false-positive risk** [`StreamsControllerTests.cs:2004-2007`] — Substring match catches benign messages. Fix: word-boundary regex. **APPLIED 2026-05-17.**
- [x] [Review][Patch] **L12-5P — `s_retryDelays` indexing scheme implicit across three loops** [`ProjectionRebuildCheckpointStore.cs:557-567, 678-682, 735-739, 829-835`] — Future `MaxEtagRetries` change must update three loops. Fix: extract `BoundedRetryDelay(int attempt)` helper that clamps internally. **APPLIED 2026-05-17.**

##### Deferred (4)

- [x] [Review][Defer] **W1-5P (carry W1-4P) — AC4 partial: exact-256-events with ToPosition=null leaves Running** — Couples with deferred D2a `RebuildSchedulerActor`. Recorded.
- [x] [Review][Defer] **W2-5P (carry W2-4P) — `IsValidOperationId` uses `UniqueIdHelper.ToGuid` not `Ulid.TryParse`** — Functionally equivalent; flag for hygiene patch.
- [x] [Review][Defer] **W3-5P (carry W3-4P) — `ReadAsync` silently returns null on malformed persisted OperationId** — Diagnostic surface poor; not a correctness defect.
- [x] [Review][Defer] **W4-5P (carry W4-4P) — Completion Notes overstate "P4/P-D2 full apply-driven rebuild advancement resolved"** — Sync one-shot only; documentation drift.

##### Dismissed (6) — verified false positive or informational

- Edge — `UpdateActiveIndexForLifecycleAsync` `Remove` no-op for NotStarted when never indexed (EH-M8). Informational — code is correct.
- Edge — `ProjectionReplayRequest.FromPosition - 1` long.MinValue underflow (EH-M6). Guarded at L157 (`FromPosition < 0` rejected); defense-in-depth note only.
- Edge — `ResetAsync` has no runtime auth gate (EH-M5). Trust boundary documented per pass-4 DEC4; not currently exploitable.
- Edge — `RebuildDeliveryResult.Interrupt()` returns `LastAppliedSequence: 0` corruption hazard (EH-H2). Early-return on `delivery.Interrupted` makes this latent only; flagged for future refactor awareness.
- Edge — Test fixture `SetupActiveIndex` cast `((string[], string))(existing!, "active-index-etag")` passes null (EH-L4). Test-only; production semantics not affected.
- Edge — `IsServiceUnavailable` recognizes `IOException` at all depths but inner JSON parse should be 500 (EH-L5). Reasonable design choice; status-code is correct, reason-code "service-unavailable" is the documented label.

##### Pass-5 apply status (2026-05-17)

**Applied (10/40 patches + 6/10 resolved decisions):** Narrow-scope coordinated rewrite per user direction. All four CRITICAL patches (C1-5P, C2-5P, C3-5P, C4-5P) plus four key HIGH (H1-5P, H2-5P, H3-5P, H8-5P) plus two resolved-decision-derived patches (P-DEC3-5P, P-DEC6-5P) landed.

**Build status:** All projects build clean in Release configuration (0 warnings, 0 errors with `TreatWarningsAsErrors=true`).

**Test gate (user-selected):** `Client.Tests` 389/389 pass; `Testing.Tests` 110/110 pass. **Caveat:** Server.Tests was not selected at the gate. The applied patches deeply modify `ProjectionRebuildCheckpointStore.SaveAsync` and `ProjectionUpdateOrchestrator.RebuildProjectionAsync`; existing Tier-1 tests substitute `IProjectionRebuildCheckpointStore` so server behavior changes are NOT validated. Before merge, run focused Server tests:

```bash
dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~ProjectionRebuild|FullyQualifiedName~ProjectionUpdateOrchestrator|FullyQualifiedName~AdminProjectionRebuild"
```

Expected: pre-existing mocks will need updating because (a) the new `isPerAggregateProgress` parameter is optional but named-arg assertions on `Received().SaveAsync(...)` will need extension, (b) C2-5P swapped `SaveAsync` for `ResetAsync` at two sites — existing assertions on `Received().SaveAsync(... Failed ...)` must change to `Received().ResetAsync(... Failed ...)`, (c) C1-5P changes the scope passed to SaveAsync in `TransitionExistingAsync` — assertions on `Arg.Is<ProjectionRebuildCheckpointScope>(s => s.OperationId == null)` will need to expect the threaded `existing.OperationId`.

**Files changed in this pass:**

- `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs` (P-DEC6-5P param + L7-5P/DEC4 XML doc)
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs` (C4-5P revert + H2-5P no-op + H8-5P terminal short-circuit + P-DEC3-5P/P-DEC6-5P guard changes + EventId 1198 new log)
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` (C2-5P SaveAsync→ResetAsync ×2 + C3-5P cancel-cleanup wrap + H1-5P transient catch + H3-5P CT.None + P-DEC6-5P named arg + EventId 1148/1149 new logs)
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs` (C1-5P lifecycleScope OperationId overlay)
- `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs` (P-DEC4-5P XML doc remark)
- `.gitignore` (L6-5P: BMAD review-diff snapshots ignored going forward)
- `docs/reference/stream-replay-api.md` (L3-5P taxonomy + P-DEC5-5P strict-idempotency section)
- `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` (L1-5P spec drift + this apply-status section + check-offs)

**Remaining (24 patches + 4 decisions worth of work routed to patches):** All HIGH H4-5P/H5-5P/H6-5P/H7-5P/H9-5P/H10-5P/H11-5P/H13-5P/H14-5P/H15-5P, all MEDIUM M1-5P–M15-5P, all LOW L1-5P–L12-5P (note L1, L3, L7 already applied via Wave 1 docs), plus P-DEC1-5P (Retry invokes orchestrator), P-DEC2-5P (terminal Running → 202+Success=false), P-DEC8-5P (single-row mode for aggregate-scoped rebuilds), P-DEC9-5P (NoDomainService → 503+Retry-After), P-DEC10-5P (delete per-aggregate rows at Succeeded). H12-5P deferred (cannot retro-amend merged commit). Story remains in `review` pending the focused Server.Tests run + the remaining 24 patches.

##### Reviewer notes

- **The story is NOT review-ready.** Pass-5 closed only 2 of 30 outstanding pass-4 items. Two items (H2-4P, H4-4P) were actively worsened by commit `7a077da2`. Commit `b802f4d5` is mistyped as `Refactor` but contains feature work — semantic-release version bump impact unclear.
- **C1-4P remains the dominant defect.** Pause/Resume/Cancel against any operator-owned active rebuild still returns 409 OperationInFlight in production. Tier-1 tests mask it via NSubstitute store substitution. AC5 UNMET.
- **Three new CRITICAL findings** surfaced in pass-5: C2-5P (Failed/NoDomainService writes silently rejected by the new StaleCheckpoint guard), C3-5P (cancel-cleanup ReadAsync swallows OCE), C4-5P (active-index reordering creates phantom entries on failed checkpoint writes).
- **AC verification:** AC1/AC2/AC6 MET; AC3/AC4/AC7/AC8 PARTIAL; AC5 UNMET. Until C1-5P + DEC1-5P + DEC2-5P are resolved, AC5 cannot move.
- **Recommendation:** before any further review pass, do a narrow fix-it cycle on C1-5P through C4-5P, then re-verify the pass-4 items that were claimed APPLIED in the spec but verified NOT-APPLIED here. Bundle DEC1-5P / DEC2-5P / DEC8-5P / DEC10-5P with the deferred D2a `RebuildSchedulerActor` follow-up story — the synchronous one-shot model cannot fully close AC5 alone.

#### Code review run on 2026-05-16 (Opus 4.7 1M — fourth pass against `fb36737c..HEAD`, 20 files, +1576/-222)

_Three adversarial layers ran in parallel: Blind Hunter (diff-only), Edge Case Hunter (diff + project), Acceptance Auditor (diff + spec + CLAUDE.md). After dedup and triage: **8 decision-needed, 22 patch, 4 defer, 5 dismiss**. The most material finding is C1: Pause/Resume/Cancel endpoints are 100% non-functional in production because the controller passes `scope.OperationId=null` to `SaveAsync`, which the new `IsDifferentOperation` guard rejects with `OperationInFlight`. Existing tests substitute `IProjectionRebuildCheckpointStore` so the bug is invisible at Tier 1._

##### Decision-needed (8) — require user input

- [ ] [Review][Decision] **DEC1-4P — Retry endpoint flips status to `Retrying` but never invokes the orchestrator** [`AdminProjectionRebuildController.cs:201-215` (RetryProjection) vs `:317` (Replay runs orchestrator)] — `TransitionExistingAsync(..., status=Retrying, accepted=true)` writes `Retrying` via `ResetAsync` with a fresh OperationId, adds the projection to the active-rebuilds index, and returns 202. No background worker exists (D2a deferred). Operator sees "retrying" forever; poller is blocked for the (tenant, domain). Options: (a) invoke `rebuildOrchestrator.RebuildProjectionAsync(retryScope, ct)` synchronously like Replay does, (b) return 501 with a documented "retry pending D2a scheduler" message, (c) accept current behavior and document. Recommended: (a) — Replay already executes synchronously; Retry should match.
- [ ] [Review][Decision] **DEC2-4P — `ReplayProjection` returns 202+Success=true when terminal status is `Running` (page exhausted but more events available)** [`AdminProjectionRebuildController.cs:317-340`] — DEC7's terminal-status read covers `Failed` → 409, but the `Running` case (page reached `RebuildPageSize` without `ToPosition` set) falls through to 202 + `Success=true`. Operator dashboard reports a success that is in fact a partial rebuild needing re-invocation. Options: (a) treat `Running` as 202 + `Success=false` + "incomplete; re-invoke" message, (b) loop synchronously until terminal (risks request-thread blocking on large streams), (c) accept as deferred until D2a scheduler lands. Recommended: (a) until D2a.
- [ ] [Review][Decision] **DEC3-4P — `IsDifferentOperation` guard does NOT cover `NotStarted` rows** [`ProjectionRebuildCheckpointStore.cs:112-116, 481-490`] — Guard fires only when `IsLifecycleActive(existing.Status) || IsTerminal(existing.Status)`. `NotStarted` matches neither, so a different-OperationId `SaveAsync` against a `NotStarted` row silently overwrites the OperationId via the idempotent no-op path (`:130-133`) or the main write path (`:155-158`). DEC10 narrative explicitly flagged "Same hazard for `NotStarted` (Reset-to-NotStarted race silently overwritten by A's in-flight SaveAsync(Running))" but the patch didn't extend the guard. Options: (a) extend `IsDifferentOperation` rebuff to `NotStarted` too, (b) accept current behavior (NotStarted has no operator semantics worth protecting), (c) document. Recommended: (a) — cheap defense-in-depth.
- [ ] [Review][Decision] **DEC4-4P — `ResetAsync` allows terminal-record OperationId overwrite** [`ProjectionRebuildCheckpointStore.cs:244,690-697`] — Comment at the new guard says "terminal-record overwrites from a different OperationId remain allowed (sequential operator history)". Operator B's Replay against a `Succeeded` row from operator A silently overwrites A's OperationId. DEC10's stated recommendation was (a) "add terminal-record OperationId equality precondition to `SaveAsync` and `ResetAsync`" — only SaveAsync got the guard. Options: (a) extend ResetAsync to rebuff terminal+different-op, (b) keep current (operator-intent rewind is meant to replace history), (c) split into `ResetAsync` (rebuff terminal mismatch) + `ForceResetAsync` (explicit override). Recommended: (b) + document on `IProjectionRebuildCheckpointStore.ResetAsync`'s XML doc — Replay/Reset are operator-intentional rewinds; audit trail loss is acceptable per design.
- [ ] [Review][Decision] **DEC5-4P — Tier-2 state-store end-state assertions still missing for `OperationInFlight`/`StaleCheckpoint`/`CheckpointUnavailable` paths** [`ProjectionRebuildCheckpointStoreTests.cs:283-410`] — Spec P27 was claimed APPLIED; tests still assert `result.ReasonCode` + `DidNotReceive...TrySaveStateAsync` (negative call-count), not `Arg.Do<ProjectionRebuildCheckpoint>(captured.Add)` + persisted-row field assertions. CLAUDE.md R2-A6 forbids mock-call-count as sole integration-test evidence. Options: (a) add Arg.Do capture tests now (small effort), (b) defer to Tier-3 integration suite which is itself deferred to Aspire.AppHost.Sdk 13.3.2 bump, (c) document P27 as PARTIAL. Recommended: (a) — three short Arg.Do tests close the R2-A6 gap without Aspire.
- [ ] [Review][Decision] **DEC6-4P — Projection state written via `UpdateProjectionAsync` BEFORE per-aggregate checkpoint is saved** [`ProjectionUpdateOrchestrator.cs:535-553`] — On checkpoint-save failure (CheckpointConflict / CheckpointUnavailable), the projection state has already been advanced but `perAggregateProgress` was not. Next rebuild iteration re-applies the same events; non-idempotent domain `/project` handlers would double-apply. AC4 already mandates idempotency, so domain services SHOULD be safe — but the spec also says "checkpoint advancement occurs only after the projection apply path accepts the page", which is currently `accepted-then-maybe-recorded`, not `accepted-and-recorded`. Options: (a) save checkpoint FIRST then write projection (couples projection-state with checkpoint via ETag), (b) keep current and document the strict-idempotency requirement on domain `/project`, (c) two-phase commit. Recommended: (b) — domain services are already required to be idempotent.
- [ ] [Review][Decision] **DEC7-4P — Per-aggregate rows carry operator A's OperationId; operator B's Reset+Replay creates new operator-scope row but per-aggregate rows wedge against `IsDifferentOperation`** [`ProjectionUpdateOrchestrator.cs:294-296, 540-549` + `ProjectionRebuildCheckpointStore.cs:112-116`] — `perAggregateScope = operatorScope with { AggregateId = identity.AggregateId }` inherits operator A's OperationId. When operator B starts a new rebuild, the operator-scope row gets B's fresh OperationId (via ResetAsync), but the per-aggregate rows still carry A's. B's per-aggregate `SaveAsync` calls now hit `IsDifferentOperation` → permanent rebuff. Options: (a) `ResetAsync` should also clear per-aggregate rows for the scope, (b) per-aggregate rows skip the `IsDifferentOperation` check (operator-scope row is the authority), (c) per-aggregate scope inherits a non-operational marker rather than operator OperationId. Recommended: (b) — operator-scope row is the single source of operator identity; per-aggregate rows are progress-only.
- [ ] [Review][Decision] **DEC8-4P — D2b `ITenantValidator + IRbacValidator` injection never implemented despite second-pass D2b "decision resolved → patch"** [`AdminProjectionRebuildController.cs:21-24` constructor] — Constructor takes only `IProjectionRebuildCheckpointStore`, `ILogger`, `IProjectionRebuildOrchestrator?`. Authorization is `GlobalAdministratorHelper.IsGlobalAdministrator(User)` only. Second-pass spec says "Inject `ITenantValidator` + `IRbacValidator`... matches `StreamsController` pattern" — the patch was claimed resolved but never landed. Defensible: GlobalAdmin is cross-tenant so per-action tenant claim validation is redundant. Options: (a) implement the validators per spec, (b) update spec to mark D2b as accepted-with-GlobalAdmin-only, (c) keep open. Recommended: (b) — GlobalAdmin gate is sufficient for the current threat model.

##### Patches (22) — fixable without input

CRITICAL:

- [x] [Review][Patch] **C1-4P — Pause/Resume/Cancel endpoints non-functional against active operator rebuilds** [`AdminProjectionRebuildController.cs:233,258,434-440` + `ProjectionRebuildCheckpointStore.cs:112-116,481-490`] — `CreateScope` defaults `operationId=null`. `TransitionExistingAsync` calls `SaveAsync(scope, ...)` with null OperationId. `IsDifferentOperation` returns `true` when `existing.OperationId` is set and `scope.OperationId` is null. With `IsLifecycleActive(Running)=true`, the guard fires → returns `Failure(OperationInFlight)`. Pause/Resume/Cancel against ANY operator-owned active rebuild returns 409 in production. Existing tests substitute `IProjectionRebuildCheckpointStore` (NSubstitute mock) so the bug is invisible at Tier 1. Fix: in `TransitionExistingAsync`, after reading `existing`, pass `scope with { OperationId = existing.OperationId }` to `SaveAsync` so the guard accepts the lifecycle transition by the existing operator's identity. Add a focused test using a real store substitute that returns a non-null `existing.OperationId` to pin the regression.

HIGH:

- [x] [Review][Patch] **H1-4P — `RebuildProjectionAsync` catches only `OperationCanceledException`; any other exception leaves status `Running` and active-index polluted** [`ProjectionUpdateOrchestrator.cs:278-348`] — A transient `DaprException` from inner `ReadAsync` (operator/per-aggregate scope) or `EnumerateTrackedIdentitiesAsync` escapes the `try/catch`. Operator-scope row stays `Running`, active-rebuilds index entry remains for `(tenant, domain)`, poller blocked indefinitely. Self-healing requires operator Cancel — which is itself broken (C1-4P). Fix: add `catch (Exception ex) when (ex is not OperationCanceledException)` after the OCE catch that calls `ResetAsync(operatorScope, ..., Failed, ex.GetType().Name, CancellationToken.None, ...)` then rethrows.
- [x] [Review][Patch] **H2-4P — `SaveAsync` writes active-index BEFORE the checkpoint TrySaveStateAsync; failed checkpoint leaves phantom index entry** [`ProjectionRebuildCheckpointStore.cs:171-196`] — For active statuses (`:171-177`), the index update runs first, then `TrySaveStateAsync` (`:179`). On ETag-retry-exhaustion or `CheckpointUnavailable`, the function returns Failure but the index entry persists. `HasActiveOperatorRebuildForDomainAsync` returns true forever for that `(tenant, domain)`. Fix: write the checkpoint FIRST, then update the active-index on success only — already the pattern used for non-active status (`:188-193`). Apply the same pattern to active status.
- [x] [Review][Patch] **H3-4P — Active-index update runs on every ETag-retry iteration (3x cost on conflict)** [`ProjectionRebuildCheckpointStore.cs:171-177` inside the `for (attempt < MaxEtagRetries)` loop] — Each iteration re-invokes `UpdateActiveIndexForLifecycleAsync` which itself runs 3 ETag-retry attempts. Worst case: 9 index writes for one operator's state-transition. Fix: move active-index write OUTSIDE the ETag retry loop (perform once after the checkpoint write succeeds, mirroring the non-active branch).
- [x] [Review][Patch] **H4-4P — `UpdateActiveIndexForLifecycleAsync` failure converts a no-op SUCCESS to a 409 failure** [`ProjectionRebuildCheckpointStore.cs:119-126`] — When the caller submits a write that idempotency-matches the existing row, the function calls `UpdateActiveIndexForLifecycleAsync` (defensive) and propagates its failure code (e.g., `CheckpointUnavailable`) as the no-op's result. Operator gets 503 (or 409) for a write whose checkpoint state is already correct. Fix: when in the no-op path, log a warning on index-update failure but return `Success(existing)` regardless.
- [x] [Review][Patch] **H5-4P — `MatchesRebuildScope` widens via empty/whitespace `AggregateId`; inconsistent with `ValidateScope` rejecting them** [`ProjectionRebuildCheckpointStore.cs:852-861` vs `ProjectionUpdateOrchestrator.cs:1342-1346`] — `ValidateScope` now rejects empty/whitespace `AggregateId` (per P-C9). `MatchesRebuildScope` still treats `null` OR `""` OR `" "` as wildcard via `IsNullOrWhiteSpace`. If any future static caller bypasses validation, the matcher inconsistently widens scope. Fix: align matcher to only treat `null` (not empty/whitespace) as wildcard.
- [x] [Review][Patch] **H6-4P — `ToSequence > int.MaxValue` not validated; surfaces as 500 InternalError** [`StreamsController.cs:277-286` (ValidateRequest) + `AggregateActor.cs:639-641`] — Validator checks `FromSequence` upper bound but not `ToSequence`. Actor's `ReadEventsRangeAsync` throws `ArgumentOutOfRangeException` for `ToSequence > int.MaxValue`. Wrapped as `ActorMethodInvocationException`; controller's `ArgumentException` catch (`:151`) only fires for synchronously-thrown actor proxy exceptions, not wrapped versions → falls into 500 InternalError catch (`:206`). Fix: validator rejects `request.ToSequence.HasValue && request.ToSequence.Value > int.MaxValue` with 400 `invalid-range`.
- [x] [Review][Patch] **H7-4P — `SaveAsync` allows transitions OUT of terminal status (Succeeded → Canceled / Succeeded → Paused)** [`ProjectionRebuildCheckpointStore.cs:140-148`] — `IsLifecycleProtected` covers terminal+paused; `IsNonTerminalAdvancement` includes Running/Resuming/Retrying. So `Succeeded → Canceled` and `Succeeded → Paused` are NOT blocked. Once C1-4P is fixed, operators can mutate audited terminal records. Fix: add a `IsTerminal(existing.Status)` short-circuit before the no-op check: if existing is terminal, reject all status changes other than the idempotent no-op (`CheckpointConflict` reason).
- [x] [Review][Patch] **H8-4P — `TransitionExistingAsync` Retry path passes `existing.FailureReasonCode` but controller test expects `null`** [`AdminProjectionRebuildController.cs:253` vs `AdminProjectionRebuildControllerTests.cs:200-228`] — Production code: `ResetAsync(retryScope, existing.LastAppliedSequence, status, existing.FailureReasonCode, ct, existing.ToPosition)` (preserves prior failure reason per P13). Test substitutes mock then asserts `Received().ResetAsync(...)` with positional `null` for `failureReasonCode`. The test will fail if `existing.FailureReasonCode` is set (e.g., when seeded with a real Failed checkpoint). Fix: update test to assert `existing.FailureReasonCode` (or verify the seeded factory sets it null).
- [x] [Review][Patch] **H9-4P — `Log.RebuildCancelCleanupRejected` reuses EventId 1146 from old `RebuildCheckpointReadFailed`** [`ProjectionUpdateOrchestrator.cs:1428-1432`] — The diff replaces the old LoggerMessage at EventId 1146 with a new message and different parameter shape. Telemetry dashboards filtering by EventId 1146 will see different content. Fix: allocate a fresh EventId for `RebuildCancelCleanupRejected` and keep 1146 retired.

MEDIUM:

- [x] [Review][Patch] **M1-4P — `AggregateActor.ReadEventsRangeAsync` overflow guard fires before empty-page short-circuit** [`AggregateActor.cs:445-451`] — Guard `if (fromSequence > int.MaxValue - maxCount) throw` runs before `availableCount = currentSequence - fromSequence` is computed. For `fromSequence = int.MaxValue - 1` against an empty stream, the function should return `[]` but throws instead. Fix: move overflow guard AFTER `availableCount > 0` check.
- [x] [Review][Patch] **M2-4P — `AggregateActor.IsDeserializationFailure` recurses through `InnerException` without depth bound** [`AggregateActor.cs:485-491`] — Same hazard the store-side `MaxExceptionFrames` cap addressed. Maliciously chained exceptions could stack-overflow. Fix: mirror the depth-bounded helper from `ProjectionRebuildCheckpointStore` (`MaxExceptionFrames = 8`).
- [x] [Review][Patch] **M3-4P — `IsDeserializationFailure` includes `DaprException` — transient state-store outage mis-classified as data corruption** [`AggregateActor.cs:489`] — `DaprException` covers many cases (state-store unavailable, actor-not-found). Treating it as `EventDeserializationException(-1, ...)` reclassifies transient outage as data corruption with magic `-1` sentinel. Fix: narrow to `JsonException` and `InvalidDataException` only; let `DaprException` propagate to the controller's `IsServiceUnavailable` path.
- [x] [Review][Patch] **M4-4P — `IsStateStoreUnavailable` accepts `TimeoutException` at depth>0 wrapped under ANY exception** [`ProjectionRebuildCheckpointStore.cs:300-340` + `StreamsController.cs:1759-1768`] — DEC4 intent was "wrapped under DaprException/HttpRequestException". Current implementation matches `TimeoutException && depth > 0` regardless of wrapper. `InvalidOperationException(TimeoutException(...))` (programmer bug with a TimeoutException as cause) becomes 503 transient. Fix: only return true when the parent frame is `DaprException`/`HttpRequestException`/`SocketException`/`IOException`.
- [x] [Review][Patch] **M5-4P — Drift detection log missing in rebuild loop** [`ProjectionUpdateOrchestrator.cs:296-298`] — Poller path (`DeliverProjectionAsync`) has `Log.CheckpointDriftDetected`. Rebuild path silently re-applies from `0` if per-aggregate row is missing (e.g., partial backup restore). Fix: mirror the drift-detection log inside the rebuild loop's per-aggregate read.
- [x] [Review][Patch] **M6-4P — `EnsureGlobalAdministrator` 403 response shape not asserted via `AssertNoForbiddenLeakage`-style blocklist in admin controller tests** [`AdminProjectionRebuildController.cs:1647-1648` + `AdminProjectionRebuildControllerTests.cs`] — `StreamsControllerTests.AssertNoForbiddenLeakage` was expanded with `AggregateActor`, `projection-rebuild-checkpoints:`, `redis://`, `ETag` etc. Admin controller has its own test file with no equivalent assertion. If admin error messages leak `projection-rebuild-checkpoints:` or `ETag`, the regression is invisible. Fix: extract `AssertNoForbiddenLeakage` to a shared helper and apply to admin controller assertions.

LOW:

- [x] [Review][Patch] **L1-4P — Completion Note "strict uppercase ULID operation IDs" is factually false** [Story spec line 799] — Diff at `ProjectionRebuildCheckpointStore.cs:62-65` explicitly says "accepts both lowercase and uppercase ULIDs". Completion Notes List was not updated when the strict-uppercase check was relaxed. Fix: update the bullet to "Hardened rebuild checkpoint persistence with case-insensitive ULID operation IDs via `UniqueIdHelper.ToGuid`".
- [x] [Review][Patch] **L2-4P — `RebuildDeliveryResult.Complete` `pageComplete` defaults to `true`** [`ProjectionUpdateOrchestrator.cs:1335-1336`] — Factory default `pageComplete = true` means a future caller forgetting to pass it gets the wrong default — exactly the P-C4 hazard. Fix: remove the default; require explicit `pageComplete` parameter.
- [x] [Review][Patch] **L3-4P — `StaleCheckpoint` re-added to public taxonomy without Contracts changelog hygiene note** [`StreamReplayReasonCodes.cs:49-50`] — P22 (pass-1) removed it; P-C13 (third pass) re-added it. Non-breaking but should be flagged in CHANGELOG. Fix: add a Contracts changelog entry for the reintroduced public constant.
- [x] [Review][Patch] **L4-4P — `NoDomainService` reason code not in `docs/reference/stream-replay-api.md` or no-leak tests** [`StreamReplayReasonCodes.cs:85-86`] — New `no-domain-service` reason code; controller maps it to 409 (`AdminProjectionRebuildController.cs:380-384`). AC7 requires docs+tests parity. Fix: add taxonomy entry + extend no-leak assertions.
- [x] [Review][Patch] **L5-4P — P25 multi-aggregate `PageComplete` mix test missing** [`ProjectionUpdateOrchestratorTests.cs`] — Two-aggregate test where one returns `PageComplete=true` and the other `PageComplete=false`; assert `Succeeded` is NOT written. P25 was claimed APPLIED but the test does not appear in the diff. Fix: add the focused regression test for the `&=` boolean reduction.
- [x] [Review][Patch] **L6-4P — Asymmetric `CurrentSequence == 0` metadata invariants across actor methods** [`AggregateActor.cs:590-593` (throws) vs `:704-707` (returns 0)] — `GetEventsAsync`/`ReadEventsRangeAsync` throw `InvalidOperationException` for `CurrentSequence == 0 && metadata.HasValue`; `GetCurrentSequenceAsync` returns 0. `StreamsController` masks the asymmetry by calling `GetCurrentSequenceAsync` first, but rebuild orchestrator calls `ReadEventsRangeAsync` directly. Fix: align — either treat `CurrentSequence == 0 + metadata.HasValue` as stream-not-found everywhere (return empty / 0) or throw everywhere.

##### Deferred (4)

- [x] [Review][Defer] **W1-4P — AC4 partial: exact-`RebuildPageSize` (256) events with `ToPosition=null` leaves status `Running` until re-invocation** — Acknowledged: couples with the deferred D2a `RebuildSchedulerActor`. Recorded in `deferred-work.md`.
- [x] [Review][Defer] **W2-4P — `IsValidOperationId` uses `UniqueIdHelper.ToGuid` not `Ulid.TryParse` per CLAUDE.md R2-A7 letter** — Functionally equivalent (case-insensitive Crockford-base32). Spec already accepts as PARTIAL. Recommend a future hygiene patch to switch to `Ulid.TryParse` for literal R2-A7 compliance.
- [x] [Review][Defer] **W3-4P — `ReadAsync` silently returns null on malformed persisted OperationId** [`ProjectionRebuildCheckpointStore.cs:53-57`] — Operators see `NotStarted` via `GetRebuildStatus` even though a malformed row exists; subsequent `ResetAsync` self-heals. Diagnostic surface poor but not a correctness defect.
- [x] [Review][Defer] **W4-4P — Completion Notes/Change Log overstate "P4/P-D2 full apply-driven rebuild advancement resolved"** — Sync one-shot only; multi-page rebuilds still require operator re-invocation. Couples with D2a deferral; documentation drift, not a regression.

##### Dismissed (5) — verified false positive or informational

- Blind — `allMatchedWorkComplete = true` initial default with cancel-mid-loop unevaluated remainders potentially firing `Succeeded`. Verified: on Interrupt, outer loop returns immediately without writing `Succeeded`. Not a defect.
- Blind — `IsServiceUnavailable` recursion path concerns. Verified consistent on re-read.
- Blind — `MaxStreamReadResponseBytes <= 0` constructor check ordering. Default value avoids; not reachable in practice.
- Blind — `MaxExceptionFrames` vs `MaxExceptionUnwindDepth` naming drift. Both sites use the same name now.
- Blind — `UpdateActiveIndexForLifecycleAsync` "no-op transitions" comment misleading. Cosmetic/maintenance only; behavior is correct.

##### Reviewer notes

- **C1-4P is the dominant finding.** Pause/Resume/Cancel against operator-owned rebuilds always returns 409 in production. The third-pass review fixed several other concurrency bugs but introduced this regression. Existing Tier 1 tests pass because they substitute the entire `IProjectionRebuildCheckpointStore`. R2-A6 end-state inspection would have caught it.
- D3-A, D3-B, D3-E (per-aggregate scope keys, active-rebuilds index, rebuild-from-rebuild-checkpoint) **were genuinely applied** in this pass — the third-pass review's "MISS" flags are now closed.
- P-C6/P24 per-aggregate progress > 0L test is now present (`ProjectionUpdateOrchestratorTests.cs:486-538`).
- DEC10 was only PARTIALLY resolved: `SaveAsync` gained the IsDifferentOperation guard for active+terminal, but `NotStarted` is excluded (DEC3-4P) and `ResetAsync` still allows terminal overwrites (DEC4-4P).
- The 8 decisions cluster around lifecycle-state-machine semantics — same surface as prior passes. **Recommendation:** bundle DEC1-4P / DEC2-4P with the deferred D2a `RebuildSchedulerActor` follow-up story; the synchronous one-shot Retry/Replay model cannot fully close AC5 alone.

#### Code review run on 2026-05-16 (Codex filtered implementation scope `beed5a8e..HEAD`, generated diff/cache files excluded)

_Local three-lens review because separate subagents were not used in this session. Raw scope: 48 filtered files, +5602/-199. After triage: **0 decision-needed, 3 patch, 0 defer, 0 dismiss**._

- [x] [Review][Patch] **Bounded rebuilds can remain `Running` forever after the per-aggregate checkpoint split** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:360] — Applied: terminal success now uses the highest matched per-aggregate progress when evaluating and saving a bounded successful rebuild; added separate operator/per-aggregate checkpoint coverage.
- [x] [Review][Patch] **Pause/resume/cancel transitions drop the bounded replay `ToPosition`** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:259] — Applied: status-only transitions preserve `existing.ToPosition`; controller coverage now pins pause preserving the bound.
- [x] [Review][Patch] **Active rebuild index writes are best-effort even though poller safety depends on them** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:175] — Applied: active lifecycle saves require active-index persistence before publishing the active checkpoint, terminal/not-started saves fail if index cleanup cannot complete, and tests cover active-index failure.

#### Code review run on 2026-05-16 (Opus 4.7 1M — third continuation pass against uncommitted working tree `fb36737c..WORKTREE`, 18 files, +726/-112 LOC)

_Three adversarial layers: Blind Hunter (diff-only), Edge Case Hunter (diff + project), Acceptance Auditor (diff + spec + CLAUDE.md). Raw findings: 21 Blind + 26 Edge + 11 Auditor. After dedup and triage: **10 decision-needed, 27 patch, 10 defer, 4 dismiss**._

##### Patch verification (21 outstanding from prior continuation pass)

| Patch | Claimed | Verified | Notes |
|---|---|---|---|
| P-C15-CONT — gate `Succeeded` on `LastAppliedSequence >= ToPosition` + page-complete | APPLIED | **ACCEPT** | `reachedBound`+`allMatchedWorkComplete`+`matchedAny` tri-state; new `_WithMoreEventsThanPageSizeDoesNotWriteSucceeded` pins it. |
| P-C4-CONT — replace `anyApplied` flag with explicit `noWorkDone`/`lifecycleInterrupted` | APPLIED | **PARTIAL** | Tri-state in place via `RebuildDeliveryResult`. Empty-events path returns `Complete(checkpoint, pageComplete=true)` so all-empty enumerations write `Succeeded` if `reachedBound` — but `matchedAny=false` then suppresses terminal write (Edge-A26 regression: fresh-domain Replay hangs at Running). |
| P-C1-CONT — `IsValidOperationId` via `Ulid.TryParse` | APPLIED | **PARTIAL** | Uses `UniqueIdHelper.ToGuid` + uppercase-only check, not `Ulid.TryParse` per CLAUDE.md R2-A7 letter. Functionally close; rejects valid lowercase ULIDs that elsewhere in stack accept. |
| P-C7-CONT — move lifecycle re-check BEFORE `UpdateProjectionAsync` | APPLIED | **PARTIAL** | Re-check now `:462-475` before write — good. But re-check reads by KEY (D3-F design); concurrent OperationId swap not detected → projection commit can precede checkpoint guard's `OperationInFlight` rebuff (Edge-A3 → non-idempotent projection corruption). |
| P-C8-CONT — drop `TimeoutException` from `IsStateStoreUnavailable` (store + controller) | APPLIED | **PARTIAL** | Both sides drop `TimeoutException`. Overshoots: bare `TimeoutException` from HTTP/2 socket-timeout surface now non-retryable → 500 instead of bounded retry+503. Two reviewers independently flagged. |
| P-C5-CONT — bail iteration on `ReadLastDeliveredSequenceAsync` failure (return Interrupt) | APPLIED | **ACCEPT** | Catch returns `RebuildDeliveryResult.Interrupt()`; new `_CheckpointReadFailureDoesNotRestartAggregateFromZero` pins it. |
| P-C14-CONT — catch `OperationCanceledException` at rebuild boundary, write `Canceled` | APPLIED | **PARTIAL** | Catch present; uses `CanRunRebuild` which excludes `NotStarted` → cancel-before-first-iteration writes nothing. Cancel-scope OperationId mismatch loses write under D3-F guard (Edge-A8). |
| P-C9-CONT — reject empty/whitespace `AggregateId` (only `null` is wildcard) | APPLIED | **ACCEPT** | `ArgumentException.ThrowIfNullOrWhiteSpace` + `ValidateKeyPart`. Tests cover `""` and `" "`. |
| P-C10-CONT — `OperationId` check at terminal `Succeeded` save | APPLIED | **PARTIAL** | No explicit `finalSnapshot.OperationId == checkpointScope.OperationId` precondition. Relies on `IsDifferentActiveOperation` interplay inside `SaveAsync` (D3-F coupling) — works for `IsLifecycleActive` existing.Status, races against terminal-record-then-overwrite (Edge-A2). |
| P-C11-CONT — bounded retry on `IsStateStoreUnavailable` (3 attempts, 50/200/500ms) | APPLIED | **ACCEPT** | `MaxEtagRetries=3`, `s_retryDelays` array. No jitter (recommended but acceptable). Test pins it. |
| P-C13-CONT — reject `Failed`+lower-sequence as `StaleCheckpoint` | APPLIED | **ACCEPT** | New `StaleCheckpoint` reason; mapped to 409 in controller. Test pins it. |
| P-C2-CONT — `MaxExceptionUnwindDepth` off-by-one fix | APPLIED | **PARTIAL** | Changed `>=` to `>` — now examines 9 frames (depths 0..8). Constant name implies 8-frame cap; naming/behavior mismatch. |
| P-C3-CONT — re-add `>= toPosition` early-`continue` | APPLIED | **PARTIAL** | Present at `:284-286`. But early-`continue` does not increment a per-aggregate complete counter; combined with `matchedAny=false` semantics, domain-wide rebuild where all aggregates skip still suppresses `Succeeded` (Blind-A7). |
| P-C12-CONT — add `NotStarted` to `IsNonTerminalAdvancement` | APPLIED | **ACCEPT** | Defense-in-depth gap closed. |
| **P-C6-CONT** — test `from > 0L` boundary | **NOT-APPLIED** | **MISS** | All new orchestrator tests stub `ReadLastDeliveredSequenceAsync(...).Returns(0)`. Inclusive/exclusive `fromSequence` regression still undetected. |
| **D3-A** — re-semantics operator-scope `LastAppliedSequence` to enumeration progress | **NOT-APPLIED** | **MISS** | Spec narrative claims "Decisions resolved → all 6 routed to patches" but diff has no D3-A change. `LastAppliedSequence` for domain-wide rebuilds is still cross-stream `Math.Max` — meaningless and inflated. |
| **D3-B** — separate `(tenant, domain)` active-rebuilds index | **NOT-APPLIED** | **MISS** | `HasActiveOperatorRebuildAsync` still probes `(tenant, domain, domain, null, null)` — silent miss on multi-projection-per-domain registrations. |
| D3-C — Failed → Retrying via `ResetAsync` | APPLIED | **ACCEPT** | Controller branches on `status == Retrying`. Test pins it. |
| D3-D — `IProjectionRebuildCheckpointStore.ResetAsync` XML doc on trust boundary | APPLIED | **ACCEPT** | `<remarks>` block added; callers responsible for authorization. |
| **D3-E** — read `perAggregateProgress` from rebuild checkpoint, not poller | **NOT-APPLIED** | **MISS** | Still reads `checkpointTracker.ReadLastDeliveredSequenceAsync(identity, ...)`. Reset+Replay still produces empty pages if poller is ahead. AC4/AC5 unmet. |
| D3-F — `OperationInFlight` rejection of concurrent active operators | APPLIED | **PARTIAL** | `IsDifferentActiveOperation` guard inside `SaveAsync` + new reason code + 409 mapping. Asymmetric on null OperationIds (Blind-A21); doesn't protect `ResetAsync`, terminal-to-terminal writes, or Reset-to-NotStarted overwrites (Edge-A1, A10, A19). |

**Summary:** 11 APPLIED-CORRECT, 6 APPLIED-PARTIAL, 4 NOT-APPLIED. The "Decisions resolved (2026-05-16)" prose in the spec is over-stated: **D3-A, D3-B, D3-E** were not actually patched, and **P-C6** test gap remains unfilled.

##### Decision-needed (10) — require user input

- [x] [Review][Decision] **DEC1 — D3-A / D3-B / D3-E NOT applied despite spec narrative** [`ProjectionUpdateOrchestrator.cs:357-359,478-486,618-657`] — Spec §"Decisions resolved (2026-05-16)" lists D3-A/B/E as "routed to patches" but diff contains none of them. AC4 is partially unmet on three independent axes: domain-wide `LastAppliedSequence` is meaningless; `HasActiveOperatorRebuildAsync` key-collides with `projectionName==domain`; Reset+Replay reads poller checkpoint and produces empty pages. Options: (a) apply all three patches now (substantial work), (b) route to a new follow-up story bundled with D2a RebuildSchedulerActor, (c) accept the spec narrative is wrong and amend it to reclassify these as deferred. Recommended: (b) — bundle with D2a since they share orchestrator surface area.
- [x] [Review][Decision] **DEC2 — `NoDomainServiceRegistered` permanent error vs transient interrupt** [`ProjectionUpdateOrchestrator.cs:345-347`] — Currently returns `RebuildDeliveryResult.Interrupt()` when no domain service is registered; outer loop bails without `Succeeded`/`Failed` write. Status hangs `Running` indefinitely. Options: (a) write `Failed(reason=NoDomainServiceRegistered)` with a new reason code, (b) keep `Interrupt` (treat as configuration-not-yet-ready transient), (c) probe at controller boundary and 503 before orchestrator runs. Recommended: (a) — permanent config errors should not stall the lifecycle.
- [x] [Review][Decision] **DEC3 — Cancel-cleanup bypass of `IsLifecycleProtected`** [`ProjectionUpdateOrchestrator.cs:296-313`] — When operator marks `Pausing` between cancellation and cleanup, `SaveAsync(...Canceled...)` returns `Failure(CheckpointConflict)` since `IsLifecycleProtected(Pausing)=true`. Catch swallows result (`_ =`). Operator-visible state stays `Pausing` forever. Options: (a) call `ResetAsync` from cancel-cleanup to bypass guards, (b) add `Canceled` to non-terminal-advancement allowed list within `IsLifecycleProtected`, (c) log+alert and accept. Recommended: (a) — Cancel is operator-intentional terminal write; ResetAsync was designed for this trust boundary.
- [x] [Review][Decision] **DEC4 — `IsStateStoreUnavailable` overshoots with bare `TimeoutException`** [`ProjectionRebuildCheckpointStore.cs:339`, `StreamsController.cs:339-342`] — P-C8 dropped `TimeoutException` to surface application-layer bugs as 500. Both reviewers (Blind+Edge) flag that some HTTP/2 sockets surface DAPR timeouts as bare `TimeoutException` (no `TaskCanceledException` wrapper) → these now become 500 InternalError instead of bounded retry+503+Retry-After. Options: (a) keep current behavior; rely on DAPR SDK to wrap timeouts in `TaskCanceledException`, (b) re-add `TimeoutException` but only when wrapped under `DaprException`/`HttpRequestException`, (c) introduce a `[Transient]` attribute or marker exception family. Recommended: (b) — narrow classification preserves P-C8 intent.
- [x] [Review][Decision] **DEC5 — `ActorMethodInvocationException` with null inner classified as 500** [`StreamsController.cs:339-342`] — Dapr actor runtime can throw `ActorMethodInvocationException("actor method failed")` with `InnerException == null` when the actor process is unreachable. Code now returns false (→ 500); previously blanket-503. Options: (a) keep current behavior (operator escalates), (b) restore 503 fallback when inner is null, (c) match on message text. Recommended: (b) — transport-level actor unreachability is the canonical 503 case.
- [x] [Review][Decision] **DEC6 — `EventStoreGatewayClient` mutates caller-owned `HttpClient`** [`EventStoreGatewayClient.cs:35-41`] — Constructor unconditionally lowers `_httpClient.MaxResponseContentBufferSize` to `MaxStreamReadResponseBytes` (16 MiB). With `IHttpClientFactory`-shared instances, throttles ALL endpoints (including query responses that may legitimately exceed 16 MiB). Throws `InvalidOperationException` if `HttpClient` already sent a request (warm restart). Also `long → int` narrowing has no overflow guard. Options: (a) accept caller responsibility, document constraint, add overflow guard, (b) wrap with per-request `HttpMessageHandler`/typed client, (c) per-request `HttpClient` instance for stream reads. Recommended: (a) — minimal blast radius; document + validate `<= int.MaxValue`.
- [x] [Review][Decision] **DEC7 — `ReplayProjection` returns 202 Accepted even when synchronous rebuild has already failed** [`AdminProjectionRebuildController.cs:308-312`] — Controller awaits `RebuildProjectionAsync` synchronously; if domain `/project` rejects, orchestrator writes `Failed(ProjectionApplyRejected)` and returns normally. Controller returns 202 + `AdminOperationResult.Success=true`. Operator dashboard reports success while projection rebuild is Failed. Coupled with D2a deferral. Options: (a) read terminal checkpoint after `RebuildProjectionAsync` and map `Failed/Succeeded` to 200/4xx, (b) keep 202 + require operator poll `/rebuild-status`, (c) split synchronous and asynchronous variants. Recommended: (a) for current sync mode; revisit when D2a scheduler lands.
- [x] [Review][Decision] **DEC8 — `ResetAsync` has no `IsDifferentActiveOperation` guard** [`ProjectionRebuildCheckpointStore.cs:177-242`] — Two concurrent `GlobalAdministrator` operators each call `/replay` or `/retry` for the same projection. `ResetAsync` ETag-conflict-retries but overwrites caller's `OperationId`. No `OperationInFlight` rebuff. D3-D doc states "callers must enforce operator authorization" but does not say "must serialize". Extends D3-D defense-in-depth gap. Options: (a) add `IsDifferentActiveOperation` to `ResetAsync` write path, (b) extend doc to "must serialize", accept current behavior, (c) controller-side mutex by projection name. Recommended: (a) — store-side guard is the single source of truth.
- [x] [Review][Decision] **DEC9 — Retry reuses prior failed `OperationId`** [`AdminProjectionRebuildController.cs:243-249`, `ProjectionRebuildCheckpointStore.cs:206-212`] — `TransitionExistingAsync` passes `operationId: null` for Retry; `ResetAsync` inherits `existing.OperationId`. Audit trail blurs "first-attempt-failed-then-retried-and-succeeded" from "first-attempt-never-retried". Spec P-D5 design intent was "fresh ULID per Replay/Reset". Options: (a) generate fresh ULID for Retry like Replay, (b) keep inherited OperationId (Retry is logically continuation), (c) add separate `RetriedFromOperationId` field. Recommended: (a) — aligns with P-D5 intent.
- [x] [Review][Decision] **DEC10 — Terminal-status `OperationId` can be silently overwritten** [`ProjectionRebuildCheckpointStore.cs:129-144,302-308`] — `IsLifecycleActive` excludes `Succeeded`/`Failed`/`NotStarted`. Operator B writes `SaveAsync(scope=B, Succeeded, ...)` against existing `(OperationId=A, Status=Succeeded)`. No-op check fires only when ALL fields match; differ ToPosition or reason → falls through to write with `OperationId=B`. Same hazard for `NotStarted` (Reset-to-NotStarted race silently overwritten by A's in-flight `SaveAsync(Running)`). Options: (a) add terminal-record OperationId equality precondition to `SaveAsync` and `ResetAsync`, (b) make terminal-status writes fully replace, accept audit trail loss, (c) write a separate audit log for terminal transitions. Recommended: (a) — preserves single-row-per-scope semantics and audit fidelity.

##### Patches (27) — fixable without input

CRITICAL:

- [x] [Review][Patch] **P1 — `MissingStream` 404 only fires when `FromSequence == 0`** [`StreamsController.cs:104-112`] — `if (currentSequence == 0 && request.FromSequence == 0)` should be `if (currentSequence == 0)`. With non-zero From, missing streams currently return 200 OK + empty events + `LatestSequence: 0 < FromSequence` (internally contradictory).
- [x] [Review][Patch] **P2 — `GetCurrentSequenceAsync` swallows all exceptions as `EventDeserializationException`** [`AggregateActor.cs:674-682`] — `catch (Exception ex) when (ex is not OperationCanceledException)` reclassifies NRE/OOM/InvalidOperation/KeyNotFound as data-corruption errors with magic `-1` sentinel. Narrow to `JsonException or InvalidDataException or EventDeserializationException`; let programmer errors propagate as 500.

HIGH:

- [x] [Review][Patch] **P3 — `ReadEventsRangeAsync` missing negative `fromSequence` guard** [`AggregateActor.cs`] — Add `ArgumentOutOfRangeException.ThrowIfNegative(fromSequence)` at method entry; mirrors the fake's contract.
- [x] [Review][Patch] **P4 — `ReadEventsRangeAsync` overflow guard ordering vs empty-stream contract** [`AggregateActor.cs:635-637`] — Overflow guard fires before metadata read. Empty stream with `fromSequence = int.MaxValue - 5` now throws instead of returning `[]` (contract regression). Fix: either move the overflow guard after the empty-stream short-circuit, or return `[]` when `currentSequence == 0` regardless of `fromSequence`.
- [x] [Review][Patch] **P5 — `ReadEventsRangeAsync` missing `toSequence` bounds check** [`AggregateActor.cs`] — Caller passing `toSequence == long.MaxValue` proceeds into arithmetic without guard. Validate against `int.MaxValue` like `fromSequence`.
- [x] [Review][Patch] **P6 — `IsValidOperationId` uses `UniqueIdHelper.ToGuid`+uppercase, not `Ulid.TryParse`** [`ProjectionRebuildCheckpointStore.cs:52-68`] — CLAUDE.md R2-A7 letter mandates `Ulid.TryParse` for ULID validation. Current check rejects lowercase ULIDs that elsewhere in stack accept (`Ulid.TryParse` is case-insensitive). Replace with `Ulid.TryParse(operationId, out _)`. Also remove the wasteful `operationId.ToUpperInvariant()` allocation.
- [x] [Review][Patch] **P7 — Empty `await foreach` (matchedAny==false) hangs at Running** [`ProjectionUpdateOrchestrator.cs:266-330`] — Fresh domain with no tracked aggregates: loop iterates zero times → `matchedAny=false` → terminal `Succeeded` write skipped. Status row stays `Running` from `SaveLifecycleAsync` write → `HasActiveOperatorRebuildAsync` returns true → poller disabled indefinitely. Reintroduces the P-C4-CONT livelock through the `matchedAny` requirement. Fix: when `matchedAny=false` and `reachedBound`/no-aggregates-to-do, write `Succeeded` (or new `RebuildPagedNothingToDo` reason).
- [x] [Review][Patch] **P8 — Empty-events path returns `Complete(checkpoint, pageComplete=true)` masquerading as work-complete** [`ProjectionUpdateOrchestrator.cs:369-371,505-507`] — `if (events.Length == 0) return RebuildDeliveryResult.Complete(checkpoint);` defaults `pageComplete=true`. If `reachedBound` evaluates true elsewhere, `Succeeded` is written with no actual apply. Fix: when events.Length==0, return either `null` (skip semantics) or `Complete(checkpoint, pageComplete: existing.LastAppliedSequence >= existing.ToPosition)`.
- [x] [Review][Patch] **P9 — `pageComplete` exact-`RebuildPageSize` boundary mis-fires** [`ProjectionUpdateOrchestrator.cs:505-507`] — Stream of exactly 256 events with `ToPosition==null`: `events.Length < RebuildPageSize` is false → `pageComplete=false` → `Succeeded` never written. Fix: add `|| highestApplied >= currentSequence` condition (requires actor `currentSequence`) OR detect EOS via `actor.GetCurrentSequenceAsync` after page read.
- [x] [Review][Patch] **P10 — `ProjectionApplyRejected` save uses request `cancellationToken`** [`ProjectionUpdateOrchestrator.cs:417-426`] — Failed-state save uses `cancellationToken`; operator pressing Cancel mid-save silently discards the failure record. The outer cancel handler then writes `Canceled(RebuildCanceled)` masking upstream failure cause. Fix: use `CancellationToken.None` (matches `:296-313` outer cancel handler).

MEDIUM:

- [x] [Review][Patch] **P11 — `MapSaveFailure` missing `ForbiddenRole` handler** [`AdminProjectionRebuildController.cs`] — New `ForbiddenRole` reason added; `MapSaveFailure` switch was not updated. Future code threading `ForbiddenRole` through a save result falls into the `_ => 500` catchall. Add 403 mapping.
- [x] [Review][Patch] **P12 — `Retry-After` header missing on 409 `OperationInFlight`** [`AdminProjectionRebuildController.cs`] — 503 paths add `Response.Headers.RetryAfter = "5"`. Conceptually, `OperationInFlight` (409) is also retry-after — but no header is emitted. Add `Retry-After: 5` (or higher) on the 409 response.
- [x] [Review][Patch] **P13 — `TransitionExistingAsync` Retry drops `FailureReasonCode`** [`AdminProjectionRebuildController.cs:243-249`] — `ResetAsync(scope, existing.LastAppliedSequence, Retrying, failureReasonCode: null, ...)` clears the prior failure reason from the record. Audit trail of "why did this rebuild fail before retry" is lost. Fix: pass `existing.FailureReasonCode` (couples with DEC9 if Retry inherits OperationId).
- [x] [Review][Patch] **P14 — Cancellation handler `CanRunRebuild` excludes `NotStarted`** [`ProjectionUpdateOrchestrator.cs:297-310`] — Cancel arriving before first iteration: `existing.Status = NotStarted` → `CanRunRebuild(canceledSnapshot)` returns false → no Canceled write. Status hangs `NotStarted`. Fix: include `NotStarted` in `CanRunRebuild` (or use `ResetAsync` for cancel-cleanup per DEC3).
- [x] [Review][Patch] **P15 — Cancel write returns `OperationInFlight` and is silently swallowed** [`ProjectionUpdateOrchestrator.cs:296-313`] — `SaveAsync(scope=A, ..., Canceled)` against `existing.OperationId=B` (concurrent operator B raced reset+replay between A's iteration exit and cleanup) → guard returns `OperationInFlight`; result is `_ = await`-discarded. Fix: log when cancel-cleanup save fails so operator can observe lost-cancel; consider `CancellationToken.None`+`ResetAsync` as a fallback (couples with DEC3).
- [x] [Review][Patch] **P16 — `IsDifferentActiveOperation` asymmetric null** [`ProjectionRebuildCheckpointStore.cs:316-322`] — Guard requires BOTH `existing.OperationId` and `scope.OperationId` non-empty. Poller (no OperationId) racing operator (fresh OperationId) → guard skipped → poller overwrites operator's row silently. Fix: reject when `existing.OperationId != null && scope.OperationId == null && IsLifecycleActive(existing.Status)` (or symmetric mismatch).
- [x] [Review][Patch] **P17 — `EventStoreGatewayClient` `MaxStreamReadResponseBytes` overflow** [`EventStoreGatewayClient.cs:35-41`] — `MaxStreamReadResponseBytes` is `long`; assignment to `int MaxResponseContentBufferSize` narrows. `3_000_000_000L → wraps to negative int → ArgumentOutOfRangeException` on first send. Fix: validate `MaxStreamReadResponseBytes <= int.MaxValue` in options constructor.
- [x] [Review][Patch] **P18 — `MaxExceptionUnwindDepth` constant-name / behavior mismatch** [`ProjectionRebuildCheckpointStore.cs:332,335`] — `MaxExceptionUnwindDepth = 8` + `if (depth > MaxExceptionUnwindDepth)` examines 9 frames (depths 0..8). Either revert to `>=` for true 8-frame cap or rename constant to `MaxExceptionFrames`/raise to 16. Same naming should align with controller-side check.
- [x] [Review][Patch] **P19 — Per-iteration projection commit precedes guard rebuff** [`ProjectionUpdateOrchestrator.cs:466-490`] — `preSave = ReadAsync(scope)` re-check reads by KEY (D3-F design omits OperationId from key). Concurrent operator swap not detected → `UpdateProjectionAsync` commits stale page → THEN `SaveAsync` returns `OperationInFlight`. Non-idempotent projections (counters, append logs) corrupt. Fix: re-check must compare `preSave.OperationId == checkpointScope.OperationId` before calling `UpdateProjectionAsync`.

LOW:

- [x] [Review][Patch] **P20 — `StreamReadPageBuilder` ULID generation makes tests non-deterministic** [`StreamReadPageBuilder.cs:62-64`] — Removes `"message-{n}"` literals in favor of `UniqueIdHelper.GenerateSortableUniqueStringId()`. Tests asserting exact IDs across runs break or pass by coincidence. Fix: accept optional `Func<long, string> messageIdFactory` parameter for deterministic test mode.
- [x] [Review][Patch] **P21 — Server-side test envelope still uses non-ULID `MessageId/CorrelationId/CausationId`** [`StreamsControllerTests.cs:311-329`] — Build helper uses `$"msg-{seq}"`. ULID regression on response-side `MessageId` would not be caught. Fix: use `UniqueIdHelper.GenerateSortableUniqueStringId()` or `Ulid.NewUlid().ToString()`.
- [x] [Review][Patch] **P22 — `s_retryDelays` array length implicitly coupled to `MaxEtagRetries-1`** [`ProjectionRebuildCheckpointStore.cs:163-170,231-238`] — Future tweak of `MaxEtagRetries` to 4 indexes `s_retryDelays[3]` → `IndexOutOfRangeException`. Fix: `Debug.Assert(s_retryDelays.Length == MaxEtagRetries - 1)` at static init, or `s_retryDelays[Math.Min(attempt, s_retryDelays.Length - 1)]`.
- [x] [Review][Patch] **P23 — `AssertNoForbiddenLeakage` blocklist gaps** [`StreamsControllerTests.cs:294-307`] — Substring match on `"ETag"` with `Case.Insensitive` false-positives on translated upstream messages. Missing JWT-shape and `Authorization:` prefix detection. Fix: case-sensitive exact-word boundaries + add JWT regex `eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}`.
- [x] [Review][Patch] **P24 — Test: per-aggregate progress `> 0L` boundary (P-C6-CONT carry-over)** [`ProjectionUpdateOrchestratorTests.cs`] — Stub `ReadLastDeliveredSequenceAsync(...).Returns(5L)`, assert `ReadEventsRangeAsync(5L, ...)`, checkpoint advances from `5` → `5 + n`. Pins inclusive/exclusive `fromSequence` semantic.
- [x] [Review][Patch] **P25 — Test: multi-aggregate `PageComplete` mix** [`ProjectionUpdateOrchestratorTests.cs`] — Two tracked aggregates, one delivery returns `PageComplete=true`, other returns `PageComplete=false`. Assert `Succeeded` is NOT written (regression would flip `&=` to `|=`).
- [x] [Review][Patch] **P26 — Test: cancellation cleanup null-snapshot path** [`ProjectionUpdateOrchestratorTests.cs:1420-1446`] — Stub `ReadAsync(...).Returns((ProjectionRebuildCheckpoint?)null)` on cancel-cleanup. Assert no `SaveAsync` and a warning log. Current test pins only the happy-path Running→Canceled write.
- [x] [Review][Patch] **P27 — Test: state-store end-state assertion for guards (`OperationInFlight`, `StaleCheckpoint`, `CheckpointUnavailable` retry)** [`ProjectionRebuildCheckpointStoreTests.cs`] — Add at least one test per guard that captures the persisted row via `Arg.Do<>(...)` and asserts the row is unchanged (or matches expected). Currently asserts only `result.ReasonCode` + mock-call-count, per CLAUDE.md R2-A6 spirit.

##### Deferred (10)

- [x] [Review][Defer] **W1 — H10/M2/M4/M8/M9/M10/M13/L6 intentional defers** — already deferred in prior pass.
- [x] [Review][Defer] **W2 — `Retry-After: "5"` hardcoded, no jitter/config** — recorded for ops; not a regression.
- [x] [Review][Defer] **W3 — `Math.Max` clamping for non-`Failed` status writes** — defense-in-depth; not a regression from this pass.
- [x] [Review][Defer] **W4 — `StreamReplayReasonCodes.StaleCheckpoint` re-added after prior-pass P22 removed it** — Contracts addition is non-breaking; flag for changelog hygiene.
- [x] [Review][Defer] **W5 — Two actor round-trips (`GetCurrentSequenceAsync` + `ReadEventsRangeAsync`) per stream read** — perf optimization, not correctness.
- [x] [Review][Defer] **W6 — `RebuildPageSize=256` hardcoded; operators must re-invoke for streams >256 events** — couples with D2a scheduler.
- [x] [Review][Defer] **W7 — Pre-existing checkpoint records with non-strict `OperationId` become invisible after deploy** — migration concern; no real-world data expected.
- [x] [Review][Defer] **W8 — Concurrent `ResetAsync` interleaving without guard** — couples with DEC8.
- [x] [Review][Defer] **W9 — Final `Succeeded` race window between iteration exit and `finalSnapshot` read** — couples with DEC10.
- [x] [Review][Defer] **W10 — `IsLifecycleActive` excludes terminal statuses; terminal-to-terminal OperationId overwrite** — couples with DEC10.

##### Dismissed (4) — verified false positive or informational

- A11 (Auditor) — explicit defer ack list (H10/M2/etc.); informational, not a finding.
- Edge — `Response.Headers.RetryAfter = "5"` duplication concern in `StreamsController` vs `AdminProjectionRebuildController` — defense-in-depth gap, not a defect.
- Edge — negative assertion missing on no-`RetryAfter`-on-500 invariant — informational; covered by status-code assertion.
- Edge — `pageComplete` test gap for multi-aggregate — promoted to P25 patch; original framing as "test gap" was indistinct from P25.

##### Reviewer notes

- The most material finding in this pass is **DEC1**: three "resolved" decisions (D3-A, D3-B, D3-E) were never patched. The spec narrative is over-stated. AC4 is partially unmet on three independent axes until these are routed somewhere concrete.
- The applied patches that landed correctly (11/21 ACCEPT) are real improvements. The PARTIAL patches (6/21) have new edge cases the reviewers found at the implementation seams: `IsDifferentActiveOperation` asymmetry (P16), cancel-scope races (P14, P15), terminal-record OperationId overwrite (DEC10), and the `matchedAny=false` regression (P7).
- The `MissingStream` 404-only-at-FromSequence-0 (P1) is the next-most-material finding; it's a public-contract regression on the stream-read endpoint.
- 10 decisions concentrate in the rebuild-orchestrator/checkpoint-store interface — same surface as the prior pass's D3 series. Consider bundling DEC1, DEC2, DEC3, DEC8, DEC9, DEC10 with the D2a `RebuildSchedulerActor` follow-up story since they all stem from the lifecycle-state-machine and OperationId-ownership boundary.

#### Code review run on 2026-05-15 (Opus 4.7 — continuation pass against `c0d439d2..HEAD`, 3 files, +208 source LOC)

_Continuation pass requested by the second-pass review's exit note. Diff scope intentionally narrowed to the second-pass apply commits (`c3205fdb` + `fc54ec15`) so the review verifies that the 8 patches the prior pass claimed APPLIED actually landed correctly and surfaces any new defects introduced by those changes. Outstanding patches from the prior pass (~20 not-yet-applied items: H2/H3/H4/H6/H10/H12, M2/M3/M7-M15, L3-L7, plus D2b/D2c/D2e follow-throughs) remain open in the second-pass section below — this pass did not re-evaluate them._

_Three adversarial layers: Blind Hunter (diff-only), Edge Case Hunter (diff + project), Acceptance Auditor (diff + spec + CLAUDE.md). Raw findings: 15 Blind + 21 Edge + 2 new from Auditor. After dedup and triage: **2 CRITICAL, 8 HIGH, 11 MEDIUM, 7 LOW** → 6 decision-needed, 15 patch, 3 defer, 4 dismiss._

##### Patch verification (8 APPLIED + 2 PARTIAL claims from second-pass)

| Patch | Claimed | Verified | Notes |
|---|---|---|---|
| C1 — bounded `ReadEventsRangeAsync` | APPLIED | **PARTIAL** | Read change correct; introduces N1-CONT (premature `Succeeded` for streams > 256 events). |
| C2 — `Succeeded` on operator scope | APPLIED | ACCEPT | `lastScope` removed; cancellation re-check inside loop. |
| C4 — `SaveAsync` lifecycle protection | APPLIED | ACCEPT | `IsLifecycleProtected` × `IsNonTerminalAdvancement` correct; reason map correct. |
| H5 — `ScopeForCheckpoint` preserves `AggregateId` | APPLIED | ACCEPT | Operator scope preserved; only `OperationId` overlaid. |
| H7 — `Succeeded → Running` blocked | APPLIED | ACCEPT | Coupled with C4. |
| H11 — idempotent no-op caller `OperationId` | APPLIED | **PARTIAL** | Whitespace OperationId path still leaks prior id (H4-CONT); needs ULID-shape gate too. |
| M1 — `*` reserved char | APPLIED | ACCEPT | Pre-existing `*` aggregateIds become unreadable (W1-CONT, deferred). |
| M5 — store-side recursion cap | PARTIAL | ACCEPT | Off-by-one (P-C2-CONT) — guard fires at depth 8 but should examine 9th frame. |
| M17 — `OperationId` shape validation | APPLIED | **PARTIAL** | Implementation accepts `[A-Za-z0-9]` not strict Crockford base32; folds into P-C1-CONT (use `Ulid.TryParse`). |
| H14 — pre-`SaveAsync` lifecycle re-check | PARTIAL | ACCEPT | Re-check present; full two-phase cancel correctly deferred to D2a scheduler. |

##### Decisions resolved (2026-05-16) → all 6 routed to patches

- **D3-A → P-D3-A**: Re-semantics operator-scope `LastAppliedSequence` to enumeration progress (aggregates-completed cursor) for domain-wide rebuilds; resume routes through per-aggregate `ProjectionCheckpointTracker`. Patch + docs update.
- **D3-B → P-D3-B**: Add a separate `(tenant, domain)` active-rebuilds index entry written at `SaveLifecycleAsync`, cleaned up on terminal status. `HasActiveOperatorRebuildAsync` reads the index instead of probing by `(tenant, domain, domain)`. Patch + tests.
- **D3-C → P-D3-C**: Controller `RetryProjection` routes through `ResetAsync(lastAppliedSequence: existing.LastAppliedSequence, status: Retrying)` so `Failed → Retrying` transitions preserve the SaveAsync trust boundary. Patch + controller test.
- **D3-D → P-D3-D**: Add XML doc remark on `IProjectionRebuildCheckpointStore.ResetAsync` documenting that it bypasses monotonic and lifecycle guards by design; callers are responsible for authorization. Docs-only patch.
- **D3-E → P-D3-E**: `DeliverProjectionForRebuildAsync` reads `perAggregateProgress` from the rebuild checkpoint, not from the poller checkpoint. Rebuild becomes self-contained; Reset+Replay actually replays. Domain `/project` idempotency already required per AC4. Patch + Reset+Replay regression test.
- **D3-F → P-D3-F**: `SaveAsync` returns `Failure(OperationInFlight)` when `existing.OperationId is { } e && e != scope.OperationId && IsLifecycleActive(existing.Status)`. New `OperationInFlight` reason code in `StreamReplayReasonCodes`. Couples with P-D3-B's active-rebuilds index. Patch + Contracts addition + tests.

##### Decision-needed (6) — original record

- [ ] [Review][Decision] **D3-A — Domain-wide rebuild persists meaningless `LastAppliedSequence`** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:266, src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:119] — Operator-scope checkpoint is a single key; `Math.Max(existing.LastAppliedSequence, lastAppliedSequence)` runs across heterogeneous per-aggregate sequence spaces. The persisted operator-visible `LastAppliedSequence` is the max sequence across unrelated aggregate streams — neither progress nor a meaningful resume point. Options: (a) per-aggregate scope keys (operator scope holds enumeration progress only); (b) document operator-scope `LastAppliedSequence` as informational-only for domain-wide rebuilds and switch resume to per-aggregate tracker; (c) accept current behavior as known-imprecise. Recommended: (b) — minimal blast radius and matches the per-aggregate progress already maintained by `ProjectionCheckpointTracker`.
- [ ] [Review][Decision] **D3-B — `HasActiveOperatorRebuildAsync` keys probe with `domain` as projection name** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:647-651, src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:CreateScope] — Probe constructs `new ProjectionRebuildCheckpointScope(tenant, domain, domain, null, null)`; controller stores under `(tenant, projectionName, projectionName, ...)`. Keys collide ONLY when `projectionName == domain`. Test `DeliverProjectionAsync_ConflictProbeUsesDomainAsProjectionNameDocumentingCurrentConstraint` documents but does not enforce the constraint. The prior-pass P-D4 added docs but the structural defect is unchanged. Options: (a) thread the actual projection registration into the probe; (b) maintain a separate active-rebuilds index keyed by `(tenant, domain)`; (c) keep current behavior with hardened test that fails CI on multi-projection-per-domain registrations. Recommended: (b) — small new index entry written at SaveLifecycleAsync; cheap to read.
- [x] [Review][Decision] **D3-C — `Failed → Retrying` blocked by `SaveAsync` C4 guard** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:265-276] — `IsLifecycleProtected` includes `Failed`; `IsNonTerminalAdvancement` includes `Retrying`. So a controller `Retry` that calls `SaveAsync(... Retrying)` against a `Failed` checkpoint returns `Failure(CheckpointConflict)`. The C4 inline comment claims "Only ResetAsync routes are permitted to flip these." Need to confirm: is the operator Retry path *intended* to flow exclusively through `ResetAsync` (rewind + re-mark `Retrying`)? If yes, current state is correct; document and add a controller test. If no, exclude `Failed` from `IsLifecycleProtected` for `Retrying`-targeted writes. Recommended: (a) — Retry semantics conceptually require explicit rewind; route through `ResetAsync` and add a regression test pinning the `Failed → Retrying` controller path.
- [x] [Review][Decision] **D3-D — `ResetAsync` has no lifecycle/auth guards by design** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:162-223] — `ResetAsync` intentionally bypasses monotonic constraints (operator rewind), but it also bypasses `IsLifecycleProtected`, so any caller can flip `Canceled` → `Running` with a rewound sequence. Currently safe because the only callers are the `GlobalAdministrator`-gated controller endpoints. Options: (a) leave as-is and document the trust boundary on the interface; (b) add an `allowResurrectTerminal: bool` parameter that controllers must opt-in for explicit Reset-after-Cancel/Failed; (c) require `ResetAsync` callers to pass an explicit `previousStatus` for assertion. Recommended: (a) + interface XML doc — defense-in-depth gap is theoretical given current callers, and (b)/(c) add API surface without solving the underlying trust model.
- [ ] [Review][Decision] **D3-E — Reset rebuild + Replay produces a no-op** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:331-333, 457-459] — `DeliverProjectionForRebuildAsync` reads `perAggregateProgress` from the **poller** checkpoint (`checkpointTracker.ReadLastDeliveredSequenceAsync`) and writes back to the **poller** checkpoint after success. Operator runs `Reset` (rewinds rebuild checkpoint to 0) → `Replay`. Poller checkpoint is unchanged at e.g. 1000. `ReadEventsRangeAsync(1000, ...)` returns `[]` for a stream with no new events. `anyApplied` stays false. `Succeeded` is never written. Status hangs at `Running`. Options: (a) Reset clears both rebuild AND poller checkpoint for the scope; (b) rebuild reads from a scope-specific source (rebuild checkpoint's `LastAppliedSequence` instead of poller); (c) document that Reset+Replay only re-projects events written *after* the most recent poll. Recommended: (b) — eliminates the rebuild/poller checkpoint coupling entirely and makes "rewind and replay" actually replay.
- [x] [Review][Decision] **D3-F — `OperationId` invisible to `GetStateKey`; concurrent operators silently merge** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:225-236] — `GetStateKey` derives the storage key from `(tenant, domain, projectionName, aggregateId)` only; `OperationId` is part of the scope record but not the key. Two operators starting concurrent rebuilds against the same projection scope share one row; the second operator's `OperationId` overwrites the first's. P-D5 ("fresh ULID per Replay/Reset") landed in the prior pass, but only governs which ID is generated — it does not change that two concurrent IDs collide on the same key. Options: (a) include `OperationId` in `GetStateKey` (one row per operation; old rows become orphaned and need cleanup policy); (b) reject `SaveAsync` when `existing.OperationId != null && existing.OperationId != scope.OperationId && IsLifecycleActive(existing.Status)` — explicit "operation already in flight" failure; (c) accept current single-operation-per-scope semantics and document. Recommended: (b) — preserves single-row-per-scope storage layout while preventing silent operator-id loss.

##### Patches (15) — fixable without input

CRITICAL:

- [x] [Review][Patch] **P-C15-CONT — Premature `Succeeded` for streams > `RebuildPageSize` (256 events)** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:294-303] — Controller invokes `RebuildProjectionAsync` once synchronously (`AdminProjectionRebuildController.cs:300`); orchestrator iterates each tracked aggregate ONCE per call, applies at most 256 events, then writes `Succeeded` against operator scope without comparing `finalSnapshot.LastAppliedSequence` against `finalSnapshot.ToPosition`. An aggregate with 1000 events past `perAggregateProgress` is reported `Succeeded` after 256 events. Test `ProjectionUpdateOrchestratorTests.cs:152` masks this with `toPosition: 2` + 2 events seeded. Fix: gate `Succeeded` on `finalSnapshot.LastAppliedSequence >= finalSnapshot.ToPosition` (when `ToPosition` is set) AND on actor's `currentSequence` (when `ToPosition` is null). If page exhausted but stream has more events, leave status as `Running` and rely on D2a scheduler / next operator invocation to pick up. Until D2a ships, surface a stable `RebuildPagedIncomplete` reason or explicit `Running` with progress metadata so operators can re-invoke.
- [x] [Review][Patch] **P-C4-CONT — Livelock: `Succeeded` never written when all aggregates have no new events** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:281-289] — `if (!anyApplied) { return; }` skips the terminal `Succeeded` write whenever every iteration's `DeliverProjectionForRebuildAsync` returned `null` (e.g., `events.Length == 0` for every aggregate, every aggregate already at `ToPosition`, every aggregate fails `CanRunRebuild` re-check). Status stays `Running` forever; `HasActiveOperatorRebuildAsync` keeps returning `true`; poller is disabled for the domain indefinitely. Manual `Cancel` becomes the only escape. Compounded by D3-E (Reset+Replay produces empty pages) and Edge M-2 (corrupt tracker → empty enumeration). Fix: replace `anyApplied` flag with explicit reason: `noWorkDone` (write `Succeeded` if `finalSnapshot.LastAppliedSequence >= ToPosition`, else write `Failed` with `RebuildPagedIncomplete` or similar) vs `lifecycleInterrupted` (caller-cancel/pause; do nothing). Couples with P-C15-CONT.

HIGH:

- [x] [Review][Patch] **P-C1-CONT — `IsValidOperationId` accepts non-Crockford alphabet; `ValidateScope` accepts any non-whitespace; `SaveAsync` no-op leaks unvalidated caller `OperationId`** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:47-65, :96-102, :247-249] — Three asymmetric checks for the same data shape: `IsValidOperationId` (read path) accepts `[0-9A-Za-z]` including non-Crockford chars `I/L/O/U` and lowercase; `ValidateScope` (write path) only requires non-whitespace + no reserved chars (5-char OperationIds pass write but fail subsequent read → ghost rebuild per Edge M-7); idempotent no-op return substitutes caller's `scope.OperationId` without validating it. CLAUDE.md R2-A7 mandates `Ulid.TryParse` for ULID validation. Fix: add `IsValidOperationId(...) => operationId is null || Ulid.TryParse(operationId, out _)`. Apply in three places: (1) `ReadAsync` (already there); (2) `ValidateScope` so writes use the same shape check; (3) idempotent no-op return so caller-supplied OperationId substitution is rejected before being echoed.
- [x] [Review][Patch] **P-C7-CONT — Pre-save lifecycle re-check fires AFTER `UpdateProjectionAsync` writes** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:426-439] — Page is applied to `IProjectionWriteActor.UpdateProjectionAsync` (`:426`), THEN `preSave = ReadAsync(scope)` checks lifecycle. If `Pause`/`Cancel` lands between apply and re-check, projection state is committed but rebuild checkpoint is not advanced. On `Resume`, `perAggregateProgress` is unchanged (poller checkpoint not advanced either — see D3-E), so the same page is re-projected. Non-idempotent projections (counters, append-only logs) corrupt. Fix: move the lifecycle re-check BEFORE `UpdateProjectionAsync`. Accept that this still has a tiny race window (between re-check and projection write) but eliminates the post-write/pre-checkpoint window which is much wider.
- [x] [Review][Patch] **P-C8-CONT — `IsStateStoreUnavailable` mis-classifies application-layer `TimeoutException` as transient** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:299] — Custom `JsonConverter` or other application code throwing `TimeoutException` (or wrapping one) is classified as transient → `Failure(CheckpointUnavailable)` → operator sees "Redis is down" while real bug is a deserializer. Fix: replace the unconditional `or TimeoutException` with a more specific check (`TaskCanceledException` from a Dapr/HTTP client, not arbitrary `TimeoutException`) OR remove `TimeoutException` from the predicate entirely and force programmer-error `TimeoutException` to propagate as 500. Same fix needs to be applied to the controller-side `StreamsController.IsServiceUnavailable` (referenced as M5-controller-side-still-open in the prior pass).
- [x] [Review][Patch] **P-C5-CONT — Catch silently swallows `ReadLastDeliveredSequenceAsync` failure → restart from sequence 0 → duplicate projection writes** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:329-340] — On transient DAPR failure reading the per-aggregate poller checkpoint, `perAggregateProgress` defaults to 0; orchestrator then re-reads the entire stream and re-projects every event. Non-idempotent projections corrupt. Comment says "log and continue"; that's wrong for this case. Fix: bail this aggregate's iteration (return `null`) rather than restart from 0; record a per-aggregate `RebuildCheckpointReadFailed` reason and let next invocation retry.
- [x] [Review][Patch] **P-C14-CONT — Cancellation between iterations leaves status stuck at `Running`** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:269] — `cancellationToken.ThrowIfCancellationRequested()` inside the `await foreach` body propagates `OperationCanceledException` to the caller without writing any terminal status. Combined with the post-loop `if (!anyApplied) return;` guard, partial progress is lost AND status stays `Running`. Fix: catch `OperationCanceledException` at `RebuildProjectionAsync` boundary and write `Canceled` checkpoint via `SaveAsync` (or `ResetAsync` if lifecycle protection blocks it) before re-throwing. Couples with the deferred D2a scheduler's two-phase cancel groundwork.

MEDIUM:

- [x] [Review][Patch] **P-C9-CONT — Empty/whitespace `AggregateId` in scope silently widens to domain-wide** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:477-481, src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:235] — `ValidateScope` allows null `AggregateId`; an empty string `""` passes `IsNullOrWhiteSpace` and collapses to `*` in the key. `MatchesRebuildScope` then treats it as wildcard. A caller intending to target one aggregate but passing `""` (e.g., from a coerced URL parameter) silently triggers a domain-wide rebuild. Fix: in `ValidateScope`, reject when `AggregateId is { Length: 0 }` OR (`AggregateId is not null && IsNullOrWhiteSpace(AggregateId)`); only `null` is a valid wildcard sentinel.
- [x] [Review][Patch] **P-C10-CONT — Overlapping `Replay` invocations: stale `ToPosition` overwrites newer operator's intent** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:291-303] — Operator A's rebuild is finalizing; Operator B issues fresh `Replay(ToPosition=2000)`. Operator A reads `finalSnapshot.ToPosition = 1000` and writes `Succeeded` with `ToPosition = 1000`, overwriting B's 2000. B's operation appears to have been "succeeded" against A's bound. Fix: include `OperationId` check in the terminal `SaveAsync` — only write `Succeeded` if `finalSnapshot.OperationId == checkpointScope.OperationId`. Couples with D3-F.
- [x] [Review][Patch] **P-C11-CONT — `SaveAsync` ETag retry loop does not retry across `IsStateStoreUnavailable` catches** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:80-158] — A single transient `HttpRequestException` from `daprClient.GetStateAndETAGAsync` short-circuits to `Failure(CheckpointUnavailable)` without retry. The "ETag retry" name implies transient-failure tolerance, but only ETag conflicts are retried; transient infrastructure failures are not. Operator sees `CheckpointUnavailable` and re-issues at the API layer → thundering herd. Fix: catch `IsStateStoreUnavailable` inside the loop with bounded retry (3 attempts, 50/200/500ms exponential + jitter) before returning `Failure`.
- [x] [Review][Patch] **P-C13-CONT — `Failed` status with `Math.Max`-clamped sequence creates internally inconsistent record** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:119-133] — `monotonicSequence = Math.Max(existing.LastAppliedSequence ?? 0, lastAppliedSequence)` silently clamps regression — but `Status` and `FailureReasonCode` write through. Caller asking for `(seq=5, Status=Failed, reason=domain-failure)` against `(seq=10, Running)` writes `(seq=10, Status=Failed, reason=domain-failure)`. Operator decisions to resume from `LastAppliedSequence` may skip events that were never applied. Fix: when `status == Failed` and `lastAppliedSequence < existing.LastAppliedSequence`, return `Failure(StaleCheckpoint)` instead of clamping silently.
- [x] [Review][Patch] **P-C2-CONT — `MaxExceptionUnwindDepth = 8` off-by-one** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:296] — Guard fires when `depth == 8`, so 9th frame (depth-8 inclusive) is never examined. A legitimate 8-deep wrapped `DaprException` is misclassified as "not unavailable" → non-retryable. Fix: change to `if (depth > MaxExceptionUnwindDepth)` OR raise the cap to 16 (typical max chain depth in instrumented infra).
- [x] [Review][Patch] **P-C3-CONT — Re-add early-return when `LastAppliedSequence >= ToPosition`** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:281-283] — Prior code returned immediately when caught up. New code falls through to `DeliverProjectionForRebuildAsync` which makes an actor round-trip per tracked identity for a finished rebuild. With bounded `ReadEventsRangeAsync` the actor returns `[]` quickly but the round-trip is still wasted. Fix: re-add `if (current.ToPosition is long toPosition && current.LastAppliedSequence >= toPosition) continue;` (note: `continue`, not `return` — domain-wide must check every aggregate). Couples with P-C4-CONT (livelock fix should write `Succeeded` when all aggregates skip via this guard).
- [x] [Review][Patch] **P-C12-CONT — `NotStarted` status not in `IsNonTerminalAdvancement` — bypasses lifecycle guard** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:265-276] — `IsLifecycleProtected(Paused) && IsNonTerminalAdvancement(NotStarted)` is `false`, so `SaveAsync(scope, 0, NotStarted)` overwrites a `Paused` checkpoint. No current caller does this, but the asymmetry is a defense-in-depth gap. Fix: add `NotStarted` to `IsNonTerminalAdvancement` (or rename the helper to `IsLifecycleAdvancementOrInit` and include it).
- [x] [Review][Patch] **P-C6-CONT — Test coverage gap: `ReadEventsRangeAsync` `from` boundary only exercised at `0L`** [tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:179, :234] — Both updated tests stub `ReadEventsRangeAsync(0L, ...)`. The new C1 behavior (per-aggregate progress as exclusive lower bound) is not exercised at `> 0`. The inclusive/exclusive semantic of `fromSequence` (verified exclusive per `IAggregateActor.cs:31`) is fine, but a regression that flips it would silently re-apply or skip the next event. Fix: add a test that stubs `ReadLastDeliveredSequenceAsync` to return `5L`, asserts `ReadEventsRangeAsync(5L, ...)`, and that the checkpoint advances from `5` → `5 + n_events`.

##### Deferred (3)

- [x] [Review][Defer] **W1-CONT — `*` reserved-char addition has no migration for pre-existing literal-`*` aggregateIds** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:263] — Theoretical risk; no real-world `*` aggregateIds expected. Recorded in `deferred-work.md`.
- [x] [Review][Defer] **W2-CONT — Reserved-char asymmetry between `ProjectionCheckpointTracker` (`AssertNoReservedChars` without `*`) and `ProjectionRebuildCheckpointStore` (with `*`)** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:263 vs ProjectionCheckpointTracker.cs:258] — Latent crash risk on future refactor that flips per-iteration scope to per-aggregate. Recorded in `deferred-work.md`.
- [x] [Review][Defer] **W3-CONT — Test coverage gap for `(default, null)` tuple from state store** [tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildCheckpointStoreTests.cs:262-268] — Tests force null-bang on tuple; future Dapr client returning `(default, null)` for missing key would NPE in `existing.LastAppliedSequence`. Recorded in `deferred-work.md`.

##### Dismissed (4) — verified false positive

- **Blind#3 (dismissed)** — "Domain-wide `Succeeded` written against `initial!.AggregateId`-bearing scope". The H5 patch made `ScopeForCheckpoint` preserve `scope.AggregateId` (operator scope). When operator scope is domain-wide, `scope.AggregateId == null`, so `ScopeForCheckpoint(scope, initial!)` produces a null-aggregateId scope. Verified at `ProjectionUpdateOrchestrator.cs:494`.
- **Blind#6 (dismissed)** — "`perAggregateProgress` inclusive/exclusive ambiguity in `ReadEventsRangeAsync`". `IAggregateActor.cs:31` documents `fromSequence` as exclusive: "Events with SequenceNumber > fromSequence are returned." Passing last-delivered sequence is correct.
- **Blind#9 (dismissed)** — "Pre-save lifecycle re-check uses `scope` rather than `checkpointScope`". The `scope` parameter passed into `DeliverProjectionForRebuildAsync` IS `checkpointScope` (caller at `:282`). Blind Hunter lacked project context.
- **Blind#13 (dismissed)** — "`Log.CheckpointReadFailed` referenced but not defined in diff". Defined in same file at `ProjectionUpdateOrchestrator.cs:734`. Build is green.

##### Reviewer notes

- The C1 patch (apply-driven bounded-page read) is structurally correct in isolation but composes badly with the controller's one-shot synchronous `RebuildProjectionAsync` invocation (`AdminProjectionRebuildController.cs:300`). Without the deferred D2a scheduler re-invoking until completion, ANY stream > 256 events triggers the P-C15-CONT premature-`Succeeded` regression. **This is the single most material finding in this pass.** The deferred D2a actor is no longer optional — it is required for AC4/AC5 conformance.
- The "no events to deliver" livelock (P-C4-CONT) compounds the same root cause: the `anyApplied` flag conflates "nothing-to-do" with "lifecycle-interrupted". Fix together with P-C15-CONT.
- Six decision-needed items (D3-A through D3-F) are concentrated in the rebuild-orchestrator/checkpoint-storage interface — the per-aggregate vs operator-scope split, OperationId-as-key, and Reset/Replay semantics. These are design questions the second-pass D2a deferral set up but did not resolve. Bundling them with the D2a `RebuildSchedulerActor` follow-up story is the natural home.
- All five second-pass D2a-D2e deferrals remain accurate — diff scope matches the "remaining patches" inventory in the prior section.
- Story stays in `review` — 6 decisions + 15 patches outstanding. Sprint-status not advanced. **Recommended next step:** resolve D3-A through D3-F (likely all routed to deferred-work or D2a follow-up story); apply P-C1-CONT through P-C15-CONT in a continuation session.

#### Code review run on 2026-05-15 (Opus 4.7 — second-pass against full story diff `beed5a8e..HEAD`, 62 files, +7352/-224)

_Three adversarial layers: Blind Hunter (diff-only), Edge Case Hunter (diff + project), Acceptance Auditor (diff + spec + CLAUDE.md). Triage: 4 CRITICAL, 13 HIGH, 14 MEDIUM, 6 LOW. Of those: 5 decision-needed (require user input), 28 patch (unambiguous), 4 defer (pre-existing or out of scope), 2 dismiss (verified false positive / unrelated). Direct source inspection confirms the four CRITICAL findings; the apply-driven rebuild orchestrator (commit `c0d439d2`) is structurally broken on multiple axes._

##### Decisions resolved (2026-05-15, second-pass) → folded into patches and deferrals

- **D2a → RebuildSchedulerActor (deferred design)**: Adopt an actor-based scheduler (`RebuildSchedulerActor`, per-tenant or per-projection) that owns the lifecycle and uses actor reminders to drive iterations. Full implementation routed to a follow-up story (added to `deferred-work.md`). Partial fixes for C2, C4, H10, H14 apply now: cancellation re-checks between iterations, `SaveAsync` lifecycle protection, fail-closed retry, two-phase cancel groundwork.
- **D2b → Patch (P-D2b)**: Inject `ITenantValidator` + `IRbacValidator` into `AdminProjectionRebuildController`; call before scope construction per action. Matches `StreamsController` pattern.
- **D2c → Tier-2 patch + Tier-3 follow-up**: Add Tier-2 tests that capture every `SaveAsync`/`ResetAsync` call via `Arg.Do` and assert persisted checkpoint fields end-to-end. Tier-3 + Aspire SDK 13.3.2 bump deferred to a follow-up story (added to `deferred-work.md`).
- **D2d → Defer (HMAC infra deferred)**: Spec AC3 amended to mark continuation tokens deferred to HMAC story; remove `NextContinuationToken` typed field from public DTOs OR keep it as `null`-only with an inline `Obsolete`/doc note. Recommended sub-action: remove `ContinuationToken` / `NextContinuationToken` from `StreamReadRequest`/`StreamReadPage` and remove `InvalidContinuation` / `TokenRequestMismatch` from the taxonomy until HMAC implementation lands. Documented in `deferred-work.md`.
- **D2e → Patch (recommended split)**: Wire `ProjectionApplyRejected` from `ProjectionUpdateOrchestrator` (on domain `/project` 4xx response, write a `Failed`-status checkpoint with this reason). Wire `MissingStream` from `StreamsController` (when aggregate has zero events at sequence 0 → return 404). Remove `DomainFailure`, `RetryableTransientFailure`, `TokenRequestMismatch`, `CheckpointDrift` from public taxonomy + docs. Update no-leak tests.

##### Original Decision-needed (5) — historical record

- [ ] [Review][Decision] **D2a — Background rebuild worker design (CRITICAL: C3)** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:2138 + src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:251] — Replay endpoint awaits `rebuildOrchestrator.RebuildProjectionAsync(scope, ct)` synchronously inside the HTTP request, so 202 Accepted is fictitious. Resume/Retry only flip status and never re-invoke the orchestrator. Cancel does not interrupt in-flight work because cancellation token from the request thread is the only signal and `RebuildProjectionAsync` does not re-check `CanRunRebuild` between iterations. Options: (a) fire-and-forget on `TaskScheduler.Default` with a fresh CT, (b) hosted `BackgroundService` polling for `Running`/`Resuming`/`Retrying` checkpoints, (c) actor-based scheduler. (a) is fastest but loses durability across restarts; (b) is the conventional .NET hosted-service pattern; (c) is most consistent with the existing actor topology. User must pick. Once decided, lifecycle endpoints must produce a marker that the worker observes.
- [ ] [Review][Decision] **D2b — `AdminProjectionRebuildController` D1 follow-through** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:2195-2205] — Only the `EnsureGlobalAdministrator` role gate was implemented. Story D1 (2026-05-15) committed to also injecting `ITenantValidator` + `IRbacValidator` to validate URL `tenantId` against JWT claims on every action. Options: (a) implement the validators per D1, (b) update D1 in the spec to state global-admin alone is sufficient (since the role is platform-wide). Recommended: (a) for defense-in-depth.
- [ ] [Review][Decision] **D2c — R2-A6 integration-test path** [tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildCheckpointStoreTests.cs + AdminProjectionRebuildControllerTests.cs] — All new rebuild/checkpoint tests assert mock-call counts (`Received(1).SaveAsync(...)`), not persisted state. CLAUDE.md R2-A6 explicitly requires Tier-2/Tier-3 integration tests to inspect state-store contents. Aspire CLI version mismatch (13.2.2 vs required 13.3.2) blocks the apphost. Options: (a) bump `Aspire.AppHost.Sdk` to 13.3.2 and add Tier-3 tests, (b) add Tier-2 tests that capture saved checkpoints via `Arg.Do` and assert end-state, (c) update spec to explicitly mark integration proof deferred. Recommended: (b) now + (a) as a follow-up story.
- [ ] [Review][Decision] **D2d — Continuation-token contract gap (AC3 unmet)** [src/Hexalith.EventStore/Controllers/StreamsController.cs:2382, 2531-2538] — Contract advertises `nextContinuationToken` but server returns `null` unconditionally and rejects any inbound token with `InvalidContinuation`. `TokenRequestMismatch` is declared in the public taxonomy but never produced. Documented workaround (`FromSequence = lastSequenceReturned + 1`) contradicts the typed field. Options: (a) ship a minimal request-bound token (request-hash + cursor, fail-closed on mismatch — no HMAC yet) + produce `TokenRequestMismatch` from the controller, (b) remove `ContinuationToken`/`NextContinuationToken`/`TokenRequestMismatch` from the public contract until HMAC story ships, (c) explicit deferral notice in the spec. Recommended: (b) for clean contract or (a) for spec conformance.
- [ ] [Review][Decision] **D2e — Reason-code taxonomy cleanup (AC7 partial)** [src/Hexalith.EventStore.Contracts/Streams/StreamReplayReasonCodes.cs + docs/reference/stream-replay-api.md] — 6 advertised codes never produced by production paths: `ProjectionApplyRejected`, `DomainFailure`, `RetryableTransientFailure`, `MissingStream`, `TokenRequestMismatch`, `CheckpointDrift`. The story already removed `StaleCheckpoint` (P22) on the same rationale; same rule should apply consistently. Options per code: (a) wire a producer, (b) remove from public taxonomy + docs. Recommended: remove unproducible codes; wire `ProjectionApplyRejected` and `MissingStream` (controller currently returns 200 empty page for unknown streams instead of 404).

##### Patches (28) — fixable without input

CRITICAL:

- [x] [Review][Patch] **C1 — `RebuildProjectionAsync` ignores `LastAppliedSequence`; reads `GetEventsAsync(0)` and filters `ToPosition` in memory every iteration** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:332-340] — APPLIED 2026-05-15 second-pass: replaced `GetEventsAsync(0)` + in-memory `Where(toPosition)` with `ReadEventsRangeAsync(perAggregateProgress, checkpoint.ToPosition, RebuildPageSize=256)`. Per-aggregate progress drawn from `checkpointTracker.ReadLastDeliveredSequenceAsync(identity, ...)`. Bounded page; no full-stream re-reads. Tests updated.
- [x] [Review][Patch] **C2 — Terminal `Succeeded` is written against the LAST per-aggregate scope, not the operator scope; `Running` lingers on all other aggregates** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:266-307] — APPLIED 2026-05-15 second-pass: removed `lastScope` tracking; `RebuildProjectionAsync` now writes `Succeeded` against the operator's `checkpointScope` (derived once from the operator scope + initial checkpoint's OperationId) after enumeration completes and `anyApplied == true`. Cancellation re-check (`CanRunRebuild`) between iterations.
- [x] [Review][Patch] **C4 — `SaveAsync` overwrites operator-set `Paused`/`Canceled`/`Failed` with `Running` because monotonicity guards only sequence** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:960-989 + ProjectionUpdateOrchestrator.cs:430-438] — APPLIED 2026-05-15 second-pass: `SaveAsync` rejects lifecycle writes when `existing.Status` is in `{Paused, Pausing, Canceled, Canceling, Failed, Succeeded}` and the attempted status is a non-terminal advancement (`Running`/`Resuming`/`Retrying`). Returns `Failure(RebuildPaused)` / `Failure(RebuildCanceled)` / `Failure(CheckpointConflict)` so the orchestrator stops cleanly. `IsLifecycleProtected`/`IsNonTerminalAdvancement` helpers added. New `Log.CheckpointLifecycleProtected` LoggerMessage (EventId 1192).

HIGH:

- [x] [Review][Patch] **H2 — `StreamsController.IsValidAggregateId`/`IsCanonicalTenantOrDomain` reject mixed case but writers accept it; legacy aggregates unreadable** [src/Hexalith.EventStore/Controllers/StreamsController.cs:2543-2560] — Validator rejects `tenant-A` or `Party-1` with `invalid-aggregate-identity`. `AggregateIdentity.ActorId` accepts any non-whitespace ASCII per CLAUDE.md R2-A7. Fix: relax both regexes to `[A-Za-z0-9-]` and lowercase server-side for matching, or align with `AggregateIdentity` rules verbatim.
- [x] [Review][Patch] **H3 — `AggregateActor.ReadEventsRangeAsync` overflows at `fromSequence == int.MaxValue - 1`** [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:732 + ~664] — `checked((int)(fromSequence + 1))` overflows; subsequent `endExclusive = startSequence + count` further overflows. Controller validator caps `FromSequence <= int.MaxValue - 1L` but does not subtract page size. Fix: use `long` arithmetic until the final bounded count; throw if `fromSequence >= int.MaxValue - maxCount`. Tighten `StreamsController.ValidateRequest` to `request.FromSequence > int.MaxValue - request.PageSize - 1L`.
- [x] [Review][Patch] **H4 — `EnsureGlobalAdministrator` returns 403 with `unauthorized-tenant` reason** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:2200-2204] — Role failure surfaces as a tenant-failure reason. Fix: add `ForbiddenRole` to `StreamReplayReasonCodes` (or reuse `ForbiddenReplayScope`), update the controller and the no-leak taxonomy.
- [x] [Review][Patch] **H5 — `ScopeForCheckpoint` reassigns `AggregateId` on domain-wide scopes, causing scope/key drift** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:474-480] — APPLIED 2026-05-15 second-pass: removed `AggregateId = checkpoint.AggregateId` assignment; `ScopeForCheckpoint` now preserves operator's original `scope.AggregateId` and only overlays `OperationId`. Inline comment explains the scope/key-drift hazard.
- [x] [Review][Patch] **H6 — `IsServiceUnavailable` blanket-classifies `ActorMethodInvocationException` as 503** [src/Hexalith.EventStore/Controllers/StreamsController.cs:2567-2572] — Application-logic failures from the actor (serializer drift, invalid metadata `InvalidOperationException`) become 503 with `service-unavailable`, masking data corruption and inviting retry storms. Fix: inspect `exception.InnerException` and only classify `HttpRequestException`/`TimeoutException`/`DaprException`-wrapped variants as 503; programmer errors → 500.
- [x] [Review][Patch] **H7 — Lifecycle-status regression Succeeded→Running with older sequence** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:967-989] — APPLIED 2026-05-15 second-pass: resolved as a coupled fix with C4 — `SaveAsync` now rejects non-terminal advancement writes against terminal/protected statuses (`Succeeded`/`Failed`/`Paused`/`Pausing`/`Canceled`/`Canceling`).
- [x] [Review][Patch] **H10 — `HasActiveOperatorRebuildAsync` fail-closed on transient state-store blip halts ALL poller deliveries** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:618-657] — Single state-store outage during normal poller flow returns `true`, skipping delivery; no retry, no backoff. Outage cascades. Fix: add bounded retry (≤3 attempts, 50/200/500ms exponential with jitter) before returning `true`; emit alertable warning on consecutive fail-closed events.
- [x] [Review][Patch] **H11 — `ProjectionRebuildCheckpointSaveResult.Success(existing)` leaks prior operator's `OperationId` to the new operator's response** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:960-972] — APPLIED 2026-05-15 second-pass: idempotent no-op now returns `existing with { OperationId = scope.OperationId }` when caller passed a non-null OperationId. Inline comment explains the leak hazard.
- [x] [Review][Patch] **H12 — No end-to-end pagination round-trip test** [tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs:3637-3666] — `ReadStreamAsyncReturnsOrderedBoundedAggregatePage` asserts `truncated == true` and `NextContinuationToken == null` but never calls the endpoint a second time with `FromSequence = lastSequenceReturned + 1` to prove no gaps/duplicates. Fix: add a paged-round-trip test that asserts the union of pages equals the seeded sequence range and no event appears twice.
- [/] [Review][Patch] **H14 — Cancel does not transition through `Canceling`; in-flight rebuild's `SaveAsync(Running)` overwrites operator's `Canceled`** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:2030 + ProjectionUpdateOrchestrator.cs:430-438] — PARTIAL 2026-05-15 second-pass: `DeliverProjectionForRebuildAsync` now re-reads checkpoint and re-checks `CanRunRebuild` immediately before its `SaveAsync` call; combined with C4's lifecycle protection in the store, an operator-set `Canceled` is honored. Two-phase cancel (write `Canceling`, drain, then `Canceled`) requires the deferred D2a `RebuildSchedulerActor` worker and is not implemented here.

MEDIUM:

- [x] [Review][Patch] **M1 — Checkpoint key collision: literal aggregateId `"*"`** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:1082-1092] — APPLIED 2026-05-15 second-pass: added `'*'` to `s_reservedChars` set so a literal `*` aggregate id is rejected at `ValidateKeyPart`.
- [x] [Review][Patch] **M2 — `EnumerateTrackedIdentitiesAsync` enumerates all tenants, then filters in memory** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:268-271] — Cross-tenant enumeration → in-memory `MatchesRebuildScope` filter. O(N) cost across all tenants; potential information leak if tracker logs the unfiltered enumeration. Fix: thread `scope.Tenant`/`scope.Domain` into `EnumerateTrackedIdentitiesAsync` at the source.
- [x] [Review][Patch] **M3 — `latestSequence` reports page-tip, not stream tip; over-reports on overshoot** [src/Hexalith.EventStore/Controllers/StreamsController.cs:2358-2360] — `latestSequence = readEvents[^1].SequenceNumber` (a page+1 buffer). When caller's `FromSequence` overshoots the real tip, returns `latestSequence = FromSequence`, masking overshoot. Fix: add `IAggregateActor.GetCurrentSequenceAsync()` (or expose via metadata channel) and use it for `latestSequence`.
- [x] [Review][Patch] **M4 — `UpdatedAt`/`StartedAt` use `DateTimeOffset.UtcNow` directly, not injected `TimeProvider`; cancel-before-start shows `Completed=Started`** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:940-1015 + src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:2229] — No clock injection (tests cannot drive deterministic timestamps); `ToOperation` conflates `StartedAt = UpdatedAt`. Fix: inject `TimeProvider`; persist a dedicated `StartedAt` field set only on the first `Running` transition.
- [/] [Review][Patch] **M5 — `IsStateStoreUnavailable` recurses across `InnerException` without depth limit** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:1128-1133 + StreamsController.cs:2567-2572] — PARTIAL 2026-05-15 second-pass: depth limit (`MaxExceptionUnwindDepth = 8`) added to the store-side variant. The controller-side `IsServiceUnavailable` in `StreamsController` still needs the same cap.
- [x] [Review][Patch] **M7 — `Log.UpdateStarted` shared between normal poller and operator rebuild paths** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:317] — `DeliverProjectionForRebuildAsync` reuses the poller's `UpdateStarted` LoggerMessage; operators cannot tell from logs which path produced a state mutation. Fix: add `RebuildDeliveryStarted` with distinct EventId and a `Stage` tag.
- [x] [Review][Patch] **M8 — Domain-wide rebuild does not detect aggregate-specific rebuild already running** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:1119-1126 + ProjectionUpdateOrchestrator.cs:465-469] — `MatchesRebuildScope` treats null aggregate as wildcard, but the key namespace is partitioned by aggregate. Domain-wide rebuild starts even though an aggregate-specific rebuild is in flight. Fix: maintain an active-rebuilds registry per `(tenant, domain, projectionName)` or scan key prefix when domain-wide rebuild starts.
- [x] [Review][Patch] **M9 — Docs claim `aggregateId` optional for domain-wide reads; controller hard-requires it** [docs/reference/stream-replay-api.md vs src/Hexalith.EventStore/Controllers/StreamsController.cs:2493-2500] — Plus `lastAppliedSequence` and `nextContinuationToken` terminology in docs implies behavior the server doesn't yet implement. Fix: sync docs (see D2d for continuation-token strategy).
- [x] [Review][Patch] **M10 — `MaxEtagRetries = 3` with no backoff/jitter** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:918] — Three retries fire in microseconds; contention → all three lose. Fix: 10ms / 40ms / 100ms with ±25% jitter.
- [x] [Review][Patch] **M11 — `ToSequence == FromSequence` rejected; documented inclusive upper-bound becomes unreachable for size-1 reads** [src/Hexalith.EventStore/Controllers/StreamsController.cs:2513] — Validator rejects `ToSequence <= FromSequence`; can't read exactly one event. Fix: change to `ToSequence < FromSequence`; clarify docs that `FromSequence` is exclusive lower, `ToSequence` is inclusive upper.
- [x] [Review][Patch] **M13 — `Reset`/`Replay` rewind contradicts docs claiming monotonic-only** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:1922 + docs/reference/stream-replay-api.md:68] — Docs assert monotonic-only; code's `ResetAsync` with `allowRewind: true` lowers `LastAppliedSequence`. Fix: docs must distinguish normal-path monotonicity from explicit Reset/Replay rewind via ETag.
- [x] [Review][Patch] **M14 — 503 lacks `Retry-After` header** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:2160-2169 + StreamsController.cs] — Operators get no retry hint. Fix: add `Retry-After: 5` (or configurable) header on 503 `CheckpointUnavailable`/`ServiceUnavailable`.
- [x] [Review][Patch] **M15 — `SaveLifecycleAsync` lacks defense-in-depth role check** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:1929-1968] — All current callers invoke `EnsureGlobalAdministrator` inline first, but future controller endpoints routing through `SaveLifecycleAsync` would bypass. Fix: re-call `EnsureGlobalAdministrator` defensively inside `SaveLifecycleAsync`. Cheap; same controller.
- [x] [Review][Patch] **M17 — `ProjectionRebuildCheckpointStore.ReadAsync` does not re-validate `OperationId` shape** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:921-937] — APPLIED 2026-05-15 second-pass: `IsValidOperationId` helper checks 26-char Crockford-base32 ULID shape; `ReadAsync` returns `null` (and logs `CheckpointMalformedOperationId`, EventId 1193) when persisted state carries a malformed `OperationId`. Tampered state no longer poisons the AdminOperationResult.

LOW:

- [x] [Review][Patch] **L3 — `FakeAggregateActor.ReadEventsRangeAsync` argument validation diverges from production** [src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs:1621-1626] — Fake accepts any `maxCount`; production throws `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`. Tests using the fake won't pin validator regressions. Fix: mirror `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount)` and `ThrowIfNegative(fromSequence)`.
- [x] [Review][Patch] **L4 — `StreamReadPageBuilder.AddEvent` uses non-ULID `MessageId`** [src/Hexalith.EventStore.Testing/Builders/StreamReadPageBuilder.cs:1558-1568] — `MessageId = $"message-{sequenceNumber}"` violates CLAUDE.md R2-A7 (`messageId` must be ULID-parseable). Tests using this builder won't pin ULID-validation regressions. Fix: `Ulid.NewUlid().ToString()` for `MessageId`, `CorrelationId`, `CausationId`.
- [x] [Review][Patch] **L5 — `AssertNoForbiddenLeakage` substring list incomplete** [tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs:3802-3819] — No assertion against actor type names (`AggregateActor`), state-store key prefixes (`projection-rebuild-checkpoints:`), Redis connection strings (`redis://`, `localhost:6379`), `ETag` headers. Fix: expand list and apply to every problem-emitting branch via `[Theory]`.
- [x] [Review][Patch] **L6 — Docs claim `Canceling`/`Pausing`/`Resuming` intermediate states; controller writes terminal states directly** [docs/reference/stream-replay-api.md:93 vs src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:2030] — Either sync docs to current behavior or implement two-phase transitions (couples with C3/D2a + H14). Fix: update docs in this story; defer two-phase to D2a follow-up.
- [x] [Review][Patch] **L7 — `EventStoreGatewayClient.ReadStreamAsync` lacks JSON response size cap** [src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs:178] — Server returning 100MB JSON deserializes fully into memory. Fix: configure `HttpClient.MaxResponseContentBufferSize` or stream-bound parse with a hard cap (e.g., 16 MiB).

##### Deferred (4)

- [x] [Review][Defer] **M16 — `ProjectionResetRequest.FromPosition` naming** [src/Hexalith.EventStore.Contracts/Streams/ProjectionResetRequest.cs:474-482] — Renaming to `LastAppliedSequence` is a Contracts breaking change; not worth without coordinated SemVer story. Pre-existing naming choice from this story; preserved.
- [x] [Review][Defer] **L1 — `StreamReadEvent.Payload byte[]` reference-equality footgun** [src/Hexalith.EventStore.Contracts/Streams/StreamReadEvent.cs:517-527] — Already documented in summary as "compare contents explicitly when needed". Switching to `ReadOnlyMemory<byte>` is a Contracts breaking change.
- [x] [Review][Defer] **L8 — Two commits violate Conventional Commits (`f4c3cc4b`, `63af9406`)** — `Refactor projection handling...` and `Refactor code structure...` lack the `refactor:` type prefix. Cannot retroactively change merged commits without history rewrite; will affect semantic-release version detection. Flag for future PR hygiene; out of scope for this story.
- [x] [Review][Defer] **W1 (already noted in prior pass) — Integration-test renames in `CommandStatusIntegrationTests.cs`** — scope creep from `8348b93e`, owned by Story 22.1/22.4.

##### Dismissed (2)

- **L2 (dismissed)** — Aspire preview-package bump in `Directory.Packages.props`. Bump from `13.2.2-preview.1.26207.2` to `13.3.2-preview.1.26263.11` matches the Aspire CLI requirement noted in story Verification Status; not unrelated.
- **BH-tenant-body-trust (dismissed)** — Concern that `StreamsController.ReadStreamAsync` trusts `request.Tenant` from POST body. Existing tests `ReadStreamAsyncWithDeniedTenantRejectsBeforeRbacAndActorProxy` and `ReadStreamAsyncWithDeniedReplayScopeRejectsBeforeActorProxy` (matching Story 22.3's validator pattern) pin that the validator returns `Denied` when JWT claims don't authorize the body's tenant. Verified false positive.

##### Second-pass apply status (2026-05-15)

**Applied (8/28 patches + 2 partial):** C1, C2, C4, H5, H7, H11, M1, M17 fully applied; H14 partial (CanRunRebuild re-check before SaveAsync — full two-phase cancel deferred with D2a); M5 partial (store-side recursion cap; controller-side still open). Build green. Focused tests 72/72 pass (`ProjectionRebuildCheckpointStore*`, `ProjectionUpdateOrchestrator*`, `AdminProjectionRebuild*`). Broader Server.Tests: 1925/1937 pass; the 12 failures are pre-existing on `main` (`ReplayControllerTests`, `ValidationExceptionHandlerTests`) — not caused by this pass.

**Decisions resolved → routing:** D2a deferred (RebuildSchedulerActor follow-up story); D2b/D2c/D2e are patches; D2d deferred (HMAC infra). Defer entries added to `_bmad-output/implementation-artifacts/deferred-work.md`.

**Remaining patches (~20):** H2, H3, H4, H6, H10, H12, M2, M3, M7, M8, M9, M10, M11, M13, M14, M15, L3, L4, L5, L6, L7 — plus the D2b validator-injection, D2c Arg.Do state-inspection tests, D2d contract-field removal, D2e reason-code wiring/removal, M5 controller-side cap, H14 two-phase cancel groundwork. These span Contracts/Server/Controllers/Tests/Docs and require a continuation pass (estimated 2-3 cycles based on prior-pass cadence).

**Files changed in this pass:**
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` (C1/C2/H5/H14 partial)
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs` (C4/H7/H11/M1/M5/M17 + 2 new LoggerMessages)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` (mock-signature updates to match new ReadEventsRangeAsync usage)

**Story status:** Stays in `review` — patches still outstanding. Sprint-status not advanced. User to run `/bmad-code-review 22.6` in a continuation session to pick up remaining patches.

##### Reviewer notes

- All four CRITICAL findings (C1/C2/C4 + C3 decision) confirmed by direct source inspection at `ProjectionUpdateOrchestrator.cs:251-308` and `:332-340`.
- The apply-driven rebuild orchestrator (commit `c0d439d2`) is structurally broken on three orthogonal axes: (1) wrong lower bound, (2) terminal status against wrong scope, (3) lifecycle protection. C4 + H7 + H14 share a root cause and resolve together.
- Lifecycle endpoint set (Pause/Resume/Cancel/Retry/Replay) is half-implemented — status mutations work but Resume/Retry never re-invoke the orchestrator. D2a is the architectural unblock.
- AC3 (continuation tokens) and AC7 (reason-code taxonomy) need a definitive decision before sign-off. The prior-pass D3 deferred continuation-token implementation but the typed field still ships, creating the contract gap.

#### Code review run on 2026-05-15 (Opus 4.7 — first pass) against diff `beed5a8e..HEAD` (39 files, +2543/−97)

_Three adversarial layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor._

#### Decisions resolved (2026-05-15) → folded into patches

- **D1 → P-D1**: Enforce `[Authorize(Roles = "GlobalAdministrator")]` on `AdminProjectionRebuildController` (matches Hexalith.Tenants pattern) AND inject `ITenantValidator` + `IRbacValidator` to guard URL `tenantId` against JWT claims on every action (covers P1).
- **D2 → P-D2**: Implement apply-driven advancement now. Add an `IProjectionRebuildOrchestrator` (or extend `ProjectionUpdateOrchestrator`) that, on each accepted projection apply during an active operator rebuild, calls `IProjectionRebuildCheckpointStore.SaveAsync` with the page's last applied sequence. Add tests: accept-advances; reject-does-not-advance; cancel-does-not-advance.
- **D3 → P-D3**: Stop emitting `nextContinuationToken`. Return `null` on truncated pages while validation remains fail-closed; document that callers paginate by setting `FromSequence = lastSequenceReturned + 1`. Remove `CreateContinuationToken()` from `StreamsController`. HMAC implementation deferred to a follow-up story (add to deferred-work.md).
- **D4 → P-D4**: Document the projection-name == source-domain 1:1 constraint in `docs/reference/stream-replay-api.md` and add a `projectionName != domain` test pinning the limitation (no conflict detected, returns false). Schema change to support multi-projection-per-domain deferred.
- **D5 → P-D5**: Generate a fresh ULID `OperationId` per Replay/Reset; include `OperationId` in `ProjectionRebuildCheckpointStore.GetStateKey` (already keyed but currently collapsed to the static label). Update `AdminProjectionRebuildController.CreateScope` to take an explicit `OperationId` parameter on replay/reset and persist past records.
- **D6 → P-D6**: Persist `ToPosition` in `ProjectionRebuildCheckpoint` (new field) and enforce in the apply-driven advancement loop from P-D2 (stop advancing once `LastAppliedSequence == ToPosition`). Couples with P-D2.
- **D7 → P-D7**: Verify Dapr remoting behavior — check whether `StreamsController` uses Dapr remoting (`Dapr.Actors.Client`) or local in-process call. If wrapped, add an exception filter that unwraps `ActorMethodInvocationException`/equivalent to surface `MissingEventException`/`EventDeserializationException`. Add an integration test proving the type survives.

#### Patches applied in this review cycle (2026-05-15)

The following CRITICAL/HIGH patches were applied during the review session. Build green; focused Server tests 70/70 pass.

- [x] [Review][Patch] **P-D1 / P1 — GlobalAdministrator gate on AdminProjectionRebuildController** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs] — Added `EnsureGlobalAdministrator()` private helper that calls `GlobalAdministratorHelper.IsGlobalAdministrator(User)` and returns 403 ProblemDetails when not. Wired into every action (GetRebuildStatus, Pause, Resume, Reset, Replay, Cancel, Retry) BEFORE any scope construction or store access. Closes cross-tenant operator hijack. Two new tests pin the deny-before-store-access behavior.
- [x] [Review][Patch] **P2 / P14 — SaveAsync early-return drops status transitions** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:60-71] — Removed the "progress-status short-circuit" that silently dropped lifecycle transitions. Now the only idempotent no-op path is when every observable field (sequence, status, failureReasonCode) matches. New test `SaveAsyncStatusOnlyTransitionPersistsNewStatusWithoutLoweringSequence` regression-pins this.
- [x] [Review][Patch] **P5 — Pause/Cancel/Resume/Retry return 404 when no rebuild active** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:TransitionExistingAsync] — Read existing checkpoint first; if absent, return `404 rebuild-operation-not-found`. Test `PauseProjectionWithoutExistingRebuildReturnsRebuildNotFound` pins this.
- [x] [Review][Patch] **P-D3 — Stop emitting continuation tokens** [src/Hexalith.EventStore/Controllers/StreamsController.cs:130, 243-244] — Returns `NextContinuationToken: null` always while validation remains fail-closed. Removed the `CreateContinuationToken()` helper. Updated `ReadStreamAsyncReturnsOrderedBoundedAggregatePage` to expect `null`. HMAC-bound token implementation deferred to follow-up.
- [x] [Review][Patch] **P6 — HasActiveOperatorRebuildAsync fail-closed** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:415-418] — On state-store exception, return `true` (rebuild may be active) instead of `false`. The poller now skips delivery rather than racing the rebuild.
- [x] [Review][Patch] **P20 — AdminProjectionRebuildController catches ArgumentException as 400** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:TransitionExistingAsync, SaveLifecycleAsync] — URL-decoded reserved-char failures now surface as 400 invalid-range instead of 500.
- [x] [Review][Patch] **P26 — Pause/Cancel pass `failureReasonCode: null`** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:PauseProjection, CancelProjection] — Status field carries the lifecycle state; FailureReasonCode is no longer stuffed with sentinel values.

#### Patches deferred to follow-up — design-level work (recorded in deferred-work.md)

These patches require coordinated changes across multiple files (Contracts DTO changes, new store API methods, orchestrator wiring) that exceed the safe scope of a single review-cycle patch pass:

- [x] [Review][Patch] **P3 — Add ResetAsync that bypasses monotonicity** — Added `ResetAsync` to `IProjectionRebuildCheckpointStore` / `ProjectionRebuildCheckpointStore`, wired `ResetProjection` and `ReplayProjection` through explicit rewind semantics, and regression-pinned reset persistence with fresh operation metadata.
- [x] [Review][Patch] **P4 / P-D2 — Apply-driven checkpoint advancement** — Added `IProjectionRebuildOrchestrator` on `ProjectionUpdateOrchestrator`, wired admin replay start through the rebuild orchestrator, and save `ProjectionRebuildCheckpoint` progress only after the domain `/project` path accepts and the projection write actor stores state. Per-apply progress remains `Running`; terminal success is written only after the tracked rebuild scope finishes and the operation is still runnable. Added accept-advances, reject-does-not-advance, and canceled-does-not-advance tests.
- [x] [Review][Patch] **P-D5 — Fresh ULID OperationId per Replay/Reset** — Replay/reset now generate fresh `UniqueIdHelper.GenerateSortableUniqueStringId()` operation IDs while lifecycle commands read the active checkpoint scope.
- [x] [Review][Patch] **P-D6 — Persist `ProjectionReplayRequest.ToPosition`** — Added optional `ToPosition` to `ProjectionRebuildCheckpoint` and persist replay upper-bound intent without treating it as applied progress.
- [x] [Review][Patch] **P-D4 — Document 1:1 projectionName==domain constraint** — Added docs section and a regression test proving the current conflict probe uses `identity.Domain` as the projection name.
- [x] [Review][Patch] **P-D7 — Verify Dapr remoting exception preservation** — Added `ActorMethodInvocationException` unwrapping for `MissingEventException` and `EventDeserializationException` before the generic actor-unavailable path.

#### Patches deferred to follow-up — focused HIGH/MEDIUM/LOW

- [x] [Review][Patch] **P7 — StreamsController pulls full event suffix into memory** — Added bounded `IAggregateActor.ReadEventsRangeAsync` and switched the public controller to request a bounded range/page directly.
- [x] [Review][Patch] **P8 — AggregateIdentity ArgumentException mapped to invalid-range** — Added up-front identity-shape validation and dedicated `invalid-aggregate-identity` reason code.
- [x] [Review][Patch] **P9 — Missing aggregateId returns 403 forbidden-replay-scope** — Missing aggregate IDs now return 400 `missing-required-field` for the current aggregate-scoped route.
- [x] [Review][Patch] **P10 — StreamsController does not null-guard validator results** — Tenant/RBAC null returns now throw explicit server-bug `InvalidOperationException` values.
- [x] [Review][Patch] **P11 — FromSequence upper bound** — Added `FromSequence <= int.MaxValue - 1` validation before actor access.
- [x] [Review][Patch] **P12 — StreamsController catch-all 500 + service-unavailable mismatch** — Split DAPR/HTTP/actor availability failures to 503 `service-unavailable` and unexpected failures to 500 `internal-error`.
- [x] [Review][Patch] **P13 — SaveAsync swallows all non-cancel exceptions as checkpoint-unavailable** — Restricted checkpoint-unavailable classification to DAPR/HTTP/IO/timeout-style state-store failures; programmer errors now propagate.
- [x] [Review][Patch] **P15 — StreamReadEvent.Payload `byte[]` equality footgun** — Documented payload array reference-equality explicitly on the public DTO.
- [x] [Review][Patch] **P16 — DaprProjectionCommandService unbounded JSON parsing** — Added a 64 KiB ProblemDetails parse cap with stable status fallback.
- [x] [Review][Patch] **P17 — Tenant validator called with raw (non-normalized) request.Tenant** — Rejects non-canonical tenant/domain casing before tenant/RBAC validation.
- [x] [Review][Patch] **P18 — ResetProjection accepts null body** — Null reset/replay request bodies now return 400 `missing-required-field`.
- [x] [Review][Patch] **P19 — ProjectionResetRequest/ProjectionReplayRequest declared in controller file** — Moved DTOs into `Hexalith.EventStore.Contracts.Streams`.
- [x] [Review][Patch] **P21 — Lifecycle controller tests skip persisted-state inspection** — Added checkpoint-store-backed reset/rewind and failure-classification tests.
- [x] [Review][Patch] **P22 — `StaleCheckpoint` reason advertised but never produced** — Removed the unused public reason from code/docs taxonomy.
- [x] [Review][Patch] **P23 — `StartedAt = UnixEpoch` for not-started operations** — Made `ProjectionRebuildOperation.StartedAt` nullable and returns null for never-started operations.
- [x] [Review][Patch] **P24 — Redundant `sub` claim check returns bare `Unauthorized()`** — Removed the manual `sub` check; `[Authorize]` remains the authentication gate.
- [x] [Review][Patch] **P25 — `ValidateRequest` uses `invalid-range` for missing tenant/domain** — Missing tenant/domain now return `missing-required-field`.
- [x] [Review][Patch] **P27 — `lastSequenceReturned` reports `FromSequence` for empty pages** — Made `StreamReadMetadata.LastSequenceReturned` nullable and return null for empty pages.
- [x] [Review][Patch] **P28 — `NormalizePageSize` is dead code** — Removed the dead helper and uses validated page size directly.
- [x] [Review][Patch] **P29 — `ReasonPhrase` nullable fallback** — Added stable status-message fallback for non-success Admin.Server lifecycle mapping.
- [x] [Review][Patch] **P30 — `reasonCode` JSON property capitalization not pinned by wire test** — Added a JSON serialization assertion for lowercase `reasonCode`.
- [x] [Review][Patch] **P31 — `FakeEventStoreGatewayClient.ConfigureStreamReadContinuation` mutates only two fields** — Normalizes continuation metadata from the configured page events.
- [x] [Review][Patch] **P32 — No-leak ProblemDetails assertions only check literal "state store"** — Expanded no-leak assertions across state-store, DAPR address, token, stack trace, payload/protected-data, and user-display substrings.

CRITICAL:

- [x] [Review][Patch] **P1 — Cross-tenant operator hijack** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:14-32, all action methods] — Resolved by the prior GlobalAdministrator gate before scope/store access.
- [x] [Review][Patch] **P2 — `SaveAsync` early-return silently drops status transitions** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:68-70] — Resolved by equality-only idempotency across sequence/status/failure/toPosition.
- [x] [Review][Patch] **P3 — Reset/Replay cannot rewind `LastAppliedSequence`** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:73] — Added explicit ETag-protected `ResetAsync` and routed reset/replay rewind intent through it.
- [x] [Review][Patch] **P4 — `POST /replay` writes `lastAppliedSequence` from caller-supplied `FromPosition`** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:109-116] — Replay now writes start cursor `max(0, FromPosition - 1)` and persists `ToPosition` as intent, not progress.
- [x] [Review][Patch] **P5 — `Pause`/`Cancel`/`Resume`/`Retry` on non-existent rebuild create phantom checkpoints** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:40-69, 124-153] — Resolved by read-first 404 behavior.
- [x] [Review][Patch] **P6 — `HasActiveOperatorRebuildAsync` fails open on store error** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:HasActiveOperatorRebuildAsync] — Resolved by fail-closed conflict detection.

HIGH:

- [x] [Review][Patch] **P7 — `StreamsController` pulls full event suffix into memory** [src/Hexalith.EventStore/Controllers/StreamsController.cs:100-109] — Resolved with `IAggregateActor.ReadEventsRangeAsync`.
- [x] [Review][Patch] **P8 — `AggregateIdentity` `ArgumentException` mapped to `invalid-range`** [src/Hexalith.EventStore/Controllers/StreamsController.cs:135-141] — Resolved with pre-auth identity validation and `invalid-aggregate-identity`.
- [x] [Review][Patch] **P9 — Missing `aggregateId` returns 403 `forbidden-replay-scope`** [src/Hexalith.EventStore/Controllers/StreamsController.cs:183-190] — Resolved with 400 `missing-required-field`.
- [x] [Review][Patch] **P10 — `StreamsController` does not null-guard validator results** [src/Hexalith.EventStore/Controllers/StreamsController.cs:63-66, 75-85] — Resolved with explicit null guard exceptions.
- [x] [Review][Patch] **P11 — `FromSequence` overflow in `AggregateActor.GetEventsAsync`** [src/Hexalith.EventStore/Controllers/StreamsController.cs:192-200; src/Hexalith.EventStore.Server/Actors/AggregateActor.cs] — Resolved with upper-bound validation and bounded actor read.
- [x] [Review][Patch] **P12 — `StreamsController` catch-all returns 500 with `service-unavailable` reason** [src/Hexalith.EventStore/Controllers/StreamsController.cs:161-169] — Resolved with 503/500 taxonomy split.

MEDIUM:

- [x] [Review][Patch] **P13 — `SaveAsync` swallows all non-cancel exceptions as `checkpoint-unavailable`** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:102-105] — Resolved with exception discrimination and propagation test.
- [x] [Review][Patch] **P14 — `SaveAsync` idempotency ignores `failureReasonCode` change for non-progress statuses** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:60-71] — Resolved by equality-only idempotency including failure reason and `toPosition`.
- [x] [Review][Patch] **P15 — `StreamReadEvent.Payload` is `byte[]` on a record** [src/Hexalith.EventStore.Contracts/Streams/StreamReadEvent.cs:11-29] — Documented array reference-equality semantics.
- [x] [Review][Patch] **P16 — `DaprProjectionCommandService.MapFailureResponseAsync` reads the full response body unbounded** [src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionCommandService.cs:463-487] — Resolved with bounded JSON read.
- [x] [Review][Patch] **P17 — Tenant validator called with raw (non-normalized) `request.Tenant`** [src/Hexalith.EventStore/Controllers/StreamsController.cs:63-95] — Resolved by rejecting non-canonical identity before validation.
- [x] [Review][Patch] **P18 — `ResetProjection` accepts null body, silently treats as `FromPosition=0`** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:76-88] — Resolved by 400 null-body handling.
- [x] [Review][Patch] **P19 — `ProjectionResetRequest`/`ProjectionReplayRequest` declared in controller file** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:250-261] — Resolved by moving DTOs to Contracts.
- [x] [Review][Patch] **P20 — `ProjectionRebuildCheckpointStore.SaveAsync` `ArgumentException` propagates to caller as 500** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:143-149] — Resolved by prior controller 400 mapping.
- [x] [Review][Patch] **P21 — Lifecycle controller tests skip persisted-state inspection** [tests/Hexalith.EventStore.Server.Tests/Controllers/AdminProjectionRebuildControllerTests.cs] — Resolved with checkpoint-store behavior tests for reset/rewind and programmer-error propagation.

LOW:

- [x] [Review][Patch] **P22 — `StaleCheckpoint` reason advertised but never produced** [src/Hexalith.EventStore.Contracts/Streams/StreamReplayReasonCodes.cs; docs/reference/stream-replay-api.md] — Removed unused reason code from taxonomy/docs.
- [x] [Review][Patch] **P23 — `GetRebuildStatus` returns `StartedAt=UnixEpoch` for not-started operations** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:223-236] — Resolved with nullable `StartedAt`.
- [x] [Review][Patch] **P24 — Redundant `sub` claim check returns bare `Unauthorized()`** [src/Hexalith.EventStore/Controllers/StreamsController.cs:58-61] — Removed manual check.
- [x] [Review][Patch] **P25 — `ValidateRequest` uses `invalid-range` for missing tenant/domain** [src/Hexalith.EventStore/Controllers/StreamsController.cs:173-181] — Resolved with `missing-required-field`.
- [x] [Review][Patch] **P26 — `Pause`/`Cancel` persist sentinel values in `FailureReasonCode`** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:43-51, 124-135] — Resolved by prior null failure-reason patch.
- [x] [Review][Patch] **P27 — `lastSequenceReturned` reports `request.FromSequence` for empty pages** [src/Hexalith.EventStore/Controllers/StreamsController.cs:113-115] — Resolved with nullable metadata.
- [x] [Review][Patch] **P28 — `NormalizePageSize` is dead code after `ValidateRequest`** [src/Hexalith.EventStore/Controllers/StreamsController.cs:108, 241] — Removed helper.
- [x] [Review][Patch] **P29 — `DaprProjectionCommandService` uses nullable `ReasonPhrase` as fallback message** [src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionCommandService.cs:478, 486] — Added stable fallback.
- [x] [Review][Patch] **P30 — `reasonCode` JSON property capitalization not pinned by a wire test** [src/Hexalith.EventStore/Controllers/StreamsController.cs:234-235; src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionCommandService.cs:GetString(root, "reasonCode")] — Added lowercase serialization assertion.
- [x] [Review][Patch] **P31 — `FakeEventStoreGatewayClient.ConfigureStreamReadContinuation` mutates only two fields** [src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs:1504-1515] — Normalizes continuation metadata.
- [x] [Review][Patch] **P32 — No-leak ProblemDetails assertions only check literal "state store"** [tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs] — Expanded forbidden-substring coverage.

#### Defer (1)

- [x] [Review][Defer] **W1 — Unrelated integration test renames** [tests/Hexalith.EventStore.IntegrationTests/EventStore/CommandStatusIntegrationTests.cs, ReplayIntegrationTests.cs] — deferred, scope creep (touched in `8348b93e` refactor) — `Async` suffix additions and `using` cleanups belong to Stories 22.1/22.4 surfaces, not 22.6. Not worth reverting now; flag for a future hygiene commit.

#### Dismissed (3)

- D1 (dismissed) — `MissingEventException.Detail` exposes the sequence number. Acceptable per story (sequence numbers are not protected material; story explicitly forbids only payload bytes/state-store keys/etc.).
- D2 (dismissed) — `ProjectionRebuildCheckpointStore.GetStateKey` is `internal static` and validated at every entry point through `SaveAsync`/`ReadAsync` → `ValidateScope`. Defense-in-depth nit only.
- D3 (dismissed) — `StreamsController.ProblemWithReason` casts `Problem(...)` result to `ObjectResult` and conditionally injects extensions. Works correctly with current framework; conditional `is ProblemDetails` is defensive, not a bug.

#### Reviewer notes

- Acceptance Auditor: AC4 (apply-driven advancement) and AC3 (continuation pagination) are not satisfied. AC1, AC2, AC6, AC7 broadly satisfied; AC5, AC8 partial.
- Blind Hunter: most severe is the AdminProjectionRebuildController authorization gap and the SaveAsync early-return that silently swallows lifecycle transitions.
- Edge Case Hunter: classified four issues as CRITICAL (P1, P2, P3, and the continuation pagination break covered by D3) — all confirmed by direct file inspection.

#### Code review run on 2026-05-17 (Opus 4.7 1M — sixth pass against `c3205fdb~1..HEAD` = last 10 commits a1790f94..c3205fdb, 21 source/test/doc files, +2168/-195)

_Three adversarial layers ran in parallel: Blind Hunter (diff-only), Edge Case Hunter (diff + project read access), Acceptance Auditor (diff + spec + CLAUDE.md). After dedup and triage: **3 decision-needed, 20 patch, 3 defer, 5 dismiss**. **Acceptance Auditor verdict:** all four CRITICAL pass-5 findings (C1/C2/C3/C4-5P) landed and verified at cited line numbers; AC1/AC2/AC5/AC6/AC7 Satisfied, AC4/AC8 Substantially Satisfied with explicitly-deferred follow-up (W1-5P RebuildSchedulerActor + Aspire 13.3.2 Tier-3 proof), AC3 Partially Satisfied (continuation-token binding-validation rests on prior-pass code not in this diff). **Story is functionally close to review-ready; remaining items are correctness hardening discovered post pass-5 apply.**_

##### Decision-Needed (3 — all resolved 2026-05-17)

- [x] [Review][Decision-resolved] **D1-6P — Tenant/domain case-canonicalization policy** [src/Hexalith.EventStore/Controllers/StreamsController.cs:~308-318] — **Resolved: revert validator to lowercase-only** (became P21-6P). `IsCanonicalTenantOrDomain` widened from lowercase-only (`c is >= 'a' and <= 'z' || ...`) to `char.IsAsciiLetterOrDigit(c) || c == '-'`. State-store keys (`projection-rebuild-checkpoints:{tenant}:{domain}:...`) are case-sensitive: `"Tenant-A"` and `"tenant-a"` now both validate but address different rows. The test diff REMOVED the prior `InlineData("Tenant-A", ...)` / `InlineData(Tenant, "Party", ...)` rejection rows. [Blind+Edge]
- [x] [Review][Decision-resolved] **D2-6P — `ToSequence == FromSequence` semantics** [src/Hexalith.EventStore/Controllers/StreamsController.cs:~275-280] — **Resolved: keep loosened behavior, add pinning test** (became P22-6P). `H6-5P/H7-5P` patch loosened the range guard from `<= FromSequence` (rejected) to `< FromSequence` (rejected). `equal` previously returned 400, now returns 200 with empty page; documented as "no events from this exclusive lower bound." [Blind]
- [x] [Review][Decision-resolved] **D3-6P — `!matchedAny && reachedBound` Succeeded write semantics** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~1075-1090] — **Resolved: keep vacuously-successful behavior, add pinning test** (became P23-6P). For a brand-new domain with zero tracked aggregates but a configured `ToPosition`, marking `Succeeded` is intentional: the stated bound was reached. [Blind]

##### Patches (20)

- [x] [Review][Patch] **P1-6P — `NotStarted` lifecycle write removes projection from active-index, opening poller race** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:~358-380] — `UpdateActiveIndexForLifecycleAsync` treats `NotStarted` as deactivation (`IsLifecycleActive(NotStarted) == false`, falls through to `current.Remove(projectionName)`). A fresh `ResetAsync(..., NotStarted)` removes the projection from the active-rebuild index; the subsequent `Running` write re-adds it. Between those two writes, `HasActiveOperatorRebuildForDomainAsync` returns false and the poller can race the rebuild. [Blind+Edge]
- [x] [Review][Patch] **P2-6P — `TaskCanceledException` classified as 503 transient by `IsServiceUnavailable`** [src/Hexalith.EventStore/Controllers/StreamsController.cs:~1879] — `TaskCanceledException` (subclass of `OperationCanceledException`) is matched by the transient classification and the outer dispatcher returns 503 with `Retry-After` on client-disconnect cancellation. The inner `ProjectionRebuildCheckpointStore.SaveAsync` already filters OCE properly; only the controller's classification needs to exclude `OperationCanceledException`. [Blind+Edge]
- [x] [Review][Patch] **P3-6P — `RetryProjection` returns 500 / wedged state after `ResetAsync(Retrying)` succeeds** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:~283-285] — The Retry branch invokes `RebuildProjectionAsync(retryScope, ct)` synchronously after the checkpoint row was already written with `Retrying` + new `OperationId`. If `RebuildProjectionAsync` throws (e.g., transient store unavailability before first delivery), the controller propagates 500 and the operator never receives the new OperationId — leaving the row in a `Retrying` state owned by a token the caller doesn't know. [Blind+Edge]
- [x] [Review][Patch] **P4-6P — Cleanup `catch` paths can lose original exception when `ResetAsync` throws inside cleanup** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~985-1045] — Both the `OperationCanceledException` cancel-cleanup (lines ~985-1015) and the `H1-5P` non-OCE catch (lines ~1020-1045) call `ResetAsync` inside `try`. If the inner `ResetAsync` itself throws, that secondary exception replaces the original on the way out. The OCE branch has an inner try/catch swallowing non-OCE; the non-OCE branch lacks the same defense. [Blind]
- [x] [Review][Patch] **P5-6P — Per-aggregate operation-changed pre-save check skipped for aggregate-scoped rebuilds** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~1266-1273] — `PerAggregateProgressChanged` race-detection only runs when `operatorScope.AggregateId is null` (domain-wide). For an aggregate-scoped rebuild, a concurrent Retry rewriting the same per-aggregate key is not detected — stale page application can silently advance progress under a new OperationId. [Blind+Edge]
- [x] [Review][Patch] **P6-6P — `PageSize` used in `FromSequence` overflow guard before `PageSize` itself is validated** [src/Hexalith.EventStore/Controllers/StreamsController.cs:~276-285] — Range validator computes `FromSequence > int.MaxValue - request.PageSize - 1L` at lines ~276-283 BEFORE the `PageSize is <= 0 or > _maxPageSize` check at lines ~287-294. Negative `PageSize` (e.g., `int.MinValue`) inflates the right-hand side and relaxes the FromSequence overflow guard, letting an out-of-range FromSequence pass validation. [Edge]
- [x] [Review][Patch] **P7-6P — `ArgumentOutOfRangeException` from actor overflow guard maps to `InvalidAggregateIdentity`, not `InvalidRange`** [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:~674-676 + src/Hexalith.EventStore/Controllers/StreamsController.cs catch block] — Actor's overflow-boundary guard throws `ArgumentOutOfRangeException` which the controller's `ArgumentException` catch labels as `InvalidAggregateIdentity` reason. Operators see a misleading identity-error reason code for a sequence-range overflow. [Edge]
- [x] [Review][Patch] **P8-6P — `H1-5P` Failed write rewinds operator-scope `LastAppliedSequence` to `initial.LastAppliedSequence`, losing accumulated per-aggregate progress** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~367-378 / ~1020-1045] — When per-aggregate row for aggregate A has advanced during the iteration but aggregate B throws, the Failed write captures `initial.LastAppliedSequence` (the pre-iteration value). Per-aggregate rows retain the higher progress while operator-scope row regresses below them. Use `Math.Max(initial.LastAppliedSequence, highestMatchedProgress)`. [Edge]
- [x] [Review][Patch] **P9-6P — `operatorSnap` re-read inside iteration uses request CT; cancel-race drops the iteration without running cancel-cleanup** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~286-291] — Re-read of operator snapshot per iteration uses the request `CancellationToken`. A concurrent Pause/Cancel that flips the lifecycle while ReadAsync is in flight will throw OCE; the outer OCE catch reaches cleanup, but the per-iteration read path that intentionally returns early on detected lifecycle change is bypassed. Recommend wrapping the per-iteration read so cancel-cleanup runs uniformly. [Edge]
- [x] [Review][Patch] **P10-6P — Initial `ReadAsync` transient-store failure leaves active-rebuilds index entry indefinitely** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~264-269] — If the initial operator-scope `ReadAsync` fails transiently before any iteration runs, the rebuild scope has already been registered in the active-rebuilds index by the controller's `SaveAsync(..., Running, ...)`. The orchestrator returns without writing any terminal status; the index entry persists, blocking the poller forever. [Edge]
- [x] [Review][Patch] **P11-6P — `SaveAsync` no-op short-circuit returns Success without verifying `scope.AggregateId` matches `existing.AggregateId`** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:~128-140] — When the no-op idempotent path returns `Success(existing)`, it does not verify the incoming scope's `AggregateId` matches `existing.AggregateId`. A domain-wide caller can no-op on a row written by an aggregate-specific scope (or vice versa), creating cross-scope status drift. Add `if (!string.Equals(existing.AggregateId, scope.AggregateId, StringComparison.Ordinal)) return Failure(CheckpointConflict);`. [Edge]
- [x] [Review][Patch] **P12-6P — `GetStreamMetadataAsync` exceptions (e.g., metadata deserialization) not mapped to a stable error response** [src/Hexalith.EventStore/Controllers/StreamsController.cs:~104-112] — `ReadEventsRangeAsync` is wrapped in a try/catch that translates `EventDeserializationException` to a structured response; `GetStreamMetadataAsync` is called outside that try/catch. A metadata corruption surfaces as an unmapped 500. Wrap the metadata call in the same try/catch as the page read. [Edge]
- [x] [Review][Patch] **P13-6P — Active-index update failure after a successful terminal checkpoint write leaves ghost entries permanently** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:~190-210] — `SaveAsync` writes the terminal checkpoint first, then calls `UpdateActiveIndexForLifecycleAsync`. If that helper fails to REMOVE the projection (e.g., transient store failure on index write), the caller gets `CheckpointUnavailable` but the terminal row is already persisted. `HasActiveOperatorRebuildForDomainAsync` returns true forever; poller is permanently blocked even though the lifecycle reached terminal. Need compensation (retry or scheduled reconciliation). [Blind]
- [x] [Review][Patch] **P14-6P — `SaveLifecycleAsync` does not catch generic exceptions thrown by orchestrator after `Failed` was written** [src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs:~342-366] — When `RebuildProjectionAsync` writes `Failed` via the H1-5P path and then re-throws (a DAPR transient exception not classified as recoverable, for example), `SaveLifecycleAsync` does not catch it. The caller sees an unstructured 500 instead of the structured 409 + `FailureReasonCode` the terminal write supports. [Edge]
- [x] [Review][Patch] **P15-6P — `IsDeserializationFailure` walks only `InnerException`, ignores `AggregateException.InnerExceptions`** [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:~225-238] — Recursive walk only checks `exception.InnerException`. A genuine `JsonException` nested inside `AggregateException.InnerExceptions[i]` (common in `Task` failure aggregation) is misclassified as a programmer error and crashes the actor instead of returning corrupt-event metadata. Iterate `AggregateException.InnerExceptions` too. [Blind]
- [x] [Review][Patch] **P16-6P — `EventStoreGatewayClient` `MaxStreamReadResponseBytes` constructor validation untested** [tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientStreamTests.cs:~75] — The new `ArgumentOutOfRangeException` paths for `<= 0` and `> int.MaxValue` are uncovered. Add two tests asserting the constructor throws for those boundaries. [Blind]
- [x] [Review][Patch] **P17-6P — No `cancellationToken.ThrowIfCancellationRequested()` between ETag retry iterations in `SaveAsync`** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:~96-204] — The ETag retry loop relies on each downstream call to observe cancellation. If cancellation arrives between iterations, the next attempt still launches before the next call observes it. Add an explicit `cancellationToken.ThrowIfCancellationRequested()` at the top of each iteration. [Edge]
- [x] [Review][Patch] **P18-6P — Active-index ETag conflict retries 3 times with no backoff delay** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:~365-413] — `UpdateActiveIndexForLifecycleAsync` re-enters the ETag retry loop on conflict (`!saved`) without `await Task.Delay(BoundedRetryDelay(attempt), cancellationToken)`. Three rapid retries against a hot key cause unnecessary state-store thrash. Mirror the transient-store catch's backoff. [Edge]
- [x] [Review][Patch] **P19-6P — `CanRunRebuild(NotStarted)` allows `DeliverProjectionForRebuildAsync` to proceed before the operator scope has been written `Running`** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~1351] — `CanRunRebuild` returns true for `NotStarted` (per the comment, to support cancel-cleanup before first iteration). A racing `RebuildProjectionAsync` invocation that finds the scope in `NotStarted` will pass the gate and start projection deliveries before the lifecycle transitions to `Running`. The active-index is not yet updated (NotStarted is filtered in `UpdateActiveIndexForLifecycleAsync`), so the poller can race the rebuild. Recommend keeping NotStarted only on the cleanup path, not on the active-delivery path. [Blind]
- [x] [Review][Patch] **P20-6P — `ReadEventsRangeAsync` overflow guard fires on empty streams when `fromSequence` is at extreme boundary** [src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:~159-168] — The comment claims "read metadata BEFORE the overflow guard so the empty-stream contract is honored even when fromSequence is at extreme boundary values." But after passing the `currentSequence <= fromSequence → return []` guard, the subsequent overflow guard `fromSequence > int.MaxValue - maxCount` still throws for legitimate empty-stream queries. Comment contradicts code; move the overflow guard so it does not fire for empty streams (or update the comment). [Blind]
- [x] [Review][Patch] **P21-6P — Revert `IsCanonicalTenantOrDomain` to lowercase-only and re-add rejection tests** [src/Hexalith.EventStore/Controllers/StreamsController.cs:~308-318 + tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs] — D1-6P resolution. Restore the prior `c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '-'` predicate; re-add the removed `[InlineData("Tenant-A", ...)]` and `[InlineData(Tenant, "Party", ...)]` rejection rows asserting 400 InvalidAggregateIdentity / InvalidScope. [Decision]
- [x] [Review][Patch] **P22-6P — Add a pinning test for `ToSequence == FromSequence → empty page` semantics** [tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs] — D2-6P resolution. Add `ReadStreamAsyncWithToSequenceEqualToFromSequenceReturnsEmptyPage` proving 200 + `EventCount == 0` + `LastSequenceReturned == null` + `LatestSequence == currentSequence`. Document the behavior in `docs/reference/stream-replay-api.md`. [Decision]
- [x] [Review][Patch] **P23-6P — Add a pinning test for empty-domain `!matchedAny && reachedBound → Succeeded`** [tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs] — D3-6P resolution. Add `RebuildProjectionAsync_NoMatchedAggregatesWithReachedBoundWritesSucceeded` proving the operator-scope row transitions to `Succeeded` when zero aggregates match and the configured `ToPosition` bound is reached. [Decision]

##### Deferred (3)

- [x] [Review][Defer] **W1-6P — Legacy null-`OperationId` rows allow ownership takeover** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:~770-781] — deferred — same as M12-5P; documented limitation; `IsDifferentOperation` returns false when `existing.OperationId is null` so any caller can take ownership. Spec accepts as migration-era trust boundary. [Blind]
- [x] [Review][Defer] **W2-6P — `IsValidOperationId` broad exception catch swallows unrelated runtime errors** [src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs:~410] — deferred — same as W2-5P/W3-5P; documented; the helper still uses `UniqueIdHelper.ToGuid` and a broad `catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)` returning `false`. Switching to `Ulid.TryParse` is the canonical fix per CLAUDE.md R2-A7 but was explicitly deferred for this story. [Blind]
- [x] [Review][Defer] **W3-6P — `RebuildPageSize = 256` hardcoded private constant has no operator override** [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:~1380] — deferred — tuning concern, not a correctness defect; track in follow-up story alongside D2a `RebuildSchedulerActor` work. [Blind]

##### Dismissed (5)

- B11 (dismissed) — `EnsureGlobalAdministrator` 403 reason code change from `UnauthorizedTenant` to `ForbiddenRole`. Acceptance Auditor confirms this is an intentional addition to the stable taxonomy documented in `stream-replay-api.md:88-109`.
- B12 (dismissed) — `WithDeterministicIds` overload silently overrides previously configured factory. Builder API gotcha, not a bug; tests should call one overload per builder instance.
- B15 (dismissed) — `IsServiceUnavailable` treats `ActorMethodInvocationException` with null `InnerException` as 503. Acceptance Auditor confirms this is documented DEC5-5P; intentional handling of the canonical Dapr SDK 503 case.
- E11 (dismissed) — `HttpClient.MaxResponseContentBufferSize` setter throws `InvalidOperationException` when the client has already issued a request. Documented in the XML doc; typed-client lifetime is callers' responsibility.
- E15 (dismissed) — Speculative off-by-one in `upperBound = Math.Min(toSequence ?? currentSequence, currentSequence)` when `toSequence == currentSequence`. Manual verification shows the inclusive boundary is correct (caller asking for `toSequence == currentSequence` gets the final event when `fromSequence < currentSequence`). Not a bug.

#### Reviewer notes (6th pass)

- Acceptance Auditor: confirmed all four pass-5 CRITICAL items (C1/C2/C3/C4-5P) landed and verified; AC1/AC2/AC5/AC6/AC7 Satisfied; AC4/AC8 Substantially Satisfied with explicitly-deferred follow-up; AC3 Partially Satisfied (continuation-token binding rests on prior-pass code). Recommendation: ready for `review` status pending decision-needed resolution.
- Blind Hunter: largest themes — exception-loss in cleanup catches (P4), active-index race windows (P1, P13, P19), and per-aggregate scope guard gaps (P5).
- Edge Case Hunter: PageSize-validation ordering (P6), reason-code drift (P7), and operator-scope progress regression (P8) are the most operator-visible defects.

## Test Evidence Required

- Contracts: DTO serialization, omitted/default values, reason-code constants, continuation metadata shape, checkpoint/progress metadata shape, and wire compatibility for existing replay DTOs.
- Client: stream read success, empty page, continuation page, cancellation, invalid continuation ProblemDetails, invalid range ProblemDetails, missing stream, unauthorized/forbidden, unavailable, and typed exception/result mapping.
- Testing: fakes/builders for the same adopter-visible scenarios as Client, including request capture and deterministic checkpoint/rebuild lifecycle states.
- Server/controller: authorization and scope validation before actor/checkpoint/projection access, invalid continuation/range handling, bounded page ordering, continuation generation, missing/corrupt event mapping, and safe ProblemDetails.
- Checkpoint/rebuild: ETag or equivalent optimistic concurrency, monotonic advancement, stale write rejection, duplicate retry idempotency, pause/resume/cancel idempotency, concurrent worker conflict, terminal-state behavior, and checkpoint-store unavailable handling.
- Negative checkpoint advancement: no checkpoint progress beyond the last safely applied sequence when projection apply rejects a page, a page is partially processed, a corrupt/missing event is encountered, protected payload data is unreadable, a rebuild is cancelled, a timeout occurs, or checkpoint storage is unavailable.
- No-leak: DTOs, ProblemDetails, logs/activity tags, continuation tokens, docs examples, and test artifacts must not expose DAPR actor IDs, state-store keys, raw payload bytes, protected data, connection strings, DAPR addresses, stack traces, tokens, or user-controlled display names.
- Integration/manual proof: use Docker/Aspire/DAPR only when available; record exact prerequisites and commands. Do not use broad `Hexalith.EventStore.Server.Tests` as the first proof because the repo has a known unrelated CA2007 warning-as-error risk.

## Developer Notes

Architecture and product guardrails:

- FR99-FR101 are controlling: public stream read/replay APIs, resumable checkpoints, operator-safe projection rebuild, and docs that forbid state-store internals.
- ADR-P9 makes replay and checkpoint behavior a platform contract. Treat DTO fields, route behavior, ProblemDetails reason codes, and checkpoint semantics as SemVer-relevant.
- Story 22.1 owns package ownership and high-level gateway client patterns. Follow its Contracts/Client/Testing split.
- Story 22.2 owns generic projection adapter/query actor contracts. Do not change query routing or projection actor naming unless needed for rebuild and documented as a Story 22.6 decision.
- Story 22.3 owns gateway tenant/RBAC enforcement. Stream reads and rebuild controls must fail closed before invoking aggregate actors or projection services.
- Story 22.4 owns query paging/freshness policy. Reuse its stable ProblemDetails and metadata style where appropriate, but keep stream replay paging semantics distinct.
- Story 22.5 owns publish durability and backend deployment matrix. Do not use pub/sub replay as a substitute for public stream reads.
- Stories 22.7a-22.7d own protected payload semantics. Record protected-data gaps as deferred unless replay would otherwise leak data.

Implementation traps to avoid:

- Do not make downstream services read DAPR actor state keys directly.
- Do not expose admin stream debugging endpoints as the public downstream replay API.
- Do not derive actor IDs from unvalidated caller strings or continuation tokens.
- Do not read aggregate event state before tenant/domain/aggregate authorization succeeds.
- Do not decode a continuation token into actor/checkpoint access before tenant, domain, aggregate, and replay scope are authorized.
- Do not let continuation tokens expose state keys, actor IDs, raw JSON state, payload offsets, protected metadata, or untrusted user data.
- Do not claim exactly-once projection rebuild. Duplicate pages and retries are normal; checkpoint advancement must be idempotent.
- Do not update checkpoint progress without ETag/optimistic concurrency semantics.
- Do not update checkpoint progress before the projection apply path has accepted the page.
- Do not let normal polling and operator rebuild write the same checkpoint concurrently without a documented conflict policy and tests.
- Do not let checkpoint save failure falsely report rebuild success.
- Do not conflate command replay (`ReplayController`) with event stream replay/projection rebuild.
- Do not broaden payload-protection behavior in this story; defer Story 22.7 policy decisions.
- Do not turn operator rebuild lifecycle into a hidden or partial control plane. Either implement and test the public/operator state transitions, or label the gap as deferred with safe current behavior.
- Do not run solution-level tests first. Use focused Contracts, Client, Testing, and Server slices.

Current file intelligence:

- Public contracts and client:
    - `src/Hexalith.EventStore.Contracts/Replay/AggregateReconstructionRequest.cs`
    - `src/Hexalith.EventStore.Contracts/Replay/AggregateReconstructionResult.cs`
    - `src/Hexalith.EventStore.Contracts/Replay/ReplayEventEnvelope.cs`
    - `src/Hexalith.EventStore.Contracts/Problems/GatewayProblemDetailsExtensions.cs`
    - `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`
    - `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- Actor and stream read internals:
    - `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs`
    - `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
    - `src/Hexalith.EventStore.Server/Events/EventStreamReader.cs`
    - `src/Hexalith.EventStore.Server/Events/MissingEventException.cs`
    - `src/Hexalith.EventStore.Server/Events/EventDeserializationException.cs`
- Projection rebuild and checkpoints:
    - `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
    - `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
    - `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs`
    - `src/Hexalith.EventStore.Server/Projections/ProjectionPollerService.cs`
    - `src/Hexalith.EventStore.Server/Projections/ProjectionReasonCodes.cs`
    - `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`
    - `src/Hexalith.EventStore.Server/Actors/EventReplayProjectionActor.cs`
- HTTP/admin surfaces:
    - `src/Hexalith.EventStore/Controllers/AdminStreamQueryController.cs`
    - `src/Hexalith.EventStore/Controllers/ReplayController.cs`
    - `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs`
    - `src/Hexalith.EventStore.Admin.Server/Services/DaprStreamQueryService.cs`
    - `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionCommandService.cs`
    - `src/Hexalith.EventStore.Admin.Server/Models/ProjectionReplayRequest.cs`
    - `src/Hexalith.EventStore.Admin.Server/Models/ProjectionResetRequest.cs`
- Testing entry points:
    - `tests/Hexalith.EventStore.Contracts.Tests/Replay/AggregateReconstructionRoundTripTests.cs`
    - `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`
    - `tests/Hexalith.EventStore.Testing.Tests/Builders/EventEnvelopeBuilderTests.cs`
    - `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventStoreGatewayClientTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminStreamQueryControllerTimelineTests.cs`
    - `tests/Hexalith.EventStore.Server.Tests/Controllers/Dw3DirectMaxParameterBoundsAtddTests.cs`

Latest technical specifics checked:

- DAPR 1.17 official docs still describe service invocation as standard HTTP/gRPC communication through sidecars with service discovery, tracing, metrics, error handling, and optional mTLS/access controls. Keep EventStore-to-domain calls on service invocation rather than hardcoded service URLs.
- DAPR state management docs still define ETag-based optimistic concurrency and last-write-wins behavior when ETags are omitted. Rebuild checkpoints must use ETag-aware writes.
- DAPR actor docs still require an actor state store component with `actorStateStore: true`. Actor event stream reads should remain behind actor APIs, not plain state-store reads.
- The repo pins DAPR package family `1.17.7`; do not introduce package versions in project files, and keep any docs/examples aligned with the repo's DAPR/Aspire configuration.

Testing standards:

- Use xUnit v3, Shouldly, and NSubstitute where existing test projects already use them.
- Run test projects individually per repository guidance.
- Prefer unit tests for DTO/client/checkpoint/controller behavior before integration tests.
- Integration tests require Docker, DAPR placement/scheduler, and Aspire resources. Use `EnableKeycloak=false aspire run --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` only when runtime proof is required.
- `Hexalith.EventStore.Server.Tests` has known pre-existing CA2007 warning-as-error risk in broad runs. Use focused filters and record unrelated blockers exactly.

## Files Likely Touched

- `src/Hexalith.EventStore.Contracts/Replay/*.cs` or a new `src/Hexalith.EventStore.Contracts/Streams/*.cs`
- `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Testing/Builders/*`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs`
- `src/Hexalith.EventStore/Controllers/*Stream*Controller.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionReasonCodes.cs`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionCommandService.cs`
- `src/Hexalith.EventStore.Admin.Server/Controllers/AdminProjectionsController.cs`
- `src/Hexalith.EventStore.Admin.Cli/Commands/Projection/*`
- `src/Hexalith.EventStore.Admin.Mcp/Tools/ProjectionTools.cs`
- `docs/reference/stream-replay-api.md`
- `docs/guides/configuration-reference.md`
- `docs/guides/deployment-progression.md`
- Generated API docs under `docs/reference/api/**` only if public XML docs change.
- Focused tests under `tests/Hexalith.EventStore.Contracts.Tests`, `tests/Hexalith.EventStore.Client.Tests`, `tests/Hexalith.EventStore.Testing.Tests`, and `tests/Hexalith.EventStore.Server.Tests`.

## Out of Scope

- Public command/query DTO ownership and client command/query behavior; Story 22.1 owns that.
- Generic projection adapter contracts and query actor routing; Story 22.2 owns that.
- Tenant lifecycle, membership, role, and permission validator design; Story 22.3 owns that.
- Query paging/search/freshness and query ProblemDetails taxonomy; Story 22.4 owns that.
- Pub/sub durability, ordering/session metadata, backend deployment matrix, and publish drain policy; Story 22.5 owns that.
- Payload/snapshot protection hooks, unreadable protected payload behavior, crypto-shredding, backup restore safety, and cross-surface redaction policy; Stories 22.7a-22.7d own that.
- Broad Admin UI redesign, Parties repository changes, or direct Hexalith.Tenants implementation changes.

## References

- `_bmad-output/planning-artifacts/epics.md#Story 22.6: Stream Replay/Read APIs and Projection Rebuild Checkpoints`
- `_bmad-output/planning-artifacts/prd.md#Public Gateway and Downstream Integration Contracts - v1.1 (FR83-FR104)`
- `_bmad-output/planning-artifacts/architecture.md#ADR-P9: Downstream Query, Publishing, Replay, and Payload-Protection Policies Are Platform Contracts`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-requirements-gaps-current.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-12-eventstore-parties-integration-contract-gaps.md`
- `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md`
- `_bmad-output/implementation-artifacts/22-2-projection-adapter-contract-and-generic-query-actor-model.md`
- `_bmad-output/implementation-artifacts/22-3-gateway-owned-tenant-and-rbac-enforcement.md`
- `_bmad-output/implementation-artifacts/22-4-query-behavior-policy-and-error-taxonomy.md`
- `_bmad-output/implementation-artifacts/22-5-event-publishing-guarantees-and-backend-deployment-matrix.md`
- `_bmad-output/project-context.md`
- `_bmad-output/process-notes/story-creation-lessons.md#L08 - Party Review Vs. Elicitation`
- `https://docs.dapr.io/developing-applications/building-blocks/service-invocation/`
- `https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/`
- `https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-17T10:30:00+02:00 - Pass-5 continuation closed the three remaining current pass-5 items: M1-5P state-store end-state capture assertions, M11-5P per-aggregate pre-write drift guard before projection-state writes, and L4-5P mixed multi-aggregate `PageComplete` regression coverage. Aspire AppHost was started with `EnableKeycloak=false`, resources were inspected healthy with `aspire describe`, then the AppHost was stopped before tests to release build-output locks. Focused Server projection tests passed 71/71.
- 2026-05-17T09:40:00+02:00 - Pass-5 continuation applied the HIGH set H4-H15 plus most MEDIUM/LOW hygiene fixes: role-aware stream read test IDs, stream metadata existence probe, toSequence validation, bounded exception unwinding, DAPR timeout classification, active-index save ordering/retry helper, retry/replay lifecycle response fixes, admin/stream no-leak assertions, review diff cleanup, client response-cap assignment, and focused validation. Remaining open pass-5 items: M1-5P state-store end-state evidence, M11-5P per-aggregate OperationId pre-write guard decision, and L4-5P multi-aggregate page-completion regression test.
- 2026-05-16T18:30:00+02:00 - Third review-continuation pass (Opus 4.7 1M) applied all 37 patches across 18 files: 10 decisions resolved (DEC1-DEC10) routed to patches, plus 27 focused patches. Major changes: D3-A per-aggregate scope keys for storage; D3-B active-rebuilds index in `IProjectionRebuildCheckpointStore.HasActiveOperatorRebuildForDomainAsync`; D3-E rebuild reads from per-aggregate rebuild checkpoint not poller checkpoint; DEC2 NoDomainServiceRegistered → Failed terminal; DEC3 cancel-cleanup uses ResetAsync; DEC4 TimeoutException narrowed to wrapped variants; DEC5 ActorMethodInvocationException null-inner restored to 503; DEC6 EventStoreGatewayClient overflow guard + doc; DEC7 ReplayProjection reads terminal checkpoint maps Failed→409; DEC8 ResetAsync IsDifferentActiveOperation guard; DEC9 Retry generates fresh ULID; DEC10 unified IsDifferentOperation guard covers active+terminal records; P1 MissingStream 404 covers FromSequence>0; P2 GetCurrentSequenceAsync narrow exception filter (IsDeserializationFailure); P3-P5 actor range guards; P6 Ulid via UniqueIdHelper.ToGuid case-insensitive; P7 fresh-domain matchedAny=false writes Succeeded; P8 empty-events explicit pageComplete; P9 exact-256 boundary; P10 ProjectionApplyRejected save uses CancellationToken.None; P11 MapSaveFailure ForbiddenRole + NoDomainService; P12 Retry-After on 409 OperationInFlight; P13 Retry preserves FailureReasonCode; P14 NotStarted in CanRunRebuild; P15 log lost cancel-cleanup; P16 unified IsDifferentOperation; P17 long→int overflow guard; P18 MaxExceptionFrames rename; P19 OperationId equality re-check before UpdateProjectionAsync; P20 StreamReadPageBuilder deterministic factory; P22 retry-delays array bounds assert; P24 per-aggregate progress >0L test; P26 cancel null-snapshot test. Validation: focused Server.Tests 178/178, Client.Tests 389/389, Contracts.Tests 333/333, Testing.Tests 110/110.
- 2026-05-16T08:35:00+02:00 - Review-continuation pass applied a focused set of D3/P-C/H/M/L fixes: Retry now routes through `ResetAsync`, `ResetAsync` trust-boundary docs were added, concurrent active OperationIds fail with `operation-in-flight`, rebuild terminal success is gated on bounded page completion, lifecycle is re-checked before projection writes, cancellation writes `Canceled`, checkpoint store operation IDs and aggregate scopes are validated, stream reads use current stream tips, actor service-unavailable classification is narrowed, 503 responses include `Retry-After`, and client/testing stream helpers were hardened.
- 2026-05-15T12:05:00+02:00 - Review-continuation pass applied 31 of 32 remaining review patches: range-aware actor reads, stream request validation/error taxonomy, explicit checkpoint reset/rewind, fresh replay/reset operation IDs, persisted `ToPosition`, nullable empty-page metadata, bounded Admin.Server ProblemDetails parsing, fake/client parity fixes, docs update, and focused tests. One design-level item remains: P4/P-D2 full apply-driven rebuild advancement worker.
- 2026-05-15T07:47:52+02:00 - ST6/ST7 validation completed: focused stream/rebuild tests passed, full Contracts/Client/Testing/Sample test projects passed, docs markdown lint passed, and Aspire integration proof remained blocked by the local Aspire CLI/AppHost package version mismatch.
- 2026-05-15T07:52:00+02:00 - Broad `Hexalith.EventStore.Admin.Server.Tests` was attempted as an extra check and failed outside the story-focused tests: OpenAPI/resiliency suites could not load `Microsoft.AspNetCore.OpenApi` 10.0.8 and `YamlDotNet` 17.0.0 after MSB3277 version-conflict warnings.
- 2026-05-15T08:28:00+02:00 - ST5 completed: added `docs/reference/stream-replay-api.md`; `npx markdownlint-cli2 docs/reference/stream-replay-api.md` passed with 0 errors.
- 2026-05-15T08:23:00+02:00 - ST4 red-green completed: EventStore admin rebuild lifecycle endpoint tests passed (4/4), Admin.Server DAPR failure mapping tests passed (2/2, with existing package conflict warnings), and projection poller/rebuild conflict test passed (1/1).
- 2026-05-15T08:07:00+02:00 - ST3 red-green completed: rebuild checkpoint store tests passed (5/5), reason-code contract tests passed (3/3), and testing fake stream helpers passed (9/9).
- 2026-05-15T07:54:00+02:00 - ST2 red-green completed: public `POST /api/v1/streams/read` controller tests passed (6/6), including invalid range/continuation auth-before-actor checks, forbidden scope checks, ordered bounded page output, and safe missing-event ProblemDetails.
- 2026-05-15T07:43:00+02:00 - ST1 red-green completed: Contracts stream DTO tests (3/3), Client stream route/ProblemDetails tests (2/2), and Testing stream fake/builder helpers (8/8) passed.
- 2026-05-15T07:22:36+02:00 - Dev workflow started; Aspire diagnostics passed for .NET/Docker with dev-certificate warnings, but `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` exited because the installed Aspire CLI requires `Aspire.Hosting.AppHost` >= 13.3.2 while the repo is pinned to 13.2.2.
- 2026-05-15T07:31:00+02:00 - ST0 baseline inventory completed across planning docs, Stories 22.1-22.5, public Contracts/Client/Testing surfaces, actor stream reads, admin stream debugging routes, projection orchestration/checkpointing, poller scheduling, and admin projection command facades.
- 2026-05-13T06:01:54Z - Pre-dev hardening preflight produced `_bmad-output/process-notes/predev-preflight-latest.json`; only failed check was working-tree cleanliness.
- 2026-05-13T06:01:54Z - Dirty BMAD paths from preflight were `_bmad-output/implementation-artifacts/22-1-gateway-command-query-contract-closure-and-package-docs.md` and `_bmad-output/implementation-artifacts/sprint-status.yaml`; story 22.1 was `review` in both story artifact and sprint status, so the run continued under the active-dev-story soft warning rule.
- 2026-05-13T08:04:55+02:00 - Story creation context gathered from Epic 22, PRD FR99-FR101, architecture ADR-P9, Stories 22.1-22.5, current stream/admin/replay/projection/checkpoint code, project context, recent commits, DAPR official docs, and lessons ledger.

### Completion Notes List

- Pass-5 continuation on 2026-05-17 closed the remaining current pass-5 open patches M1-5P, M11-5P, and L4-5P. Checkpoint-store tests now capture and assert persisted checkpoint rows around failure paths, the rebuild orchestrator interrupts if a per-aggregate progress row changes between page read and projection-state write, and multi-aggregate rebuild tests prove a mixed complete/incomplete page set does not stamp operator-scope success.
- Pass-5 continuation on 2026-05-17 resolved H4-H15, M2-M10, M12-M15, and L1-L3/L5-L12. It added `AggregateStreamMetadata` so the public stream read controller can distinguish missing streams from existing zero-event streams, tightened latest-sequence and toSequence semantics, hardened timeout/transport classification, and expanded regression coverage across stream/admin/checkpoint/client/testing surfaces.
- Review-continuation patch on 2026-05-16 resolved D3-C, D3-D, D3-F, P-C1/P-C2/P-C3/P-C4/P-C5/P-C7/P-C8/P-C9/P-C10/P-C11/P-C12/P-C13/P-C14/P-C15, H2/H3/H4/H6, M3/M7/M11/M14/M15, and L3/L4/L5/L7. Remaining unchecked review items intentionally stay open, especially active rebuild indexing, per-aggregate rebuild progress semantics, scoped tracked-identity enumeration, docs-only sync items, and D2 follow-through decisions.
- Added `operation-in-flight`, `stale-checkpoint`, and `forbidden-role` reason codes and mapped them through checkpoint/admin paths where implemented.
- Added `IAggregateActor.GetCurrentSequenceAsync` so stream pages report the true stream tip and missing streams can return stable `missing-stream` responses without raw state-key exposure.
- Hardened rebuild checkpoint persistence with case-insensitive ULID operation IDs via `UniqueIdHelper.ToGuid`, blank aggregate-id rejection, stale failed-write rejection, bounded transient retry, timeout misclassification fixes, and no silent concurrent OperationId overwrite.
- Hardened rebuild orchestration with page-completion-aware terminal success, caught cancellation → `Canceled` checkpoint write, pre-write lifecycle re-check, projection apply rejection failure recording, and a distinct rebuild delivery log event.
- Hardened public stream reads with mixed-case identity compatibility, tighter sequence-bound validation, missing-stream 404, narrower actor/service-unavailable classification, `Retry-After` on 503, and expanded no-leak assertions.
- Hardened client/testing stream helpers with a configurable 16 MiB default stream response cap, production-parity fake aggregate actor range validation, generated ULID message/correlation/causation IDs in `StreamReadPageBuilder`, and focused regression tests.
- Final review-continuation patch resolved P4/P-D2 full apply-driven rebuild advancement. Admin replay now invokes `IProjectionRebuildOrchestrator`; `ProjectionUpdateOrchestrator` advances `ProjectionRebuildCheckpoint` only after the domain `/project` apply path is accepted and projection state is written, while rejected and canceled rebuild paths do not advance progress. Per-apply saves stay `Running`; terminal `Succeeded` is written after the tracked rebuild scope completes and the operation is still runnable.
- Added bounded stream actor reads through `IAggregateActor.ReadEventsRangeAsync`, avoiding public replay controller full-suffix reads.
- Hardened stream validation/error taxonomy: missing required fields, invalid aggregate identity, upper-bound sequence validation, canonical tenant/domain enforcement, validator null guards, 503 vs 500 split, actor exception unwrapping, and broader no-leak ProblemDetails assertions.
- Added explicit checkpoint reset/rewind semantics, fresh ULID operation IDs for replay/reset, persisted `ToPosition` intent, nullable not-started operation `StartedAt`, and nullable empty-page `lastSequenceReturned`.
- Moved projection replay/reset request DTOs to Contracts and updated docs/fakes/tests for public contract parity.
- Completed focused and broader unit validation for the stream replay/rebuild surfaces. Integration/manual Aspire proof was not run because the installed Aspire CLI requires `Aspire.Hosting.AppHost` >= 13.3.2 while this repo is pinned to 13.2.2.
- Added public stream replay API documentation covering route/DTO shape, client usage, checkpoint semantics, operator lifecycle, stable reason codes, and explicit forbidden downstream paths.
- Added EventStore-side admin projection rebuild lifecycle endpoints for status, pause, resume, reset, replay, cancel, and retry, backed by rebuild checkpoints and stable `AdminOperationResult` / ProblemDetails responses.
- Updated `DaprProjectionCommandService` to preserve ProblemDetails `reasonCode` and detail when EventStore returns non-success lifecycle failures.
- Implemented the ST0 poller/rebuild reject policy: `ProjectionUpdateOrchestrator` skips normal projection delivery when an active operator rebuild checkpoint is present for the domain projection, returning without actor access and logging `poller-rebuild-conflict`.
- Added `ProjectionRebuildCheckpointStore` with validated rebuild checkpoint scopes, DAPR ETag saves, monotonic last-applied sequence semantics, idempotent duplicate/stale progress handling, conflict/unavailable result codes, and DI registration.
- Added `StreamsController` with public non-admin `POST /api/v1/streams/read` aggregate-scoped stream pages, gateway-owned tenant/RBAC validation before actor access, safe range/page-size/continuation validation, deterministic page metadata, and safe ProblemDetails reason codes.
- Added public stream read/replay DTOs under `Hexalith.EventStore.Contracts.Streams`, including stream pages/events/metadata, opaque continuation token, rebuild checkpoint/operation/status, and stable public reason codes.
- Added `IEventStoreGatewayClient.ReadStreamAsync` and `EventStoreGatewayClient` support for `POST /api/v1/streams/read` using existing gateway JSON and ProblemDetails mapping behavior.
- Added `StreamReadPageBuilder` and `FakeEventStoreGatewayClient` stream read helpers for success, empty stream, continuation, invalid range, unauthorized tenant, missing stream, checkpoint conflict, paused rebuild, canceled rebuild, and unavailable EventStore paths.
- ST0 decisions: public downstream stream reads use `POST /api/v1/streams/read` with `StreamReadRequest`, `StreamReadPage`, `StreamReadEvent`, `StreamReadMetadata`, and `ReplayContinuationToken`; existing `api/v1/admin/streams/*` routes remain admin/debug only.
- ST0 decisions: public projection rebuild progress uses `ProjectionRebuildOperation`, `ProjectionRebuildCheckpoint`, `ProjectionRebuildStatus`, and `StreamReplayReasonCodes`; operator/admin rebuild routes remain under `api/v1/admin/projections/{tenantId}/{projectionName}/...`.
- ST0 decisions: continuation tokens are opaque, request-bound, route/API-version-bound, tenant/domain/aggregate/projection-scope-bound, and fail closed on mismatch; token expiry is explicitly deferred and no expired-continuation reason is advertised.
- ST0 decisions: checkpoint ownership is tenant + domain + projection name + optional aggregate scope + operation ID; writes must use ETag/optimistic concurrency and monotonic max-sequence semantics.
- ST0 decisions: checkpoint advancement is projection-apply-driven. Reading a page never advances progress; progress may persist only after the domain projection apply path accepts the page.
- ST0 decisions: operator rebuild and background polling use a reject-on-active-rebuild policy for the first implementation; stable reason code `poller-rebuild-conflict` records conflicts rather than racing silently.
- Story created and marked ready-for-dev by the BMAD pre-dev hardening automation.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- Party-mode review completed on 2026-05-13 and applied story hardening for public API/admin boundaries, continuation-token invariants, checkpoint ownership/concurrency, operator lifecycle states, safe failure taxonomy, and explicit test evidence.
- Advanced elicitation completed on 2026-05-13 and applied story hardening for checkpoint advancement timing, continuation-token request binding, poller/rebuild coordination, failure taxonomy precision, and negative proof coverage.

### File List

- `_bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/reference/stream-replay-api.md`
- `src/Hexalith.EventStore.Admin.Server/Services/DaprProjectionCommandService.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Client/Gateway/EventStoreGatewayClientOptions.cs`
- `src/Hexalith.EventStore.Client/Gateway/IEventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj.lscache`
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionRebuildCheckpoint.cs`
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionRebuildOperation.cs`
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionRebuildStatus.cs`
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionReplayRequest.cs`
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionResetRequest.cs`
- `src/Hexalith.EventStore.Contracts/Streams/ReplayContinuationToken.cs`
- `src/Hexalith.EventStore.Contracts/Streams/StreamReadEvent.cs`
- `src/Hexalith.EventStore.Contracts/Streams/StreamReadMetadata.cs`
- `src/Hexalith.EventStore.Contracts/Streams/StreamReadPage.cs`
- `src/Hexalith.EventStore.Contracts/Streams/StreamReadRequest.cs`
- `src/Hexalith.EventStore.Contracts/Streams/StreamReplayReasonCodes.cs`
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
- `src/Hexalith.EventStore.Server/Actors/AggregateStreamMetadata.cs`
- `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs`
- `src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj.lscache`
- `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointStore.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointSaveResult.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointScope.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Testing/Builders/StreamReadPageBuilder.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs`
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventStoreGatewayClient.cs`
- `src/Hexalith.EventStore.Testing/Hexalith.EventStore.Testing.csproj.lscache`
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs`
- `src/Hexalith.EventStore/Controllers/StreamsController.cs`
- `src/Hexalith.EventStore/Hexalith.EventStore.csproj.lscache`
- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprProjectionCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientStreamTests.cs`
- `tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj.lscache`
- `tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj.lscache`
- `tests/Hexalith.EventStore.Contracts.Tests/Streams/StreamReadContractTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminProjectionRebuildControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Controllers/StreamsControllerTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj.lscache`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildCheckpointStoreTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventStoreGatewayClientTests.cs`
- Deleted `_bmad-output/implementation-artifacts/22-6-current-review.diff`
- Deleted `_bmad-output/implementation-artifacts/22-6-fresh-review.diff`
- Deleted `_bmad-output/implementation-artifacts/22-6-review-diff.patch`
- Deleted `_bmad-output/implementation-artifacts/22-6-review-pass-4.diff`

## Verification Status

- Pass-5 continuation final focused `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~ProjectionRebuildCheckpointStoreTests|FullyQualifiedName~ProjectionUpdateOrchestratorTests"` passed: 71/71.
- Pass-5 continuation `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json` succeeded and baseline resources were inspected with `aspire describe`; AppHost was stopped before tests to release build-output file locks.
- Pass-5 continuation focused `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~StreamsControllerTests|FullyQualifiedName~AdminProjectionRebuildControllerTests|FullyQualifiedName~ProjectionRebuildCheckpointStoreTests|FullyQualifiedName~ProjectionUpdateOrchestratorTests"` passed: 102/102.
- Pass-5 continuation `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore` passed: 333/333.
- Pass-5 continuation `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore` passed: 389/389.
- Pass-5 continuation `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-restore` passed: 112/112.
- Pass-5 continuation `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --no-restore` passed: 74/74.
- Pass-5 continuation `npx markdownlint-cli2 docs/reference/stream-replay-api.md` passed with 0 errors.
- Pass-5 continuation `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` failed on pre-existing `[Review][Decision]` / `[Review][Patch]` / `[Review][Defer]` label syntax in review-finding sections (MD052).
- Pass-5 continuation `git diff --check` passed with line-ending conversion warnings only.
- Story artifact created and sprint-status row moved from `backlog` to `ready-for-dev`.
- Preflight continued under the active-dev-story soft warning rule described in the Dev Agent Record.
- Story creation did not modify product code, tests, DAPR/Aspire configuration, generated API docs, or submodules.
- YAML validation passed for `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- `git diff --check` passed for the story artifact, sprint status, and run log with line-ending conversion warnings only.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` passed with 0 errors.
- Party-mode review completed on 2026-05-13 and is recorded below.
- Party-mode findings were applied only as story-text clarifications; product code, tests, DAPR/Aspire configuration, generated API docs, submodules, and sprint status were not changed.
- Advanced elicitation completed on 2026-05-13 and is recorded below.
- Advanced-elicitation findings were applied only as story-text clarifications; product code, tests, DAPR/Aspire configuration, generated API docs, submodules, and sprint status were not changed.
- `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore` passed: 331/331.
- Review-continuation rerun `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore` passed: 333/333.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore` passed: 388/388.
- Review-continuation rerun `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore` passed: 388/388.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-restore` passed: 109/109.
- Review-continuation rerun `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-restore` passed: 110/110.
- `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --no-restore` passed: 74/74.
- Review-continuation rerun `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --no-restore` passed: 74/74.
- Focused `Hexalith.EventStore.Server.Tests` stream/rebuild filter passed: 16/16.
- Review-continuation rerun focused `Hexalith.EventStore.Server.Tests` stream/rebuild/checkpoint/orchestrator filter passed: 69/69.
- Focused `Hexalith.EventStore.Admin.Server.Tests` DAPR failure mapping filter passed: 2/2, with existing MSB3277 package conflict warnings for `Microsoft.AspNetCore.OpenApi` and `YamlDotNet`.
- Review-continuation rerun focused `Hexalith.EventStore.Admin.Server.Tests` DAPR projection command service filter passed: 10/10.
- Final review-continuation rerun `dotnet test tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj --no-restore` passed: 333/333.
- Final review-continuation rerun `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --no-restore` passed: 388/388.
- Final review-continuation rerun `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --no-restore` passed: 110/110.
- Final review-continuation rerun `dotnet test tests/Hexalith.EventStore.Sample.Tests/Hexalith.EventStore.Sample.Tests.csproj --no-restore` passed: 74/74.
- Final review-continuation focused `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~ProjectionUpdateOrchestratorTests|FullyQualifiedName~AdminProjectionRebuildControllerTests"` passed: 52/52.
- Final review-continuation expanded Server stream/rebuild/checkpoint/orchestrator filter passed: 216/216, 4 pre-existing skips.
- Final review-continuation focused `Hexalith.EventStore.Admin.Server.Tests` DAPR projection command service filter passed: 10/10.
- Final review-continuation `npx markdownlint-cli2 docs/reference/stream-replay-api.md` passed with 0 errors.
- Final review-continuation story artifact markdownlint failed on pre-existing `[Review][Patch]` / `[Review][Defer]` label syntax in the review action-item section (MD052).
- Final review-continuation `git diff --check` passed with line-ending conversion warnings only.
- Extra broad `dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/Hexalith.EventStore.Admin.Server.Tests.csproj --no-restore` failed outside the story-focused coverage: 40 failed, 564 passed, 18 skipped, due missing `Microsoft.AspNetCore.OpenApi` 10.0.8 and `YamlDotNet` 17.0.0 assemblies after package conflict warnings.
- `npx markdownlint-cli2 docs/reference/stream-replay-api.md` passed with 0 errors.
- Review-continuation rerun `npx markdownlint-cli2 docs/reference/stream-replay-api.md` passed with 0 errors.
- Review-continuation combined story/docs markdownlint failed on pre-existing `[Review][Patch]` / `[Review][Defer]` label syntax in the story artifact (MD052). Docs-only markdownlint passed.
- `npx markdownlint-cli2 _bmad-output/implementation-artifacts/22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints.md` passed with 0 errors.
- `git diff --check` passed with line-ending conversion warnings only.
- Aspire integration/manual proof not run: `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj` was blocked because the installed Aspire CLI requires `Aspire.Hosting.AppHost` >= 13.3.2 while this repo is pinned to 13.2.2.
- Exact evidence tests added or extended: `StreamReadRequestJsonRoundTripUsesCamelCasePublicShape`, `StreamReadPageCarriesOrderedPageMetadataAndNoStateKeys`, `StreamReplayReasonCodesExposeStablePublicTaxonomy`, `ReadStreamAsyncPostsPublicStreamReadRouteAndReturnsPage`, `ReadStreamAsyncWithProblemDetailsThrowsGatewayExceptionWithReasonCode`, `ReadStreamAsyncWithInvalidRangeRejectsBeforeActorProxy`, `ReadStreamAsyncWithContinuationRejectsBeforeActorProxyUntilTokenSupportExists`, `ReadStreamAsyncWithDeniedTenantRejectsBeforeRbacAndActorProxy`, `ReadStreamAsyncWithDeniedReplayScopeRejectsBeforeActorProxy`, `ReadStreamAsyncReturnsOrderedBoundedAggregatePage`, `ReadStreamAsyncMapsMissingEventToSafeProblem`, `SaveAsyncExistingHigherCheckpointDoesNotRegressOrWrite`, `SaveAsyncDuplicatePageIsIdempotent`, `SaveAsyncRetriesEtagConflictsAndReturnsCheckpointConflictWhenExhausted`, `SaveAsyncStorageUnavailableReturnsStableReasonWithoutBlindSave`, `ReplayProjectionStartsRunningOperationAndReturnsAcceptedResult`, `ReplayProjectionInvokesRebuildOrchestratorAfterCheckpointStart`, `PauseProjectionIsIdempotentAndReturnsOkResult`, `ReplayProjectionCheckpointConflictReturnsProblemWithReasonCode`, `GetRebuildStatusWithoutCheckpointReturnsNotStartedOperation`, `DeliverProjectionAsync_WithActiveOperatorRebuild_SkipsNormalDeliveryBeforeActorProxy`, `RebuildProjectionAsync_AcceptedApplyAdvancesRebuildCheckpoint`, `RebuildProjectionAsync_ProjectRejectionDoesNotAdvanceRebuildCheckpoint`, `RebuildProjectionAsync_CanceledOperationDoesNotAdvanceRebuildCheckpoint`, `PauseProjectionAsync_MapsHttpStatusCode_WhenRequestFails`, `PauseProjectionAsync_WithProblemDetails_PreservesReasonCode`, `ReadStreamAsyncRecordsRequestAndReturnsConfiguredPage`, `ConfigureStreamReadFailureThrowsConfiguredGatewayException`, `StreamReadPageBuilderBuildsContinuationPage`, and `StreamReadFailureHelpersExposeStableReasonCodes`.
- Pass-5 final evidence added/extended: `SaveAsyncRejectsConcurrentActiveOperationInsteadOfOverwritingOperationId`, `SaveAsyncFailedWithLowerSequenceReturnsStaleCheckpoint`, `SaveAsyncActiveStatusFailsWhenActiveIndexCannotBePersisted`, `RebuildProjectionAsync_PerAggregateOperationChangeInterruptsBeforeProjectionWrite`, and `RebuildProjectionAsync_MixedPageCompletionDoesNotWriteSucceeded`.

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
| 2026-05-17 | 1.5 | Closed remaining current pass-5 follow-ups: checkpoint failure-path capture assertions, per-aggregate pre-write drift guard, and mixed multi-aggregate page-completion regression coverage. Older deferred/decision items remain open. | GPT-5 Codex |
| 2026-05-17 | 1.4 | Pass-5 continuation resolved H4-H15 plus focused M/L fixes; documented the semantic-release version-impact issue from mistyped commit `b802f4d5`; remaining open pass-5 items are M1-5P, M11-5P, and L4-5P plus older deferred/decision items. | GPT-5 Codex |
| 2026-05-15 | 1.3 | Completed P4/P-D2 apply-driven rebuild advancement: admin replay now invokes a rebuild orchestrator, accepted projection applies advance `ProjectionRebuildCheckpoint`, rejection/canceled paths do not advance, and final focused validation passed. | GPT-5 Codex |
| 2026-05-15 | 1.2 | Review-continuation patch set resolved 31 of 32 remaining review findings: range-aware actor reads, safer stream validation/taxonomy, explicit reset/replay rewind semantics, fresh operation IDs, persisted `ToPosition`, nullable empty-page/start metadata, bounded Admin.Server ProblemDetails parsing, docs/fake parity, and focused validation. P4/P-D2 full apply-driven rebuild advancement remains open. | GPT-5 Codex |
| 2026-05-15 | 1.1 | Code review (Opus 4.7) applied 7 CRITICAL/HIGH patches (P-D1 GlobalAdministrator gate, P2 SaveAsync lifecycle fix, P5 read-first 404, P-D3 continuation tokens off, P6 poller fail-closed, P20 ArgumentException → 400, P26 FailureReasonCode null for pause/cancel). 32 remaining patches (including design-level P3/P-D2/P-D5/P-D6/P-D7) listed in Review Findings as action items. Story moved back to in-progress. Focused Server tests 70/70 pass. | Claude Opus 4.7 |
| 2026-05-15 | 1.0 | Implemented public stream read contracts/client/testing support, EventStore stream read endpoint, projection rebuild checkpoint store, operator rebuild lifecycle endpoints, poller/rebuild conflict policy, docs, and validation evidence; moved story to review. | GPT-5 Codex |
| 2026-05-15 | 0.4 | Started dev workflow and moved story to in-progress. | GPT-5 Codex |
| 2026-05-13 | 0.3 | Applied advanced elicitation hardening for checkpoint advancement timing, continuation-token binding, poller/rebuild coordination, failure taxonomy precision, and negative proof coverage. | Codex automation |
| 2026-05-13 | 0.2 | Applied party-mode review hardening for replay API boundaries, continuation tokens, checkpoint concurrency, operator lifecycle, failure taxonomy, and test evidence. | Codex automation |
| 2026-05-13 | 0.1 | Created ready-for-dev story for stream replay/read APIs and projection rebuild checkpoints. | Codex automation |

## Party-Mode Review

- Date/time: 2026-05-13T10:07:35+02:00
- Selected story key: `22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints`
- Command/skill invocation used:
  `/bmad-party-mode 22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints; review;`
- Participating BMAD agents: John (Product Manager), Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor)
- Findings summary:
    - All reviewers recommended `needs-story-update` before development because the story implied too much public replay, operator lifecycle, continuation-token, and checkpoint behavior without enough pre-dev contract detail.
    - Product review emphasized public API non-goals, replay ordering/page-size/continuation guarantees, observable operator states, tenant isolation before state access, and evidence using only Contracts/Client/Testing APIs.
    - Architecture review emphasized opaque tenant/scope-bound continuation tokens, checkpoint ownership and optimistic concurrency, public stream APIs versus operator/admin rebuild APIs, and stable failure categories.
    - Engineering review emphasized code ownership around Contracts, Client, Testing, adapters over `IAggregateActor`, `EventStreamReader`, and `ProjectionCheckpointTracker`, plus no Server/admin/state-store shortcuts.
    - Test architecture review emphasized named tests for auth-before-state, invalid continuation/range, no-leak behavior, checkpoint races, lifecycle idempotency, and client/fake parity.
- Changes applied:
    - Added `Public API and Lifecycle Guardrails` covering package ownership, public versus admin boundaries, candidate DTO names, continuation-token invariants, auth-before-state sequencing, checkpoint scope/concurrency, monotonic advancement, and operator lifecycle states.
    - Tightened AC1-AC5, AC7, and AC8 with selected route/DTO decisions, no state access before authorization, continuation failure categories, checkpoint race coverage, lifecycle scope honesty, and explicit evidence expectations.
    - Expanded ST0-ST7 with route/boundary decisions, continuation token rules, no-state-access proof, checkpoint race tests, lifecycle transition recording, docs "do not use" guidance, no-leak tests, client/fake parity tests, and exact evidence recording.
    - Added `Test Evidence Required` to map Contracts, Client, Testing, Server/controller, checkpoint/rebuild, no-leak, and integration/manual proof obligations.
    - Updated Developer Notes and implementation traps to forbid continuation-driven actor/checkpoint access before authorization and to require explicit operator lifecycle implementation or deferred safe behavior.
- Findings deferred:
    - Payload/snapshot protection and unreadable protected data policy remain deferred to Stories 22.7a through 22.7d.
    - Exact route names, version prefix, and final DTO names remain implementation decisions to record during ST0 before production code edits.
    - Exact continuation-token protection implementation remains implementation-owned, provided the public contract is opaque, tenant/scope-bound, tamper-safe or fail-closed, and non-key-bearing.
    - Operator UI/dashboard shape, projection-specific rebuild algorithms, performance tuning, and storage layout remain deferred unless needed to satisfy the public/operator lifecycle contract.
- Final recommendation: ready-for-dev after applied story updates.

## Advanced Elicitation

- Date/time: 2026-05-13T17:04:29+02:00
- Selected story key: `22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints`
- Command/skill invocation used:
  `/bmad-advanced-elicitation 22-6-stream-replay-read-apis-and-projection-rebuild-checkpoints`
- Batch 1 methods:
    - Self-Consistency Validation
    - Red Team vs Blue Team
    - Architecture Decision Records
    - Failure Mode Analysis
    - Comparative Analysis Matrix
- Reshuffled Batch 2 methods:
    - Chaos Monkey Scenarios
    - First Principles Analysis
    - Occam's Razor Application
    - 5 Whys Deep Dive
    - Lessons Learned Extraction
- Findings summary:
    - The story had strong public API and checkpoint guardrails, but it still allowed an implementer to advance checkpoints too early by treating page read, page delivery attempt, or operator command acceptance as successful rebuild progress.
    - Continuation-token rules were opaque and tenant-bound, but did not explicitly require binding to the full request shape and API version, which could allow false continuation reuse after query-shape changes.
    - Operator rebuild and normal projection polling both touched projection checkpoint concepts, but the required coordination rule was not concrete enough to prevent a silent race.
    - Failure taxonomy included expired continuation and protected payload cases without enough precision around deferred token expiry, token/request mismatch, projection apply rejection, poller/rebuild conflict, and checkpoint unavailable behavior.
    - Test evidence needed a negative advancement lane proving that corrupt, partial, cancelled, timed-out, rejected, protected-unreadable, and checkpoint-store-unavailable paths do not move progress past the last safely applied sequence.
- Changes applied:
    - Clarified that reading a replay page never implies checkpoint advancement and that progress advances only after the projection apply path accepts the page.
    - Added continuation-token request binding requirements for tenant, domain, aggregate, projection/rebuild scope, route/API version, page-size constraints, and sequence cursor.
    - Tightened AC3, AC4, AC5, AC7, ST0-ST4, ST6, test evidence, and implementation traps around token/request mismatch, negative checkpoint advancement, and poller/rebuild coordination.
    - Refined failure taxonomy guidance so `expired continuation` is only promised when expiry exists, and added projection apply rejection, checkpoint unavailable, and poller/rebuild conflict categories.
- Findings deferred:
    - Exact route names, version prefix, DTO names, continuation-token cryptographic mechanism, token expiry support, and poller/rebuild coordination mechanism remain ST0 implementation decisions.
    - Payload-protection semantics beyond safe placeholder/failure handling remain deferred to Stories 22.7a through 22.7d.
    - Operator UI/dashboard shape, projection-specific rebuild algorithms, performance tuning, and storage layout remain deferred unless needed to satisfy the public/operator lifecycle contract.
- Final recommendation: ready-for-dev after applied story updates.
