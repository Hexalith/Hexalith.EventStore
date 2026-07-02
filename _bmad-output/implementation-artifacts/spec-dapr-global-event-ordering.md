---
title: 'DAPR Global Event Ordering'
type: 'feature'
created: '2026-07-02T14:38:15+02:00'
status: 'done'
context:
  - '{project-root}/_bmad-output/project-context.md'
baseline_commit: '507e58218fef91a909c030ee5c751410a613bcbd'
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** Event envelopes expose `GlobalPosition`, but persisted events currently store `0`, so consumers cannot derive a real cross-aggregate order. CloudEvent ids also derive from correlation id plus aggregate-local sequence, which can collide across aggregates when the same correlation writes multiple streams.

**Approach:** Introduce a single DAPR actor-backed allocator that assigns monotonic global positions in contiguous ranges per persisted command, use those positions on stored envelopes, and publish CloudEvents using the persisted event message id as the subscriber dedupe key. Preserve complete command results in idempotency records so duplicate commands replay the original outcome.

## Boundaries & Constraints

**Always:** Keep aggregate-local sequence numbers gapless within an aggregate. Keep persistence in the aggregate actor state store. Register the global allocator actor through the existing server actor registration. Use `ConfigureAwait(false)` on awaited calls.

**Ask First:** Any change that moves event persistence out of the aggregate actor, changes public command/event contract names, or requires a new external database/index beyond DAPR actor state.

**Never:** Do not use process-local static counters for production ordering. Do not keep CloudEvent ids based only on correlation id and aggregate-local sequence. Do not change projection replay architecture in this correction.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| First persisted batch | Allocator current position is absent and command emits 3 events | Events receive global positions 1, 2, 3 while aggregate sequence follows the stream sequence | N/A |
| Existing allocator state | Allocator current position is 40 and command emits 2 events | Events receive global positions 41, 42 | Checked arithmetic failure propagates as infrastructure failure |
| Duplicate command | Idempotency record contains accepted result with event count and payload | Duplicate returns the full cached result | N/A |
| CloudEvent publication | Persisted event has `MessageId=msg-123` | Published CloudEvent metadata has `cloudevent.id=msg-123` | Existing publish error handling remains unchanged |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- constructs the per-call persister and must pass the global allocator.
- `src/Hexalith.EventStore.Server/Actors/GlobalPositionActor.cs` -- new DAPR actor that serializes global range allocation.
- `src/Hexalith.EventStore.Server/Events/EventPersister.cs` -- assigns `GlobalPosition` on persisted envelopes.
- `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- stamps CloudEvent ids for subscriber idempotency.
- `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs` -- records cached command result fidelity.
- `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- registers allocator service and actor type.
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPersisterTests.cs` -- verifies allocated global positions.
- `tests/Hexalith.EventStore.Server.Tests/Events/EventPublisherTests.cs` and `SubscriberIdempotencyTests.cs` -- verify CloudEvent id contract.
- `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyCheckerTests.cs` -- verifies cached result fidelity.
- `tests/Hexalith.EventStore.Server.Tests/Actors/GlobalPositionActorTests.cs` -- verifies monotonic actor allocation.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.EventStore.Server/Actors/GlobalPositionActor.cs` and `IGlobalPositionActor.cs` -- add a DAPR actor that allocates monotonic contiguous ranges.
- [x] `src/Hexalith.EventStore.Server/Events/IGlobalPositionAllocator.cs` and `DaprGlobalPositionAllocator.cs` -- add the server abstraction and DAPR actor proxy implementation.
- [x] `src/Hexalith.EventStore.Server/Events/EventPersister.cs` and `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs` -- assign allocated global positions to persisted events.
- [x] `src/Hexalith.EventStore.Server/Events/EventPublisher.cs` -- switch `cloudevent.id` to the persisted event `MessageId`.
- [x] `src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs` -- store and restore the full `CommandProcessingResult` fields.
- [x] `src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs` -- register the allocator and global position actor.
- [x] `tests/Hexalith.EventStore.Server.Tests` -- add/update focused tests for global order, CloudEvent id uniqueness, and idempotency result fidelity.

**Acceptance Criteria:**
- Given a command emits multiple events, when those events are persisted, then each envelope has a non-zero monotonic `GlobalPosition` supplied by the DAPR-backed allocator.
- Given two aggregates publish events from the same correlation and local sequence, when the events are published, then their CloudEvent ids remain distinct because the persisted event message ids differ.
- Given a duplicate command is detected, when the cached idempotency record is returned, then event count, result payload, and backpressure fields match the original result.

## Spec Change Log

## Design Notes

The allocator returns the first position in a reserved range so one command batch can assign positions without one actor call per event. Positions are monotonic and unique; gaps can occur only if a downstream aggregate-state commit fails after allocation, which is preferable to reusing global positions.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Server.Tests/ --filter "FullyQualifiedName~EventPersisterTests|FullyQualifiedName~EventPublisherTests|FullyQualifiedName~SubscriberIdempotencyTests|FullyQualifiedName~IdempotencyCheckerTests|FullyQualifiedName~IdempotencyRecordTests|FullyQualifiedName~GlobalPositionActorTests|FullyQualifiedName~EventStoreServerServiceCollectionExtensionsTests"` -- passed: 74 passed, 0 failed.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/` -- passed: 2209 passed, 25 skipped, 0 failed.
- `dotnet test tests/Hexalith.EventStore.Testing.Tests/` -- passed: 144 passed, 0 failed.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- passed: 0 warnings, 0 errors.

## Suggested Review Order

**Global Ordering**

- Single DAPR actor serializes cross-aggregate range allocation.
  [`GlobalPositionActor.cs:20`](../../src/Hexalith.EventStore.Server/Actors/GlobalPositionActor.cs#L20)

- Persister allocates after payload protection and stamps global positions.
  [`EventPersister.cs:93`](../../src/Hexalith.EventStore.Server/Events/EventPersister.cs#L93)

- Aggregate actor passes the DI allocator into per-call persistence.
  [`AggregateActor.cs:400`](../../src/Hexalith.EventStore.Server/Actors/AggregateActor.cs#L400)

- Server registers the allocator service and actor type.
  [`ServiceCollectionExtensions.cs:40`](../../src/Hexalith.EventStore.Server/Configuration/ServiceCollectionExtensions.cs#L40)

**Event Identity**

- Publisher now uses persisted event message id for CloudEvent id.
  [`EventPublisher.cs:195`](../../src/Hexalith.EventStore.Server/Events/EventPublisher.cs#L195)

- Subscriber contract covers same correlation and same sequence across aggregates.
  [`SubscriberIdempotencyTests.cs:146`](../../tests/Hexalith.EventStore.Server.Tests/Events/SubscriberIdempotencyTests.cs#L146)

**Idempotency Fidelity**

- Idempotency state preserves full command result replay fields.
  [`IdempotencyRecord.cs:17`](../../src/Hexalith.EventStore.Server/Actors/IdempotencyRecord.cs#L17)

- Cached result tests enforce event count, payload, and backpressure replay.
  [`IdempotencyCheckerTests.cs:34`](../../tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyCheckerTests.cs#L34)
