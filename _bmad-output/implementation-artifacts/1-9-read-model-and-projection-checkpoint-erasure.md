---
baseline_commit: 19e0c1997cd26b03d2fd780b31455e0a29dc175a
---

# Story 1.9: Read-Model And Projection Checkpoint Erasure

Status: review

**Requirements covered:** FR5, FR36, NFR2, NFR16
**Governed by:** AD-7 (Read Models And Cursors Use Platform Seams), AD-12 (High-Risk Verification Requires Persisted Evidence), AD-2 (Domain Modules Stay Domain-Centric)

## Story

As a domain projection maintainer,
I want generic read-model and checkpoint erasure,
so that removing or recreating an aggregate cannot leave stale projection state that discards valid future events.

## Background — the concrete bug this closes

`ProjectionUpdateOrchestrator.UpdateProjectionAsync` (`src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs:111-153`) reads a per-aggregate delivery checkpoint before every projection delivery:

```csharp
long lastDeliveredSequence = await checkpointTracker.ReadLastDeliveredSequenceAsync(identity, cancellationToken)...
...
long highestAvailableSequence = events.Max(e => e.SequenceNumber);
if (lastDeliveredSequence > highestAvailableSequence) {
    Log.CheckpointDriftDetected(...);
    return; // projection delivery is silently skipped
}
```

If an aggregate is deleted and recreated with the **same identifier** but the checkpoint at `projection-checkpoints:{TenantId}:{Domain}:{AggregateId}` (state store `ProjectionOptions.CheckpointStateStoreName`, default `"statestore"`) is never cleared, the stale `lastDeliveredSequence` from the old aggregate is higher than the new aggregate's sequence-1 event, so `CheckpointDriftDetected` fires and delivery is **silently dropped** — the recreated aggregate never gets a projection. This is documented as the "still blocked" G3 finding in `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md` (lines 41-58). This story closes that gap generically at the platform level.

## Acceptance Criteria

1. `IReadModelStore` (`src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`) exposes a new asynchronous, cancellation-aware, ETag-aware erase operation (mirrors `TrySaveAsync`'s shape/idempotency contract, not `SaveAsync`'s unconditional one). Deleting an already-absent key returns success (idempotent no-op), and an ETag mismatch against a present key returns a conflict result — never an exception for either case.
2. `DaprReadModelStore` and `InMemoryReadModelStore` both implement the new erase operation with equivalent observable behavior for: success, absent-key, ETag-conflict, cancellation, and retry. `InMemoryReadModelStore` gains a deterministic partial-failure/conflict injection hook for the new operation (same spirit as the existing `ConcurrentWriteBeforeTrySave` hook).
3. A platform-owned coordinated erase operation, given an `AggregateIdentity` and a caller-supplied set of read-model `(storeName, key)` targets, erases every supplied read-model key **and** the companion projection-delivery checkpoint (`ProjectionCheckpointTracker`'s per-aggregate `ProjectionCheckpoint` at `"projection-checkpoints:" + identity.ActorId`) as one logical, tenant/domain/aggregate/projection-scoped operation.
4. When every key in the coordinated erase (including the checkpoint key) resolves to the same DAPR state-store component name, the operation issues one atomic `DaprClient.ExecuteStateTransactionAsync` transaction. When they don't share a store (or the backend has no transaction support), the operation uses a documented resumable protocol: partial completion is never reported as success, and retrying the same request converges to the fully-erased end state without side effects from the partial attempt.
5. After erasure completes for an `AggregateIdentity`, a new event stream created under the **same** identifier is delivered starting at sequence 1 without triggering `CheckpointDriftDetected` / being silently dropped by `ProjectionUpdateOrchestrator` (persisted-path proof, not a mock assertion — see AD-12).
6. Tenant isolation: an erase request whose `AggregateIdentity`/targets belong to tenant A cannot delete or reveal any state keyed under tenant B. Persisted-state tests assert the denial **and** both tenants' unaffected end state (reuse `Hexalith.EventStore.Testing.Assertions.StorageKeyIsolationAssertions.AssertKeyBelongsToTenant`, following the pattern in `tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs`).
7. Out of scope — this story must not touch, and tests must not assert against: event streams, snapshots, broker/pub-sub history, backups, audit evidence, or cryptographic keys (that remains GDPR-1, `_bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md`), and `IProjectionRebuildCheckpointStore` / `ProjectionRebuildCheckpoint` operator-rebuild-progress state (`src/Hexalith.EventStore.Server/Projections/ProjectionRebuildCheckpointStore.cs`), which is Story 1.14's correctness surface, not a per-aggregate delivery high-water mark.
8. The new erase/transaction logic lives entirely inside platform projects (`Hexalith.EventStore.Client`, `Hexalith.EventStore.Server`) — never inside a domain module. `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` (its `DaprStateAccessMarkers` list already includes `DeleteStateAsync` and `ExecuteStateTransactionAsync`) must continue to pass unmodified.

## Tasks / Subtasks

- [x] Task 1 — `IReadModelStore` single-key erase (AC: 1, 2)
  - [x] Add an ETag-aware erase method to `IReadModelStore` (`src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`), documented with the same `<param>`/`<returns>` style as `TrySaveAsync`. Absent-key ⇒ returns success/true idempotently; ETag-mismatch on a present key ⇒ returns false (conflict), never throws.
  - [x] Implement in `DaprReadModelStore` (`src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`) using `DaprClient.TryDeleteStateAsync(storeName, key, etag, stateOptions, metadata, cancellationToken)` (already referenced by `RecordingDaprClient.cs:142` as a real `DaprClient` override point — not yet used in production code). Validate args the same way as the existing three methods (`ArgumentException.ThrowIfNullOrWhiteSpace` / `ArgumentNullException.ThrowIfNull`).
  - [x] Implement in `InMemoryReadModelStore` (`src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`): mirror `TrySaveAsync`'s ETag-matching logic (`bool matches = exists ? etag == current.ETag : ...`), but for erase, absent-key must be `true` regardless of the supplied ETag (idempotent), and a present key requires the ETag to match or returns `false`. Add a deterministic failure-injection hook analogous to `ConcurrentWriteBeforeTrySave`.
  - [x] Extend `tests/Hexalith.EventStore.Client.Tests/Projections/RecordingDaprClient.cs`: replace the `NotSupportedException` stub for `TryDeleteStateAsync` (line ~142) with a real override that captures `storeName`/`key`/`etag`/`stateOptions`, mirroring the existing `TrySaveStateAsync` override (lines 27-42).
  - [x] Add/extend tests in `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs` and `InMemoryReadModelStoreTests.cs` covering success, absent-key idempotency, ETag conflict, and cancellation — follow the existing `[Fact]`/`[Theory]` + Shouldly style already in those files.

- [x] Task 2 — `IProjectionCheckpointTracker` erase (AC: 3, 4, 5)
  - [x] Add an erase method to `IProjectionCheckpointTracker` (`src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`) for the per-aggregate delivery checkpoint, keyed the same way as `ReadLastDeliveredSequenceAsync`/`SaveDeliveredSequenceAsync` (`GetStateKey(identity) = "projection-checkpoints:" + identity.ActorId` in `options.Value.CheckpointStateStoreName`). Same idempotent-absent / ETag-aware contract as Task 1. Do **not** touch `TrackIdentityAsync`'s scope/identity index — erasing a checkpoint value does not need to remove the aggregate from the enumeration index (leaving a stale index entry pointing at an absent checkpoint is already a state the poller must tolerate; do not expand this story into index-pruning unless a test proves the poller mishandles it).
  - [x] Implement in `ProjectionCheckpointTracker` (`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`), consistent with its existing raw-`DaprClient` style (it does not use `IReadModelStore`).
  - [x] Build the coordinated multi-target erase entry point required by AC3/AC4: given the caller's read-model `(storeName, key, etag)` targets plus the checkpoint's own `(CheckpointStateStoreName, GetStateKey(identity), checkpointEtag)`, execute one `DaprClient.ExecuteStateTransactionAsync` when every target shares one `storeName` (DAPR transactions are single-store only — confirmed, no cross-store transaction exists in the SDK), else fall back to sequential per-key erase calls using the Task 1 idempotent-absent primitive (safe to retry because each individual erase is itself idempotent). Recommended home for this coordinating logic: a new small Server-project type composing `IReadModelStore` + `IProjectionCheckpointTracker` (Server already depends on Client per the architecture stack diagram), rather than adding checkpoint-shaped knowledge to the Client project. Confirm this placement against `AD-2`/`AD-7` intent before finalizing; the acceptance criteria are the fixed contract, this specific composition is a recommendation.
  - [x] Add a persisted-path test proving AC5 end-to-end: erase an aggregate's checkpoint (and read-model key), post a fresh sequence-1 event under the same `AggregateIdentity`, and assert `ProjectionUpdateOrchestrator` delivers it instead of hitting `CheckpointDriftDetected` (existing precedent for asserting that log path: `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs:1436` and `:1469`).

- [x] Task 3 — Same-store transaction path (AC: 4)
  - [x] Extend `RecordingDaprClient.cs`'s `ExecuteStateTransactionAsync` override (currently `throw new NotSupportedException()` at line ~74) to capture `storeName`/`operations` and return a configurable result, following the `TrySaveStateAsync` capture pattern.
  - [x] Reference pattern for building `StateTransactionRequest[]` (fetch-ETag → build requests → `ExecuteStateTransactionAsync` → catch `Dapr.DaprException` → bounded retry) is demonstrated in the sibling reference project `references/Hexalith.Memories/src/Hexalith.Memories.Server/Tenants/TenantRegistryService.cs` (read-only reference — do not modify that submodule). No such pattern exists yet inside this repo's own `src/`.
  - [x] Test: all-targets-same-store erase issues exactly one `ExecuteStateTransactionAsync` call with the expected `StateOperationType.Delete` requests.

- [x] Task 4 — Resumable cross-store protocol (AC: 4)
  - [x] Document (XML doc + Dev Notes reference) the exact resumable protocol: order of operations, what "partial completion" state looks like, and why retrying it is safe (every underlying erase is independently idempotent-absent, so re-issuing the same coordinated erase after a partial failure converges without re-deleting already-erased keys or reporting false success).
  - [x] Test: inject a failure between the read-model erase and the checkpoint erase (via the Task 1 failure-injection hook) and assert (a) the operation does not report success, and (b) retrying it completes and reaches the fully-erased end state.

- [x] Task 5 — Tenant isolation proof (AC: 6)
  - [x] Add a persisted-state test (pattern: `tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs`) that erases tenant A's scope and asserts tenant B's read-model and checkpoint state is untouched, using `StorageKeyIsolationAssertions.AssertKeyBelongsToTenant`.

- [x] Task 6 — Scope guardrails (AC: 7, 8)
  - [x] Do not add any method to `IProjectionRebuildCheckpointStore` / `ProjectionRebuildCheckpointStore.cs` in this story.
  - [x] Do not add event-stream, snapshot, broker-history, backup, audit, or crypto-key deletion anywhere.
  - [x] Run `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs` unmodified and confirm it still passes (proves the new logic didn't leak a raw-DAPR-state-access pattern into a domain module).

## Dev Notes

### Current interface shape (before this story)

`IReadModelStore` (`src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`) has exactly three members today: `GetAsync`, `SaveAsync` (unconditional), `TrySaveAsync` (ETag-aware, first-write-wins via `DaprClient.TrySaveStateAsync(..., new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, ...)`). There is **no** delete/erase member — this is the exact "no public delete/erase hook was identified" gap recorded in Story 1.8's proof packet.

`IProjectionCheckpointTracker` (`src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`) has `ReadLastDeliveredSequenceAsync`, `SaveDeliveredSequenceAsync` (monotonic — never lowers an existing higher sequence), `TrackIdentityAsync`, `EnumerateTrackedIdentitiesAsync`. No erase member.

### DAPR SDK facts confirmed against this codebase (Dapr .NET SDK 1.18.4, `Directory.Packages.props`)

- `DaprClient.TryDeleteStateAsync(storeName, key, etag, stateOptions?, metadata?, cancellationToken?) -> Task<bool>` — the ETag-conditional single-key delete to use for Task 1. Confirmed as a real override point via `RecordingDaprClient.cs:142`; not used anywhere in production code today.
- `DaprClient.DeleteStateAsync(storeName, key, stateOptions?, metadata?, cancellationToken?) -> Task` — unconditional delete. Already used once, unrelated context: `src/Hexalith.EventStore.Admin.Server/Services/DaprHealthHistoryCollector.cs:220`.
- `DaprClient.ExecuteStateTransactionAsync(storeName, IReadOnlyList<StateTransactionRequest>, metadata?, cancellationToken?) -> Task` with `StateTransactionRequest(key, value, StateOperationType, etag?, metadata?, options?)` — **all operations in one call must target the same `storeName`** (single state-store component; there is no cross-store DAPR transaction). No production usage exists in this repo yet; only a `NotSupportedException` stub in `RecordingDaprClient.cs:74` and a forbidden-marker entry in the domain-module guardrail test.

### Where the checkpoint that must be erased actually lives

`ProjectionCheckpointTracker.GetStateKey(AggregateIdentity identity)` (`src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs:243-246`) returns `"projection-checkpoints:" + identity.ActorId`, stored in `options.Value.CheckpointStateStoreName` (`ProjectionOptions.CheckpointStateStoreName`, default `"statestore"` — `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs:12`). This is the exact key `ProjectionUpdateOrchestrator` reads/writes at the two call sites cited in the Background section above. `"statestore"` is also the default/typical store name domain read models use (see `TenantProjectionHandler`'s `StateStoreName = "statestore"` below), which is why the same-store atomic-transaction path (AC4) is the expected common case, not an edge case.

### Explicitly out of scope: the *other* checkpoint store

`IProjectionRebuildCheckpointStore` / `ProjectionRebuildCheckpointStore.cs` (`src/Hexalith.EventStore.Server/Projections/`) is a **separate** subsystem tracking active *operator-triggered* rebuild progress (keyed by `ProjectionRebuildCheckpointScope(Tenant, Domain, ProjectionName, AggregateId?, OperationId?)`, with `Succeeded`/`Failed`/`Canceled` status, an active-rebuild index, and its own cleanup service). It is not a passive per-aggregate high-water mark and is not what blocks a recreated aggregate in the Background scenario — `ProjectionUpdateOrchestrator` only checks `HasActiveOperatorRebuildForDomainAsync` against it (collision avoidance with an in-flight rebuild), not a stale-sequence gate. This store is Story 1.14's ("Correct Paged Rebuild And Replay Equivalence") correctness surface. Do not add erasure here — see AC7/Task 6.

### Read-model key composition is caller-owned, not platform-derived

`IReadModelStore` is key-format-agnostic — callers compose their own keys. Real example, `references/Hexalith.Tenants/src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs:24-30` (read-only reference, do not modify): `StateStoreName = "statestore"`, per-aggregate key `"projection:tenants:" + aggregateId`, plus a **cross-aggregate singleton** index key `"projection:tenant-index:singleton"`. This is why AC3 requires the *caller* to supply the set of read-model targets to erase (the platform cannot generically discover "all read-model keys for aggregate X" — a domain may have per-aggregate keys, singleton/index keys it merges into, or both, and only the domain knows which). The platform's job is the generic, ETag-aware, coordinated erase primitive plus the checkpoint erasure it owns outright — not discovering domain-specific key layouts.

### Tenant isolation precedent to reuse for AC6

`AggregateIdentity.ActorId => $"{TenantId}:{Domain}:{AggregateId}"` (`src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs:55`) is the repo-wide composite-key convention, constructor-validated so `:` cannot appear inside any component (tenants/domains/aggregates cannot collide across the key space). `Hexalith.EventStore.Testing.Assertions.StorageKeyIsolationAssertions.AssertKeyBelongsToTenant` (`src/Hexalith.EventStore.Testing/Assertions/StorageKeyIsolationAssertions.cs:18`) already encodes this assertion; reuse it rather than hand-rolling a new check. Precedent test file: `tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs`.

### Idempotent-absent / ETag-conflict pattern to mirror

`InMemoryReadModelStore.TrySaveAsync` (`src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs:66-95`) is the reference idiom:

```csharp
bool exists = _entries.TryGetValue(composite, out Entry? current);
bool matches = exists ? string.Equals(current!.ETag, etag, StringComparison.Ordinal) : etag.Length == 0;
if (!matches) { return Task.FromResult(false); }
```

For erase, invert the "absent" branch: absent ⇒ always a successful idempotent no-op (regardless of supplied ETag), present-with-mismatched-ETag ⇒ conflict (`false`).

### Testing standards (from project context + this codebase)

- xUnit v3 (`[Fact]`/`[Theory]`), Shouldly assertions (`ShouldBe`, `ShouldBeTrue`, etc.) — never raw `Assert.*`.
- Two DAPR-mocking conventions coexist: `RecordingDaprClient` (hand-rolled fake, used specifically for `DaprReadModelStoreTests`) and `Substitute.For<DaprClient>()` (NSubstitute, used elsewhere e.g. `tests/Hexalith.EventStore.Client.Tests/Commands/DaprStreamActivityTrackerTests.cs`). Extend `RecordingDaprClient` for the new `DaprReadModelStore` erase/transaction methods to stay consistent with the existing `DaprReadModelStoreTests.cs` file; NSubstitute's sequential `.Returns(false, true)` + `Received(2)` pattern (`DaprStreamActivityTrackerTests.TrackAsync_EtagMismatch_RetriesUntilSaveSucceeds`) is the reusable model for asserting ETag-conflict-then-retry elsewhere in this story if a substitute-based test is more natural (e.g. `ProjectionCheckpointTracker` tests, which likely already use NSubstitute given its Server.Tests conventions).
- Run test projects individually, never solution-level `dotnet test`.
- AD-12 requires persisted-state evidence, not mock-call-count evidence, for the cross-tenant denial (AC6) and stale-checkpoint (AC5) proofs specifically — assert actual store/fake end-state (`InMemoryReadModelStore.Snapshot<T>(...)`, or captured `RecordingDaprClient` args), not just "the method returned true."
- `ConfigureAwait(false)` on every awaited call in production code (CA2007 is a build-breaking warning in this repo).

### Project Structure Notes

- No new projects/files outside the existing platform layout are needed. Touch points: `src/Hexalith.EventStore.Client/Projections/` (interface + Dapr impl), `src/Hexalith.EventStore.Testing/Fakes/` (in-memory fake), `src/Hexalith.EventStore.Server/Projections/` (checkpoint tracker + new coordinating type), plus their mirrored test projects.
- No `.csproj` package-version edits needed — `Dapr.Client` is already referenced by both `Client` and `Server` projects at the centrally-pinned version.
- Do not modify `references/Hexalith.Tenants` or `references/Hexalith.Memories` (submodules; cited here only as read-only precedent).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story-1.9-Read-Model-And-Projection-Checkpoint-Erasure] (lines 657-696)
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-7] (lines 85-89), [#AD-12] (lines 115-119), [#AD-2] (lines 55-59)
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-11.md#4.4] (lines 243-247) — the authoritative rewrite of this story's scope
- [Source: _bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-proof-packet.md#G3-read-model-erasure-hooks] (lines 41-58) — the exact blocker this story closes
- [Source: _bmad-output/planning-artifacts/backlog/gdpr-1-aggregate-erasure.md] — write-model/event-stream erasure boundary (out of scope)
- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`
- `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
- `src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs`
- `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`
- `src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionCheckpoint.cs`
- `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- `src/Hexalith.EventStore.Server/Configuration/ProjectionOptions.cs`
- `src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs`
- `src/Hexalith.EventStore.Testing/Assertions/StorageKeyIsolationAssertions.cs`
- `tests/Hexalith.EventStore.Client.Tests/Projections/RecordingDaprClient.cs`
- `tests/Hexalith.EventStore.DomainService.Tests/DomainModuleAuthoringGuardrailTests.cs`

## Validation

Run focused validation first, then the broadest practical EventStore validation lane:

```bash
dotnet test tests/Hexalith.EventStore.Client.Tests/
dotnet test tests/Hexalith.EventStore.Testing.Tests/
dotnet test tests/Hexalith.EventStore.Server.Tests/
dotnet test tests/Hexalith.EventStore.DomainService.Tests/
dotnet build Hexalith.EventStore.slnx --configuration Release
git diff --check
```

All configured tests must pass before this story is marked complete (per project-context testing rules); `Hexalith.EventStore.Server.Tests` has a pre-existing unrelated CA2007 build gap documented as a known baseline exclusion — do not treat that specific pre-existing red as caused by this story, but do not introduce any *new* CA2007 violations either.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-11: Task 1 RED — Client tests failed to compile because `TryEraseAsync` and the erase injection hook did not exist.
- 2026-07-11: Task 1 GREEN — Client 563/563, Testing 144/144, Server 2272 passed (25 skipped), DomainService 85/85.
- 2026-07-11: Task 2 RED — Server focused build failed because checkpoint erase and coordinated eraser types did not exist.
- 2026-07-11: Task 2 GREEN — focused tracker 27/27 and orchestrator 55/55; regression Client 563/563, Testing 144/144, Server 2276 passed (25 skipped), DomainService 85/85.
- 2026-07-11: Task 3 RED — recorder-backed same-store test failed on the transaction override's `NotSupportedException`.
- 2026-07-11: Task 3 GREEN — atomic-path test 1/1; regression Client 564/564, Testing 144/144, Server 2276 passed (25 skipped), DomainService 85/85.
- 2026-07-11: Task 4 GREEN — resumable-protocol tests 2/2; regression Client 565/565, Testing 144/144, Server 2276 passed (25 skipped), DomainService 85/85.
- 2026-07-11: Task 5 GREEN — storage-isolation tests 33/33; regression Client 565/565, Testing 144/144, Server 2277 passed (25 skipped), DomainService 85/85.
- 2026-07-11: Task 5 final audit RED/GREEN — forged tenant label initially bypassed ownership; physical tenant-prefix validation added, then isolation 33/33, orchestrator 55/55, and coordinated-erasure 2/2 passed.
- 2026-07-11: Task 6 GREEN — scope diff excludes rebuild/write-model surfaces and the unmodified domain-module authoring guardrails pass 25/25.
- 2026-07-11: Final gate GREEN — Client 565/565, Testing 144/144, Server 2277 passed (25 skipped), DomainService 85/85; Release solution build 0 warnings/errors; `git diff --check` clean.

### Completion Notes List

- Implemented Task 1 with ETag-aware, cancellation-aware erase behavior in the public store seam and both DAPR/in-memory implementations; absent keys are idempotent and present-key conflicts preserve state.
- Implemented Task 2 with an ETag-aware checkpoint erase seam and a Server-owned coordinated eraser. Persisted fake-store proof confirms stale read-model/checkpoint removal, fresh sequence-1 projection delivery, no drift signal, and a new sequence-1 checkpoint.
- Implemented Task 3 recorder support and exact atomic-batch verification: one DAPR transaction contains every read-model delete plus the checkpoint delete with caller ETags and first-write concurrency.
- Implemented Task 4 documentation and persisted partial-failure proof: read models erase in request order, checkpoint last, partial completion returns false, and retry converges safely through absent-key idempotency.
- Implemented Task 5 fail-closed target ownership: both the declared owner and physical tenant-prefixed key must match the operation identity; forged labels return false before DAPR mutation, with both tenants' persisted end state unchanged.
- Completed Task 6 scope audit: only Client/Server projection state and test infrastructure changed; rebuild checkpoints and write-model/event-history deletion remain untouched.
- Story complete: all acceptance criteria and tasks are satisfied, all configured validation lanes pass, and the implementation is ready for review.

### File List

- _bmad-output/implementation-artifacts/1-9-read-model-and-projection-checkpoint-erasure.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs
- src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs
- src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs
- src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs
- src/Hexalith.EventStore.Server/Projections/IProjectionCheckpointTracker.cs
- src/Hexalith.EventStore.Server/Projections/IProjectionStateEraser.cs
- src/Hexalith.EventStore.Server/Projections/ProjectionCheckpointTracker.cs
- src/Hexalith.EventStore.Server/Projections/ProjectionStateEraser.cs
- src/Hexalith.EventStore.Server/Projections/ReadModelEraseTarget.cs
- tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs
- tests/Hexalith.EventStore.Client.Tests/Projections/InMemoryReadModelStoreTests.cs
- tests/Hexalith.EventStore.Client.Tests/Projections/ProjectionStateEraserTests.cs
- tests/Hexalith.EventStore.Client.Tests/Projections/RecordingDaprClient.cs
- tests/Hexalith.EventStore.Server.Tests/Projections/Dw1PollerCorruptionAtddTests.cs
- tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionCheckpointTrackerTests.cs
- tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionPollerServiceTests.cs
- tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs
- tests/Hexalith.EventStore.Server.Tests/Security/StorageKeyIsolationTests.cs

## Change Log

- 2026-07-11: Added ETag-aware single-key erasure, atomic same-store and resumable cross-store coordinated projection erasure, tenant-key isolation guards, persisted recreation proof, and comprehensive tests.
