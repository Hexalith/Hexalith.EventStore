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

_Code review run on 2026-05-15 (Opus 4.7) using `/bmad-code-review 22-6` against diff `beed5a8e..HEAD` (39 files, +2543/−97). Three adversarial layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor._

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

## Verification Status

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

## Change Log

| Date | Version | Description | Author |
| --- | ---: | --- | --- |
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
