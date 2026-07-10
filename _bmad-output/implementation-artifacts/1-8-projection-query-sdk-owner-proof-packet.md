# EventStore Projection/Query SDK Owner Proof Packet

- EventStore commit SHA: `f31777ae8dd3902f65a27777a04ee49d790a6e8f`
- Owner approval source:
  - PR: not recorded for this local owner-proof packet
  - reviewer: Administrator
  - approval date: 2026-07-10
  - source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-10.md`

## Inspected SDK Source Surfaces

- `src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`
- `src/Hexalith.EventStore.DomainService/IDomainQueryHandler.cs`
- `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`
- `src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs`
- `src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`
- `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs`
- `src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs`
- `src/Hexalith.EventStore.DomainService/DomainQueryDispatcher.cs`
- `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
- `src/Hexalith.EventStore.Client/Registration/ReadModelStoreServiceCollectionExtensions.cs`
- `src/Hexalith.EventStore.Client/Registration/QueryCursorCodecServiceCollectionExtensions.cs`

## Evidence by Requirement

### G3 read-model erasure hooks

- classification: blocked
- source paths:
  - `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`
  - `src/Hexalith.EventStore.Client/Projections/DaprReadModelStore.cs`
  - `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Projections/InMemoryReadModelStoreTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Projections/DaprReadModelStoreTests.cs`
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - `dotnet test tests/Hexalith.EventStore.Testing.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - PASS: `Hexalith.EventStore.Testing.Tests` passed 144/144.
  - Missing API: `IReadModelStore` exposes `GetAsync`, `SaveAsync`, and `TrySaveAsync` only. There is no public delete/erase operation, no DAPR implementation, and no in-memory fake/test coverage for read-model erasure.

### G10 index batching or approved equivalent

- classification: already available
- source paths:
  - `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs`
  - `src/Hexalith.EventStore.Testing/Fakes/InMemoryReadModelStore.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelWritePolicyTests.cs`
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - Approved equivalent: multi-key/index read models are supported through distinct state keys plus optimistic `ReadModelWritePolicy.UpdateAsync`, `ApplyEventsAsync`, and `MergeAsync`. `ReadModelWritePolicyTests.AggregateAndIndexWrites_UpdateSeparateKeysAndMergeIndexAfterConflict` proves aggregate and index writes across separate keys with conflict-aware merge behavior.

### G6 freshness mapping

- classification: blocked
- source paths:
  - `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessState.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelFreshness.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelFreshnessExtensions.cs`
  - `src/Hexalith.EventStore.Contracts/Queries/QueryResponseMetadata.cs`
  - `src/Hexalith.EventStore.Contracts/Queries/QueryWarningCodes.cs`
  - `src/Hexalith.EventStore.Contracts/Queries/QueryProblemReasonCodes.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelFreshnessTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Gateway/EventStoreGatewayClientTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Queries/QueryRouterTests.cs`
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - `dotnet test tests/Hexalith.EventStore.Server.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - PASS: `Hexalith.EventStore.Server.Tests` passed 2268/2293 with 25 skipped.
  - Existing support is partial: `ReadModelFreshnessState` models `Unknown`, `Current`, `Aging`, and `Stale`; `QueryResponseMetadata` carries `IsStale`, `IsDegraded`, warning codes, projection version, ETag, and served time. There is no EventStore-owned mapping that preserves all Parties states `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly` with current `ProjectionFreshnessMetadata` semantics.

### Duplicate and out-of-order replay

- classification: already available
- source paths:
  - `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs`
  - `src/Hexalith.EventStore.DomainService/DomainServiceRequestRouter.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs`
  - `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateReplayTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - `dotnet test tests/Hexalith.EventStore.Server.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - PASS: `Hexalith.EventStore.Server.Tests` passed 2268/2293 with 25 skipped.
  - Evidence: `AggregateReplayerTests.Replay_OutOfOrderInput_AppliesInSequenceOrder`, `Replay_DuplicateSequenceNumber_FailsExplicitly`, and sequence-gap tests cover replay ordering and duplicate/gap failures. `ProjectionUpdateOrchestratorTests.UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState` proves repeated projection triggers keep identical full-replay state.

### Full rebuild verification

- classification: blocked
- source paths:
  - `src/Hexalith.EventStore.Server/Projections/IProjectionRebuildOrchestrator.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
  - `src/Hexalith.EventStore.Server/DomainServices/IAggregateStateReconstructor.cs`
  - `src/Hexalith.EventStore.Server/DomainServices/DaprAggregateStateReconstructor.cs`
- test paths:
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/DomainServices/DaprAggregateStateReconstructorTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs`
  - `tests/Hexalith.EventStore.Sample.Tests/Counter/CounterAggregateReplayTests.cs`
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - `dotnet test tests/Hexalith.EventStore.Server.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - PASS: `Hexalith.EventStore.Server.Tests` passed 2268/2293 with 25 skipped.
  - Existing support is partial: projection rebuild orchestration and aggregate replay are covered separately. Missing behavior: no public EventStore proof path or test verifies rebuilt projection read models against canonical aggregate replay before a consuming module deletes its local rebuild/rollback path.

### Cursor scope compatibility

- classification: already available
- source paths:
  - `src/Hexalith.EventStore.Client/Queries/IQueryCursorCodec.cs`
  - `src/Hexalith.EventStore.Client/Queries/QueryCursorCodec.cs`
  - `src/Hexalith.EventStore.Client/Queries/QueryCursorScope.cs`
  - `src/Hexalith.EventStore.Client/Registration/QueryCursorCodecServiceCollectionExtensions.cs`
  - `src/Hexalith.EventStore.DomainService/EventStoreDataProtectionServiceCollectionExtensions.cs`
  - `src/Hexalith.EventStore.DomainService/DaprXmlRepository.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Queries/QueryCursorCodecTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Queries/QueryCursorScopeTests.cs`
  - `tests/Hexalith.EventStore.Client.Tests/Registration/ReadModelAndCursorRegistrationTests.cs`
  - `tests/Hexalith.EventStore.DomainService.Tests/EventStoreDataProtectionTests.cs`
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - `dotnet test tests/Hexalith.EventStore.DomainService.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - PASS: `Hexalith.EventStore.DomainService.Tests` passed 85/85.
  - Evidence covers opaque protected cursors, query type binding, scope binding, tamper/key-rotation rejection, purpose isolation, size limits, registration, DataProtection key persistence, and collision-safe `QueryCursorScope` escaping.

## Additional Validation

- `dotnet build Hexalith.EventStore.slnx --configuration Release` -> PASS, 0 warnings, 0 errors.
- `git diff --check` -> PASS.

## Rollback Note

No production SDK code was changed by this proof packet. Consuming modules must keep their local projection/query actors, rebuild services, freshness adapters, erasure handling, and rollback paths until a later EventStore owner proof packet records every required item as available and the consuming repository verifies its checked-out EventStore pin matches the approved SHA.

## Known Limitations

- `IReadModelStore` lacks public read-model delete/erase operations.
- EventStore does not yet expose an owner-approved mapping for all Parties freshness states: `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`.
- Projection rebuild orchestration and aggregate replay are tested separately, but there is no owner proof that full rebuild output is verified against aggregate replay before consumer rollback deletion.

## Final Decision

`still blocked`

The consuming Parties matrix row must remain `needs-additive-api`; no consuming story is authorized to mark the EventStore projection/query SDK prerequisite `available` from this packet.
