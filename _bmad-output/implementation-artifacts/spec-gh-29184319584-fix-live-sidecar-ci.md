---
title: 'Fix resumable batch Redis compaction in CI'
type: 'bugfix'
created: '2026-07-12'
status: 'done'
baseline_commit: '67b462de3f87993b72671e68c760573a83f96bc4'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/1-10-coordinated-read-model-batch-writes.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run 29184319584 fails two new live-sidecar tests because the resumable read-model batch protocol installs Redis values with FirstWrite ETags, then attempts unconditional LastWrite compaction. Dapr Redis rejects that transition, leaving committed envelopes behind and returning `Indeterminate` instead of durable completion.

**Approach:** Make post-install compaction and pre-commit compensation compare-and-set transitions using the freshly read envelope ETag for both writes and deletes. Preserve truthful reconciliation: an ETag race must be re-read and proven converged or remain non-successful, never overwrite a concurrent value.

## Boundaries & Constraints

**Always:** Preserve the public batch API and fingerprint format; keep marker-gated visibility and terminal receipts; use raw byte-state APIs; keep DAPR and in-memory accessors behaviorally equivalent; assert persisted Redis end state and unchanged checkpoints.

**Ask First:** Any public API change, fingerprint/marker/envelope format change, dependency change, or modification inside a root-declared submodule.

**Never:** Weaken or skip the live-sidecar lane; switch Redis to the transaction-qualified profile; hide the failure by changing assertions; use unconditional writes/deletes to replace an installed envelope; modify projection checkpoints or unrelated dirty files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Successful compaction | Committed marker and owned write envelopes | Candidate values replace envelopes using their current ETags; terminal receipt is retained | Return `Completed` only after durable proof |
| Successful delete | Committed marker and owned delete envelope | Envelope is conditionally deleted and receipt completes | Absence is accepted only when proven |
| Pre-commit conflict | Prepared marker after a prefix of envelopes was installed | Previous values/absence are restored using current envelope ETags | Return optimistic conflict after compensation is proven |
| Compaction race | Envelope changes after it is read | Do not overwrite the changed value | Reconcile and prove convergence or return non-success |
| Identity retry | Completed receipt with same/different fingerprint | Same fingerprint is already-completed; different fingerprint is identity conflict | No new logical mutation |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Client/Projections/ReadModelBatchProtocol.cs` -- resumable install, commit, ETag-aware compaction, compensation, reconciliation, and receipt transitions.
- `src/Hexalith.EventStore.Client/Projections/IReadModelBatchStateAccessor.cs` -- internal raw state seam requiring conditional delete support.
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelBatchStateAccessor.cs` -- maps conditional writes/deletes to pinned Dapr.Client byte/state APIs.
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs` -- in-memory accessor parity and deterministic race simulation.
- `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelBatchTests.cs` -- DAPR adapter request-shape and regression coverage.
- `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelBatchStoreTests.cs` -- protocol compaction, compensation, and race outcomes.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs` -- authoritative Redis persisted-state proof for the failed CI lane.

## Tasks & Acceptance

**Execution:**
- [x] `IReadModelBatchStateAccessor.cs`, `DaprReadModelBatchStateAccessor.cs`, and `InMemoryReadModelStore.cs` -- add ETag-conditional delete parity and remove protocol reliance on unconditional envelope replacement.
- [x] `ReadModelBatchProtocol.cs` -- compact and compensate owned envelopes with freshly read ETags; verify any raced transition before classifying completion/conflict; retain the receipt through a guarded marker transition.
- [x] `DaprReadModelBatchTests.cs` and `ReadModelBatchStoreTests.cs` -- cover conditional write/delete request shape, successful convergence, and a raced transition that cannot be reported as success.
- [x] `ReadModelBatchLiveSidecarTests.cs` -- retain direct Redis assertions and add delete/compensation evidence if deterministic coverage does not already prove those paths against Redis.

**Acceptance Criteria:**
- Given Redis keys installed with FirstWrite ETags, when a resumable batch commits, then detail/index values are compacted, the completion receipt is durable, the checkpoint is unchanged, and retry is already-completed.
- Given an installed envelope must be deleted or restored, when compaction or compensation runs, then the transition uses the envelope's current ETag and never overwrites a raced value.
- Given the same batch identity is reused with another fingerprint, when it executes after completion, then it returns identity conflict and the original compacted value remains unchanged.
- Given the focused and full live-sidecar lanes run, when results are inspected, then all tests pass and Redis contains no pending envelope for completed batches.

## Spec Change Log

## Design Notes

Dapr Redis records `first-write=0` on a FirstWrite install. Its LastWrite request supplies ETag `0`, which cannot replace that versioned key; the current implementation therefore commits visibility but fails during cleanup. The repair follows Story 1.10's existing requirement to restore/compact with internally re-read ETags and makes delete behavior symmetric with write behavior.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Release -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: clean build with warnings as errors.
- `dotnet test tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Release --no-build` -- expected: all deterministic client tests pass.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj --configuration Release` -- expected: in-memory parity tests pass.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false` -- expected: the workflow-equivalent live-sidecar lane passes, including both failures from run 29184319584.
- `git diff --check` -- expected: no whitespace errors.

**Observed results (2026-07-12):**
- `Hexalith.EventStore.Client` Release build: passed with 0 warnings and 0 errors.
- Fresh `Hexalith.EventStore.Client.Tests` assembly: 637 passed, 0 failed, including four marker-CAS race cases added during adversarial review.
- Fresh `Hexalith.EventStore.Testing.Tests` assembly: 144 passed, 0 failed.
- Fresh full live-sidecar assembly against DAPR/Redis: 29 passed, 0 failed; the failed CI class passed 3/3 with direct write/delete/receipt/checkpoint evidence.
- Scoped `git diff --check`: passed.
- The broad test-project rebuild is separately blocked by out-of-scope story 1-9 working-tree deletions while `ServiceCollectionExtensions.cs:56` still references `IProjectionStateEraser` and `ProjectionStateEraser`; focused project builds and direct xUnit v3 assembly execution followed the repository fallback ladder.

## Suggested Review Order

**Resumable protocol safety**

- Start with ETag-guarded compaction, receipt proof, and final verification.
  [`ReadModelBatchProtocol.cs:337`](../../src/Hexalith.EventStore.Client/Projections/ReadModelBatchProtocol.cs#L337)

- Follow compensation through abort transitions without overwriting concurrent values.
  [`ReadModelBatchProtocol.cs:412`](../../src/Hexalith.EventStore.Client/Projections/ReadModelBatchProtocol.cs#L412)

- Confirm DAPR maps both writes and deletes to FirstWrite compare-and-set calls.
  [`DaprReadModelBatchStateAccessor.cs:29`](../../src/Hexalith.EventStore.Client/Projections/DaprReadModelBatchStateAccessor.cs#L29)

- Review the minimal internal accessor contract that removes unconditional envelope mutation.
  [`IReadModelBatchStateAccessor.cs:18`](../../src/Hexalith.EventStore.Client/Projections/IReadModelBatchStateAccessor.cs#L18)

**Fake parity and race behavior**

- Check in-memory conditional write/delete behavior mirrors the DAPR seam.
  [`InMemoryReadModelStore.cs:233`](../../src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs#L233)

**Verification evidence**

- See authoritative Redis write/delete/receipt/checkpoint and identity-reuse proof first.
  [`ReadModelBatchLiveSidecarTests.cs:39`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReadModelBatchLiveSidecarTests.cs#L39)

- Verify recorder assertions reject unconditional compaction and compensation requests.
  [`DaprReadModelBatchTests.cs:45`](../../tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelBatchTests.cs#L45)

- Inspect receipt and abort marker-CAS ownership races added during review.
  [`DaprReadModelBatchTests.cs:173`](../../tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelBatchTests.cs#L173)

- Confirm compensation races preserve concurrent values and remain indeterminate.
  [`ReadModelBatchStoreTests.cs:217`](../../tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelBatchStoreTests.cs#L217)

- Confirm compaction races cannot become successful completion.
  [`ReadModelBatchStoreTests.cs:297`](../../tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelBatchStoreTests.cs#L297)

**Concurrent-work boundary**

- Review confirmed out-of-scope Story 1.9 findings separately from this fix.
  [`deferred-work.md:198`](deferred-work.md#L198)

- The active Story 1.9 contract owns the unrelated dirty erasure changes.
  [`1-9-read-model-and-projection-checkpoint-erasure.md:58`](1-9-read-model-and-projection-checkpoint-erasure.md#L58)
