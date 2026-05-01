# Server-Managed Projection Builder (Mode B)

## Problem

The query pipeline (contracts, routing, caching, SignalR notifications) is fully built, but no concrete `ProjectionActor` exists to serve queries. The Blazor UI counter always shows 0 because `QueryRouter` cannot find a registered projection actor.

## Architecture Decision

Two projection modes will be supported:

- **Mode A (Pub/Sub, domain-driven)**: Domain service subscribes to events via DAPR pub/sub, builds and stores its own read model, exposes a query endpoint. Not covered in this spec.
- **Mode B (Server-managed, pull-based)**: EventStore manages the projection lifecycle — polls the event stream, sends new events to the domain service, caches returned state. **This spec covers Mode B.**

The domain microservice owns both the Apply logic and the projection state. EventStore orchestrates delivery and serves queries. The domain microservice technical layer stays thin.

## Design

### Projection Update Flow

```text
Events persisted by AggregateActor
        |
        v
[RefreshIntervalMs = 0?]
        |
       yes --> Fire-and-forget background task (immediate, non-blocking)
        |
       no  --> Background IHostedService polls per tenant:domain:aggregateId
        |
        v
EventStore reads new events since last-sent checkpoint
(via AggregateActor.GetEventsAsync — encapsulated, no DAPR key coupling)
        |
        v
POST /project to domain service via DAPR service invocation
  Request:  { tenantId, domain, aggregateId, events[] }
  Response: { projectionType, state }
        |
        v
ProjectionActor.UpdateProjectionAsync(state) via actor proxy
ETag regenerated -> SignalR broadcast -> UI refreshes
```

### Query Flow

```text
Blazor UI queries /api/v1/queries
        |
        v
QueryRouter -> ProjectionActor (generic, in Server project)
        |
        v
ProjectionActor.ExecuteQueryAsync reads from DAPR actor state
(CachingProjectionActor base handles in-memory ETag caching on top)
```

### Components

#### 1. `EventReplayProjectionActor` (Server project)

- Concrete implementation of `CachingProjectionActor`
- Registered with DAPR actor type name `"ProjectionActor"` (matching `QueryRouter.ProjectionActorTypeName`) in `ServiceCollectionExtensions`
- Implements two actor interfaces:
    - `IProjectionActor` (existing, read-only): `QueryAsync(QueryEnvelope)` — serves queries
    - `IProjectionWriteActor` (new): `UpdateProjectionAsync(ProjectionState)` — receives state from projection builder
- `ExecuteQueryAsync` reads the last persisted state from DAPR actor state (cache miss path)
- `CachingProjectionActor` base provides in-memory ETag caching on top (no double-caching — base caches in memory, subclass persists to DAPR state)
- Keyed by query routing identity (existing 3-tier model)

**Caching interaction:**

1. Projection builder calls `UpdateProjectionAsync` → writes state to DAPR actor state + regenerates ETag
2. Query arrives → `CachingProjectionActor.QueryAsync` checks in-memory ETag cache
3. Cache miss (ETag changed) → calls `ExecuteQueryAsync` → reads from DAPR actor state → populates in-memory cache
4. Cache hit → returns in-memory cached payload directly

#### 2. Projection Checkpoint Tracker

- Tracks last-sent event sequence **per aggregate**: `tenant:domain:aggregateId`
- Stored in DAPR state store via dedicated checkpoint state key: `projection-checkpoints:{tenant}:{domain}:{aggregateId}`
- Updated **only after** successful `UpdateProjectionAsync` call (write-after-success guarantees at-least-once delivery)
- Immediate delivery reads from the stored checkpoint and calls `AggregateActor.GetEventsAsync(lastDeliveredSequence)`. Missing checkpoints use `0` for the first delivery.
- Checkpoint writes keep the maximum observed sequence so delayed duplicate triggers do not lower an already advanced checkpoint. The current implementation uses read-before-write max preservation; duplicate delivery can still occur under concurrent fire-and-forget triggers, but silent event skipping is not allowed.
- Idempotent: if the same events are sent twice, the domain service must handle them idempotently (Apply methods are inherently idempotent when replaying from a known state)

#### 3. Immediate Trigger (RefreshIntervalMs = 0, default)

- **Fire-and-forget background task** — does NOT block command processing (preserves CQRS separation)
- Triggered after `EventPublisher` persists events via a lightweight event/callback, NOT by adding dependencies to `AggregateActor`
- Uses `IProjectionUpdateOrchestrator` injected into the event publication path (not into AggregateActor itself)
- Reads new events from the aggregate via `AggregateActor.GetEventsAsync(fromSequence)` (actor proxy call)
- Sends to domain service `/project` endpoint
- Updates checkpoint and ProjectionActor state
- Failures are logged and retried on the next trigger — stale projections are acceptable (eventual consistency)

#### 4. Background Poller (RefreshIntervalMs > 0)

- **Deferred**: polling product behavior is tracked separately from immediate checkpoint-tracked delivery.
- `IHostedService` that polls **per aggregate** at the configured interval
- Discovers aggregates that need polling from checkpoint state (any aggregate with a known checkpoint is polled)
- New aggregates are discovered when the immediate trigger fires for the first time (creates the initial checkpoint)
- Reads new events since checkpoint via `AggregateActor.GetEventsAsync(fromSequence)`
- Sends to domain service `/project` endpoint
- Updates checkpoint and ProjectionActor state

#### 5. Domain Service `/project` Endpoint (thin)

- Convention-based: any registered domain service exposing `/project` gets automatic projection wiring
- **Per-aggregate granularity**: one call per aggregate, not batched across aggregates
- Contract:
    - **Request**: `ProjectionRequest { string TenantId, string Domain, string AggregateId, ProjectionEventDto[] Events }`
    - **Response**: `ProjectionResponse { string ProjectionType, JsonElement State }`
- `ProjectionEventDto` is a new wire-format DTO in Contracts (not the Server-internal `EventEnvelope`)
    - Fields: `string EventTypeName, byte[] Payload, string SerializationFormat, long SequenceNumber, DateTimeOffset Timestamp, string CorrelationId`
    - Mapped from Server `EventEnvelope` by the projection builder before sending
- Domain service applies events to its own stored projection state
- Domain service owns the projection state persistence (e.g., in-memory, DAPR state, or any store)
- Returns the current state after applying the new events
- **Idempotency**: domain service must handle duplicate event delivery (events include SequenceNumber for dedup)

**Rationale for `JsonElement State`**: Projection state is domain-specific. EventStore treats it as opaque bytes — it stores and serves it without understanding the schema. This keeps the Server project decoupled from domain types.

#### 6. Convention-Based Discovery

- Domain services already registered in `EventStore:DomainServices` (for command routing) that also expose a `/project` endpoint are automatically wired for projections
- `EventStore:Projections:Domains` configures per-domain refresh intervals; the domain service identity comes from the existing `EventStore:DomainServices` registration
- No explicit projection registration needed beyond the existing domain service setup
- Discovery happens at startup via DAPR service invocation health check or first query

#### 7. Event Reading via `AggregateActor.GetEventsAsync`

- **Decision**: Use a new read-only method on `IAggregateActor`, not direct DAPR state store key access
- `GetEventsAsync(long fromSequence)` returns `EventEnvelope[]` for events with sequence > `fromSequence`
- Encapsulates DAPR actor state key format — projection infrastructure never knows the internal key layout
- **Concurrency**: DAPR actors are single-threaded, so this call blocks command processing while executing. Mitigated by: (a) only reads events since the last checkpoint (typically a small batch), so the call is short; (b) for high-throughput aggregates, use `RefreshIntervalMs > 0` to batch reads and reduce actor contention; (c) the projection builder should not hold the actor proxy longer than necessary
- The projection builder maps the returned Server `EventEnvelope[]` to `ProjectionEventDto[]` before sending to the domain service

### Configuration

```json
{
  "EventStore": {
    "Projections": {
      "DefaultRefreshIntervalMs": 0,
      "CheckpointStateStoreName": "statestore",
      "Domains": {
        "counter": {
          "RefreshIntervalMs": 0
        }
      }
    }
  }
}
```

- `DefaultRefreshIntervalMs`: 0 = immediate (fire-and-forget after persistence), >0 = polling interval in ms
- `CheckpointStateStoreName`: DAPR state store component for projection delivery checkpoints. Defaults to `statestore`.
- Per-domain override via `Domains:{domain}:RefreshIntervalMs`

### Error Handling

- **Domain service unavailable**: Log warning, skip update. Projection stays at last known state (stale). Next trigger/poll retries.
- **Domain service returns error**: Log warning, do NOT update checkpoint. Same events will be resent on next trigger/poll.
- **Checkpoint update ordering**: Checkpoint updated ONLY after successful `UpdateProjectionAsync` call. This guarantees at-least-once delivery.
- **Checkpoint read failure**: Log warning and replay from sequence `0` for that update. This can duplicate delivery but does not skip events or fail command processing.
- **Checkpoint save failure**: Log warning after the projection actor write and leave the previous checkpoint unchanged. The next trigger resends from the old checkpoint.
- **Projection builder crash**: On restart, resumes from last committed checkpoint. Events are replayed from that point (idempotent).
- **AggregateActor.GetEventsAsync failure**: Log warning, skip. Retried on next trigger/poll.
- **Degraded mode**: Stale projections are explicitly acceptable. The system favors availability over consistency (AP in CAP). Queries return the last known state, which may be behind by one or more events.

### Projection Change Notification

After the domain service returns updated state:

1. Projection builder calls `ProjectionActor.UpdateProjectionAsync(state)` via actor proxy
2. ProjectionActor persists state to DAPR actor state
3. `IETagActor.RegenerateAsync()` called to invalidate cache
4. `IProjectionChangedBroadcaster.BroadcastChangedAsync()` sends SignalR notification
5. Connected Blazor UI clients receive notification and re-query

### Security

- Tenant isolation enforced: projection updates scoped to `tenant:domain:aggregateId`
- Events sent to domain service mapped to `ProjectionEventDto` (excludes internal Server-only fields)
- Projection state in domain service follows same tenant scoping rules
- No cross-tenant event leakage in polling or immediate trigger
- `IProjectionWriteActor.UpdateProjectionAsync` is an actor method — DAPR actor proxy enforces invocation context

### Counter Sample Integration

The Sample domain service needs:

1. A `/project` endpoint in `Program.cs` (thin — ~10 lines)
2. Projection state storage (in-memory dictionary keyed by `{tenantId}:{aggregateId}`)
3. Apply logic reuses existing `CounterProcessor.RehydrateCount()` or `CounterState`
4. Returns `{ projectionType: "counter", state: { "count": N } }`

### Files to Create/Modify

**Server project (src/Hexalith.EventStore.Server/):**

- `Actors/EventReplayProjectionActor.cs` — new, concrete projection actor (implements IProjectionActor + IProjectionWriteActor)
- `Actors/IProjectionWriteActor.cs` — new, write interface for projection state updates
- `Actors/IAggregateActor.cs` — modified, add `GetEventsAsync(long fromSequence)`
- `Actors/AggregateActor.cs` — modified, implement `GetEventsAsync`
- `Projections/ProjectionCheckpointTracker.cs` — new, tracks last-sent sequence per aggregate
- `Projections/IProjectionUpdateOrchestrator.cs` — new, interface for triggering projection updates
- `Projections/ProjectionUpdateOrchestrator.cs` — new, immediate trigger + domain service invocation
- `Projections/ProjectionPollerService.cs` — new, background polling IHostedService
- `Configuration/ServiceCollectionExtensions.cs` — modified, register ProjectionActor + projection services
- `Configuration/ProjectionOptions.cs` — new, configuration model

**Contracts project (src/Hexalith.EventStore.Contracts/):**

- `Projections/ProjectionRequest.cs` — new, `/project` endpoint request DTO
- `Projections/ProjectionResponse.cs` — new, `/project` endpoint response DTO
- `Projections/ProjectionEventDto.cs` — new, wire-format event DTO (not Server-internal EventEnvelope)

**Sample domain service (samples/Hexalith.EventStore.Sample/):**

- `Program.cs` — modified, add `/project` endpoint
- `Counter/CounterProjectionHandler.cs` — new, thin handler reusing existing Apply logic

### Testing

- Unit tests for `EventReplayProjectionActor` (mock domain service responses, test cache interaction)
- Unit tests for checkpoint tracking (sequence tracking, write-after-success ordering, idempotency)
- Unit tests for immediate trigger (verify fire-and-forget, non-blocking, failure tolerance)
- Unit tests for `AggregateActor.GetEventsAsync` (read-only, partial reads from sequence)
- Integration test: submit command -> verify projection state updated -> verify query returns correct count
- Sample domain `/project` endpoint tests (idempotent event replay)
