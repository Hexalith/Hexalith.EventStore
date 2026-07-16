---
baseline_commit: 322e3193d22295153c74d16baee32a7e74f6d72a
created: 2026-07-12
---

# Story 4.1: Event Identity And Duplicate Result Fidelity

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an event consumer,
I want persisted event identity and duplicate command results to be stable and complete,
so that subscribers can deduplicate reliably and retried commands receive semantically identical responses.

## Acceptance Criteria

1. **Actor-allocated global positions.** Given a non-empty command result reaches the production `AggregateActor` persistence path, when events are persisted, then one contiguous range is reserved through the DAPR-backed `GlobalPositionActor`, every persisted event receives a unique non-zero `GlobalPosition`, and aggregate-local `SequenceNumber` values remain gapless and unchanged by the allocation. A failed aggregate commit may leave a gap in the reserved global positions; global positions are not promised to be gapless or strict commit order.
2. **Stable CloudEvent identity.** Given a persisted event is published or republished, when `EventPublisher` supplies DAPR CloudEvent metadata, then `cloudevent.id` equals that persisted envelope's `MessageId`. Re-publishing the same persisted event keeps the same id, while different events—including events from different aggregates with the same correlation id and local sequence—have distinct ids.
3. **Complete duplicate result fidelity.** Given idempotency state written with the corrected record shape is resolved as a duplicate, when the actor returns the cached command result, then all current `CommandProcessingResult` fields match the stored original: `Accepted`, `ErrorMessage`, `CorrelationId`, `EventCount`, `ResultPayload`, `BackpressureExceeded`, `BackpressurePendingCount`, and `BackpressureThreshold`. Both accepted and rejected/error results are covered; callers do not receive a degraded duplicate response.
4. **Production wiring and persistence evidence.** Given the EventStore server is composed normally, when registrations and focused tests execute, then `IGlobalPositionAllocator` resolves to the DAPR actor-backed implementation, `GlobalPositionActor` is registered, its committed state advances across allocations, persisted envelopes contain the allocated positions, and the testing fake continues to emit non-zero positions. The zero-producing no-op allocator remains a compatibility/direct-test fallback only and is not treated as compliant production behavior.
5. **Brownfield reconciliation.** Given commit `3ccb1054eba45ca171b51aa040fc8b02d62f7e06` and the frozen global-ordering spec already shipped the production behavior, when this story is implemented, then the existing seams are verified and preserved rather than re-created. Production code changes occur only if current-baseline verification proves a regression; the expected functional change is limited to strengthening caller-visible duplicate-result coverage.
6. **Green gates.** Given Story 4.1 changes are complete, when the focused server tests, full Server test project, Testing test project, and Release solution build run, then all configured tests pass with no warnings-as-errors regression. Evidence records current results rather than relying on the historical July 2 counts.

## Tasks / Subtasks

- [x] **Task 1 — Verify the shipped implementation against the frozen contract** (AC: 1, 2, 3, 4, 5)
  - [x] Re-read `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md` and preserve its frozen intent. Do not edit or reinterpret the spec in this story.
  - [x] Confirm `3ccb1054` remains an ancestor of the working baseline and inspect current diffs/history for the files listed in Dev Notes. Treat historical test results as context only.
  - [x] Verify the production path remains `AggregateActor` -> `EventPersister` -> one `IGlobalPositionAllocator.AllocateAsync(count)` call -> staged event/metadata writes -> actor-owned `SaveStateAsync`.
  - [x] If all production seams match the contract, make no production source change. If drift is found, repair only the narrow regression and document it.

- [x] **Task 2 — Re-prove global-position allocation and persistence** (AC: 1, 4)
  - [x] Preserve `GlobalPositionActor` actor id `global`, state key `current-global-position`, checked arithmetic, contiguous range return, and committed-state update.
  - [x] Preserve one range reservation per non-empty batch, independent aggregate-local sequence calculation, and non-zero production positions. Do not allocate for an empty/no-op result.
  - [x] Confirm `AddEventStoreServer` registers both `IGlobalPositionAllocator` and `GlobalPositionActor`; do not replace the actor with a process-local/static counter.
  - [x] Re-run the actor, persister, registration, and testing-fake evidence. Assert committed actor state and persisted envelopes—not only invocation counts.

- [x] **Task 3 — Re-prove stable CloudEvent ids** (AC: 2)
  - [x] Preserve `EventPublisher`'s `cloudevent.id = eventEnvelope.MessageId` assignment and the rest of the persisted envelope identity during publish-time payload unprotection/restamping.
  - [x] Preserve the tests proving: same persisted event republished -> same id; different events -> different ids; different aggregates with the same correlation and sequence -> different ids.
  - [x] Do not derive the id from correlation id, aggregate-local sequence, global position, or a newly generated publish-time value.

- [x] **Task 4 — Strengthen caller-visible duplicate-result fidelity** (AC: 3, 5)
  - [x] Update `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIdempotencyTests.cs` so an accepted duplicate seeds a complete `IdempotencyRecord` and asserts all eight returned `CommandProcessingResult` fields exactly.
  - [x] Strengthen the rejected duplicate case to assert `ErrorMessage`, correlation, event/payload defaults or values, and backpressure fields—not only `Accepted == false`.
  - [x] If useful, overload `AggregateActorTestHelper.ConfigureDuplicate` to accept an `IdempotencyRecord` or `CommandProcessingResult`; preserve existing callers and keep helper changes test-only.
  - [x] Keep the existing `IdempotencyRecordTests` JSON round-trip and `IdempotencyCheckerTests` field mapping coverage. Do not change production mapping unless the strengthened actor-path test exposes a real defect.

- [x] **Task 5 — Enforce scope and compatibility boundaries** (AC: 1, 2, 3, 5)
  - [x] Do not re-key idempotency/status/archive state, move tenant validation, redefine transient retryability, or change resume matching; those are Story 4.2 / FR27.
  - [x] Do not shard or re-scope global allocation and do not promise gapless global order; frozen-spec renegotiation and sharding belong to Story 4.6 / FR24.
  - [x] Do not change projection-handler marker/checkpoint semantics (Story 1.13), publication recovery (Story 4.4), public event/command contract names, topology, UI, or generated REST status-location behavior.
  - [x] Preserve additive defaults on `IdempotencyRecord` for older serialized records. Do not claim that legacy records can reconstruct values that were never persisted.
  - [x] Introduce no package changes, no `Guid.TryParse` for EventStore identifiers, no payload logging, and no missing `ConfigureAwait(false)` in production code.

- [x] **Task 6 — Validate and reconcile tracking** (AC: 6)
  - [x] Run the focused and full validation commands below. Record exact pass/fail/skip counts and any environment-specific fallback command.
  - [x] Confirm the only intended source diff is test coverage unless Task 1 found a verified regression.
  - [x] Update the Dev Agent Record and File List honestly; do not cite the July 2 verification as current execution.
  - [x] After implementation and review gates succeed, advance Story 4.1 through the normal workflow; do not mark downstream Epic 4 stories complete.

### Review Findings

- [x] [Review][Patch] Prove the exact DAPR allocator implementation and `GlobalPositionActor` registration [tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs:37]
- [x] [Review][Patch] Add executable coverage for non-zero, cross-aggregate `FakeEventPersister` global positions [src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs:18]
- [x] [Review][Patch] Exercise duplicate fidelity through production-shaped `CommandProcessingResult` to `IdempotencyRecord` round trips [tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIdempotencyTests.cs:37]
- [x] [Review][Patch] Reconcile the incomplete and prematurely finalized Dev Agent Record with exact gate accounting [4-1-event-identity-and-duplicate-result-fidelity.md:189]
- [x] [Review][Patch] Replace the stale post-edit caller-coverage source reference [4-1-event-identity-and-duplicate-result-fidelity.md:180]

## Dev Notes

### Top Guardrails

- **This is verify-and-reconcile work, not greenfield implementation.** The production behavior landed in `3ccb1054` before the formal Phase 4 PRD/epics existed. The frozen spec is `done`, while current sprint tracking says `backlog`; reconcile that planning history without duplicating the allocator, publisher, or idempotency types.
- **Expected product-code changes: none.** The current baseline already satisfies FR23. The concrete coverage gap is at the caller boundary: `AggregateActorIdempotencyTests` asserts only `Accepted` and `CorrelationId` for accepted duplicates and only `Accepted` for rejected duplicates.
- **Production allocator is mandatory.** `EventPersister` accepts an optional allocator to support direct construction and legacy tests, but normal server composition registers `DaprGlobalPositionAllocator`. Never present a zero `GlobalPosition` from `NoOpGlobalPositionAllocator` as a production-compliant result.
- **Global means monotonic/unique reservation, not gapless commit order.** The actor reserves before the aggregate commits. If the aggregate commit fails, the reserved range may become a gap. Only per-aggregate sequence is gapless.
- **Stable event identity survives recovery.** Re-publication must reuse persisted `MessageId`; Story 4.4 depends on this property for duplicate-safe recovery.
- **Duplicate fidelity is record-shape fidelity, not idempotency redesign.** Story 4.2 owns exact command matching, tenant-before-idempotency reads, retryable records, and `{tenant}:{messageId}` status/archive identity.
- **No UI impact.** HTTP acceptance or a duplicate cached reply remains command acceptance, not projection-confirmed UI success.

### Current Baseline Read During Story Creation

Baseline: `322e3193d22295153c74d16baee32a7e74f6d72a` on 2026-07-12. The shared worktree already contained an unrelated modified `references/Hexalith.Tenants` submodule pointer/worktree state; preserve it and do not include it in this story.

| File | Current state | Story action / preservation constraint |
| --- | --- | --- |
| `src/Hexalith.EventStore.Server/Actors/GlobalPositionActor.cs` | Reads the latest actor state, allocates `[current + 1 .. current + count]` with checked arithmetic, stages the last value, saves actor state, returns the first position. | Verify only. Preserve actor id/type, state key, turn-based serialization, checked overflow, and commit. |
| `src/Hexalith.EventStore.Server/Actors/IGlobalPositionActor.cs` | Remoting contract exposes `AllocateAsync(count)` and `GetCurrentAsync()`. | Verify only; no public/remote contract rename. |
| `src/Hexalith.EventStore.Server/Events/IGlobalPositionAllocator.cs` | Platform seam returns the first position of a reserved range. | Verify only; do not add a competing allocator abstraction. |
| `src/Hexalith.EventStore.Server/Events/DaprGlobalPositionAllocator.cs` | Uses actor type `GlobalPositionActor` and actor id `global`; validates count/cancellation before proxy invocation. | Verify only; no local counter or new store. |
| `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | Registers `DaprGlobalPositionAllocator` and `GlobalPositionActor` alongside existing server actors. | Verify registration; preserve all unrelated registrations/order. |
| `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` | Receives the allocator through DI, passes it to the per-call persister, commits staged event/pipeline state, and returns a cached duplicate result directly. | Verify only unless tests expose drift. Preserve tenant, pipeline, protection, recovery, status, and commit behavior. |
| `src/Hexalith.EventStore.Server/Events/EventPersister.cs` | Protects/serializes payloads, reserves one range, stamps local/global positions, generates one ULID-like `MessageId` per event, and stages event + metadata without calling `SaveStateAsync`. | Verify only. Preserve identity-scoped keys, payload metadata, one allocation per batch, and actor-owned commit. |
| `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` | Reconstructs the publish envelope without changing persisted identity and sets `cloudevent.id` to persisted `MessageId`. | Verify only. Preserve support-safe protection failure paths, sequential publication, DAPR resiliency ownership, and no payload logging. |
| `src/Hexalith.EventStore.Server/Actors/CommandProcessingResult.cs` | Canonical result has eight fields. | No shape change. Tests must enumerate every field. |
| `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs` | Stores all eight result fields plus `CausationId`/`ProcessedAt`; `FromResult`/`ToResult` map field-for-field with additive defaults. | Verify only unless caller-path test exposes a defect; preserve serialization compatibility. |
| `src/Hexalith.EventStore.Server/Actors/IdempotencyChecker.cs` | Reads/stages `idempotency:{causationId}` and delegates mapping to `IdempotencyRecord`; actor owns commit. | Verify result fidelity only. Do not re-key here in Story 4.1. |
| `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs` | Uses per-aggregate local sequence and a non-zero cross-aggregate in-memory position counter. | Verify parity; no test-only zero positions. |
| `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIdempotencyTests.cs` | Duplicate caller tests under-assert the complete result. | **UPDATE expected:** add full accepted and rejected/error result assertions. |
| `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` | `ConfigureDuplicate` currently seeds only accepted/correlation defaults. | **UPDATE optional:** add a compatible overload/helper for complete records. |

### Existing Focused Evidence To Preserve

- `GlobalPositionActorTests`: first allocation starts at 1 and persists current state; subsequent allocation continues; invalid count fails.
- `EventPersisterTests`: one allocation for a batch, local sequence `[11,12,13]`, global positions `[50,51,52]`, and distinct persisted `MessageId` values.
- `EventPublisherTests`: outbound `cloudevent.id` equals persisted `MessageId` and the publish envelope preserves persisted metadata.
- `SubscriberIdempotencyTests`: re-publishing one event keeps its id; different aggregates with the same correlation and local sequence use different ids.
- `IdempotencyRecordTests`: result mapping and serialized round-trip preserve correlation, accepted/error state, event count, payload, and all backpressure fields.
- `IdempotencyCheckerTests`: cache hit returns the complete corrected record and staged state carries event count/payload.
- `EventStoreServerServiceCollectionExtensionsTests`: global allocator registration exists.

Story-creation verification at baseline `322e3193`: the focused command below passed **81/81**, with 0 failed and 0 skipped. The dev agent must rerun it after its changes.

### Architecture Compliance

- **AD-1 / AD-5:** Keep event sourcing on DAPR; `AggregateActor` remains the only durable event mutation coordinator.
- **AD-6:** This story's invariant—gapless aggregate sequence, non-zero allocated position, persisted `MessageId` as CloudEvent id, complete duplicate results.
- **AD-8:** Pub/sub is at-least-once and unordered. Subscribers deduplicate by `MessageId`; `SequenceNumber` is never global ordering.
- **AD-9:** No topology change is expected. If a verified fix changes actor registration/app ids/YAML, AppHost, DAPR configuration, and topology tests must move together.
- **AD-12 / NFR7:** Verify stored actor state, persisted envelopes, outbound CloudEvent metadata, serialized idempotency state, and caller-visible reconstruction. Status codes alone are not evidence.
- **AD-13:** Sharding or changing global-position meaning is spec-first and outside Story 4.1.

### Library / Framework Requirements And Current Technical Notes

- Keep repository-pinned `.NET SDK 10.0.302`, `net10.0`, and DAPR .NET SDK `1.18.4`; no package update is needed. Versions remain centralized—never add a `Version` to a project `PackageReference`.
- DAPR actor turn-based access serializes calls per actor instance, which is why one `GlobalPositionActor` with id `global` can reserve unique monotonic ranges. This is per-actor serialization and may be a throughput bottleneck; Story 4.6 owns any sharding decision.
- DAPR allows publish metadata keys such as `cloudevent.id` to override generated CloudEvent attributes and explicitly notes that DAPR does not automatically deduplicate messages. Keep the application-owned stable `MessageId` contract.
- CloudEvents requires the `(source, id)` pair to identify distinct events and permits a retransmitted duplicate to retain the same id. Do not use the id for ordering or correlation semantics.

### Testing Requirements

- Framework: xUnit v3 + Shouldly; NSubstitute for actor state/DAPR collaborators. Do not introduce raw `Assert.*` in new tests.
- Run tests by project. Use `Hexalith.EventStore.slnx` only for restore/build, not solution-level tests.
- Test method names remain PascalCase and scenario-focused. Use `.ConfigureAwait(false)` for any awaited production call; follow the existing test-project analyzer policy for test awaits.
- The caller-path fidelity test should compare the returned record/result as a whole or explicitly assert every field so a future added field cannot silently disappear. If whole-record equality is used, still make failure intent obvious.
- Focused inner loop and gates:

```bash
dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~EventPersisterTests|FullyQualifiedName~EventPublisherTests|FullyQualifiedName~SubscriberIdempotencyTests|FullyQualifiedName~IdempotencyCheckerTests|FullyQualifiedName~IdempotencyRecordTests|FullyQualifiedName~AggregateActorIdempotencyTests|FullyQualifiedName~GlobalPositionActorTests|FullyQualifiedName~EventStoreServerServiceCollectionExtensionsTests" \
  -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet test tests/Hexalith.EventStore.Testing.Tests/Hexalith.EventStore.Testing.Tests.csproj \
  --configuration Release -p:UseHexalithProjectReferences=false

dotnet build Hexalith.EventStore.slnx --configuration Release \
  -p:UseHexalithProjectReferences=false
```

If project-level filtering is blocked by the local Microsoft.Testing.Platform/xUnit v3 runner, build the test project and invoke the built test assembly with supported single-dash `-class`/`-method` arguments; record the exact fallback rather than weakening the gate.

### Git Intelligence

- `3ccb1054` (2026-07-02) introduced the DAPR global allocator, non-zero position stamping, persisted-`MessageId` CloudEvent identity, complete idempotency records, tests, and the frozen spec. Core relevant lines remain materially unchanged at the story baseline.
- Formal Story 4.1 was added later in planning commit `12e56bba` (2026-07-05), and backlog tracking followed separately. This chronology explains the code/status contradiction.
- Later `e0ad0fbe` changed projection-trigger behavior around `EventPublisher` but preserved CloudEvent id selection; do not regress that newer behavior while verifying Story 4.1.
- An older commit/title using “Story 4.1” refers to a previous numbering scheme and shared-topic publishing. It is not this Phase 4 story.

### Project Structure Notes

- No new project, package, public contract, actor type, state store, AppHost resource, DAPR YAML, UI component, or generated REST artifact is expected.
- Production seams remain under `src/Hexalith.EventStore.Server/{Actors,Events,Configuration}`; aligned test fakes remain in `src/Hexalith.EventStore.Testing/Fakes`; focused tests remain in `tests/Hexalith.EventStore.Server.Tests/{Actors,Events,Configuration}`.
- Preserve one C# type per file, file-scoped namespaces, existing brace/style conventions, and no new copyright header.
- Do not modify `references/Hexalith.Tenants` for this story.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:1471-1502` — Epic 4 / Story 4.1 foundation and acceptance criteria]
- [Source: `_bmad-output/planning-artifacts/prd.md:147-160` — FR23 and Event Correctness and Recovery]
- [Source: `_bmad-output/planning-artifacts/prd.md:205-225` — NFR6, NFR7, NFR16]
- [Source: `_bmad-output/planning-artifacts/architecture.md:73-83` — AD-5 and AD-6]
- [Source: `_bmad-output/planning-artifacts/architecture.md:91-125` — AD-8, AD-9, AD-12, AD-13]
- [Source: `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md:11-79` — frozen intent, completed code map, accepted gap semantics, historical verification]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-02-global-event-ordering.md:12-90` — defect origin, selected direct correction, success criteria]
- [Source: `src/Hexalith.EventStore.Server/Actors/GlobalPositionActor.cs:17-36`]
- [Source: `src/Hexalith.EventStore.Server/Events/EventPersister.cs:47-142`]
- [Source: `src/Hexalith.EventStore.Server/Events/EventPublisher.cs:177-207`]
- [Source: `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs:17-64`]
- [Source: `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:116-135,400-446,500-538`]
- [Source at baseline `322e3193`: `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIdempotencyTests.cs:37-49,119-132` — original caller-visible coverage gap]
- [DAPR actor turn-based access model](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-features-concepts/)
- [DAPR CloudEvents metadata and deduplication guidance](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-cloudevents/)
- [CloudEvents core specification](https://github.com/cloudevents/spec/blob/ce@stable/cloudevents/spec.md)
- [NuGet: Dapr.Client 1.18.4](https://www.nuget.org/packages/Dapr.Client/1.18.4)
- [Source: `_bmad-output/project-context.md` — EventStore technology, identity, testing, and workflow rules]

## Dev Agent Record

### Agent Model Used

Not recorded by the implementation session; review patches applied by Codex (GPT-5).

### Implementation Plan / Decisions

- Preserve the shipped production allocator, publication identity, and idempotency mapping; close only executable evidence gaps found during review.
- Use public DAPR `ActorRuntime.RegisteredActors` metadata to prove actor registration, and round-trip production-shaped command results through `IdempotencyRecord.FromResult` at the actor boundary.

### Debug Log References

- 2026-07-12: Verified frozen spec and baseline ancestry (`3ccb1054` is an ancestor of `322e3193`); inspected relevant history and production seams. No production regression found.
- 2026-07-12: Pre-change focused Server evidence passed 81/81 (0 failed, 0 skipped).
- 2026-07-12: Re-proved allocator state commit, contiguous persisted positions, one allocation per non-empty batch, production registrations, and non-zero testing-fake source behavior through focused evidence and seam inspection.
- 2026-07-12: Re-proved stable CloudEvent identity and publish-envelope identity preservation with the 81/81 focused run and explicit subscriber-idempotency scenarios.
- 2026-07-12: RED — strengthened actor-boundary duplicate assertions failed 2/7 against the intentionally incomplete fixtures, exposing missing seeded event/payload/backpressure fidelity.
- 2026-07-12: GREEN — seeded complete accepted and rejected records; actor idempotency tests passed 7 passed, 0 failed, 0 skipped, and the focused Story 4.1 lane passed 81 passed, 0 failed, 0 skipped.
- 2026-07-12: Scope audit (`git diff --check` clean) confirmed the functional diff is test-only; no production, package, topology, contract, state-key, retry, projection, or identifier changes were introduced.
- 2026-07-12: Pre-review gates passed without fallback: focused Server 81 passed, 0 failed, 0 skipped; full Server 2303 passed, 0 failed, 25 skipped; Testing 144 passed, 0 failed, 0 skipped; Release solution build 0 warnings and 0 errors.
- 2026-07-12: Review-patch gates passed without fallback: focused Server 82 passed, 0 failed, 0 skipped; full Server 2304 passed, 0 failed, 25 skipped; Testing 145 passed, 0 failed, 0 skipped; Release solution build 0 warnings and 0 errors.

### Completion Notes List

- Task 1: Preserved the shipped implementation unchanged after confirming allocator, persistence, publication identity, idempotency mapping, and actor-owned commit behavior match the frozen contract.
- Task 2: Confirmed actor-backed global allocation and persisted-envelope evidence remain compliant; review added exact allocator/actor registration proof and cross-aggregate testing-fake position coverage without changing production code.
- Task 3: Confirmed publication and re-publication retain persisted `MessageId` identity across same-event and distinct-event scenarios; no source changes were required.
- Task 4: Strengthened accepted and rejected duplicate caller-path coverage to round-trip production-shaped eight-field command results through `IdempotencyRecord.FromResult`. Existing production mapping passed unchanged.
- Task 5: Preserved all Story 4.2/4.4/4.6 and compatibility boundaries; only caller-visible duplicate-result test coverage changed.
- Task 6: Completed current focused, regression, testing-fake, and Release build gates successfully; code review resolved all five patch findings and advanced Story 4.1 to done. Downstream Epic 4 stories remain unchanged.

### File List

- `_bmad-output/implementation-artifacts/4-1-event-identity-and-duplicate-result-fidelity.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorIdempotencyTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Configuration/EventStoreServerServiceCollectionExtensionsTests.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventPersisterTestEvent.cs`
- `tests/Hexalith.EventStore.Testing.Tests/Fakes/FakeEventPersisterTests.cs`

### Change Log

- 2026-07-12: Reconciled the shipped Story 4.1 behavior and strengthened complete accepted/rejected duplicate-result caller coverage; all required gates passed.
- 2026-07-12: Applied code-review patches for exact DAPR registrations, testing-fake global positions, production-shaped duplicate round trips, and story-record traceability.
