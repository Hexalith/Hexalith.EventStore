# Sprint Change Proposal - DAPR Global Event Ordering

- **Date:** 2026-07-02
- **Author:** Administrator (via BMAD Correct Course)
- **Project:** Hexalith.EventStore
- **Mode:** Batch; user approved implementation direction
- **Change classification:** Minor/Moderate
- **Status:** Implemented locally; pending review/commit

---

## 1. Issue Summary

The EventStore architecture exposed event metadata for cross-aggregate ordering through `GlobalPosition`, but persisted events always stored `0`. This made the contract misleading and prevented consumers from using a reliable global order. The same analysis also found that CloudEvent ids were based on `{correlationId}:{sequenceNumber}`, which can collide when one correlation writes multiple aggregates that each start at sequence 1, and that idempotency records lost some command result fields.

Formal PRD and epic documents were not present under `_bmad-output/planning-artifacts`; impact analysis used the repository context, implementation spec, and code inspection.

## 2. Impact Analysis

### Epic Impact

No product epic is resequenced. This is a correctness correction inside the EventStore server architecture.

### Story Impact

No existing story status is changed. Future event-store stories should treat these as invariants:

- `GlobalPosition` is allocated by a DAPR actor, not a process-local counter.
- CloudEvent subscriber idempotency uses the persisted event `MessageId`.
- Duplicate command replies must preserve the original `CommandProcessingResult` fields.

### Artifact Impact

| Artifact | Impact |
|----------|--------|
| `src/Hexalith.EventStore.Server/Actors/GlobalPositionActor.cs` | New DAPR actor owning global event position allocation. |
| `src/Hexalith.EventStore.Server/Events/DaprGlobalPositionAllocator.cs` | New allocator adapter using the global actor proxy. |
| `src/Hexalith.EventStore.Server/Events/EventPersister.cs` | Persists non-zero global positions when allocator is provided. |
| `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` | Uses `MessageId` as `cloudevent.id`. |
| `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs` | Stores and restores full command result fields. |
| `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` | Registers allocator and global actor type. |
| `src/Hexalith.EventStore.Testing/Fakes/FakeEventPersister.cs` | Aligns the testing fake with non-zero global positions. |
| `tests/Hexalith.EventStore.Server.Tests/*` | Adds and updates focused tests for ordering, CloudEvent id, and idempotency fidelity. |

## 3. Recommended Approach

**Selected path: Direct Adjustment.**

The user explicitly confirmed that global order should be maintained by a DAPR actor. The implementation keeps aggregate-local stream writes inside the aggregate actor while delegating only global position allocation to one serialized actor. This preserves the existing persistence model and avoids introducing a separate database or process-local ordering risk.

Trade-off: positions are monotonic and unique, but a gap can occur if the aggregate-state commit fails after allocation. Reusing global positions after a failed write would be riskier than accepting gaps.

## 4. Detailed Change Proposals

### CP-1 - DAPR Actor Global Position Allocator

**OLD:** `EventPersister` wrote `GlobalPosition: 0` for every event.

**NEW:** `GlobalPositionActor` persists the latest allocated position and returns contiguous ranges; `EventPersister` stamps those values onto envelopes.

**Rationale:** A single DAPR actor provides turn-based serialization without moving aggregate event streams out of their aggregate actor state.

### CP-2 - CloudEvent Id Uses Event MessageId

**OLD:** `cloudevent.id = "{correlationId}:{sequenceNumber}"`.

**NEW:** `cloudevent.id = eventEnvelope.MessageId`.

**Rationale:** `MessageId` is unique per persisted event and stable across republish attempts, so it supports at-least-once subscriber deduplication without cross-aggregate collisions.

### CP-3 - Idempotency Records Preserve Full Results

**OLD:** Cached duplicate command results preserved only accepted/error/correlation fields.

**NEW:** Cached results also preserve event count, result payload, and backpressure fields.

**Rationale:** Duplicate command responses should be semantically identical to the original response.

## 5. Implementation Handoff

- **Scope:** Minor/Moderate.
- **Route:** Developer validation, then normal code review.
- **Implementation status:** Applied locally.
- **Spec artifact:** `_bmad-output/implementation-artifacts/spec-dapr-global-event-ordering.md`.

### Success Criteria

1. Persisted events receive actor-allocated non-zero global positions.
2. CloudEvent ids are stable per event and do not collide for same-correlation/same-sequence events from different aggregates.
3. Duplicate command results preserve all `CommandProcessingResult` fields.
4. Release build and server tests pass.

## 6. Checklist Summary

| Item | Status | Notes |
|------|--------|-------|
| 1.1 Triggering story | N/A | User requested implementation after architecture analysis. |
| 1.2 Core problem | Done | Misleading global position contract and event identity collision risk. |
| 1.3 Evidence | Done | Code inspection showed `GlobalPosition: 0` and correlation/sequence CloudEvent id. |
| 2.1-2.5 Epic impact | Done | No epic resequencing required. |
| 3.1 PRD conflicts | N/A | PRD not present; product scope unchanged. |
| 3.2 Architecture conflicts | Done | Server persistence, DAPR actor registration, and subscriber idempotency affected. |
| 3.3 UI/UX conflicts | N/A | No UI change. |
| 3.4 Other artifacts | Done | Tests and implementation spec updated. |
| 4.1 Direct adjustment | Viable | Selected and implemented. |
| 4.2 Rollback | Not viable | No rollback simplifies this correction. |
| 4.3 MVP review | Not viable | MVP/product scope unchanged. |
| 4.4 Recommended path | Done | DAPR actor allocator with message-id CloudEvent identity. |
| 5.1-5.5 Proposal components | Done | Included above. |
| 6.1-6.2 Review | Done | Local self-check plus tests/build. |
| 6.3 Approval | Done | User explicitly approved DAPR actor global ordering. |
| 6.4 Sprint status | N/A | No epic/story status change. |
| 6.5 Handoff | Done | Ready for normal code review. |

## 7. Verification

- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~EventPersisterTests|FullyQualifiedName~EventPublisherTests|FullyQualifiedName~SubscriberIdempotencyTests|FullyQualifiedName~IdempotencyCheckerTests|FullyQualifiedName~IdempotencyRecordTests|FullyQualifiedName~GlobalPositionActorTests|FullyQualifiedName~EventStoreServerServiceCollectionExtensionsTests"`: passed, 74 tests.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/`: passed, 2209 passed, 25 skipped.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/`: passed, 144 passed.
- `dotnet build Hexalith.EventStore.slnx --configuration Release`: passed, 0 warnings, 0 errors.
