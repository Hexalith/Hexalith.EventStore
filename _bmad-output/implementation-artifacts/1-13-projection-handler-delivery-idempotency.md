---
baseline_commit: 5223e9c9c2f0dd71673003c710b8739efc8484ff
---

# Story 1.13: Projection Handler Delivery Idempotency

Status: ready-for-dev

**Requirements covered:** FR7, FR36, NFR6, NFR7, NFR16  
**Governed by:** AD-7, AD-8, AD-10, AD-12, AD-19, AD-20  
**Builds on:** Stories 1.9-1.12. Story 1.12's final named-dispatch and durable-retry contracts are a direct prerequisite; Story 1.10's live DAPR/Redis batch-evidence gate must pass before production wiring.  
**Feeds:** Stories 1.14-1.15 and the baseline that Stories 6.3/6.4 must preserve

## Story

As an operator,
I want projection delivery to be duplicate-safe and order-safe through the real handler path,
so that at-least-once unordered delivery cannot corrupt detail or index state.

## Acceptance Criteria

1. **Persisted, projection-scoped delivery identity**  
   **Given** a projection delivery is admitted  
   **When** its identity is persisted  
   **Then** idempotency and checkpoint state are scoped by tenant, domain, aggregate, and projection type  
   **And** duplicate identity uses the EventStore `MessageId`  
   **And** `SequenceNumber` is interpreted only within that aggregate stream, never as a global order.

2. **Completed and in-progress duplicates are safe**  
   **Given** the same `MessageId`, sequence, and content is delivered again  
   **When** the matching delivery is already completed  
   **Then** it is an idempotent no-op  
   **And given** the matching delivery is still in progress  
   **When** its duplicate arrives  
   **Then** it is deferred/retryable  
   **And** the logical detail/index batch is not applied twice.

3. **Lower, gap, and conflicting deliveries fail safely**  
   **Given** a lower sequence is already proven within retained history  
   **When** its exact identity and content are delivered again  
   **Then** it is ignored safely  
   **And given** a future sequence leaves a gap  
   **When** admission is evaluated  
   **Then** it remains retryable and no checkpoint advances  
   **And given** a sequence, `MessageId`, or content conflicts with durable evidence  
   **When** admission is evaluated  
   **Then** it fails safely with no handler, read-model, batch-marker, delivery-state, or checkpoint mutation.

4. **Durable writes, marker, delivery completion, and checkpoint converge**  
   **Given** a handler requires coordinated detail/index writes  
   **When** delivery completes  
   **Then** its projection checkpoint advances only after the required durable writes and Story 1.10 completion receipt are proven  
   **And** delivery completion and the projection-scoped sequence checkpoint are committed as one delivery-state transition  
   **And** a crash, cancellation, or ambiguous response between phases retries the same stable identity and converges to the state produced by one successful delivery.

5. **Deduplication history is bounded and fail-closed**  
   **Given** completed identity receipts exceed the configured retained-history limit  
   **When** compaction runs  
   **Then** recent receipts remain bounded and a durable retention floor and prefix fingerprint remain  
   **And given** an old delivery cannot be proven from retained evidence  
   **When** it is delivered  
   **Then** it is not applied silently and an explicit rebuild/reconciliation-required path is recorded  
   **And** retention, migration, compaction, and recovery behavior are documented and tested.

6. **Production-path persisted-state proof**  
   **Given** detail and index handlers use the production asynchronous named-dispatch path  
   **When** in-order, exact duplicate, reverse-trigger order, gap-then-missing, partial-failure duplicate, and sequence/identity/content-conflict scenarios enter production orchestration  
   **Then** every admitted leg runs through DAPR `/project/v2`, async dispatch, the real handlers, and the durable store, while completed duplicates, active duplicates, gaps, and conflicts prove that no v2/handler call occurs  
   **And** persisted detail, index, Story 1.10 batch receipt/marker, delivery state, lifecycle state, retry state, and projection checkpoint are compared with the single in-order baseline  
   **And** HTTP status, mock call counts, or aggregate replay alone are not accepted as proof  
   **And** the later Story 6.3/6.4 tail-delivery optimizations must preserve this baseline.

## Resolved Implementation Contract

These decisions are part of the story. If the final Story 1.12 code contradicts one directly, stop and return the story for architecture correction rather than weakening either contract.

### 1. One versioned delivery-state owner

- Evolve the existing projection-scoped checkpoint row (`projection-checkpoints:{ActorId}:{projectionName}`) into a versioned delivery-state record. The row remains scoped by `(tenant, domain, aggregate, projection type)` and remains the source read by the internal projection checkpoint seam. Preserve the existing `TenantId`, `Domain`, `AggregateId`, `LastDeliveredSequence`, and `UpdatedAt` JSON property names so old rows can be read and migrated deliberately.
- Keep `IProjectionCheckpointTracker`, `ProjectionCheckpoint`, the legacy aggregate-wide key, and the lazy migration marker source/binary/JSON compatible. Do not add released-interface members. Refactor the scoped implementation behind a new internal state-store seam instead of growing `ProjectionCheckpointTracker` further.
- The versioned row contains at least: schema version; `WriterProtocolVersion = 2`; identity scope; last contiguous completed sequence; last completed `MessageId`; completed-prefix fingerprint; bounded recent completed receipts; first retained sequence; optional active reservation; migration provenance; and `UpdatedAt`.
- One active reservation contains the admitted suffix range, head `MessageId`, dispatch ID, manifest fingerprint, fencing token, admitted/expiry times, and attempt. It is written before invoking `/project/v2`.
- DAPR ETag/first-write concurrency is authoritative across replicas. The existing process-local keyed semaphore may reduce contention but is not correctness evidence. Exhausted conflicts return a retryable state-unavailable result; they never fall back to last-write-wins.
- Completing a reservation and advancing `LastDeliveredSequence` happen in one conditional write to this row. The old `SaveDeliveredSequenceAsync` path must not bypass the delivery-state transition in named production delivery.
- This in-place upgrade does **not** support mixed Story 1.12/1.13 writers. Require a maintenance cutover: stop and verify quiescence of every old server and retry worker, back up delivery state, deploy/migrate all writers, then restore readiness. Persist `projection-delivery-writer-protocol` (schema version, `WriterProtocolVersion = 2`, cutover commit, activation time) only after quiescence; new readiness requires version 2, while old binaries never know or write that key. A rolling downgrade is forbidden. Once the marker is v2, a missing/older row writer version or five-field overwrite is classified as migration/schema regression and fails closed to reconciliation; never silently recreate receipts from the downgraded sequence. Add upgrade/downgrade-write tests and an operator runbook. JSON readability by an old binary is not write compatibility.
- Preserve the existing projection lifecycle gate and erasure ordering. Story 1.9 accepted a point-in-time erase/write TOCTOU residual; do not claim this story closes that separate residual unless the implementation explicitly adds and proves a persistent lifecycle fence. If any **per-scope** companion key is unavoidable, add it to the erasure manifest and delete/verify it before the delivery checkpoint row. The store-global `projection-delivery-writer-protocol` marker is control-plane state: never include or delete it during tenant/aggregate/projection erasure; manage it only through the maintenance cutover, backup, and recovery runbook.

### 2. Frozen fingerprint and ordering rules

- Reject a named production delivery if any event lacks a non-blank persisted `MessageId`; never synthesize identity from sequence or parse it as `Guid`.
- Require positive, strictly increasing, contiguous per-aggregate sequence numbers in `ProjectionRequest.Events`. Do not sort malformed wire input into apparent validity.
- Add a v1, golden-vector-tested fingerprint over explicit length-prefixed binary fields, not ambient JSON serialization. Encode strings as UTF-8 with signed 32-bit little-endian byte lengths (`-1` null, `0` empty), payloads with a non-negative 32-bit length, integers as fixed-width little-endian, and timestamps as UTC ticks; reject overflow and invalid values.
- Freeze a domain-separated chain: `H0 = SHA256("hexalith.projection.delivery.prefix.v1" || canonical scope+projection)`; `E(n) = SHA256("hexalith.projection.delivery.event.v1" || canonical sequence, MessageId, event type, payload, serialization format, UTC ticks, correlation ID, user ID)`; `Hn = SHA256("hexalith.projection.delivery.step.v1" || H(n-1) || E(n))`. Store digests as `v1:` base64url strings. Golden vectors must pin `H0`, individual `E(n)`, and multi-event `Hn` values.
- Persist digests only. Never persist or log raw payloads in delivery state, and never log digests, state keys, ETags, exception messages, or batch envelopes.
- Maintain the chained prefix fingerprint through `LastDeliveredSequence`. Before admitting a new suffix, normal full-history delivery recomputes the chain through `LastDeliveredSequence` and compares every retained-overlap event. For an old head still inside the retained window, compare its exact receipt and every retained overlap present in that request; a head below the window goes to explicit full-chain reconciliation. Stories 6.3/6.4 may optimize this cost only after reproducing the same chain and overlap proof.
- “Reverse order” in AC6 means triggers for later and earlier observed heads arrive in reverse order while EventStore still supplies a canonical stream. A reversed event array is malformed input and must not be silently reordered.

### 3. Admission and outcome matrix

| Durable observation | Action before handler mutation | v2 outcome |
|---|---|---|
| Exact retained completed identity and every retained-overlap fingerprint present in the request | Skip `/project/v2`; the authoritative row already contains its checkpoint | `AlreadyCompleted` / `delivery_already_completed` |
| Exact active identity inside its lease | Coalesce with existing retry work; do not invoke concurrently | `Retryable` / `delivery_in_progress` |
| Active reservation with a different future head | Recover the active identity first; do not leapfrog it | `Retryable` / `delivery_in_progress` |
| New contiguous suffix with matching completed prefix | Persist a fenced reservation, then invoke only the admitted projection route | Continue to handler |
| Missing next sequence or non-contiguous suffix | Do not reserve, invoke, or checkpoint | `Retryable` / `delivery_gap` |
| Retained sequence with different `MessageId` or fingerprint; same `MessageId` with different sequence/content | Leave durable state unchanged | `Failed` / `delivery_identity_conflict` |
| Head is at/below the retention floor and exact completion cannot be proven | Record support-safe reconciliation work outside the delivery row; do not invoke | `Failed` / `delivery_reconciliation_required` |
| A v2 row was overwritten by an older/five-field writer | Stop delivery and preserve the regressed row for diagnosis | `Failed` / `delivery_schema_regression` |
| Delivery state cannot be read or conditionally saved | Do not invoke | `Retryable` / `delivery_state_unavailable` |

- Add the bounded reason-code constants to the existing additive `ProjectionDispatchReasonCodes` contract; do not add a v2 status, field, or optional member.
- Partition Story 1.12 fan-out by projection route before dispatch: completed routes synthesize `AlreadyCompleted`, unsafe routes synthesize their bounded outcome, and only successfully reserved routes are sent to `/project/v2`. Sibling routes remain independently durable.
- Keep the v2 envelope and numeric status values frozen. `DispatchId` remains the highest-sequence persisted event's `MessageId` and is passed unchanged as `ReadModelBatchScope.BatchId` on every retry.
- The legacy `POST /project` path remains wire compatible. It cannot pre-admit by projection type and is not accepted as Story 1.13 production-path evidence; do not pretend post-response sequence tracking makes it duplicate-safe.

### 4. Completion, uncertainty, and lease recovery

- Only `Completed` and `AlreadyCompleted` from the real named handler, after its required Story 1.10 batch is durable, may complete the reservation. Any server-owned compatibility actor write and ETag regeneration must also succeed first.
- After handler success, conditionally replace the reservation with completed receipts, compact the recent window, update the prefix fingerprint, and advance the sequence in one write. A stale fencing token cannot complete or clear a newer reservation.
- A retry after “batch committed, response/ledger transition lost” reuses the same dispatch/batch identity. The handler/batch returns `AlreadyCompleted`; the coordinator then completes the delivery row without applying detail/index again.
- `Retryable` retains/coalesces the reservation and schedules the same identity through Story 1.12 retry work; it may represent a resumable partial batch. `Indeterminate`, malformed post-dispatch outcome, transport ambiguity, timeout, or cancellation after dispatch does the same and must not fabricate success or clear evidence using the canceled caller token. Cancellation before invocation may release the untouched reservation conditionally.
- A deterministic handler `Failed` result may clear only its matching reservation with the fencing token and record the bounded terminal outcome in Story 1.12 work evidence; it never advances delivery state. A handler that may have performed partial or uncertain durable work must return `Retryable` or `Indeterminate`, not `Failed`.
- Use `TimeProvider`. Define `ProjectionDeliveryIdempotencyOptions` with `CompletedReceiptLimit = 256` (valid 1-4096), `ReservationLease = 5 minutes` (valid 30 seconds-24 hours), and `MaxStateTransitionAttempts = 8` (valid 1-32). Reuse the configured projection state-store name. Lease expiry is not permission for a different identity to leapfrog. Story 1.12's durable retry worker reclaims the same identity with a higher fencing token and the same dispatch ID; an old attempt cannot finalize the ledger.
- The fencing token is not present in frozen v2 and cannot stop a still-running old handler. Reclaim is allowed only for a route whose unchanged dispatch ID and Story 1.10 batch/equivalent durable idempotency prove that a late old attempt cannot reapply writes. Otherwise remain reconciliation-required. Test the race where the expired old attempt resumes after the reclaimed attempt completes.
- Do not build a second retry scheduler. Extend the final Story 1.12 work item/store so active reservation identity, route, observed head, dispatch ID, catalog fingerprint, attempts, and due time remain restart-safe.

### 5. Retention, migration, and reconciliation

- Retain the configured latest completed event receipts. Compaction is count-based and deterministic; do not use state-store TTL as the correctness boundary. Persist the first retained sequence and the cumulative prefix fingerprint.
- A full-history request with a new contiguous suffix may include events below the receipt floor: verify its cumulative completed-prefix fingerprint and retained overlap, then admit only the new suffix. A standalone/old head below the floor whose exact completion cannot be proven routes to reconciliation.
- Lazy migration from a non-zero sequence-only checkpoint cannot silently invent `MessageId` or content evidence. Mark that scope reconciliation-required. Provide a maintenance-only `ReconcileFromEventStore` operation that reuses existing admin authentication **and tenant/scope authorization**, fails closed on missing/wrong scope, records attributable operator identity and bounded audit evidence, and, while old writers are quiesced, reloads the authoritative contiguous EventStore prefix through the persisted checkpoint. It validates every non-blank identity, computes the frozen chain/recent window, preserves (never advances) the old sequence, and records `HydratedFromPersistedCheckpoint` provenance. It does not invoke a handler or mutate detail/index/batch state. A zero/absent checkpoint may initialize normally. A checkpoint ahead of EventStore or an unreadable/gapped prefix remains failed closed.
- Use the same explicit reconciliation operation for a below-floor old delivery: recompute the authoritative chain through the current completed head and compare it with the stored chain before rehydrating receipts or confirming the no-op. A mismatch records rebuild-required; it never applies the old event. This gives Story 1.13 an executable recovery path without depending on named-handler rebuild support from Story 1.14.
- Persist reconciliation-required work/status with scope, bounded reason, observed sequence, and delivery-state version; do not copy payloads. A destructive rebuild still uses the existing authorized lifecycle/erasure path, but Story 1.14 owns paged staging/promotion and must not be implemented here.
- Story 1.10 terminal batch receipts remain indefinite in this story. Do not add raw TTL deletion to its v1 markers/receipts or delete `Prepared`/`Aborting` evidence. Bounded delivery receipts satisfy this story's deduplication horizon; any future batch-receipt cleanup requires an additive/versioned index and proof that the delivery floor/rebuild epoch makes deletion safe.
- For an abandoned Story 1.10 marker/envelope, retry the same stable batch identity and reconcile its recorded operations. A different batch remains blocked and produces explicit reconciliation evidence; never delete a foreign active envelope merely because wall-clock time elapsed.

### 6. Support-safe observability and evidence truth

- Emit counters/traces for admitted, completed, already-completed, in-progress, gap, identity-conflict, reconciliation-required, lease-reclaimed, and state-unavailable outcomes, tagged only with bounded route/status/reason metadata.
- Keep tenant isolation and authorization fail-closed. State scope/key composition must reuse `AggregateIdentity` and `ProjectionKeySegments` validation; do not hand-roll escaping or cross-tenant lookup behavior. Reconciliation is an attributable admin mutation: authorize the exact tenant/scope before any read or write and emit bounded success/denial audit evidence without payloads or secrets.
- Readiness/parity claims require persisted detail, index, receipt/marker, delivery row, lifecycle state, retry work, and checkpoint evidence. A `200`, `202`, log line, in-memory marker, mocked handler, or aggregate replay count is insufficient.
- Record the exact EventStore commit, DAPR/runtime versions, component configuration, commands, and persisted-state assertions used for Tier-3 proof. Parties remains blocked until Story 1.15 owner approval and exact-SHA pinning; do not modify any consuming submodule.

## Tasks / Subtasks

- [ ] **Task 0: Satisfy direct-contract and batch-production gates** (AC: 4, 6)
  - [ ] Re-read the final landed Story 1.9 and Story 1.12 artifacts/code. Confirm the scoped checkpoint/lifecycle seams, v2 envelope, named coordinator, per-route retry item, dispatch identity, and outcome mapping; stop on a direct contradiction.
  - [ ] Before wiring Story 1.10 batching into named production dispatch, make `ReadModelBatchLiveSidecarTests` pass against real DAPR/Redis and add the deferred partial-prefix old-view, conflict/abort restoration, and post-dispatch cancellation/reconciliation scenarios.
  - [ ] Capture the exact commands, runtime/component versions, fixture health, and persisted proof. The earlier fixture exit 144 is not a pass.
  - [ ] Define and rehearse the no-mixed-writer maintenance cutover, store-level v2 protocol marker, quiescence/readiness checks, state backup, rollback boundary, and downgrade prohibition before migrating an existing environment.

- [ ] **Task 1: Implement the versioned delivery-state contract and migration** (AC: 1, 2, 3, 5)
  - [ ] Add one-type-per-file state, receipt, reservation, admission/result, options, validator, and internal state-store abstractions under `Hexalith.EventStore.Server/Projections` and configuration.
  - [ ] Refactor the projection-scoped part of `ProjectionCheckpointTracker` to read/write the compatible versioned row without changing released `IProjectionCheckpointTracker` behavior or erasing extra delivery fields.
  - [ ] Implement ETag/first-write transitions, bounded retry, zero-state creation, sequence-only migration classification, writer-version regression detection, read-back classification, and deterministic compaction using `TimeProvider`.
  - [ ] Add the authenticated, tenant/scope-authorized maintenance-only EventStore hydration/reconciliation seam, operator attribution, audit evidence, and provenance; prove it preserves the sequence and never invokes handlers or mutates read models/batches.
  - [ ] Keep the current key when feasible. If a companion is required, update erasure manifest/resume/read-back tests before using it.

- [ ] **Task 2: Freeze and test canonical delivery fingerprints** (AC: 1, 3, 5)
  - [ ] Implement the exact v1 domain-separated event/prefix chain and byte encoding; reject missing identity and malformed/non-contiguous history before state mutation.
  - [ ] Add golden vectors covering `H0`, `E(n)`, multi-event `Hn`, null/empty optional fields, binary payloads, culture/time-zone invariance, scope/projection separation, every field change, retained-overlap changes, and ordered prefix extension.
  - [ ] Prove no payload/digest/key/ETag leaks through serialization diagnostics, logs, traces, or exception text.

- [ ] **Task 3: Add the delivery idempotency coordinator** (AC: 1-5)
  - [ ] Implement the full admission matrix, conditional claim/complete/release/reclaim transitions, fencing-token checks, checkpoint convergence, and support-safe reason mapping outside the large orchestrator class.
  - [ ] Extend Story 1.12's durable retry state and worker for same-identity reservation recovery; do not create another queue or dispatch ID.
  - [ ] Route out-of-retention and sequence-only migration states to durable authorized reconciliation without invoking a handler.

- [ ] **Task 4: Integrate per-route admission with final Story 1.12 dispatch** (AC: 2, 3, 4)
  - [ ] Update the focused named-dispatch/orchestration collaborator to partition routes, reserve before `/project/v2`, preserve deterministic route order, and synthesize truthful outcomes for skipped routes.
  - [ ] Complete only after `Completed`/`AlreadyCompleted` durable proof and any compatibility actor write/ETag; leave or release reservations according to the uncertainty rules.
  - [ ] Preserve legacy `/project` compatibility, lifecycle admission, catalog drift handling, per-projection checkpoint independence, and immediate/polling/retry ownership.

- [ ] **Task 5: Prove state-machine behavior deterministically** (AC: 1-5)
  - [ ] Cover concurrent cross-replica-style CAS claims; completed and active duplicates; retained lower delivery; gap then missing; reverse trigger order; same sequence/different ID; same ID/different sequence or content; malformed order; and tenant/projection isolation.
  - [ ] Inject failure/cancellation before handler, after batch receipt, before delivery completion, during completion CAS, and after completion before retry cleanup. Every retry must converge or remain explicitly retryable/indeterminate without duplicate writes.
  - [ ] Cover lease expiry/fenced reclaim, a late expired handler resuming after the new attempt, routes without reclaim-safe idempotency, exhausted ETag conflicts, 256/4096 compaction boundaries, below-floor reconciliation, zero and non-zero legacy hydration, cross-tenant/wrong-scope reconciliation denial, audit attribution, prefix/overlap mismatch, checkpoint drift, writer downgrade, global-marker survival across per-scope erasure, and erased aggregate recreation at sequence 1.
  - [ ] Keep in-memory/fake externally observable behavior identical to DAPR behavior; do not repeat the generic event-marker fake/production asymmetry.

- [ ] **Task 6: Produce real production-path parity evidence** (AC: 6)
  - [ ] Add a live-sidecar fixture that drives orchestration through DAPR `/project/v2`, final named dispatch, real detail/index handlers, `IReadModelBatchStore`, Redis/state store, delivery state, lifecycle state, retry state, and checkpoint.
  - [ ] Run the six AC6 scenarios, including concurrent invocations from separate orchestrator instances. Inspect persisted values after quiescence and compare them field-for-field with one in-order application.
  - [ ] Assert per-projection sibling isolation and batch identity reuse. Include active-reservation and partial-batch recovery, not only completed duplicate replay; assert completed/active duplicates, gaps, conflicts, and schema regression never invoke `/project/v2` or a handler.
  - [ ] Save bounded evidence with exact repository SHA and runtime/component versions for Story 1.15; do not promote mock-only or skipped evidence.

- [ ] **Task 7: Document and guard the contract** (AC: 1-6)
  - [ ] Update projection/replay and event-envelope documentation: at-least-once duplicate behavior, per-aggregate sequence semantics, EventStore `MessageId` identity, v2 guarantee boundary, defaults, migration, compaction, leases, and operator reconciliation.
  - [ ] Correct stale guidance that derives CloudEvent identity from correlation/sequence; do not change the already-frozen persisted EventStore `MessageId` contract.
  - [ ] Add registration/options validation, public API compatibility, serialization shape, reason-code bound, architecture-boundary, docs-link, and release-package guard tests.
  - [ ] Restore/build `Hexalith.EventStore.slnx`, then run every affected test `.csproj` individually (including Contracts, Client, DomainService, Server, and Server.LiveSidecar), formatting/analyzers, `git diff --check`, and the required Tier-3 lane. Never run solution-level `dotnet test` and do not use `.sln` files.

## Dev Notes

### Current implementation reality

- `ProjectionUpdateOrchestrator` currently reloads from sequence zero, uses a same-process keyed semaphore, calls legacy `/project`, learns the projection type after the response, writes state, then saves only a monotonic sequence. Story 1.12 is actively replacing this route with named async v2 dispatch. Integrate with the final focused collaborator; do not add another state machine to the already large orchestrator.
- `IProjectionDeliveryCheckpointStore` is internal and already scopes the row by aggregate identity plus projection name. `ProjectionCheckpointTracker` implements lazy migration and ETag retries but stores no message identity, fingerprint, active claim, or retained history.
- `IReadModelBatchStore` already owns coordinated detail/index operations, stable fingerprints, `Completed`/`AlreadyCompleted`, conflicts, resumable reconciliation, and receipt-last read-back. Reuse it. Story 1.13 owns delivery admission/checkpoint truth, not a replacement batch protocol.
- `DaprEventStoreDomainEventMarkerStore` is a different generic consumer seam and its production implementation does not persist a true in-progress claim. It is precedent only, not closure evidence for projection handlers.
- The worktree contained active Story 1.12 edits while this story was authored. Baseline commit is the committed repository snapshot above, not the uncommitted v2 implementation. Re-read and consume the landed 1.12 contracts rather than copying this story's file-name guesses.

### Project structure and likely touchpoints

- Existing Server files likely updated: `IProjectionDeliveryCheckpointStore.cs`, `ProjectionCheckpointTracker.cs`, `ProjectionUpdateOrchestrator.cs` (delegation only), final Story 1.12 coordinator/retry files, `ProjectionReasonCodes.cs`, `ProjectionEraseCoordinator.cs`, and configuration/registration files.
- New Server files likely belong under `src/Hexalith.EventStore.Server/Projections/`: `IProjectionDeliveryStateStore`, `ProjectionDeliveryState`, `ProjectionDeliveryReceipt`, `ProjectionDeliveryReservation`, admission/result types, fingerprint helper, and `ProjectionDeliveryIdempotencyCoordinator`. Put each C# type in its own file.
- Add options/validation under `src/Hexalith.EventStore.Server/Configuration/`. Extend the existing public dispatch reason-code class additively; do not alter the v2 envelope.
- Unit tests belong in the matching Server/Contracts test projects. Real DAPR/Redis evidence belongs in `tests/Hexalith.EventStore.Server.LiveSidecar.Tests`; extend shared fixtures rather than spawning ad hoc sidecars.
- Do not edit Tenants, Parties, or other reference submodules. Do not implement Story 1.14 paging/staging or Story 6.3/6.4 tail reads.

### Engineering constraints

- Repository baseline: .NET SDK 10.0.301, `net10.0`, C# 14, DAPR .NET packages 1.18.4, and xUnit v3; use centrally managed package versions and add no dependency for hashing/state transitions.
- Follow Allman braces, file-scoped namespaces, nullable annotations, XML docs, source-generated logging, one type per file, CRLF for C#, and `ConfigureAwait(false)` in production awaits.
- Use `AggregateIdentity`, `ProjectionKeySegments`, ordinal comparisons/order, stable EventStore/ULID strings, and `TimeProvider`. Never substitute `Guid` identity or current culture/time-zone behavior.
- Preserve cancellation tokens through reads/invocation, but use a bounded independent finalization token only when durable completion must be reconciled after caller cancellation; test that boundary explicitly.
- No UX surface changes are required. Operator-visible state must still obey evidence truth: projection-confirmed success can be shown only after durable delivery completion and checkpoint evidence exists.

### Technical research notes

- DAPR state is last-write-wins when ETags are omitted; Story 1.13 must always use ETags/first-write semantics and retry conflicts deliberately.
- DAPR state-store TTL is capability-dependent and is unsuitable as the deduplication correctness boundary. The count-based retained window and persisted floor are application-owned.
- DAPR pub/sub provides at-least-once delivery, and service invocation may retry transient failures. Treat either transport as capable of replaying the same logical work.
- Stay on repository-pinned package versions. This story requires no library upgrade.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 1, Story 1.13]
- [Source: `_bmad-output/planning-artifacts/prd.md` — FR7, FR36, NFR6, NFR7, NFR16 and MVP scope]
- [Source: `_bmad-output/planning-artifacts/architecture.md` — AD-7, AD-8, AD-10, AD-12, AD-13, AD-19, AD-20]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md` — projection idempotency and retention changes]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-13.md` — parallel execution and direct-contract dependency clarification]
- [Source: `_bmad-output/implementation-artifacts/1-9-read-model-and-projection-checkpoint-erasure.md` — scoped checkpoint/lifecycle implementation and accepted TOCTOU residual]
- [Source: `_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md` — frozen batch identity, outcome, receipt, and checkpoint boundary]
- [Source: `_bmad-output/implementation-artifacts/1-12-asynchronous-multi-projection-dispatch.md` — v2 envelope, dispatch identity, per-route retry and outcome truth]
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md` — Story 1.10 Tier-3 hard gate and omitted scenarios]
- [Source: `src/Hexalith.EventStore.Server/Projections/IProjectionDeliveryCheckpointStore.cs` — internal scoped checkpoint seam]
- [Source: `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs` — current key, lazy migration, and ETag behavior]
- [Source: `src/Hexalith.EventStore.Client/Projections/ReadModelBatchProtocol.cs` — resumable batch and receipt-last proof]
- [DAPR state management overview](https://docs.dapr.io/developing-applications/building-blocks/state-management/state-management-overview/)
- [DAPR pub/sub overview](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/)
- [DAPR service invocation overview](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

To be completed by the development agent.

### Completion Notes List

- Story context created from the canonical Epic 1/Story 1.13 contract, upstream implementation artifacts, architecture decisions, source inspection, git history, and current official DAPR behavior.
- Validation resolved the checkpoint/ledger ownership, fingerprint, state machine, migration, retention, lease recovery, v2 compatibility, evidence, and hard-gate ambiguities before implementation.

### File List

To be completed by the development agent.
