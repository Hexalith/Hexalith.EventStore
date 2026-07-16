---
created: 2026-07-15
story_id: "1.19"
story_key: 1-19-correct-paged-rebuild-and-replay-equivalence
status: done
supersedes: 1-14-correct-paged-rebuild-and-replay-equivalence.md
crosswalk: ../planning-artifacts/story-id-migration-2026-07-15.md
---

# Story 1.19: Correct Paged Rebuild And Replay Equivalence

Status: done

## Reissue Decision

This is the active review identity for historical Story 1.14. Its implementation, task
history, test evidence, and review findings remain in
`1-14-correct-paged-rebuild-and-replay-equivalence.md`; renumbering does not reset or
duplicate development work.

## Acceptance Boundary

- Rebuild handlers use explicit full-replay or incremental semantics; a page is never
  presented as a complete stream.
- Operation-scoped staging preserves the last complete live model until durable promotion.
- Bounded pages neither skip, duplicate, nor reorder events and match canonical replay at
  the same position.
- Cancel, failure, and resume keep live state intact and report only durable progress.
- Every normalized projection target produced by Story 1.17 completes before promotion;
  idempotency/checkpoint behavior follows Stories 1.15-1.18.
- Review evidence must include multi-page, multi-projection, failure/resume, and persisted
  read-back equivalence—not aggregate-only or mock-only proof.

Next action: complete the existing review under this identity and record its disposition;
do not rerun implementation solely because of the migration.

## Review Findings

- [x] [Review][Patch] Make legacy delivery lifecycle leases recoverable after a crash by using durable identity and bounded reclamation [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:288]
- [x] [Review][Patch] Retain named-delivery recovery work until every lifecycle lease is durably released [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:532]
- [x] [Review][Patch] Centralize terminal rebuild cleanup so cancel, fail, and success paths cannot strand `Rebuilding` lifecycles [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:463]
- [x] [Review][Patch] Persist each operation's frozen aggregate target before reading, staging, or promoting its prefix [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:841]
- [x] [Review][Patch] Acquire every required named-projection lifecycle fence before freezing and reading the replay prefix [src/Hexalith.EventStore.Server/Projections/NamedProjectionDispatchCoordinator.cs:74]
- [x] [Review][Patch] Revalidate durable operator ownership after named rebuild dispatch and atomically fence promotion against cancel or preemption [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:1065]
- [x] [Review][Patch] Reconcile or discard staged legacy candidates on every terminal, cancellation, crash-recovery, and new-operation path [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:1054]
- [x] [Review][Patch] Put named read models, legacy actor state, freshness/ETag state, and checkpoints behind one marker-gated visibility boundary [src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs:208]
- [x] [Review][Patch] Read back and verify every promoted output and persisted freshness version before advancing rebuild checkpoints [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:1130]
- [x] [Review][Patch] Enforce the byte safety ceiling incrementally before a complete page is materialized and decrypted [src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:1234]
- [x] [Review][Patch] Exercise the real DAPR rebuild-write gateway in the live-sidecar production-path equivalence test [tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/NamedProjectionDispatchLiveSidecarTests.cs:578]
- [x] [Review][Patch] Verify terminal named-rebuild failure discards the candidate, preserves the route reason, fails the checkpoint, and releases all lifecycles [tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:667]
- [x] [Review][Patch] Verify lifecycle coherence when a query actor returns a projection type different from the routed alias [tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs:662]
- [x] [Review][Defer] Define the pre-existing query-visibility policy for `Erasing` and unknown lifecycle phases [src/Hexalith.EventStore.Server/Queries/QueryRouter.cs:248] — deferred, pre-existing; already tracked in `deferred-work.md`

## Review Completion

Disposition: approved after patching all 13 in-scope findings. The pre-existing query-visibility
policy remains explicitly deferred in `deferred-work.md`.

Validation on 2026-07-16:

- Client tests: 673 passed.
- Domain service tests: 143 passed.
- Server tests: 2,620 passed, 25 skipped, 0 failed.
- Post-patch lifecycle tests: 22 passed; named coordinator tests: 16 passed.
- Real DAPR/Redis paged-rebuild equivalence test: 1 passed.
- Release solution build: succeeded with 0 warnings and 0 errors.
