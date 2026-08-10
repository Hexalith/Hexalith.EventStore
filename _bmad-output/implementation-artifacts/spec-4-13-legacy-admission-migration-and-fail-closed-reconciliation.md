---
title: 'Story 4.13: Legacy Admission Migration And Fail-Closed Reconciliation'
type: 'feature'
created: '2026-08-10'
status: 'done'
review_loop_iteration: 0
story_key: '4-13-legacy-admission-migration-and-fail-closed-reconciliation'
baseline_commit: '8358ffc399bdb1f1574bd049f17b3b6ebf907619'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Existing legacy-admission scaffolding has no durable closed-inventory proof or source redirect, activates a copied target before canonical authority is proven, and trusts incomplete `Migrated` markers. Ambiguous, corrupt, or unscanned consumed keys can therefore become unavailable indefinitely or be mistaken for fresh authority.

**Approach:** Implement a versioned tenant inventory and crash-resumable migration protocol that proves the exact legacy source, prepares a non-executable target, acknowledges it, persists a payload-free source redirect, flips one canonical authority, and only then activates exact replay. Every incomplete or unverifiable condition remains read-only and fail-closed.

## Boundaries & Constraints

**Always:** Preserve current authorization, stable execution identities, exact logical result mapping, Story 4.11 current-fence rules, and Story 4.12 inclusive expiry/tombstone/lifecycle behavior. Bind inventory, source checkpoint, aliases, target acknowledgement, redirect, and phase to one tenant and migration identity. Retain source evidence through the irreversible redirect boundary; before it rollback may remove only an unactivated prepared target, while afterward recovery is forward-only.

**Ask First:** Changing public command/API contracts, AD-25 phase semantics, replay or deletion retention, supported legacy-shape policy, submodules, packages, or Story 4.14/4.15 multi-host closure.

**Never:** Persist or expose raw idempotency keys, canonical intent, digest-key material, raw source state keys, or protected result payloads in diagnostics/evidence. Never reuse the aggregate-local copy/delete shortcut as cross-aggregate proof, manufacture missing fields, convert unknown state to `NoLegacy`/fresh admission, activate before redirect and authority proof, or invoke domain/provider/repository/projection/audit/scheduling work during diagnosis.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Exact migration | Closed known inventory; exact self-describing source | Prepare target, acknowledge, redirect source, flip authority, activate, replay exactly | Persist each checkpoint before advancing |
| Interrupted migration | Restart/failure at any phase or key rotation mid-flight | Resume the pinned target idempotently; preserve one authority | Roll back only before redirect; otherwise recover forward |
| Unsafe evidence | Open/uninventoried, ambiguous, cross-tenant, unknown schema/version, malformed or colliding state | Read-only bounded diagnosis and zero protected work | Stable unsafe/collision/conflict/unavailable outcome; never Missing |
| Invalid completion | `Migrated` marker lacks matching source redirect, target acknowledgement, activation, or directory | Refuse replay and fresh authority | Preserve evidence for support-safe reconciliation |
| Expired source | Exact consumed legacy evidence is already expired | Preserve consumed knowledge using non-executable expiry semantics | Never replay or execute |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/IdempotencyLegacyInventoryActor.cs:21-156` and adjacent inventory contracts -- per-key entries exist, but closure, strict schema/tenant/phase validation, ambiguity recording, rollback checkpoints, and governed cleanup do not.
- `src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs:21-170,322-527` -- current order prepares and activates a target without persisting or verifying the aggregate-source redirect; `Migrated` falls through unchecked.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs:16-194` and `Actors/AggregateActor.cs:131-160` -- reuse exact message-keyed read-only inspection; do not reuse the causation-key copy/delete path at lines 134-142.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs:61-95,463-581` -- prepared targets are already non-executable; extend their acknowledgement/inspection and pre-redirect rollback without weakening promotion currentness.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs:31-105,189-275` -- serialize migration with active-tenant authority and retain/purge inventory references under existing deletion governance.
- `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyLegacyInventoryActorTests.cs:17-118`, `IdempotencyAdmissionActorTests.cs:450-704`, `AggregateActorFencingTests.cs:95-147`, and `Pipeline/SubmitCommandHandlerIdempotencyAdmissionTests.cs:84-108,380-526` -- focused unit seams for phases, read-only diagnosis, and zero-work assertions.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionLiveSidecarTests.cs:17-237` -- reuse real Dapr/Redis restart and persisted-state inspection; do not claim Story 4.14 multi-host PostgreSQL evidence.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Server/Actors/IdempotencyLegacyInventory*.cs` -- add immutable versioned closure/manifest and strict entry, ambiguity, phase, redirect-checkpoint, rollback, and cleanup transitions; absence is `NoLegacy` only after valid closure or explicit clean-install policy.
- [x] `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`, `IdempotencyChecker.cs`, and new internal legacy-source contracts -- inspect only exact supported source state, persist/verify a payload-free non-executable redirect, and retain original evidence.
- [x] `src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs`, `IdempotencyTenantLifecycleActor.cs`, and interfaces -- expose hash-bound target acknowledgement/inspection, safe pre-redirect rollback, lifecycle-serialized migration, and governed reference cleanup.
- [x] `src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs` -- enforce prepare → acknowledge → source redirect → inventory/directory flip → activate, pin the target across rotation/restart, and re-prove completed migrations before replay.
- [x] `tests/Hexalith.EventStore.Server.Tests/**/Idempotency*Tests.cs` -- cover every matrix row, supported and rejected legacy shapes, cross-aggregate ambiguity, corruption/unavailability, phase crashes, rollback boundary, exact replay, expiry, leakage, and zero downstream work.
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionLiveSidecarTests.cs` and `docs/guides/configuration-reference.md` -- prove persisted target/redirect/inventory/directory authority across restart and document closed-inventory/clean-install operation.

**Acceptance Criteria:**
- Given exact closed legacy evidence, when migration completes or resumes after any durable phase, then exactly one proven target becomes executable and the original logical result replays unchanged after restart.
- Given a pre-redirect rollback or a post-redirect interruption, when recovery runs, then only the permitted rollback or forward path occurs and source evidence is never deleted first.
- Given any unclosed, unsupported, ambiguous, corrupt, unavailable, expired, or inconsistent migrated state, when admission is attempted, then no fresh authority or downstream side effect occurs and the bounded support-safe classification preserves consumed-key knowledge.
- Given tenant deletion or digest rotation during migration, when the next phase runs, then lifecycle and pinned-alias checks prevent cross-tenant authority, premature key retirement, or dual execution.
- Given raw-key and canonical-intent sentinels, when unit and live-state evidence is inspected, then neither sentinel appears in state, logs, errors, metrics, traces, or generated evidence.

## Spec Change Log

## Design Notes

The irreversible boundary is the durable legacy-source redirect. Before it, only an unactivated prepared target may be rolled back; after it, the source is permanently non-executable and reconciliation must finish the already-bound target. Existing directory promotion and signed read-only aggregate reconciliation are the golden patterns; inventory metadata is evidence, never execution authority by itself.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -m:1` -- zero warnings/errors.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Actors.IdempotencyLegacyInventoryActorTests` plus focused coordinator/fencing/handler classes -- all pass with no skips.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build -m:1` -- full Server.Tests lane passes except only pre-existing documented skips.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release --filter "FullyQualifiedName~IdempotencyAdmissionLiveSidecarTests" -m:1` -- migration/restart persisted-state proof passes when Dapr/Docker are available.
- `git diff --check` -- no whitespace errors.

## Suggested Review Order

**Admission and authority flow**

- Start here: inventory diagnosis now gates every mutable authority operation.
  [`IdempotencyAdmissionCoordinator.cs:18`](../../src/Hexalith.EventStore.Server/Commands/IdempotencyAdmissionCoordinator.cs#L18)

- Lifecycle ownership serializes the complete resumable migration and rollback protocol.
  [`IdempotencyTenantLifecycleActor.cs:118`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs#L118)

- Completed migrations follow only proven forward redirect chains after later rotations.
  [`IdempotencyTenantLifecycleActor.cs:978`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyTenantLifecycleActor.cs#L978)

- Read-only directory inspection proves authority without creating or advancing state.
  [`IdempotencyAdmissionDirectoryActor.cs:17`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionDirectoryActor.cs#L17)

**Durable inventory and source proof**

- Versioned closure binds tenant, scan versions, entries, and immutable logical evidence.
  [`IdempotencyLegacyInventoryActor.cs:92`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyLegacyInventoryActor.cs#L92)

- Phase transitions reprove manifest binding and enforce immutable redirect checkpoints.
  [`IdempotencyLegacyInventoryActor.cs:262`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyLegacyInventoryActor.cs#L262)

- Aggregate-local inspection and redirect remain exact, bounded, and source-preserving.
  [`IdempotencyChecker.cs:207`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs#L207)

**Target safety and expiry**

- Promotion preparation persists non-executable, hash-bound original and current-state evidence.
  [`IdempotencyAdmissionActor.cs:504`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs#L504)

- Compaction atomically advances current-state proof while preserving original acknowledgement.
  [`IdempotencyAdmissionActor.cs:1095`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyAdmissionActor.cs#L1095)

- Aggregate actor exposes only internal exact source inspection and irreversible redirect seams.
  [`AggregateActor.cs:164`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L164)

**Verification and operations**

- Coordinator tests prove unsafe inventory performs zero lifecycle, directory, or admission work.
  [`IdempotencyAdmissionActorTests.cs:739`](../../tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyAdmissionActorTests.cs#L739)

- Lifecycle tests cover every checkpoint, response loss, expiry, and forward rotation.
  [`IdempotencyTenantLifecycleActorTests.cs:305`](../../tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyTenantLifecycleActorTests.cs#L305)

- Inventory tests exercise closure corruption, transition tampering, rollback, and governed purge.
  [`IdempotencyLegacyInventoryActorTests.cs:33`](../../tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyLegacyInventoryActorTests.cs#L33)

- Live Dapr/Redis proof migrates, restarts, reproves, and replays without duplicate domain work.
  [`IdempotencyAdmissionLiveSidecarTests.cs:232`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Actors/IdempotencyAdmissionLiveSidecarTests.cs#L232)

- Operator guidance freezes closure, rollback, rotation, expiry, purge, and leakage rules.
  [`configuration-reference.md:280`](../../docs/guides/configuration-reference.md#L280)
