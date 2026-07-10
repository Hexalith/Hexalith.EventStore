# EventStore Projection/Query SDK Owner Proof Packet

- EventStore commit SHA: `f31777ae8dd3902f65a27777a04ee49d790a6e8f`
- Owner approval source:
  - status: pending proof-result review
  - PR: not recorded for this local owner-proof packet
  - reviewer: pending
  - approval date: pending
  - story authorization: Administrator approved creation and implementation of Story 1.8 on 2026-07-10
  - authorization source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-10.md`

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

### Intended EventStore pin

- classification: already available
- source paths:
  - `_bmad-output/implementation-artifacts/1-8-projection-query-sdk-owner-parity-proof.md`
- test paths:
  - not applicable; this is repository identity evidence
- validation command:
  - `git rev-parse HEAD`
- result:
  - PASS: Story execution started at `f31777ae8dd3902f65a27777a04ee49d790a6e8f`, the EventStore runtime commit intended for consuming modules.
  - The later proof-only documentation commit does not change the intended runtime pin; consuming repositories must compare their checked-out `references/Hexalith.EventStore` SHA to this value before migration.

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
  - Missing lifecycle behavior: erasure must also remove the companion sequence/checkpoint state. Leaving a stale high-water mark can cause valid events for a recreated aggregate with the same identifier to be discarded. No generic API or test proves coordinated read-model and checkpoint erasure.

### G10 index batching or approved equivalent

- classification: blocked
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
  - Existing support is partial: multi-key/index read models can address distinct state keys through optimistic `ReadModelWritePolicy.UpdateAsync`, `ApplyEventsAsync`, and `MergeAsync` operations.
  - Missing behavior or approval: `ReadModelWritePolicyTests.AggregateAndIndexWrites_UpdateSeparateKeysAndMergeIndexAfterConflict` performs two sequential single-key writes. It does not prove batching, flush behavior, transactional consistency, or recovery from a failure between detail and index writes, and no owner approval accepts that weaker behavior as the G10 equivalent.
  - G10 remains blocked until EventStore provides a generic batch capability or records an explicit approved-equivalent contract with partial-failure, idempotency, and flush semantics plus focused tests.

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

- classification: blocked
- source paths:
  - `src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs`
  - `src/Hexalith.EventStore.Server/Actors/AggregateActor.cs`
  - `src/Hexalith.EventStore.Server/Projections/ProjectionUpdateOrchestrator.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
- test paths:
  - `tests/Hexalith.EventStore.Client.Tests/Aggregates/AggregateReplayerTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs`
  - `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionUpdateOrchestratorTests.cs`
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - `dotnet test tests/Hexalith.EventStore.Server.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - PASS: `Hexalith.EventStore.Server.Tests` passed 2268/2293 with 25 skipped.
  - Existing support is partial: `AggregateActor.GetEventsAsync(0)` reconstructs the canonical stream in sequence order, and `ProjectionUpdateOrchestratorTests.UpdateProjectionAsync_RepeatTriggersOnSameAggregate_ProducesIdenticalProjectionState` proves repeated full-replay triggers produce identical state.
  - The cited `AggregateReplayer` tests cover a separate aggregate-reconstruction path; duplicate sequence numbers fail explicitly there, while Parties requires projection delivery to be an idempotent no-op with no caller exception.
  - Missing proof: no focused test drives duplicate and out-of-order delivery through the `ProjectionUpdateOrchestrator` to `IDomainProjectionHandler` path and verifies final state equals one in-order delivery. The dispatcher forwards `ProjectionRequest.Events` unchanged, so the required projection-path behavior is not established.

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
- validation command:
  - `dotnet test tests/Hexalith.EventStore.Client.Tests/`
  - `dotnet test tests/Hexalith.EventStore.Server.Tests/`
- result:
  - PASS: `Hexalith.EventStore.Client.Tests` passed 535/535.
  - PASS: `Hexalith.EventStore.Server.Tests` passed 2268/2293 with 25 skipped.
  - Existing support is partial: projection rebuild orchestration and aggregate replay are covered separately.
  - Concrete blocker: rebuild delivery reads at most 256 events from the current per-aggregate checkpoint and sends only that page to `IDomainProjectionHandler`, whose contract requires the aggregate's complete event sequence. Each page response then overwrites the projection actor state. For streams longer than one page, a later page can therefore replace the projection with page-only state instead of a full rebuild.
  - Missing verification: no EventStore proof path or test compares rebuilt detail and index read models against canonical aggregate replay before a consuming module deletes its local rebuild/rollback path.

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
  - Evidence covers opaque protected cursors, query type binding, scope binding, tamper rejection, unrelated or lost key-ring rejection, purpose isolation, size limits, registration, DataProtection key persistence, and collision-safe `QueryCursorScope` escaping.
  - Normal Data Protection key rotation retains older keys and should preserve outstanding cursors; the unrelated ephemeral-provider test models key loss or replacement, not routine rotation.

## Additional Blocking SDK Constraints

### Projection handler persistence seam

- classification: blocked
- source paths:
  - `src/Hexalith.EventStore.DomainService/IDomainProjectionHandler.cs`
  - `src/Hexalith.EventStore.Client/Projections/IReadModelStore.cs`
  - `src/Hexalith.EventStore.Client/Projections/ReadModelWritePolicy.cs`
- result:
  - `IDomainProjectionHandler.Project` is synchronous, while all `IReadModelStore` and `ReadModelWritePolicy` persistence operations are asynchronous.
  - The current handler contract cannot safely await the detail/index persistence path required by Parties. A generic asynchronous projection seam or a platform-owned persistence handoff is required.

### Multiple projection handlers for one domain

- classification: blocked
- source paths:
  - `src/Hexalith.EventStore.DomainService/DomainProjectionHandlerRouteValidator.cs`
  - `src/Hexalith.EventStore.DomainService/DomainProjectionDispatcher.cs`
  - `src/Hexalith.EventStore.Contracts/Projections/ProjectionResponse.cs`
- result:
  - Projection handlers are routed only by `Domain`, duplicate domain registrations are rejected, and one request returns one `ProjectionResponse`.
  - Parties requires both detail and cross-aggregate index projection handlers for the `party` domain. The current route and response shape cannot express that fan-out without a generic composite or multi-projection contract.

## Additional Validation

- `dotnet build Hexalith.EventStore.slnx --configuration Release` -> PASS, 0 warnings, 0 errors.
- `git diff --check` -> PASS.

## Rollback Note

No production SDK code was changed by this proof packet. Consuming modules must keep their local projection/query actors, rebuild services, freshness adapters, erasure handling, and rollback paths until a later EventStore owner proof packet records every required item as available and the consuming repository verifies its checked-out EventStore pin matches the approved SHA.

## Known Limitations

- `IReadModelStore` lacks public read-model delete/erase operations and coordinated companion checkpoint erasure.
- Existing read-model writes are sequential single-key operations; G10 batching or an explicitly approved equivalent remains absent.
- EventStore does not yet expose an owner-approved mapping for all Parties freshness states: `Current`, `Stale`, `Rebuilding`, `Degraded`, `Unavailable`, and `LocalOnly`.
- Duplicate/out-of-order idempotency is not proven through the projection handler path.
- Paged rebuild delivery conflicts with the stateless full-replay handler contract and can overwrite state with a partial page.
- Projection rebuild output is not verified against aggregate replay before consumer rollback deletion.
- The synchronous projection handler cannot use the asynchronous read-model persistence seam safely.
- Domain-only projection routing and a single response cannot produce both Parties detail and index projections.

## Final Decision

`still blocked`

The consuming Parties matrix row must remain `needs-additive-api`; no consuming story is authorized to mark the EventStore projection/query SDK prerequisite `available` from this packet.
