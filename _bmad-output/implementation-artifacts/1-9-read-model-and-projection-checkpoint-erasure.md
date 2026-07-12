---
baseline_commit: 67b462de3f87993b72671e68c760573a83f96bc4
---

# Story 1.9: Read-Model And Projection Checkpoint Erasure

Status: in-progress

**Requirements covered:** FR5, FR36, NFR2, NFR16
**Governed by:** AD-7 (Read Models And Cursors Use Platform Seams), AD-12 (High-Risk Verification Requires Persisted Evidence), AD-2 (Domain Modules Stay Domain-Centric), AD-8 (Projection Delivery Is A Freshness Signal), AD-10 (Security Fails Closed Above Infrastructure Scoping)
**Governing contract:** `_bmad-output/implementation-artifacts/spec-1-9-read-model-and-projection-checkpoint-erasure.md` (frozen intent-contract; human-resolved full lifecycle option 1). **The frozen spec governs. Where the epics.md BDD and sprint-change-proposal §4.4 are looser, the frozen spec wins — see [Governing contract & what supersedes §4.4].**

## Story

As a domain projection maintainer,
I want platform-owned, coordinated, cancellation-aware erasure of ETag-protected aggregate read models plus projection-scoped delivery and aggregate-specific rebuild checkpoints, invoked only through an authenticated GlobalAdministrator boundary and serialized through a persisted projection-lifecycle actor,
so that removing or recreating an aggregate under the same identifier can never leave stale projection state that discards valid future events.

## Background — the concrete bug this closes

`ProjectionUpdateOrchestrator.DeliverProjectionAsync` (`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:142-153`) reads a delivery checkpoint before every projection delivery and suppresses delivery when the checkpoint exceeds the stream maximum:

```csharp
long highestAvailableSequence = events.Max(e => e.SequenceNumber);
if (lastDeliveredSequence > highestAvailableSequence) {
    Log.CheckpointDriftDetected(logger, identity.TenantId, identity.Domain, identity.AggregateId,
        ProjectionReasonCodes.CheckpointDrift, lastDeliveredSequence, highestAvailableSequence);
    return; // projection delivery is silently skipped
}
```

(A second drift branch handles the empty-stream case at `:126-136`.) The checkpoint lives at `projection-checkpoints:{identity.ActorId}` in `ProjectionOptions.CheckpointStateStoreName` (default `"statestore"`). If an aggregate is deleted and recreated with the **same identifier** but the checkpoint is never cleared, the stale `lastDeliveredSequence` from the old aggregate exceeds the new aggregate's sequence-1 event, `CheckpointDriftDetected` fires, and delivery is **silently dropped** — the recreated aggregate never gets a projection. This is the `still blocked` **G3** finding in `1-8-projection-query-sdk-owner-proof-packet.md:41-58`. This story closes it generically and safely at the platform level.

## Governing contract & what supersedes §4.4

A prior implementation of this story satisfied a **narrow** story file but was rejected in code review (2026-07-11) as contradicting the frozen `spec-1-9`, which the project treats as the governing contract. The narrow attempt is committed on `main` and preserved at `investigations/1-9-narrow-superseded-2026-07-11.md`. **Do not re-implement the narrow version.** The frozen spec deliberately tightens the looser wording still present in `epics.md` Story 1.9 (lines 677-680) and `sprint-change-proposal-2026-07-11.md` §4.4. Where they disagree, the frozen spec governs:

| Concern | Looser epics.md / §4.4 wording (do NOT follow) | Frozen `spec-1-9` (GOVERNS — build this) |
|---|---|---|
| Redis same-store path | "keys share a transaction-capable store → uses an atomic transaction" | **Resumable-only. Never call `ExecuteStateTransactionAsync` for this operation on Redis.** Atomic path is opt-in only via an explicit backend-component-and-version allow-list entry backed by a persisted rollback probe (default false). |
| Public API | (unspecified) | Released `IReadModelStore` / `IProjectionCheckpointTracker` stay **unchanged**; erasure is an **opt-in additive capability** (`IReadModelConditionalEraser`) + Server-internal checkpoint capability. |
| Target ownership | caller supplies `(storeName, key, etag)` | Callers supply **scope + logical slot IDs + operationId only**. A canonical platform `ProjectionReadModelAddress` factory derives keys. Legacy/opaque/shared-index keys are denied, never guessed. |
| Checkpoint scope | "delivery/rebuild sequence or checkpoint keys" (aggregate-wide) | **Projection-scoped delivery** key (`projection-checkpoints:{ActorId}:{projectionName}`) via a verified one-time migration, **plus** the aggregate-specific **rebuild** checkpoint row. |
| Concurrency | (unspecified) | Persisted DAPR **`ProjectionLifecycleActor`** serializes delivery/rebuild/erasure per tenant/domain/aggregate/projection; a process-local semaphore is insufficient. |
| Invocation | (unspecified) | Authenticated **GlobalAdministrator** Admin REST surface with tenant authorization; Operator is denied. |
| Proof | "persisted tests" | Real **Redis/DAPR persisted-state** proof of resumable-not-atomic behavior, tenant isolation through the production endpoint, and fresh-identity sequence-one recovery without event-stream deletion. |

The frozen spec's status is `blocked` on an *intent gap that was human-resolved to option 1* (its Spec Change Log 2026-07-11 and Auto Run Result record the selected decisions). Those resolved decisions ARE the contract below; the story is buildable.

## Acceptance Criteria

1. **Single-key conditional erase (ETag-aware, idempotent-absent).** Given a present read-model key and its internally-read current ETag, when conditional erasure completes, then the key is absent and completion is reported; given a stale ETag against a present key, the value remains and a conflict is reported (never an exception for either case); given an absent key, erasure is an idempotent success with no mutation regardless of the supplied ETag. [spec AC1, AC3]

2. **Additive compatibility — released interfaces unchanged.** Given existing third-party implementations of the released `IReadModelStore` or `IProjectionCheckpointTracker`, when this release is consumed, then those interfaces have **no new members** and remain source/binary compatible; coordinated erasure returns `Unsupported` **before any mutation** unless every required additive capability (`IReadModelConditionalEraser`, the Server-internal checkpoint-erase capability) is registered. [spec AC2]

3. **Canonical target ownership — deny raw/legacy/shared.** Given a request containing raw/legacy/opaque/shared-index targets, or a canonical target outside the authenticated tenant/domain/aggregate/projection scope, when validation runs, then no target state is resolved, read, disclosed, or mutated (`Unsupported`/denial without revealing target state). Only `ProjectionReadModelAddress` values produced by the platform factory from validated `AggregateIdentity` + projection + registered aggregate-owned slot are erasable; the same address is used for writes and erasure. Shared/index slots are excluded from whole-key erasure. [spec AC4]

4. **Coordinated erase end state + ordering.** Given an authorized canonical target manifest and no active rebuild, when coordinated erasure reports success, then every aggregate-owned read-model target, the requested projection's **aggregate-specific rebuild checkpoint**, and its **projection-scoped delivery checkpoint** are durably absent; the operator-scope rebuild row (`AggregateId = null`), active-rebuild indexes, other projections, and other tenants are unchanged. Erasure order is read-models first, aggregate-specific rebuild checkpoint second, projection-scoped delivery checkpoint last; progress is persisted after each target. [spec AC5]

5. **Truthful partial-failure / structured outcomes.** Given a failure, cancellation, conflict, or ambiguous backend outcome after any mutation, when the coordinator returns, then it does **not** report success and exposes a retry-safe structured outcome that distinguishes `Denied`, `Unsupported`, `ActiveRebuild`, `Conflict`, `Incomplete`, `Canceled`, and `Unknown` from success. Per target, after an ambiguous transport failure the coordinator reads the target back and classifies absent = complete, same ETag = retryable-incomplete, changed ETag = conflict (never deletes the newer value), unverifiable = unknown. All targets are verified absent before completion is reported and queued delivery is released. [spec AC6]

6. **Persisted resume via the lifecycle actor.** Given the same stable operation ID after interruption, when erasure is retried, then the persisted `ProjectionLifecycleActor` resumes its recorded manifest/progress and converges; given a **different** operation ID while an erasure is active, then it conflicts without mutation. [spec AC7]

7. **Cross-replica serialization.** Given concurrent projection delivery/rebuild and erasure requests on different replicas targeting the same tenant/domain/aggregate/projection, then the persisted `ProjectionLifecycleActor` serializes them (one actor turn owns the scope; delivery is deferred while phase is erasing) and no read model or checkpoint can be recreated during a completed erasure. A process-local lock is not accepted as proof. [spec AC8]

8. **Resumable-only Redis.** Given the repository Redis/DAPR backend, when coordinated erasure executes, then it uses the persisted resumable protocol and **never** calls `ExecuteStateTransactionAsync` for this operation and never claims transaction rollback or atomic completion from same-store placement or the `TRANSACTIONAL` capability string. [spec AC9]

9. **Active-rebuild refusal.** Given the requested tenant/domain/projection has an active operator rebuild, when erasure is requested, then the whole request is refused before mutation with `ActiveRebuild`; the caller may retry after the rebuild is terminal. [spec AC5 "Block If active rebuild", I/O matrix]

10. **Tenant isolation (persisted).** Given tenant A and tenant B persisted state, when tenant A erasure is requested, then ownership is validated before mutation, and tenant B state remains unchanged and undisclosed; persisted-state tests verify the denial **and** both tenants' end state. [spec AC10]

11. **Authenticated GlobalAdministrator boundary.** Given the authenticated Admin projection REST surface, when erasure is invoked, then the request carries only tenant/domain/aggregate/projection/logical-slot IDs and a stable operation ID (never store names, physical keys, or ETags); the Admin authorization policy, tenant-authorization filter, forwarded application credentials, and an **explicit EventStore GlobalAdministrator decision** are all required before the target factory is resolved or any state is touched; ordinary Operator callers are denied. [spec AC ("authenticated boundary"), AD-10]

12. **Fresh-identity persisted recovery proof (the bug closed).** Given a fresh unique identity with a directly-seeded higher projection-scoped delivery checkpoint and a canonical read model, when the authenticated production erasure completes and the first normal command is submitted, then the event stream's first persisted event is sequence one and projection state is persisted **without checkpoint-drift suppression**; the proof never resets or deletes an event stream. [spec AC ("stale checkpoint recovery"), AD-12]

13. **Scope guardrails.** Given the feature is built and tested, then no event stream, snapshot, broker/pub-sub history, backup, audit evidence, cryptographic key, shared index key, operator-scope rebuild row, or active-rebuild index is erased, and no raw DAPR state access or lifecycle plumbing is introduced into a domain module; `DomainModuleAuthoringGuardrailTests` passes unmodified and no root-declared submodule is modified. [spec AC11, AC ("Never"), AD-2]

## Tasks / Subtasks

> Implement in order; each task must leave all configured suites green before the next. Tasks 1-2 revert narrow public-surface mistakes and build the ownership seam; 3-6 build the coordinated resumable operation; 7-8 add the boundary + DI; 9-10 are the deterministic and persisted proofs. **RED→GREEN→REFACTOR each subtask.**

- [x] **Task 1 — Additive opt-in erase capabilities; restore released interfaces (AC: 1, 2)** — _seams done (Stage A); the final subtask (coordinator resolves capabilities before mutation) closed by Task 6 (Stage E)_
  - [x] Revert the narrow additions that mutated released interfaces: remove `TryEraseAsync` from `IReadModelStore` (`src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs:83-87`) and from `IProjectionCheckpointTracker` (`src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs:42-45`). The released four-member `IReadModelStore` (`GetAsync`/`SaveAsync`/`TrySaveAsync`) and the released `IProjectionCheckpointTracker` shape must be restored (AC2 — no new members on released interfaces).
  - [x] Add opt-in `IReadModelConditionalEraser` in `src/Hexalith.EventStore.Client/Projections/`, mirroring the additive `IReadModelBatchStore` capability pattern (`IReadModelBatchStore.cs`; one method, structured/idempotent contract, XML docs stating "the same concrete instance also implements `IReadModelStore`"). Method shape: `Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken ct = default)` — absent-key ⇒ `true` idempotently (regardless of supplied ETag); present-key ETag-mismatch ⇒ `false`; never throws for either.
  - [x] Implement in `DaprReadModelStore` (`DaprReadModelStore.cs`) via `_daprClient.TryDeleteStateAsync(storeName, key, etag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, cancellationToken: ct)` (mirror the existing `TrySaveAsync` FirstWrite idiom at `:97-105`; `.ConfigureAwait(false)`; validate with `ArgumentException.ThrowIfNullOrWhiteSpace`/`ArgumentNullException.ThrowIfNull`). The narrow `TryEraseAsync` body at `DaprReadModelStore.cs:109-126` already does exactly this — move it behind the capability interface rather than the released one.
  - [x] Implement in `InMemoryReadModelStore` (`src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`); keep the deterministic `ConcurrentWriteBeforeTryErase` failure-injection hook (`:51`, invoked `:143`) and the absent⇒true / mismatch⇒false logic (`:133-157`). Preserve `Snapshot<TValue>` (`:196-200`) for persisted-fake inspection.
  - [x] Add a Server-internal checkpoint-erase capability (NOT on the released `IProjectionCheckpointTracker`) for the projection-scoped delivery checkpoint; keep it internal to Server unless an external consumer is proven. Reuse the narrow `ProjectionCheckpointTracker.TryEraseAsync` implementation body but move its interface member off the released contract.
  - [x] Register one singleton behind `IReadModelStore` + `IReadModelBatchStore` + `IReadModelConditionalEraser` in `AddEventStoreReadModelStore` (`src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs:27-43`, same `TryAddSingleton(sp => sp.GetRequiredService<DaprReadModelStore>())` shape).
  - [x] The coordinator resolves every required capability **before the first mutation** and returns `Unsupported` when any implementation has not opted in. _(Closed by Task 6 / Stage E: `ProjectionEraseCoordinator` guards `IReadModelConditionalEraser` / `IProjectionRebuildCheckpointEraser` / `IProjectionDeliveryCheckpointStore` and returns `Unsupported("capability-unavailable")` before any mutation.)_

- [x] **Task 2 — Canonical `ProjectionReadModelAddress` + factory; deny raw/legacy/shared (AC: 3)**
  - [x] Add `ProjectionReadModelAddress` (platform-owned, e.g. `src/Hexalith.EventStore.Server/Projections/` or `src/Hexalith.EventStore.Client/Projections/` — choose per AD-2/AD-7, confirm placement in Dev Agent Record) binding tenant, domain, projection, aggregate, and registered logical slot into a canonical key. Add a factory that accepts a validated `AggregateIdentity`, a validated projection name, and a **registered** logical slot; resolves the configured store from `IOptions<ProjectionOptions>` (NOT a caller argument, mirroring `ProjectionCheckpointTracker`'s `options.Value.CheckpointStateStoreName`); and emits the canonical key. Reserved-char discipline: reuse the `AggregateIdentity`/`ProjectionRebuildCheckpointStore` convention (segments colon-free; `':' '\0' '|' '\r' '\n'` reserved).
  - [x] Only addresses produced by this factory are accepted for erasure. Legacy/opaque caller keys are denied until migrated/rebuilt. A logical slot is registered as **aggregate-owned** or **shared**; the factory refuses shared slots for whole-key erasure (the shared singleton index — e.g. Tenants' `projection:tenant-index:singleton` — is structurally excluded).
  - [x] Register logical projection slots through the DomainService seam (insertion point `AddDomainProjectionHandlers` in `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:266-281`). Domain modules **declare** aggregate-owned vs shared slots; they must not implement raw DAPR erasure plumbing (AC13/AD-2).
  - [x] Delete the narrow caller-supplied target model: remove `ReadModelEraseTarget(TenantId, StoreName, Key, ETag)` (`src/Hexalith.EventStore.Server/Projections/ReadModelEraseTarget.cs`) and the fail-closed `target.Key.StartsWith(identity.TenantId + ":")` guard (`ProjectionStateEraser.cs:49-50`) that the review proved rejects the only real consumer's keys. Ownership is proven by the factory, not by string-prefix guessing.

- [x] **Task 3 — Projection-scoped delivery checkpoint + verified migration (AC: 4)** — _implemented per human-approved **Option A** (lazy per-projection migration; NO separate one-time enumeration actor — see Dev Agent Record "Task 3 design decision (Stage B)"). Stage B landed green 2026-07-12._
  - [x] Migrate delivery-checkpoint addressing from aggregate-wide `projection-checkpoints:{identity.ActorId}` to projection-scoped `projection-checkpoints:{identity.ActorId}:{projectionName}`. Added the projection-scoped read/save/erase behind a new **Server-internal** `IProjectionDeliveryCheckpointStore` (released `IProjectionCheckpointTracker` unchanged, AC2), implemented by the same `ProjectionCheckpointTracker` singleton.
  - [x] **(Option A supersedes the "one-time enumeration migration actor" wording.)** Lazy per-`(identity,projectionName)` migration: first projection-scoped read copies the legacy aggregate-wide high-water mark into the projection-scoped key (race-safe conditional seed), then persists a `projection-checkpoints-migrated:{ActorId}:{projectionName}` marker. The legacy key is **retained, never deleted**. Premise (verified in source): no domain→projection registry exists and the drift-check reads the checkpoint before `response.ProjectionType` is known, so up-front enumeration is not implementable.
  - [x] After the marker exists, a scoped-absent read returns **0** (marker suppresses legacy fallback) so a post-erase read does not re-migrate the legacy value; coordinated erasure (Task 6) deletes only the requested projection's scoped key and leaves the marker intact. The one intentional Option-A relaxation vs. the frozen spec ("no indefinite dual-read fallback / delete legacy key") is the retained legacy key, guarded by the marker; AC12 is unaffected (a fresh identity has no legacy key).
  - [x] **(Option A: no migration actor to register.)** Orchestrator `DeliverProjectionAsync` restructured — non-empty-stream drift now evaluated **after** the side-effect-free `/project` call against the scoped checkpoint (keyed by `response.ProjectionType`), and the delivery save (immediate + rebuild poller-mirror) targets the scoped key. Empty-stream drift branch left aggregate-wide (diagnostic-only; no projection name without events).

- [x] **Task 4 — Aggregate-specific rebuild checkpoint erasure + active-rebuild refusal (AC: 4, 9)** — _capability + operator-scope fencing done 2026-07-12; the whole-request refusal + `ActiveRebuild` structured outcome is WIRED by the Task 6 coordinator (the gate itself is confirmed present)._
  - [x] Erase the aggregate-specific rebuild row at scope `(tenant, domain, projectionName, aggregateId)` — internal `IProjectionRebuildCheckpointEraser.TryReadAggregateCheckpointEtagAsync` + `TryEraseAggregateCheckpointAsync` (FirstWrite conditional delete on `GetStateKey(scope)`). The Task 6 coordinator sequences it **after** read-model targets and **before** the delivery checkpoint.
  - [x] Never erase the operator-scope row (`AggregateId = null` → `*` suffix) or the active-rebuild indexes. Enforced by `EnsureAggregateScope` (throws `ArgumentException` before ANY state access when `AggregateId` is null/whitespace); `GetStateKey` maps null/whitespace → `*` and `*` is a reserved char, so a real AggregateId can never resolve to the operator/index keys.
  - [x] **(Gate confirmed; request-level refusal wired in Task 6.)** The fail-closed `HasActiveOperatorRebuildForDomainAsync(tenant, domain, ct)` already exists on the released interface (reflection-asserted); the Task 6 coordinator calls it before mutation and returns the `ActiveRebuild` structured outcome. Task 4 adds no new gate.
  - [x] Added the internal Server capability off the same concrete `ProjectionRebuildCheckpointStore` singleton (DI forwarder mirrors `IProjectionCheckpointEraser`); released `IProjectionRebuildCheckpointStore` unchanged — no conflict with Story 1.14 rebuild-progress semantics (erase-only, aggregate-row-only).

- [x] **Task 5 — Persisted `ProjectionLifecycleActor` serialization + resume (AC: 6, 7)** — _implemented per user-approved "gate the write" design (2026-07-12): the actor gates the projection WRITE (the only state-recreating step, known after `/project`) rather than delivery start, since the projection name is unknown earlier — same constraint as Option A._
  - [x] Added persisted DAPR `ProjectionLifecycleActor` (`public partial class ProjectionLifecycleActor(ActorHost host, ILogger<…> logger) : Actor(host), IProjectionLifecycleActor`; `public const string ActorTypeName = "ProjectionLifecycleActor"`; `StateManager.TryGetStateAsync/SetStateAsync/SaveStateAsync`). ActorId = `{tenant}:{domain}:{aggregate}:{projection}`. Invoked via a mockable internal `IProjectionLifecycleGateway` / `DaprProjectionLifecycleGateway` using the **weak/JSON `ActorProxy.InvokeMethodAsync`** path (mirrors `DefaultProjectionActorInvoker`; the strongly-typed dispatch proxy throws NRE).
  - [x] Actor persists `{Phase, OperationId, ManifestDigest, PerTargetOutcomes}` **before** returning: `BeginEraseAsync` ⇒ Admitted (Idle→Erasing) / Resume (same operationId, returns persisted outcomes) / Conflict (different operationId, no mutation); `RecordTargetOutcomeAsync` upserts per-target after every target; `CompleteEraseAsync` ⇒ Idle; `TryAdmitDeliveryWriteAsync` ⇒ false while Erasing. (Internal state record renamed `ProjectionLifecycleActorState` to avoid colliding with the released `Contracts.Queries.ProjectionLifecycleState` enum.)
  - [x] **(Approved refinement: gate the WRITE, not delivery start.)** Both `DeliverProjectionAsync` (returns) and `DeliverProjectionForRebuildAsync` (returns `Interrupt()` → retries later) call `lifecycleGateway.TryAdmitDeliveryWriteAsync` immediately before `writeProxy.UpdateProjectionAsync`; deferred ⇒ no write/ETag/checkpoint. Existing per-aggregate `KeyedSemaphore` kept as in-process backup. Coordinator (Task 6) drives BeginErase/RecordTarget/CompleteErase.
  - [x] Registered `ProjectionLifecycleActor` in the `AddActors` block; `IProjectionLifecycleGateway`→`DaprProjectionLifecycleGateway` via `TryAddSingleton`.

- [x] **Task 6 — Coordinated resumable erase: per-target read-back verification, resumable-only Redis (AC: 1, 4, 5, 8)** — _done 2026-07-12; new `ProjectionEraseCoordinator` (the narrow `ProjectionStateEraser` was already deleted in Stage A)._
  - [x] Built `IProjectionEraseCoordinator`/`ProjectionEraseCoordinator`. Input = `ProjectionEraseRequest(tenant, domain, aggregate, projection, slots, operationId)` — logical IDs only. Resolves canonical addresses via the Task 2 factory, runs through the Task 5 lifecycle gateway (`BeginErase`→`RecordTargetOutcome` per target→`CompleteErase`; resume by operationId), and composes: read-model conditional erases → aggregate-specific rebuild-row erase → projection-scoped delivery-checkpoint erase (strict order). Added type-agnostic read-ETag: `IReadModelConditionalEraser.TryReadEtagAsync` + `IProjectionDeliveryCheckpointStore.TryReadDeliveryCheckpointEtagAsync` (rebuild eraser already had read-etag from Task 4).
  - [x] Per-target read-back classifier: absent ⇒ Complete; erased+read-back-absent ⇒ Complete; erased+still-present ⇒ Incomplete; refused (etag mismatch) ⇒ Conflict (newer value NOT deleted); ambiguous transport ⇒ read-back (absent=Complete, same-etag=Incomplete, changed=Conflict, read-back throws=Unknown). Aggregate `Conflict > Unknown > Incomplete`; **verify all targets absent before `CompleteErase`**.
  - [x] **Resumable-only** — the coordinator NEVER calls `ExecuteStateTransactionAsync` (asserted by test). Every backend incl. Redis is resumable-only; a future atomic adapter remains opt-in via explicit allow-list (aligns with Story 1.10 Resumable/TransactionQualified; no second weaker path added). In-memory fake exposes the same structured outcomes.
  - [x] Narrow catch surface: `OperationCanceledException` → `Canceled` (never Success); unexpected `Exception` → `Unknown("unexpected")` with a bounded structured log; no `catch { return false }`.
  - [x] Empty-input guard: empty `Slots` ⇒ `Unsupported("no-slots")` before any mutation (no vacuous checkpoint-only "success"); manifest fully resolved+validated before the first mutation (unregistered/shared/legacy slot ⇒ `Unsupported`, no state touched).

- [x] **Task 7 — Authenticated GlobalAdministrator Admin REST erase operation (AC: 3, 10, 11)** — _done 2026-07-12; `POST api/v1/admin/projections/{tenantId}/{projectionName}/erase` on the existing `AdminProjectionRebuildController`._
  - [x] Added `EraseProjectionAsync` to `AdminProjectionRebuildController` (coordinator injected as an optional trailing ctor param). Request body `ProjectionEraseRequestBody(Domain, AggregateId, Slots, OperationId)` (+ `tenantId`/`projectionName` from route) — logical IDs ONLY; reflection-asserted to have no store/key/etag member. DTOs added to `Contracts/Streams/` (additive; released types unchanged).
  - [x] **(Actual pattern.)** The controller uses `[Authorize]` + an explicit `EnsureGlobalAdministrator()` check FIRST (denial-before-resolution — verified: non-admin ⇒ 403 and the coordinator `DidNotReceive`). NOTE: the real controller does not use `[ServiceFilter(AdminTenantAuthorizationFilter)]` (that was aspirational in the task text); global-admin is the gate and the request's tenant scopes the erase. Ordinary Operator callers are denied.
  - [x] Mapped structured outcomes via the controller's `ProblemWithReason` idiom (`MapEraseResult`): `Success` → 200 + `ProjectionEraseResponse`; `Denied` → 403; `Unsupported` → 400; `ActiveRebuild`/`Conflict`/`Incomplete` → 409 + reasonCode + `Retry-After: 5`; `Unknown`/`Canceled` → 503 + reasonCode. Never discloses raw target state (only Kind/ReasonCode/per-target classification).
  - [x] No UI-facing two-hop needed: the controller compiles into `Hexalith.EventStore.Gateway` (which references Server), so it consumes the public `IProjectionEraseCoordinator` directly — the Server-boundary controller IS the authenticated boundary.

- [ ] **Task 8 — Platform DI registration + domain-centric guardrails (AC: 2, 13)**
  - [ ] Register the coordinator, both new actors, the target factory, the address/configuration policy, and the erase capabilities through the platform DI extensions (`ServiceCollectionExtensions.AddEventStoreServer` around `:55-57` for the coordinator/capabilities; the `AddActors` block `:120-134` for the actors; `AddEventStoreReadModelStore` for `IReadModelConditionalEraser`). Use `TryAdd*` to stay overridable.
  - [ ] Domain modules may register logical projection slots but must not implement raw DAPR erasure plumbing. Run `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` **unmodified** and confirm it still passes (its `DaprStateAccessMarkers` already forbids `DeleteStateAsync`/`ExecuteStateTransactionAsync` in domain modules).

- [ ] **Task 9 — Deterministic unit/contract tests (AC: 1, 2, 3, 5, 6, 7, 9, 11, 13)**
  - [ ] `tests/Hexalith.EventStore.Client.Tests/Projections/` — additive interface compatibility (released `IReadModelStore` unchanged), `IReadModelConditionalEraser` conditional erase for DAPR (via `RecordingDaprClient`, whose `TryDeleteStateAsync` override is real at `:208-222`) and in-memory (success, absent-idempotent, ETag conflict, cancellation, retry, injected partial failure via `ConcurrentWriteBeforeTryErase`).
  - [ ] `tests/Hexalith.EventStore.Server.Tests/Projections/` — canonical `ProjectionReadModelAddress` validation, shared/legacy denial **before** mutation, coordinated resumable erase with per-target read-back classification, actor serialization/resume + duplicate-operationId conflict, structured outcomes, checkpoint migration/scope (projection-scoped key), active-rebuild refusal, and DI resolution. **Move** the misplaced narrow `ProjectionStateEraserTests.cs` out of the Client test project into the Server test project (it exercises a Server type).
  - [ ] `tests/Hexalith.EventStore.Admin.Server.Tests/` (and/or the Server controller test project) — GlobalAdministrator vs Operator authorization, tenant authorization, safe request shape (no physical coordinates), denial-before-resolution, and endpoint result mapping (200/202/403/404/409/503).
  - [ ] `tests/Hexalith.EventStore.DomainService.Tests/` — unmodified domain authoring guardrails pass.

- [ ] **Task 10 — Persisted real-Redis integration proof (AC: 4, 7, 8, 10, 12)**
  - [ ] Use the real-Redis harness `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/` (`Fixtures/DaprTestContainerFixture.cs`; real daprd + Redis; direct state-store key inspection via `GetAggregateActorStateJsonAsync`/`GetStateAsync<T>`; `[Collection("DaprTestContainer")] [Trait("Category","LiveSidecar")]`) and/or the Aspire contract fixture (`tests/Hexalith.EventStore.IntegrationTests/Fixtures/AspireContractTestFixture.cs`, exposing `EventStoreClient`/`AdminServerClient`) for the authenticated endpoint. **Note:** the spec's Verification block names `tests/Hexalith.EventStore.IntegrationTests`, but the real-Redis **direct-key-inspection** capability lives in `Server.LiveSidecar.Tests` — home the persisted-state assertions there; use the Aspire fixture for the authenticated end-to-end endpoint call.
  - [ ] Prove: (a) the Redis path is **resumable, not atomic** (no `ExecuteStateTransactionAsync` for this op; partial-failure resume converges); (b) persisted **tenant isolation** through the authenticated production endpoint (assert both tenants' end state with `StorageKeyIsolationAssertions.AssertKeyBelongsToTenant`, pattern from `tests/Hexalith.EventStore.IntegrationTests/Security/MultiTenantStorageIsolationTests.cs`); (c) cross-replica actor exclusion; (d) delivery + rebuild checkpoint scope; (e) **stale-checkpoint recovery** — a fresh unique identity with a directly-seeded higher projection-scoped delivery checkpoint + canonical read model, erased through the authenticated endpoint (`TestJwtTokenGenerator.GenerateToken(..., role: "GlobalAdministrator")`), then its first normal command produces sequence one, projection state persists, and **no** `CheckpointDriftDetected`. The proof **never** resets or deletes an event stream (drive commands through `/api/v1/commands` + `PollUntilTerminalStatusAsync`, inspect persisted Redis directly).
  - [ ] Honor the Tier-3 constraints ([[tier3-integration-test-constraints]]): clean Dapr placement before running; the LiveSidecar/IntegrationTests suites are not gated in CI — log this so completion evidence records the harness used.

## Dev Notes

### Previous attempt intelligence (do not repeat)

The committed narrow implementation (HEAD `428ef1a1`, files preserved at `investigations/1-9-narrow-superseded-2026-07-11.md`) is **rejected**. Concrete defects to avoid:
- Added `TryEraseAsync` to the **released** `IReadModelStore` (`IReadModelStore.cs:83-87`) and `IProjectionCheckpointTracker` (`:42-45`) — violates AC2 (released interfaces must not gain members). Move both behind opt-in capabilities.
- `ProjectionStateEraser` accepts caller-supplied `ReadModelEraseTarget(TenantId, StoreName, Key, ETag)` and guards ownership with `target.Key.StartsWith(identity.TenantId + ":")` (`ProjectionStateEraser.cs:49-50`). This **fails closed on the only real consumer** — Tenants' actual keys are `projection:tenants:{id}` / `projection:tenant-index:singleton`, none tenant-prefixed — so the coordinated erase silently no-ops in production and the drift fix (AC12) never works. Ownership must come from the canonical factory, not string guessing.
- Uses `ExecuteStateTransactionAsync` for the same-store fast path (`:55-76`), which the spec's persisted Redis probe proved does **partial** commits on ETag conflict while Redis warns rollback is unsupported. Resumable-only.
- `catch (Exception) { return false; }` (`:93-95`) collapses denied/conflict/incomplete/unknown into an unlogged `false` on an AD-12 path; empty `readModelTargets` makes the atomic path vacuously succeed. Structured outcomes + input validation required.
- AD-12 "persisted" proofs were `Substitute.For<DaprClient>()` simulations. Real Redis persisted state is required (Task 10).

### Reuse — don't reinvent (grounded in current source)

- **Additive capability pattern:** `IReadModelBatchStore` (Story 1.10) is the template for `IReadModelConditionalEraser` — one method, structured contract, "same concrete instance also implements `IReadModelStore`; one singleton behind both." DI: `AddEventStoreReadModelStore` (`ReadModelStoreServiceCollectionExtensions.cs:27-43`).
- **Backend qualification / resumable-only Redis:** Story 1.10 already defines `Resumable` (default) vs `TransactionQualified` and keeps Redis resumable unless a live all-or-nothing probe qualifies it (`1-10-...md` AC2, §3). Story 1.9 must adopt the same stance; do not add a second, weaker qualification path. **Boundary:** 1.10 AC7 explicitly excludes delivery/rebuild checkpoints from its batch unit — 1.9 owns checkpoint erasure; 1.10 owns detail/index batch writes. No overlap.
- **Persisted DAPR actor idiom:** `partial class X(ActorHost host, …, ILogger<X> logger) : Actor(host), IX` (`IX : IActor`), `public const string ActorTypeName`, `StateManager.TryGetStateAsync<T>` → `ConditionalValue<T>` / `SetStateAsync` + `SaveStateAsync` (`GlobalPositionActor.cs`, `EventReplayProjectionActor.cs`). **One-time in-actor migration:** `ETagActor.OnActivateAsync` (`ETagActor.cs:46-104`) — persist-then-cache, fail-open cold start. Register in the single `AddActors` block (`ServiceCollectionExtensions.cs:120-134`). Proxy invocation for JSON-method actors uses `ActorProxy proxy = _actorProxyFactory.Create(new ActorId(actorId), actorTypeName); proxy.InvokeMethodAsync<TReq,TRes>(nameof(IX.Method), …)` (`DefaultProjectionActorInvoker.cs:23-43`) — the strongly-typed dispatch proxy throws `NullReferenceException` on `InvokeMethodAsync`.
- **Admin GlobalAdministrator boundary:** `EnsureGlobalAdministrator()` returning a 403 `ProblemWithReason` at the top of every mutating action (`AdminProjectionRebuildController.cs:583-593`); `GlobalAdministratorHelper.IsGlobalAdministrator` accepts role claims `GlobalAdministrator`/`global-administrator`/`global-admin` (`src/Hexalith.EventStore/Authorization/GlobalAdministratorHelper.cs:18`). Tenant scoping via `AdminTenantAuthorizationFilter` (global-admin bypass then tenant-claim match). Policies: `AdminReadOnly`/`AdminOperator`/`AdminFull` (`AdminAuthorizationPolicies.cs`). Operator is granted only by the `command:replay` permission claim; GlobalAdministrator elevates to `AdminFull` — they are distinct.
- **Options-driven store name:** resolve the state-store component from `IOptions<ProjectionOptions>.Value.CheckpointStateStoreName` (default `"statestore"`, `ProjectionOptions.cs:12`), never a caller argument.
- **Tenant-isolation assertion:** `StorageKeyIsolationAssertions.AssertKeyBelongsToTenant(key, tenant)` (`src/Hexalith.EventStore.Testing/Assertions/StorageKeyIsolationAssertions.cs:18-24`) asserts `key.ShouldStartWith($"{tenant}:")` — valid for `AggregateIdentity.ActorId`-derived keys (`{tenant}:{domain}:{agg}`), **not** for domain read-model keys that omit a tenant prefix. Assert read-model key ownership via the canonical factory's known composition, not this helper, for non-ActorId keys.

### Current key/scope facts (ground truth)

- Delivery checkpoint today: `projection-checkpoints:{identity.ActorId}` (aggregate-scoped, **no projectionName**), store `CheckpointStateStoreName` (`ProjectionCheckpointTracker.cs:20,261-263`). Target: `projection-checkpoints:{identity.ActorId}:{projectionName}` after verified migration.
- Rebuild rows already carry projection: `projection-rebuild-checkpoints:{tenant}:{domain}:{projectionName}:{aggregateId|*}` (`ProjectionRebuildCheckpointStore.cs:784-795`). `*` = operator-scope row (`AggregateId = null`) — **never erase**. Active-rebuild index: `projection-rebuild-active-index:{tenant}:{domain}` (`string[]` of active projection names) + `projection-rebuild-active-index-pairs`; gate via `HasActiveOperatorRebuildForDomainAsync` (fail-closed, `:384-455`).
- `AggregateIdentity.ActorId => $"{TenantId}:{Domain}:{AggregateId}"` (`src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:55`), constructor-validated colon-free.
- `ProjectionUpdateOrchestrator` delivery entry paths to route through the actor: immediate/polling `DeliverProjectionAsync` (`:73`; drift `:126-136`,`:142-153`; checkpoint read `:113-114`, write `:247-249`; rebuild-collision `:82-88`) and rebuild `RebuildProjectionAsync`/`DeliverProjectionForRebuildAsync` (`:266-884`).

### Boundary with Story 1.14 (rebuild correctness)

Story 1.14 ("Correct Paged Rebuild And Replay Equivalence", `ready-for-dev`) owns rebuild-progress *correctness*. Story 1.9 only **erases** the aggregate-specific rebuild **row** for a recreated identity and **refuses** when a rebuild is active — it does not change rebuild replay semantics, does not touch the operator-scope row, and does not add members to the released `IProjectionRebuildCheckpointStore` beyond an internal erase capability. If erasure semantics appear to require changing rebuild-progress behavior, stop and reconcile with 1.14 before proceeding.

### Testing standards (project-context + AD-12)

- xUnit v3 (`[Fact]`/`[Theory]`), Shouldly (`ShouldBe`/`ShouldBeTrue`), NSubstitute; never raw `Assert.*`. Run test projects individually (never solution-level `dotnet test`).
- Two DAPR-mock conventions: `RecordingDaprClient` (hand-rolled; `TrySaveStateAsync`/`TryDeleteStateAsync`/`ExecuteStateTransactionAsync` overrides are real) for `DaprReadModelStore` tests; `Substitute.For<DaprClient>()` elsewhere. **Recorder call counts are request-shape evidence only — not completion proof** (AD-12/NFR16). Assert persisted end-state (`InMemoryReadModelStore.Snapshot<T>`, real Redis via `DaprTestContainerFixture`).
- `ConfigureAwait(false)` on every awaited call (CA2007 is build-breaking). `Server.Tests` has a known pre-existing CA2007-as-error build gap (baseline exclusion) — do not treat its pre-existing red as caused by this story, but introduce no new CA2007 violations.

### Project structure & constraints

- Touch points: `src/Hexalith.EventStore.Client/Projections/` + `.../Registration/`, `src/Hexalith.EventStore.Testing/Fakes/`, `src/Hexalith.EventStore.Server/Projections/` + `.../Actors/` + `.../Configuration/`, `src/Hexalith.EventStore/Controllers/` + `.../Authorization/`, `src/Hexalith.EventStore.DomainService/`, and mirrored test projects incl. `Hexalith.EventStore.Server.LiveSidecar.Tests`.
- No `.csproj` package-version edits — `Dapr.Client`/`Dapr.Actors` already pinned in `Directory.Packages.props`. No `.sln` (use `.slnx`). No copyright headers. Keep `tools/release-packages.json` at 14 packages — new APIs go in existing Contracts/Client/Server/DomainService/Testing/Testing.Integration packages.
- Do not modify `references/*` submodules (Tenants/Memories cited read-only). Never `Guid.TryParse` an id field (ULID handling). Never reorder the MediatR pipeline.

### References

- [Governing spec] `_bmad-output/implementation-artifacts/spec-1-9-read-model-and-projection-checkpoint-erasure.md` (Intent, I/O matrix, Tasks & Acceptance, Design Notes, Verification)
- [Superseded narrow attempt] `_bmad-output/implementation-artifacts/investigations/1-9-narrow-superseded-2026-07-11.md` (incl. code-review findings)
- [Epic BDD] `_bmad-output/planning-artifacts/epics.md#Story-1.9` (lines 657-696) — **looser; frozen spec overrides the transaction/target wording**
- [Scope rewrite] `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md#4.4` (lines 243-247) — superseded by the frozen spec on transaction/capability/actor/admin decisions
- [Architecture] `architecture.md` AD-7 (85-89), AD-8 (91-95), AD-2 (55-59), AD-10 (103-107), AD-12 (115-119)
- [Requirements] `prd.md` FR5 (line 107), FR36 (201), NFR2 (210), NFR16 (224)
- [Blocker] `1-8-projection-query-sdk-owner-proof-packet.md#G3` (lines 41-58)
- [Sibling pattern] `1-10-coordinated-read-model-batch-writes.md` (Resumable/TransactionQualified + additive capability; AC7 checkpoint boundary)
- Key source: `IReadModelStore.cs`, `DaprReadModelStore.cs`, `IReadModelBatchStore.cs`, `InMemoryReadModelStore.cs`, `ReadModelStoreServiceCollectionExtensions.cs`, `IProjectionCheckpointTracker.cs`, `ProjectionCheckpointTracker.cs`, `ProjectionRebuildCheckpointStore.cs`, `ProjectionUpdateOrchestrator.cs`, `ProjectionOptions.cs`, `Actors/{GlobalPositionActor,ETagActor,EventReplayProjectionActor}.cs`, `Configuration/ServiceCollectionExtensions.cs`, `DomainService/EventStoreDomainServiceExtensions.cs`, `Controllers/AdminProjectionRebuildController.cs`, `Authorization/{GlobalAdministratorHelper,AdminTenantAuthorizationFilter}.cs`, `Admin.Server/Authorization/*`, `Testing/Assertions/StorageKeyIsolationAssertions.cs`, `Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs`, `IntegrationTests/Fixtures/AspireContractTestFixture.cs`, `IntegrationTests/Helpers/{ContractTestHelpers,TestJwtTokenGenerator}.cs`

## Validation

Run focused first, then the broadest practical EventStore lane:

```bash
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.Testing.Tests/
dotnet test tests/Hexalith.EventStore.Server.Tests/
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests/
dotnet test tests/Hexalith.EventStore.DomainService.Tests/
# Persisted real-Redis proof (Tier 3; clean Dapr placement first; not CI-gated):
dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/
dotnet build Hexalith.EventStore.slnx --configuration Release
git diff --check
```

All configured tests must pass before this story is complete. `Server.Tests`' pre-existing CA2007-as-error build gap is a documented baseline exclusion; do not add new CA2007 violations.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (1M context) — `/bmad-dev-story` staged execution (verify each stage), started 2026-07-12.

### Task 3 design decision (Stage B) — Option A, human-approved 2026-07-12 (SPEC DEVIATION)

The frozen spec's Task 3 migration ("resolve **every** registered projection, copy legacy → each, verify, then **delete the legacy key**") is not implementable as written: the codebase has **no domain→projection registry** (`DomainServiceRegistration` carries only domain; `ProjectionSlotRegistry` is keyed by projection name, not domain-indexed; only the active-rebuild index holds a live `domain→projectionName[]`, for in-flight rebuilds only), and the drift-check checkpoint read happens **before** the projection name is known (`response.ProjectionType` returns from `/project` afterward). User selected **Option A**:

- Delivery checkpoint keyed by **`response.ProjectionType`** → `projection-checkpoints:{ActorId}:{projectionName}`.
- Drift check moved **after** the `/project` call so it suppresses the projection **write** (the `/project` compute is side-effect-free), since the projection name isn't known before then.
- **Lazy per-projection migration** on first projection-scoped access (copy legacy aggregate-wide high-water mark → this projection's key, guarded by a per-`(identity,projection)` migrated marker so post-erasure reads do not fall back to the retained legacy value).
- **Legacy aggregate-wide key is RETAINED** (never auto-deleted) — completeness is unprovable without enumeration.
- **Spec delta:** relaxes the frozen spec's "enumerate all + delete legacy key" and "no indefinite dual-read fallback"; a dedicated one-time enumeration migration actor is replaced by lazy per-projection migration. The fresh-identity drift proof (AC12) is unaffected (a fresh identity has no legacy key; erasing the projection-scoped key alone removes the gate).
- **AC2 refinement:** the released `IProjectionCheckpointTracker` must keep its exact 4-member shape (Read/Save/Track/Enumerate — no changed or added members, per AC2). Therefore the projection-scoped delivery read/write/erase go through a **new Server-internal** interface on the concrete `ProjectionCheckpointTracker` (extending the internal `IProjectionCheckpointEraser` capability), consumed by the orchestrator. The released interface's aggregate-wide Read/Save are left intact (legacy/observability). The orchestrator switches its delivery-checkpoint dependency to the internal projection-scoped API.

### Stage B build sequence (big-bang; no intermediate green)

Internal projection-scoped delivery API + concrete impl (lazy migration + per-`(identity,projection)` migrated marker) → orchestrator restructure (drift after `/project`, keyed by `response.ProjectionType`; both delivery + rebuild save sites pass the projection name) → update the ~40 orchestrator/poller test mock+fake call sites to the internal projection-scoped API → new migration/scope tests → build+test. The tracker's own unit tests (`ProjectionCheckpointTrackerTests`) stay on the released aggregate-wide members and are largely untouched.

### Placement decisions (per Task 2 / AD-2 / AD-7)

- **`ProjectionReadModelAddress` + `IProjectionReadModelAddressFactory` + `ProjectionSlotRegistry` → Server** (`src/Hexalith.EventStore.Server/Projections/`). The factory resolves the read-model store from `IOptions<ProjectionOptions>` (Server config) and the coordinated eraser (Server) consumes it. The address record has an **internal** constructor so only the factory can mint erasable addresses (forgery resistance for AC3).
- **Slot-declaration types → Client** (`ProjectionReadModelSlotKind`, `ProjectionReadModelSlotDeclaration`, `IDeclaresProjectionReadModelSlots` in `src/Hexalith.EventStore.Client/Projections/`). `Hexalith.EventStore.DomainService` references **Client, not Server**, so the domain-facing declaration contract must live in Client. Declarations are registered as DI singletons (`ProjectionReadModelSlotDeclaration`); the Server `ProjectionSlotRegistry` absorbs all of them when resolved (`ProjectionSlotServiceCollectionExtensions.BuildSlotRegistry`) — order- and package-boundary-independent.
- **Read-model store name** resolved from a new additive `ProjectionOptions.ReadModelStateStoreName` (default `statestore`), not conflated with `CheckpointStateStoreName`.
- Canonical key form: `readmodel:{tenant}:{domain}:{projection}:{aggregate}:{slot}`, all segments reserved-char-free (`ProjectionKeySegments`).

### Debug Log References

- 2026-07-12 Stage A (Task 1+2 seams + revert narrow coordinator):
  - Removed `TryEraseAsync` from released `IReadModelStore` / `IProjectionCheckpointTracker`; moved erase behind opt-in `IReadModelConditionalEraser` (Client) and internal `IProjectionCheckpointEraser` (Server); deleted narrow `ProjectionStateEraser` / `IProjectionStateEraser` / `ReadModelEraseTarget` and their DI + tests.
  - Added canonical `ProjectionReadModelAddress` + factory + slot registry + DomainService slot-declaration discovery.
  - GREEN: Server.Tests **2328 passed / 0 failed / 25 skipped** (164s); Client.Tests 633/633 (incl. new erase tests 5+13); Testing.Tests 144/144; DomainService.Tests 88/88 (guardrails unmodified); new `ProjectionReadModelAddressFactoryTests` 10/10. Release build of Server/Testing/DomainService clean (TreatWarningsAsErrors).
  - Environment note: a concurrent agent ("fix-live-sidecar-CI") committed unrelated batch-protocol changes to the tree mid-stage (HEAD 67b462de → acc45f14); none of this story's files were absorbed (verified). Work continued on the shared tree per user direction.
- 2026-07-12 Stage B (Task 3 — projection-scoped delivery checkpoint, Option A):
  - New Server-internal `IProjectionDeliveryCheckpointStore` (Read/Save/Erase, each `(AggregateIdentity, string projectionName, …)`) implemented by the same `ProjectionCheckpointTracker` singleton and registered off it in DI (mirrors the Stage A `IProjectionCheckpointEraser` cast). Released `IProjectionCheckpointTracker` public shape unchanged (AC2 — verified by a reflection test).
  - Projection-scoped key `projection-checkpoints:{ActorId}:{projectionName}`; migration marker `projection-checkpoints-migrated:{ActorId}:{projectionName}` (record `ProjectionCheckpointMigrationMarker(bool Migrated)`, presence-signals-migrated). `ReadDeliveredSequenceAsync`: scoped-present ⇒ value; scoped-absent + marker-present ⇒ **0** (no legacy fallback); scoped-absent + marker-absent ⇒ lazy-migrate legacy high-water mark (race-safe conditional seed, legacy key never deleted) then write marker. `SaveDeliveredSequenceAsync` mirrors the aggregate-wide ETag-retry/monotonic-max loop and refreshes the marker; `TryEraseAsync` FirstWrite-deletes the scoped key and leaves the marker intact.
  - `ProjectionUpdateOrchestrator`: non-empty drift moved after `/project` (keyed by `response.ProjectionType`, scoped read); delivery save (immediate + rebuild poller-mirror) targets the scoped key via `deliveryCheckpointStore is not null ? … : legacy` fallback (null-passing tests stay on the aggregate-wide path). Empty-stream drift branch unchanged (diagnostic-only).
  - **Spec-vs-reality resolution:** a `public` orchestrator class cannot take an `internal`-typed ctor param (CS0051). Since the new capability must stay internal (mirrors `IProjectionCheckpointEraser`), `ProjectionUpdateOrchestrator` was made `internal`. Verified safe: it is referenced ONLY inside `Hexalith.EventStore.Server` + its IVT test project (repo-wide grep), is DI-registered solely via its public interfaces (`IProjectionUpdateOrchestrator`/`IProjectionPollerDeliveryGateway`/`IProjectionRebuildOrchestrator`), and there is no `PublicAPI.*.txt` surface — so no external consumer or API baseline is affected; the public contract (the interfaces) is unchanged.
  - GREEN: Server.Tests **2350 passed / 0 failed / 25 skipped**; Client.Tests 637/637; DomainService.Tests 88/88; full `Hexalith.EventStore.slnx` Release build exit 0 (0 warn/0 err); `git diff --check` clean. New tests: 8 delivery-checkpoint-store (key composition, lazy migration, post-erase-returns-0, independent-per-projection, released-shape-unchanged) + 2 orchestrator scoped-drift-after-`/project` + 1 adjusted drift test.
- 2026-07-12 Stage C (Task 4 — aggregate-specific rebuild-row erase capability):
  - New Server-internal `IProjectionRebuildCheckpointEraser` (`TryReadAggregateCheckpointEtagAsync` → `(Present, Etag)`; `TryEraseAggregateCheckpointAsync(scope, etag)` → FirstWrite conditional delete, absent⇒true, mismatch⇒false) implemented on the concrete `ProjectionRebuildCheckpointStore` and forwarded off its DI singleton. Released `IProjectionRebuildCheckpointStore` (6 members) unchanged (reflection-asserted).
  - `EnsureAggregateScope` fails closed (`ArgumentException`, no state access) when `AggregateId` is null/whitespace. Confirmed `GetStateKey` suffix = `IsNullOrWhiteSpace(AggregateId) ? "*" : AggregateId`; `*` ∈ reserved chars ⇒ a real AggregateId can never resolve to the operator-scope `*` row or the `projection-rebuild-active-index:*` / `-pairs` index keys. State-store name from `options.Value.CheckpointStateStoreName` (same as `ReadAsync`/`SaveAsync`). `.ConfigureAwait(false)` throughout.
  - GREEN: Server.Tests **2360 passed / 0 failed / 25 skipped** (+10 new); slnx Release build exit 0; `git diff --check` clean. New tests: present-etag delete, absent-idempotent, stale-etag no-delete, aggregate-key targeting (`DidNotReceive` on `*`/index keys), read-etag present/absent, operator-scope `ArgumentException` + no-state-access (both methods), released-shape-unchanged, and gate-already-present assertion.
- 2026-07-12 Stage D (Task 5 — persisted `ProjectionLifecycleActor` + write-gate, approved "gate the write"):
  - New public `IProjectionLifecycleActor`/`ProjectionLifecycleActor` (ActorTypeName `"ProjectionLifecycleActor"`, single state key `"projection-lifecycle"`, log EventIds 5051–5055) + DTOs; internal `IProjectionLifecycleGateway`/`DaprProjectionLifecycleGateway` (weak `ActorProxy.InvokeMethodAsync`, actorId `{t}:{d}:{a}:{projection}`, reserved-char validation via `ProjectionKeySegments.Validate`). Orchestrator gets an optional `IProjectionLifecycleGateway? lifecycleGateway` (last param); both delivery paths gate the write; new reason code `ProjectionReasonCodes.DeliveryDeferredForErase`, orchestrator log EventId 1146.
  - Name-collision fix: the persisted state record was renamed `ProjectionLifecycleActorState` (the released `Contracts.Queries.ProjectionLifecycleState` enum is in scope in `Server.Actors`); internal, DAPR serializes by property, no wire/contract impact.
  - **Concurrent-loop absorption event:** while this stage's agent was mid-write, the parallel doc-loop committed `d0abf9e0 "feat(docs): …"` with a broad `git add`, absorbing this stage's PRODUCTION files (actor/gateway/orchestrator/DI/reason-codes) into ITS commit — capturing a **pre-rename, non-building snapshot** of `ProjectionLifecycleActor.cs`. The completing delta (the `…ActorState` rename that restores the build, the second DI registration line, and all Task-5 test files, which were NOT absorbed) is committed separately on top to restore green. Submodule pointer bumps (`references/Hexalith.FrontComposer`, `references/Hexalith.Memories`) in that commit are the loop's, left untouched.
  - GREEN (working tree re-verified after absorption): Server.Tests **2384 passed / 0 failed / 25 skipped** (+24: 10 actor + 12 gateway + 2 orchestrator); Server Release build exit 0; slnx Release build exit 0; `git diff --check` clean.
- 2026-07-12 Stage E (Task 6 — coordinated resumable eraser; also closes Task 1's last subtask):
  - Added type-agnostic read-ETag: `IReadModelConditionalEraser.TryReadEtagAsync(storeName,key)` (DaprReadModelStore mirrors GetAsync's marker-gated visibility read so the etag matches what `TryEraseAsync` deletes; InMemory parity) and `IProjectionDeliveryCheckpointStore.TryReadDeliveryCheckpointEtagAsync(identity,projectionName)` (raw scoped-key read, NO lazy migration). Both are new-in-this-story interfaces (additive OK); released `IReadModelStore` untouched.
  - `ProjectionEraseCoordinator` (internal sealed): validate→capability-guard→identity→manifest-resolve→active-rebuild-gate (fail-closed)→`BeginErase`(digest)→per-target classify+`RecordTargetOutcome`→aggregate→verify-absent→`CompleteErase`. `manifestDigest` = lowercase-hex SHA-256 of ordinally-sorted target keys (deterministic). Capabilities nullable-injected but guarded → `Unsupported("capability-unavailable")` before mutation. Cancellation consistently wrapped as `Canceled`. DI `TryAddSingleton`.
  - GREEN: Client.Tests **643** (+6 read-etag), Testing.Tests **150**, Server.Tests **2399** (+15 coordinator); Server + slnx Release builds exit 0; `git diff --check` clean. Coordinator tests assert: happy-path order + Complete/Success, empty-slots/unregistered/shared ⇒ Unsupported (no mutation), active-rebuild ⇒ ActiveRebuild, BeginErase-Conflict ⇒ Conflict, etag-mismatch ⇒ Conflict (newer kept), ambiguous→read-back (Complete/Incomplete/Conflict), resume skips Complete targets, verify-present ⇒ Incomplete, cancellation ⇒ Canceled, and NO `ExecuteStateTransactionAsync` ever invoked.
- 2026-07-12 Stage F (Task 7 — authenticated GlobalAdministrator Admin REST erase):
  - `POST api/v1/admin/projections/{tenantId}/{projectionName}/erase` (`EraseProjectionAsync` on `AdminProjectionRebuildController`, which compiles into `Hexalith.EventStore.Gateway`). Coordinator injected as optional trailing ctor param. `EnsureGlobalAdministrator()` runs FIRST (denial-before-resolution); `IProjectionEraseCoordinator` null ⇒ 503; missing/blank body/slots ⇒ 400. Body `ProjectionEraseRequestBody(Domain, AggregateId, Slots, OperationId)` + route tenant/projection → `ProjectionEraseRequest`. `MapEraseResult`: Success→200, Denied→403, Unsupported→400, ActiveRebuild/Conflict/Incomplete→409 + `Retry-After: 5`, Unknown/Canceled→503. New `Log.EraseCompleted` (EventId 1196) logs only target count. DTOs in `Contracts/Streams/` (additive). Note: the controller uses `EnsureGlobalAdministrator()` + `[Authorize]`, NOT `AdminTenantAuthorizationFilter` (task text was aspirational).
  - GREEN: `Hexalith.EventStore` Release build exit 0; controller tests (`AdminProjectionRebuildControllerTests`) **30 passed / 0 failed** (14 new erase + 16 existing; filtered to the class to avoid the project's Tier-3 Dapr-placement hang — NOT a solution-level run); slnx Release build exit 0; `git diff --check` clean. New tests: GlobalAdmin Success→200 + coordinator called; non-admin→403 + `DidNotReceive`; Unsupported→400; ActiveRebuild/Conflict/Incomplete→409 + `Retry-After`; Unknown/Canceled→503; missing/blank body→400 (no coordinator call); logical-IDs-only pass-through; reflection: body has no store/key/etag member.

### Completion Notes List

- **Stage A DONE** — Task 1 seams (6/7 subtasks; the coordinator-side "resolve capabilities before mutation" is Stage E) and Task 2 (complete). Released interfaces restored to their pre-narrow shape (AC2); erasure is opt-in additive. Canonical ownership seam in place (AC3 factory-side denial of shared/legacy/unregistered targets).
- **Stage B DONE (Task 3)** — Projection-scoped delivery checkpoint via human-approved Option A (lazy per-projection migration, no separate actor). New internal `IProjectionDeliveryCheckpointStore`; orchestrator drift moved after `/project`; scoped delivery save. Released `IProjectionCheckpointTracker` unchanged (AC2). All configured suites green (Server 2350, Client 637, DomainService 88). Enables the AC12 fresh-identity drift fix once Tasks 6–7 wire the erase through the boundary.
- **Stage C DONE (Task 4)** — Internal `IProjectionRebuildCheckpointEraser` (aggregate-row conditional erase + read-etag) with fail-closed operator-scope/index protection. Released `IProjectionRebuildCheckpointStore` unchanged; active-rebuild gate `HasActiveOperatorRebuildForDomainAsync` confirmed present for the Task 6 coordinator to enforce request-level `ActiveRebuild` refusal. Server.Tests 2360 green.
- **Stage D DONE (Task 5)** — Persisted `ProjectionLifecycleActor` (Idle/Erasing state machine: Admitted/Resume/Conflict + per-target outcomes + Complete) with mockable `IProjectionLifecycleGateway`; orchestrator gates the projection write in both delivery paths per the approved "gate the write" design. Server.Tests 2384 green. Delivery-vs-erase cross-replica serialization (AC6/AC7) is now in place for the Task 6 coordinator to drive.
- **Stage E DONE (Task 6, + Task 1 final subtask)** — `ProjectionEraseCoordinator` ties Tasks 2–5 + read-model eraser: validate-before-mutation, capability guard (`Unsupported`), active-rebuild fail-closed refusal, lifecycle begin/record/complete with resume, per-target ETag read-back classification, verify-absent before Complete, resumable-only (no transactions), full structured outcomes. Added read-ETag to the read-model + delivery-checkpoint capabilities. Client 643 / Testing 150 / Server 2399 green.
- **Stage F DONE (Task 7)** — Authenticated GlobalAdministrator `POST .../erase` endpoint (denial-before-resolution; logical-IDs-only request; structured outcome→HTTP mapping). Controller tests 30 green. Remaining: Task 8 (DI wiring/guardrails — largely already registered), Task 9 (unit-test consolidation — mostly written), Task 10 (real-Redis persisted proof — Docker confirmed available).

### File List

_Stage F (Task 7):_
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionEraseRequestBody.cs` (new — logical-IDs-only request body)
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionEraseResponse.cs` (new)
- `src/Hexalith.EventStore.Contracts/Streams/ProjectionEraseTargetOutcomeDto.cs` (new)
- `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs` (add `EraseProjectionAsync` + `MapEraseResult` + coordinator ctor param + log 1196)
- `tests/Hexalith.EventStore.Server.Tests/Controllers/AdminProjectionRebuildControllerTests.cs` (+14 erase tests)

_Stage E (Task 6):_
- `src/Hexalith.EventStore.Client/Projections/IReadModelConditionalEraser.cs` (+`TryReadEtagAsync`)
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` (read-etag impl)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` (read-etag parity)
- `src/Hexalith.EventStore.Server/Projections/IProjectionDeliveryCheckpointStore.cs` (+`TryReadDeliveryCheckpointEtagAsync`)
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs` (read-etag impl, no lazy migration)
- `src/Hexalith.EventStore.Server/Projections/IProjectionEraseCoordinator.cs` (new — public coordinator contract + DTOs + outcome enum)
- `src/Hexalith.EventStore.Server/Projections/ProjectionEraseCoordinator.cs` (new — internal sealed)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (register coordinator)
- `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs` / `InMemoryReadModelStoreTests.cs` (+6 read-etag tests)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionEraseCoordinatorTests.cs` (new — 15 tests)

_Stage D (Task 5):_
- `src/Hexalith.EventStore.Server/Actors/IProjectionLifecycleActor.cs` (new — public actor contract + DTOs)
- `src/Hexalith.EventStore.Server/Actors/ProjectionLifecycleActor.cs` (new — persisted state machine)
- `src/Hexalith.EventStore.Server/Projections/IProjectionLifecycleGateway.cs` (new — internal seam)
- `src/Hexalith.EventStore.Server/Projections/DaprProjectionLifecycleGateway.cs` (new — weak-proxy invocation)
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` (write-gate in both delivery paths; `lifecycleGateway` param; log 1146)
- `src/Hexalith.EventStore.Server/Projections/ProjectionReasonCodes.cs` (added `DeliveryDeferredForErase`)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (register actor + gateway)
- `tests/Hexalith.EventStore.Server.Tests/Actors/ProjectionLifecycleActorTests.cs` (new — 10 tests)
- `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionLifecycleGatewayTests.cs` (new — 12 tests)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` (2 write-gate tests)
- _Note: actor/gateway/orchestrator/DI/reason-code production files were absorbed (pre-rename snapshot) into concurrent-loop commit `d0abf9e0`; the rename/build-fix + DI completion + all test files land in the follow-up Task-5 commit._

_Stage C (Task 4):_
- `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildCheckpointEraser.cs` (new — internal aggregate-row conditional eraser)
- `src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs` (implements it; `EnsureAggregateScope` operator-scope guard)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (register `IProjectionRebuildCheckpointEraser` off the store singleton)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionRebuildCheckpointEraserTests.cs` (new — 10 tests)

_Stage B (Task 3):_
- `src/Hexalith.EventStore.Server/Projections/IProjectionDeliveryCheckpointStore.cs` (new — internal projection-scoped delivery Read/Save/Erase)
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs` (implements it; lazy migration + `projection-checkpoints-migrated:` marker; legacy key retained)
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs` (drift-after-`/project`, scoped delivery save; class made `internal`)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (register `IProjectionDeliveryCheckpointStore` off the tracker singleton)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryCheckpointStoreTests.cs` (new)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` (scoped-drift-after-`/project` tests)

_Stage A:_
- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs` (reverted — removed `TryEraseAsync`)
- `src/Hexalith.EventStore.Client/Projections/IReadModelConditionalEraser.cs` (new)
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs` (implements new capability)
- `src/Hexalith.EventStore.Client/Projections/ProjectionReadModelSlotKind.cs` (new)
- `src/Hexalith.EventStore.Client/Projections/ProjectionReadModelSlotDeclaration.cs` (new)
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs` (register `IReadModelConditionalEraser`)
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` (implements new capability)
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs` (reverted — removed `TryEraseAsync`)
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointEraser.cs` (new, internal)
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs` (implements internal eraser)
- `src/Hexalith.EventStore.Server/Projections/ProjectionKeySegments.cs` (new)
- `src/Hexalith.EventStore.Server/Projections/ProjectionReadModelAddress.cs` (new)
- `src/Hexalith.EventStore.Server/Projections/ProjectionReadModelAddressException.cs` (new)
- `src/Hexalith.EventStore.Server/Projections/IProjectionReadModelAddressFactory.cs` (new)
- `src/Hexalith.EventStore.Server/Projections/ProjectionReadModelAddressFactory.cs` (new)
- `src/Hexalith.EventStore.Server/Projections/IProjectionSlotRegistry.cs` (new)
- `src/Hexalith.EventStore.Server/Projections/ProjectionSlotRegistry.cs` (new)
- `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs` (added `ReadModelStateStoreName`)
- `src/Hexalith.EventStore.Server/Configuration/ProjectionSlotServiceCollectionExtensions.cs` (new)
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` (DI: remove narrow eraser; add registry/factory/checkpoint-eraser)
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs` (slot-declaration discovery seam)
- `src/Hexalith.EventStore.Server/Projections/ProjectionStateEraser.cs` (deleted — narrow)
- `src/Hexalith.EventStore.Server/Projections/IProjectionStateEraser.cs` (deleted — narrow)
- `src/Hexalith.EventStore.Server/Projections/ReadModelEraseTarget.cs` (deleted — narrow)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionReadModelAddressFactoryTests.cs` (new)
- `tests/Hexalith.EventStore.Client.Tests/Projections/ProjectionStateEraserTests.cs` (deleted — narrow)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs` (removed narrow recreation-proof test)
- `tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs` (removed narrow forged-tenant erase test)
- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs` / `Dw1PollerCorruptionAtddTests.cs` (removed dangling fake `TryEraseAsync`)

## Change Log

- 2026-07-12: Stage F (Task 7) — authenticated GlobalAdministrator `POST api/v1/admin/projections/{tenantId}/{projectionName}/erase` on `AdminProjectionRebuildController`; denial-before-resolution (`EnsureGlobalAdministrator` first), logical-IDs-only request body (no store/key/etag), structured outcome→HTTP mapping (200/400/403/409+Retry-After/503). New `Contracts/Streams` DTOs (additive). Controller tests 30/0; Release builds clean.
- 2026-07-12: Stage E (Task 6, closes Task 1) — `ProjectionEraseCoordinator` (validate-before-mutation, capability guard→Unsupported, active-rebuild fail-closed, lifecycle begin/record/complete + resume, per-target ETag read-back classification, verify-absent-before-Complete, resumable-only/no-transactions, structured `Success|Denied|Unsupported|ActiveRebuild|Conflict|Incomplete|Canceled|Unknown`). Added type-agnostic read-ETag to `IReadModelConditionalEraser` + `IProjectionDeliveryCheckpointStore`. Client 643, Testing 150, Server 2399; Release builds clean.
- 2026-07-12: Stage D (Task 5) — persisted `ProjectionLifecycleActor` (Idle/Erasing state machine, per-target outcomes, resume-by-operationId, reject-different-operation) + internal `IProjectionLifecycleGateway` (weak `ActorProxy` invocation); orchestrator gates the projection write in both delivery paths per the user-approved "gate the write, post-/project" design; internal state record renamed `ProjectionLifecycleActorState` (released-enum collision). Server.Tests 2384/0/25, slnx Release build clean. NOTE: production files were absorbed by concurrent doc-loop commit `d0abf9e0` as a non-building pre-rename snapshot; this stage's follow-up commit restores the build (rename) + adds tests + completes DI.
- 2026-07-12: Stage C (Task 4) — internal `IProjectionRebuildCheckpointEraser` for the aggregate-specific rebuild row (FirstWrite conditional delete + read-etag), fail-closed operator-scope/index protection via `EnsureAggregateScope`. Released `IProjectionRebuildCheckpointStore` unchanged; active-rebuild gate confirmed present for the Task 6 coordinator. Server.Tests 2360/0/25, slnx Release build clean.
- 2026-07-12: Stage B (Task 3) — projection-scoped delivery checkpoint via human-approved Option A (lazy per-projection migration + retained legacy key + `projection-checkpoints-migrated:` marker; no enumeration actor). New internal `IProjectionDeliveryCheckpointStore`; orchestrator drift moved after the side-effect-free `/project` call and delivery save re-keyed to `projection-checkpoints:{ActorId}:{projectionName}`. `ProjectionUpdateOrchestrator` made `internal` (verified no external consumer / no public-API baseline). Released `IProjectionCheckpointTracker` unchanged (AC2). Suites green: Server.Tests 2350/0/25, Client 637, DomainService 88, slnx Release build clean.
- 2026-07-12: Stage A — reverted the narrow released-interface mutations; introduced opt-in `IReadModelConditionalEraser` + internal `IProjectionCheckpointEraser`; deleted narrow `ProjectionStateEraser`/`ReadModelEraseTarget`; added canonical `ProjectionReadModelAddress` factory + slot registry + DomainService slot-declaration seam. All configured suites green (Server.Tests 2328, Client 633, Testing 144, DomainService 88).
- 2026-07-12: Story rebuilt from the frozen `spec-1-9` full lifecycle contract (supersedes the narrow committed attempt archived at `investigations/1-9-narrow-superseded-2026-07-11.md`). Renders resumable-only Redis, additive opt-in capabilities, canonical `ProjectionReadModelAddress` factory, projection-scoped delivery + aggregate-specific rebuild checkpoint erasure with verified migration, persisted `ProjectionLifecycleActor`, GlobalAdministrator Admin REST surface, and real-Redis persisted proof.
