# Story 1.9: Read-Model And Projection Checkpoint Erasure

Status: ready-for-dev

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

- [ ] **Task 1 — Additive opt-in erase capabilities; restore released interfaces (AC: 1, 2)**
  - [ ] Revert the narrow additions that mutated released interfaces: remove `TryEraseAsync` from `IReadModelStore` (`src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs:83-87`) and from `IProjectionCheckpointTracker` (`src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs:42-45`). The released four-member `IReadModelStore` (`GetAsync`/`SaveAsync`/`TrySaveAsync`) and the released `IProjectionCheckpointTracker` shape must be restored (AC2 — no new members on released interfaces).
  - [ ] Add opt-in `IReadModelConditionalEraser` in `src/Hexalith.EventStore.Client/Projections/`, mirroring the additive `IReadModelBatchStore` capability pattern (`IReadModelBatchStore.cs`; one method, structured/idempotent contract, XML docs stating "the same concrete instance also implements `IReadModelStore`"). Method shape: `Task<bool> TryEraseAsync(string storeName, string key, string etag, CancellationToken ct = default)` — absent-key ⇒ `true` idempotently (regardless of supplied ETag); present-key ETag-mismatch ⇒ `false`; never throws for either.
  - [ ] Implement in `DaprReadModelStore` (`DaprReadModelStore.cs`) via `_daprClient.TryDeleteStateAsync(storeName, key, etag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, cancellationToken: ct)` (mirror the existing `TrySaveAsync` FirstWrite idiom at `:97-105`; `.ConfigureAwait(false)`; validate with `ArgumentException.ThrowIfNullOrWhiteSpace`/`ArgumentNullException.ThrowIfNull`). The narrow `TryEraseAsync` body at `DaprReadModelStore.cs:109-126` already does exactly this — move it behind the capability interface rather than the released one.
  - [ ] Implement in `InMemoryReadModelStore` (`src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`); keep the deterministic `ConcurrentWriteBeforeTryErase` failure-injection hook (`:51`, invoked `:143`) and the absent⇒true / mismatch⇒false logic (`:133-157`). Preserve `Snapshot<TValue>` (`:196-200`) for persisted-fake inspection.
  - [ ] Add a Server-internal checkpoint-erase capability (NOT on the released `IProjectionCheckpointTracker`) for the projection-scoped delivery checkpoint; keep it internal to Server unless an external consumer is proven. Reuse the narrow `ProjectionCheckpointTracker.TryEraseAsync` implementation body but move its interface member off the released contract.
  - [ ] Register one singleton behind `IReadModelStore` + `IReadModelBatchStore` + `IReadModelConditionalEraser` in `AddEventStoreReadModelStore` (`src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs:27-43`, same `TryAddSingleton(sp => sp.GetRequiredService<DaprReadModelStore>())` shape).
  - [ ] The coordinator resolves every required capability **before the first mutation** and returns `Unsupported` when any implementation has not opted in.

- [ ] **Task 2 — Canonical `ProjectionReadModelAddress` + factory; deny raw/legacy/shared (AC: 3)**
  - [ ] Add `ProjectionReadModelAddress` (platform-owned, e.g. `src/Hexalith.EventStore.Server/Projections/` or `src/Hexalith.EventStore.Client/Projections/` — choose per AD-2/AD-7, confirm placement in Dev Agent Record) binding tenant, domain, projection, aggregate, and registered logical slot into a canonical key. Add a factory that accepts a validated `AggregateIdentity`, a validated projection name, and a **registered** logical slot; resolves the configured store from `IOptions<ProjectionOptions>` (NOT a caller argument, mirroring `ProjectionCheckpointTracker`'s `options.Value.CheckpointStateStoreName`); and emits the canonical key. Reserved-char discipline: reuse the `AggregateIdentity`/`ProjectionRebuildCheckpointStore` convention (segments colon-free; `':' '\0' '|' '\r' '\n'` reserved).
  - [ ] Only addresses produced by this factory are accepted for erasure. Legacy/opaque caller keys are denied until migrated/rebuilt. A logical slot is registered as **aggregate-owned** or **shared**; the factory refuses shared slots for whole-key erasure (the shared singleton index — e.g. Tenants' `projection:tenant-index:singleton` — is structurally excluded).
  - [ ] Register logical projection slots through the DomainService seam (insertion point `AddDomainProjectionHandlers` in `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs:266-281`). Domain modules **declare** aggregate-owned vs shared slots; they must not implement raw DAPR erasure plumbing (AC13/AD-2).
  - [ ] Delete the narrow caller-supplied target model: remove `ReadModelEraseTarget(TenantId, StoreName, Key, ETag)` (`src/Hexalith.EventStore.Server/Projections/ReadModelEraseTarget.cs`) and the fail-closed `target.Key.StartsWith(identity.TenantId + ":")` guard (`ProjectionStateEraser.cs:49-50`) that the review proved rejects the only real consumer's keys. Ownership is proven by the factory, not by string-prefix guessing.

- [ ] **Task 3 — Projection-scoped delivery checkpoint + verified one-time migration actor (AC: 4)**
  - [ ] Migrate delivery-checkpoint addressing from aggregate-wide `projection-checkpoints:{identity.ActorId}` (`ProjectionCheckpointTracker.cs:20,261-263`) to projection-scoped `projection-checkpoints:{identity.ActorId}:{projectionName}`. Add a `projectionName` dimension to the tracker's key composition and its read/write paths.
  - [ ] Add a one-time aggregate-scoped DAPR **migration actor** (mirror `ETagActor.OnActivateAsync` one-time migration idiom: `src/Hexalith.EventStore.Server/Actors/ETagActor.cs:46-104` — persist-then-cache, fail-open cold start). Before projection-scoped delivery or erasure is enabled for an identity, the migration actor resolves every registered projection, conditionally copies the legacy high-water mark to each projection-specific key, **verifies every copy**, then conditionally deletes the legacy key and persists a completed-migration marker. Conflict, cancellation, or unknown outcome leaves projection delivery/erasure deferred and the migration retryable.
  - [ ] After the marker is complete, delivery reads only projection-specific keys and coordinated erasure deletes only the requested projection's key. **No indefinite dual-read fallback** and **no** projection-scoped request silently deletes the aggregate-wide key.
  - [ ] Register the migration actor in the single `AddActors(options => options.Actors.RegisterActor<…>())` block at `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs:120-134` (use a `public const string ActorTypeName`, mirror `GlobalPositionActor` registration).

- [ ] **Task 4 — Aggregate-specific rebuild checkpoint erasure + active-rebuild refusal (AC: 4, 9)**
  - [ ] Erase the aggregate-specific rebuild row at scope `(tenant, domain, projectionName, aggregateId)` — the `ProjectionRebuildCheckpointStore` key `projection-rebuild-checkpoints:{tenant}:{domain}:{projectionName}:{aggregateId}` (non-null AggregateId; `GetStateKey` at `ProjectionRebuildCheckpointStore.cs:784-795`). Erase it **after** read-model targets and **before** the delivery checkpoint.
  - [ ] Never erase the operator-scope row (`AggregateId = null` → `*` suffix) or the active-rebuild indexes (`projection-rebuild-active-index:*`, `projection-rebuild-active-index-pairs`).
  - [ ] Refuse the whole request **before mutation** when the target projection has an active rebuild. Use the existing fail-closed `HasActiveOperatorRebuildForDomainAsync(tenant, domain, ct)` (`ProjectionRebuildCheckpointStore.cs:384-455`) as the gate (or a projection-name-scoped equivalent); return the `ActiveRebuild` structured outcome.
  - [ ] Add an internal Server capability for this rebuild-row erase (do not widen the released `IProjectionRebuildCheckpointStore` beyond what Story 1.14 owns; confirm no conflict with 1.14 rebuild-progress semantics — see Dev Notes "Boundary with 1.14").

- [ ] **Task 5 — Persisted `ProjectionLifecycleActor` serialization + resume (AC: 6, 7)**
  - [ ] Add a persisted DAPR `ProjectionLifecycleActor` addressed by tenant/domain/aggregate/projection. Mirror the canonical idiom (`GlobalPositionActor`/`ETagActor`): `public partial class ProjectionLifecycleActor(ActorHost host, …, ILogger<ProjectionLifecycleActor> logger) : Actor(host), IProjectionLifecycleActor` where `IProjectionLifecycleActor : IActor`; `public const string ActorTypeName`; persist via `StateManager.SetStateAsync`/`SaveStateAsync`, read via `TryGetStateAsync<T>` → `ConditionalValue<T>` (use CancellationToken overloads as `EventReplayProjectionActor` does). Compose the ActorId as a colon-separated tenant/domain/aggregate/projection string (extend the `AggregateIdentity.ActorId` convention; keep segments colon-free).
  - [ ] The actor persists `{operationId, scope, targetManifestDigest, phase, perTargetOutcomes}` **before** mutation, defers delivery while `phase == Erasing`, records progress **after every target**, **rejects a different active operation** (conflict without mutation), and **resumes the same operationId** after crash/retry.
  - [ ] Route immediate delivery, polling delivery, rebuild delivery, and erasure through this actor. Wire the orchestrator entry paths through it: immediate/polling `DeliverProjectionAsync` (`ProjectionUpdateOrchestrator.cs:73`, checkpoint read `:113-114`, write `:247-249`) and rebuild `RebuildProjectionAsync`/`DeliverProjectionForRebuildAsync` (`:266-884`). A process-local `KeyedSemaphore`/`SemaphoreSlim` is insufficient.
  - [ ] Register `ProjectionLifecycleActor` in the `AddActors` block (`ServiceCollectionExtensions.cs:120-134`).

- [ ] **Task 6 — Coordinated resumable erase: per-target read-back verification, resumable-only Redis (AC: 1, 4, 5, 8)**
  - [ ] Rewrite the coordinator (replace the narrow `ProjectionStateEraser`/`IProjectionStateEraser`; keep the type names only if the new signature no longer takes caller `ReadModelEraseTarget`s). Input is scope + logical slot IDs + operationId; the coordinator resolves canonical addresses via the Task 2 factory, runs through the Task 5 actor, and composes: read-model conditional erases → aggregate-specific rebuild-row erase → projection-scoped delivery-checkpoint erase.
  - [ ] For each target, **internally read its ETag** and conditionally erase. Absent ⇒ complete; changed ETag ⇒ conflict (must not delete the newer value); after an ambiguous transport failure, **read the target back** and classify absent = complete, same ETag = retryable-incomplete, changed ETag = conflict, unverifiable = unknown. Verify **all** targets absent before marking the actor operation completed and releasing queued delivery.
  - [ ] **Resumable-only.** Remove the narrow same-store `ExecuteStateTransactionAsync` fast path (`ProjectionStateEraser.cs:55-76`). Treat every production backend including Redis as resumable-only; a future atomic adapter is opt-in **only** through an explicit backend-component-and-version allow-list entry whose persisted failure probe proves rollback of a conditional multi-delete (default false). **Align with Story 1.10's backend-qualification model** (`Resumable` default / `TransactionQualified` only when a live all-or-nothing probe qualifies the exact runtime/component; Redis stays `Resumable` — see `1-10-...md` AC2/§3). In-memory may use one internal lock but must expose the same structured outcomes.
  - [ ] Narrow the catch surface (the review flagged `catch (Exception) { return false; }` swallowing programming/config faults). Inject an `ILogger`; emit bounded, support-safe structured-outcome logs distinguishing `Denied`/`Unsupported`/`ActiveRebuild`/`Conflict`/`Incomplete`/`Canceled`/`Unknown`; rethrow `OperationCanceledException`.
  - [ ] Guard empty input: an empty read-model target set must not vacuously "succeed" by deleting only the checkpoint (the review's `targets.All(...)` empty-set bug). Validate the manifest before mutation.

- [ ] **Task 7 — Authenticated GlobalAdministrator Admin REST erase operation (AC: 3, 10, 11)**
  - [ ] Add the erase action under the existing authenticated Admin projection surface, following `src/Hexalith.EventStore/Controllers/AdminProjectionRebuildController.cs` (`[ApiController] [Authorize] [Route("api/v1/admin/projections")]`). The request DTO carries **only** tenant, domain, aggregate, projection, logical-slot IDs, and a stable operation ID — never store names, physical keys, or ETags.
  - [ ] Require: the Admin authorization policy, `[ServiceFilter(typeof(AdminTenantAuthorizationFilter))]` tenant authorization, forwarded application credentials, and an **explicit** `EnsureGlobalAdministrator()` check (`AdminProjectionRebuildController.cs:583-593`, `GlobalAdministratorHelper.IsGlobalAdministrator(User)`) **before** resolving the target factory or touching state. Ordinary Operator callers (`AdminAuthorizationPolicies.Operator`) are denied.
  - [ ] Map structured outcomes to HTTP with the existing `ProblemWithReason`/`MapSaveFailure` idiom (`AdminProjectionRebuildController.cs:480-574`): success → 200/202; `Denied` → 403; not-found/absent → idempotent success; `ActiveRebuild`/`Conflict` → 409 with `reasonCode` extension + `Retry-After`; backend-unavailable → 503. Never disclose target state on denial.
  - [ ] If a UI-facing hop is required, extend `IProjectionCommandService` (`src/Hexalith.EventStore.Admin.Abstractions/Services/IProjectionCommandService.cs`) + `DaprProjectionCommandService` two-hop seam; otherwise the Server controller is the authenticated boundary. Confirm the chosen surface in Dev Agent Record.

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

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-12: Story rebuilt from the frozen `spec-1-9` full lifecycle contract (supersedes the narrow committed attempt archived at `investigations/1-9-narrow-superseded-2026-07-11.md`). Renders resumable-only Redis, additive opt-in capabilities, canonical `ProjectionReadModelAddress` factory, projection-scoped delivery + aggregate-specific rebuild checkpoint erasure with verified migration, persisted `ProjectionLifecycleActor`, GlobalAdministrator Admin REST surface, and real-Redis persisted proof.
