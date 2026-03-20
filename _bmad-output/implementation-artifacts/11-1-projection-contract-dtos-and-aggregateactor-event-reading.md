# Story 11.1: Projection Contract DTOs & AggregateActor Event Reading

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform developer,
I want wire-format DTOs for the /project endpoint and a read-only method on AggregateActor to fetch events,
so that the projection builder can deliver events without coupling to DAPR internal key formats.

## Acceptance Criteria

1. **Given** the Contracts project,
   **When** `ProjectionEventDto` is defined,
   **Then** it contains: EventTypeName, Payload, SerializationFormat, SequenceNumber, Timestamp, CorrelationId
   **And** excludes Server-internal fields (CausationId, UserId, DomainServiceVersion, Extensions).

2. **Given** the Contracts project,
   **When** `ProjectionRequest` is defined,
   **Then** it contains: TenantId, Domain, AggregateId, ProjectionEventDto[] — with per-aggregate granularity.

3. **Given** the Contracts project,
   **When** `ProjectionResponse` is defined,
   **Then** it contains: ProjectionType (string), State (JsonElement) — State is opaque, CommandApi never interprets it.

4. **Given** `IAggregateActor`,
   **When** `GetEventsAsync(long fromSequence)` is called,
   **Then** it returns `EventEnvelope[]` for events after fromSequence
   **And** encapsulates DAPR actor state key format internally
   **And** returns empty array for new aggregates
   **And** throws `MissingEventException` if a persisted event key is missing (consistent with `EventStreamReader`).

## Definition of Done

- All 4 ACs verified against actual code
- Build: `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
- Tier 1 + Tier 2 tests pass, no regressions
- Branch: `feat/story-11-1-projection-contract-dtos-and-aggregateactor-event-reading`

## Tasks / Subtasks

- [ ] Task 1: Create Projection Contract DTOs (AC: 1, 2, 3)
  - [ ] Create `src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs`
  - [ ] Create `src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs`
  - [ ] Create `src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs`
  - [ ] Create `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs` with JSON round-trip tests (include empty Payload edge case)
  - [ ] Run Tier 1 Contracts tests to verify green
- [ ] Task 2: Add GetEventsAsync to IAggregateActor (AC: 4)
  - [ ] Add `GetEventsAsync(long fromSequence)` to `IAggregateActor` interface
  - [ ] Implement in `AggregateActor` — reuse `EventStreamReader` event-reading pattern (metadata lookup → batch state reads)
  - [ ] Update `FakeAggregateActor` in Testing project with configurable event list
  - [ ] Create `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs` (include batch boundary and future-sequence edge cases)
  - [ ] Run Tier 2 Server tests to verify no regression
- [ ] Task 3: Full build and test verification
  - [ ] `dotnet build Hexalith.EventStore.slnx --configuration Release` — 0 errors, 0 warnings
  - [ ] All Tier 1 tests pass (Contracts + Client + Sample + Testing + SignalR)
  - [ ] Tier 2 Server tests pass (requires DAPR slim init)

## Dev Notes

### Architecture Context: Server-Managed Projection Builder (Mode B)

This story creates the **data contracts and event access foundation** for Epic 11. The full projection pipeline is:

```
Events persisted → AggregateActor.GetEventsAsync(fromSequence) → map to ProjectionEventDto[]
  → POST /project to domain service via DAPR service invocation (ProjectionRequest)
  → domain service returns ProjectionResponse { ProjectionType, State }
  → ProjectionActor stores state → ETag regenerated → SignalR broadcast → UI refreshes
```

**This story covers only the first two layers:** the wire-format DTOs and the event retrieval method. Stories 11-2 through 11-5 build the rest.

### Task 1: Projection Contract DTOs

**Namespace:** `Hexalith.EventStore.Contracts.Projections`

The `Projections/` directory already exists with `ProjectionChangedNotification.cs`. Add three new records:

#### ProjectionEventDto

Wire-format event sent to domain services. **Deliberately excludes** Server-internal fields to maintain security boundary:

| Include | Exclude (Server-internal) |
|---------|---------------------------|
| EventTypeName | CausationId |
| Payload (byte[]) | UserId |
| SerializationFormat | DomainServiceVersion |
| SequenceNumber | GlobalPosition |
| Timestamp | MetadataVersion |
| CorrelationId | Extensions |
|  | MessageId |
|  | AggregateId/Type/TenantId/Domain (already in ProjectionRequest) |

```csharp
// src/Hexalith.EventStore.Contracts/Projections/ProjectionEventDto.cs
namespace Hexalith.EventStore.Contracts.Projections;

public record ProjectionEventDto(
    string EventTypeName,
    byte[] Payload,
    string SerializationFormat,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId);
```

#### ProjectionRequest

Per-aggregate granularity — one call per aggregate instance:

```csharp
// src/Hexalith.EventStore.Contracts/Projections/ProjectionRequest.cs
namespace Hexalith.EventStore.Contracts.Projections;

public record ProjectionRequest(
    string TenantId,
    string Domain,
    string AggregateId,
    ProjectionEventDto[] Events);
```

#### ProjectionResponse

State is opaque `JsonElement` — CommandApi stores/serves it without understanding the schema:

```csharp
// src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Projections;

public record ProjectionResponse(
    string ProjectionType,
    JsonElement State);
```

#### Tests

Create `tests/Hexalith.EventStore.Contracts.Tests/Projections/ProjectionContractTests.cs` with:
- `ProjectionEventDto_RoundTrips_Json` — serialize/deserialize, verify all 6 fields survive
- `ProjectionRequest_RoundTrips_Json` — verify TenantId, Domain, AggregateId, Events[] survive
- `ProjectionResponse_RoundTrips_Json` — verify ProjectionType and opaque State survive

Follow existing test pattern in `ProjectionChangedNotificationTests.cs`. Use **Shouldly** for assertions.

Additional edge case test:
- `ProjectionEventDto_EmptyPayload_RoundTripsJson` — verify `byte[] Payload = []` survives JSON round-trip (marker events with no payload data)

### Task 2: AggregateActor.GetEventsAsync

#### Interface Change

Add to `src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs`:

```csharp
Task<EventEnvelope[]> GetEventsAsync(long fromSequence);
```

Add `using Hexalith.EventStore.Server.Events;` to the file.

#### Implementation in AggregateActor

The implementation follows the existing `EventStreamReader.RehydrateAsync` pattern but simplified (no snapshot handling). **Critical code paths to reuse:**

1. **Parse actor ID** → `GetAggregateIdentityFromActorId()` (existing method at line 770)
2. **Load metadata** → `StateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)` (existing pattern)
3. **Read events** → Batch reads using `identity.EventStreamKeyPrefix + seq` (existing pattern in `EventStreamReader.cs` lines 96-128)

**Key facts from the existing code:**
- `AggregateMetadata` record: `(long CurrentSequence, DateTimeOffset LastModified, string? ETag)` — at `src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs`
- Event state key pattern: `{tenant}:{domain}:{aggId}:events:{seq}` — from `AggregateIdentity.EventStreamKeyPrefix` + sequence number
- Metadata state key: `{tenant}:{domain}:{aggId}:metadata` — from `AggregateIdentity.MetadataKey`
- Existing batch reads use `MaxConcurrentStateReads = 32` — reuse the same constant and pattern
- `EventEnvelope` is the Server-internal 17-field record at `src/Hexalith.EventStore.Server/Events/EventEnvelope.cs`

**Implementation outline:**

```csharp
public async Task<EventEnvelope[]> GetEventsAsync(long fromSequence)
{
    // Defensive: clamp negative input to 0 (read all events)
    fromSequence = Math.Max(0, fromSequence);

    AggregateIdentity identity = GetAggregateIdentityFromActorId();

    // Load metadata to get current sequence
    ConditionalValue<AggregateMetadata> metadataResult = await StateManager
        .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
        .ConfigureAwait(false);

    if (!metadataResult.HasValue)
    {
        return []; // New aggregate — no events
    }

    long currentSequence = metadataResult.Value.CurrentSequence;
    if (currentSequence <= fromSequence)
    {
        return []; // No new events since requested sequence
    }

    // Read events from fromSequence + 1 to currentSequence
    int startSequence = checked((int)(fromSequence + 1));
    int eventCount = checked((int)(currentSequence - fromSequence));
    string keyPrefix = identity.EventStreamKeyPrefix;

    var events = new List<EventEnvelope>(eventCount);
    int cursor = startSequence;
    int endExclusive = startSequence + eventCount;

    while (cursor < endExclusive)
    {
        int batchSize = Math.Min(MaxConcurrentStateReads, endExclusive - cursor);
        // ... batch read pattern from EventStreamReader lines 102-120
        // CRITICAL: results from Task.WhenAll must be ordered by sequence number
        // before adding to the list — same as EventStreamReader line 123:
        //   loadedBatch.OrderBy(x => x.Sequence)
        // Without this, events may arrive in non-deterministic order from parallel reads,
        // causing projection state corruption in Story 11-3.
    }

    return [.. events];
}
```

**Error handling contract:** `GetEventsAsync` follows the same error semantics as `EventStreamReader`:
- **Negative `fromSequence`** → clamp to 0 (defensive — only trusted internal code calls this, but prevents accidental `MissingEventException` from reading key `{prefix}0`)
- **No metadata** → return empty array (new aggregate)
- **`fromSequence >= currentSequence`** → return empty array (no new events, including future sequence values)
- **Missing event key** → throw `MissingEventException` (data corruption — same as `EventStreamReader` line 115)
- **Deserialization failure** → throw `EventDeserializationException` (same as `EventStreamReader` line 111)
- The caller (`ProjectionUpdateOrchestrator` in Story 11-3) wraps in try/catch with fail-open semantics — this method should NOT swallow errors

**Design rationale (ADR-11.1a):** `GetEventsAsync` returns Server-internal `EventEnvelope` deliberately — not `ProjectionEventDto`. The mapping from `EventEnvelope` to `ProjectionEventDto` (stripping CausationId, UserId, Extensions, etc.) is the orchestrator's responsibility in Story 11-3. This keeps the aggregate actor decoupled from projection concerns (SRP). Do NOT change the return type to `ProjectionEventDto`.

**Null Extensions warning:** `EventEnvelope.Extensions` is `IDictionary<string, string>?` — it can be null. The mapping layer in Story 11-3 must handle this when converting to `ProjectionEventDto` (which excludes Extensions entirely). Not a concern for this story, but documented for cross-story awareness.

**Concurrency note:** DAPR actors are single-threaded. `GetEventsAsync` blocks command processing while executing. Mitigated by:
- Only reads events since `fromSequence` (typically small batch)
- For high-throughput aggregates, Stories 11-3/11-4 add RefreshIntervalMs > 0 to batch reads

#### FakeAggregateActor Update

Update `src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs`:

```csharp
// Add property for configurable events
public EventEnvelope[] ConfiguredEvents { get; set; } = [];

// Add interface implementation
public Task<EventEnvelope[]> GetEventsAsync(long fromSequence)
    => Task.FromResult(ConfiguredEvents
        .Where(e => e.SequenceNumber > fromSequence)
        .OrderBy(e => e.SequenceNumber)
        .ToArray());
```

Add `using Hexalith.EventStore.Server.Events;` to the file.

**Note:** `FakeAggregateActor` references `EventEnvelope` from the Server project. `Hexalith.EventStore.Testing.csproj` already references `Hexalith.EventStore.Server` (line 9) — no new project reference needed. The Testing NuGet package's transitive dependency graph is unchanged.

#### Tests

Create `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs`:

- `GetEventsAsync_NewAggregate_ReturnsEmptyArray` — mock `IActorStateManager.TryGetStateAsync<AggregateMetadata>` to return no value
- `GetEventsAsync_WithEvents_ReturnsEventsAfterSequence` — mock state manager with metadata (CurrentSequence=3) and 3 stored EventEnvelope records; call `GetEventsAsync(1)` and assert 2 events returned (sequences 2, 3)
- `GetEventsAsync_FromSequenceAtCurrent_ReturnsEmpty` — when fromSequence == currentSequence, returns empty
- `GetEventsAsync_FromSequenceBeyondCurrent_ReturnsEmpty` — when fromSequence > currentSequence (e.g., caller has stale checkpoint), returns empty array (not an error)
- `GetEventsAsync_ExactlyMaxBatchSize_ReturnsAllEvents` — mock 32 events (MaxConcurrentStateReads boundary), verify all 32 returned correctly ordered
- `GetEventsAsync_MoreThanMaxBatchSize_ReturnsAllEvents` — mock 33 events (triggers second batch iteration in the while loop), verify all 33 returned correctly ordered — this is the highest-risk off-by-one test
- `GetEventsAsync_MissingEventKey_ThrowsMissingEventException` — mock metadata with CurrentSequence=3 but event key for sequence 2 returns no value; verify `MissingEventException` is thrown (validates error contract)
- `GetEventsAsync_NegativeFromSequence_ClampsToZero` — call with `fromSequence = -1`, verify it returns all events (same as `fromSequence = 0`) without throwing

Follow existing test setup pattern from `AggregateActorTests.cs` in the same folder. The AggregateActor constructor requires:
- `ActorHost` — use `ActorHost.CreateForTest<AggregateActor>()`
- `ILogger<AggregateActor>` — use `NullLogger`
- `IDomainServiceInvoker` — NSubstitute mock
- `ISnapshotManager` — NSubstitute mock
- `IEventPayloadProtectionService` — NSubstitute mock
- `ICommandStatusStore` — NSubstitute mock
- `IEventPublisher` — NSubstitute mock
- `IOptions<EventDrainOptions>` — use `Options.Create(new EventDrainOptions())`
- `IOptions<BackpressureOptions>` — use `Options.Create(new BackpressureOptions())`
- `IDeadLetterPublisher` — NSubstitute mock

Set actor ID to `"tenant-a:counter:counter-1"` format (3 colon-separated parts).

### CRITICAL: Scope Boundaries

**This story ONLY creates:**
- 3 new record types in Contracts/Projections/
- 1 new method on IAggregateActor + AggregateActor implementation
- 1 update to FakeAggregateActor
- Test files for both

**Do NOT create or modify:**
- `EventReplayProjectionActor` — that's Story 11-2
- `IProjectionWriteActor` — that's Story 11-2
- `IProjectionUpdateOrchestrator` — that's Story 11-3
- `ProjectionUpdateOrchestrator` — that's Story 11-3
- `ProjectionOptions` — that's Story 11-4
- Counter `/project` endpoint — that's Story 11-5
- `EventPublisher` — no changes in this story
- `ServiceCollectionExtensions` — no DI changes in this story

### CRITICAL: Do NOT Break Existing Tests

- `IAggregateActor` gains a new method — all existing implementations must be updated
- `FakeAggregateActor` (Testing project) must implement `GetEventsAsync`
- Any other `IAggregateActor` implementations must be found and updated (grep for `: IAggregateActor`)
- Existing `ProcessCommandAsync` behavior must not change
- All 2231 existing tests (Tier 1: 698, Tier 2: 1533) must continue to pass

### Existing Code Patterns to Follow

**Record definition style** (from `ProjectionChangedNotification.cs`):
```csharp
namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>XML doc</summary>
/// <param name="FieldName">Param doc.</param>
public record TypeName(params...);
```

**Test style** (from existing Contracts.Tests):
- xUnit `[Fact]` attributes
- Shouldly assertions (`result.ShouldBe(...)`, `result.ShouldNotBeNull()`)
- No test base class needed for simple record tests

**Actor test style** (from existing Server.Tests):
- `ActorHost.CreateForTest<T>()` for actor instantiation
- NSubstitute for mocking `IActorStateManager` and dependencies
- Tier 2 — requires DAPR slim init for full test run

### Project Structure Notes

```
src/Hexalith.EventStore.Contracts/Projections/
  ProjectionChangedNotification.cs  [EXISTS]
  ProjectionEventDto.cs             [NEW — this story]
  ProjectionRequest.cs              [NEW — this story]
  ProjectionResponse.cs             [NEW — this story]

src/Hexalith.EventStore.Server/Actors/
  IAggregateActor.cs                [MODIFY — add GetEventsAsync]
  AggregateActor.cs                 [MODIFY — implement GetEventsAsync]

src/Hexalith.EventStore.Testing/Fakes/
  FakeAggregateActor.cs             [MODIFY — add GetEventsAsync]

tests/Hexalith.EventStore.Contracts.Tests/Projections/
  ProjectionChangedNotificationTests.cs  [EXISTS — follow this pattern]
  ProjectionContractTests.cs             [NEW — this story]

tests/Hexalith.EventStore.Server.Tests/Actors/
  AggregateActorGetEventsTests.cs        [NEW — this story]
```

### Previous Story Intelligence (Story 10-2)

Key learnings from the most recent story:
- **Branch naming:** `feat/story-11-1-projection-contract-dtos-and-aggregateactor-event-reading`
- **Commit message pattern:** `feat: <description for Story 11-1>`
- **Audit/gap-fill pattern works well:** Previous stories used structured task checklists with pass/fail tracking
- **Test count baseline:** Tier 1: 698 passed, Tier 2: 1533 passed (total 2231)
- **Build must pass:** `dotnet build Hexalith.EventStore.slnx --configuration Release` with 0 warnings, 0 errors
- **extern alias pattern:** Server.Tests use `extern alias commandapi` for CommandApi integration tests — not needed for actor unit tests

### Git Intelligence

Recent commits (last 5):
- `31cd5b2` Story 10-2 merge (Redis backplane for SignalR)
- `c61948a` Mark story 10-2 done
- `9258ff9` Story 10-2 PR merge
- `6463ea7` Story 10-1 done, prepare story 10-2
- `8dedf4e` Story 10-1 merge (SignalR hub)

Pattern: feature branches merged via PRs, conventional commit messages (`feat: ...`).

### Package/Framework Reference

- .NET 10 SDK `10.0.103` (from `global.json`)
- DAPR SDK `1.17.0` — `IActor`, `ActorHost`, `IActorStateManager`, `ConditionalValue<T>`
- xUnit `2.9.3`, Shouldly `4.3.0`, NSubstitute `5.3.0`
- `System.Text.Json` — for `JsonElement` in `ProjectionResponse`
- TreatWarningsAsErrors = true — any warning is a build failure

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 11.1] — Story requirements and acceptance criteria
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 11] — Epic overview: Server-Managed Projection Builder
- [Source: docs/superpowers/specs/2026-03-15-server-managed-projection-builder-design.md] — Full design spec (Mode B architecture, ProjectionEventDto fields, GetEventsAsync design, error handling, security)
- [Source: docs/superpowers/plans/2026-03-15-server-managed-projection-builder.md#Chunk 1] — Implementation plan Tasks 1-2 with code sketches
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-03-15-projection-builder.md] — SCP adding projection builder stories
- [Source: src/Hexalith.EventStore.Server/Actors/IAggregateActor.cs] — Current interface (ProcessCommandAsync only)
- [Source: src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:770-783] — GetAggregateIdentityFromActorId pattern
- [Source: src/Hexalith.EventStore.Server/Events/EventStreamReader.cs:56-128] — Event reading pattern (metadata → batch state reads)
- [Source: src/Hexalith.EventStore.Server/Events/EventEnvelope.cs] — Server-internal 17-field EventEnvelope record
- [Source: src/Hexalith.EventStore.Server/Events/AggregateMetadata.cs] — AggregateMetadata record (CurrentSequence, LastModified, ETag)
- [Source: src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs] — Key derivation (EventStreamKeyPrefix, MetadataKey)
- [Source: src/Hexalith.EventStore.Contracts/Projections/ProjectionChangedNotification.cs] — Existing Projections namespace pattern
- [Source: src/Hexalith.EventStore.Testing/Fakes/FakeAggregateActor.cs] — Fake actor requiring update
- [Source: _bmad-output/implementation-artifacts/10-2-redis-backplane-for-multi-instance-signalr.md] — Previous story (test counts, conventions)

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
