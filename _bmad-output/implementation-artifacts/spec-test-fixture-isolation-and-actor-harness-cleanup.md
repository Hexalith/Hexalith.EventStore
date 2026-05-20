---
title: 'Test fixture isolation and actor harness cleanup'
type: 'refactor'
created: '2026-05-20'
status: 'in-review'
baseline_commit: '6adf47fe196f03a685bd004a6f1a50e872ca1ccb'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/test-artifacts/test-review.md'
---

<frozen-after-approval reason="human-owned intent - do not modify unless human renegotiates">

## Intent

**Problem:** The approved RV found that the test suite still carries flake and maintenance risk from duplicated actor StateManager reflection setup, shared mutable Dapr fixture fakes, and a few wall-clock assertions or sleeps.

**Approach:** Centralize the actor test StateManager injection, make the shared Dapr fixture expose an explicit reset boundary for every test instance, and replace narrow timing assertions with deterministic bounds that do not depend on "recent enough" wall-clock windows.

## Boundaries & Constraints

**Always:** Keep this as test-infrastructure cleanup only. Preserve current production behavior. Use existing xUnit, Shouldly, NSubstitute, fake Dapr, and helper patterns. Keep all edits local to tests/test helpers unless a helper already lives in `src/Hexalith.EventStore.Testing`.

**Ask First:** Any production API change, new package dependency, broad test-file decomposition, Redis topology change, or Aspire app model change.

**Never:** Do not rewrite the Dapr integration suite, change test collection topology, introduce recursive submodule operations, or normalize unrelated legacy assertion style.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Actor helper | A test creates an `AggregateActor` with a mocked `IActorStateManager` | The test calls one helper to inject the state manager; raw reflection no longer repeats across actor tests | Helper throws a clear `InvalidOperationException` if Dapr changes the expected property |
| Dapr fixture reuse | xUnit creates another test class in `[Collection("DaprTestContainer")]` | Fake domain responses, published events, dead letters, and command status state are reset before class setup | Fixture cleanup restores env vars even when initialization fails |
| Timestamp assertions | A test checks a generated timestamp | Assertions compare against a captured before/after window or fixed value, not an arbitrary wall-clock recency window | Failure reports the actual captured timestamp bounds |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.Tests/TestUtilities/ActorStateManagerTestHelper.cs` -- new shared helper for Dapr actor StateManager injection.
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs` and other actor/observability/security tests -- replace duplicated reflection setup with the helper.
- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` -- add reset support to match existing fake reset surfaces.
- `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs` -- add a shared `ResetTestState` boundary and failure-safe env restore.
- `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyRecordTests.cs` -- replace recency assertion with captured before/after bounds.
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs` -- replace wall-clock recency assertions with captured before/after bounds.
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs` -- replace `Thread.Sleep(50)` with bUnit wait semantics.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Server.Tests/TestUtilities/ActorStateManagerTestHelper.cs` -- add a single helper for actor StateManager injection -- prevents repeated reflection drift.
- [x] Actor test files under `tests/Hexalith.EventStore.Server.Tests` -- replace direct `typeof(Actor).GetProperty("StateManager")` blocks with the helper -- closes the RV maintainability finding.
- [x] `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs` and `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs` -- reset shared fake state before class setup and restore DAPR env vars in init failure paths -- reduces collection state bleed.
- [x] `IdempotencyRecordTests.cs`, `DeadLetterMessageCompletenessTests.cs`, and `Dw5TypeCatalogRenderLoopAtddTests.cs` -- remove narrow wall-clock/sleep checks -- reduces deterministic flake points.
- [x] Run targeted verification for touched test projects where practical.

**Acceptance Criteria:**
- Given actor tests create an `AggregateActor`, when mocked actor state is attached, then no actor test file repeats direct StateManager reflection setup.
- Given a Dapr collection test constructor configures fake responses, when it runs after another collection test, then domain, event, dead-letter, and command-status fake state starts clean.
- Given fixture initialization throws after setting DAPR env vars, when the exception leaves `InitializeAsync`, then prior env-var values are restored.
- Given timestamp tests execute under scheduler jitter, when assertions run, then they compare the observed timestamp to captured before/after bounds rather than a fixed recent-window guess.

## Spec Change Log

## Design Notes

This story intentionally does not split oversized files. The RV identified that as a larger maintainability project; this pass removes high-leverage duplicated setup and flake sources without changing behavioral coverage.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~IdempotencyRecordTests|FullyQualifiedName~DeadLetterMessageCompletenessTests"` -- expected: targeted tests pass or pre-existing Server.Tests CA2007 build issue is identified separately.
- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --filter FullyQualifiedName~Dw5TypeCatalogRenderLoopAtddTests` -- expected: targeted bUnit tests pass.

## Dev Agent Record

### Debug Log

- Captured baseline commit `6adf47fe196f03a685bd004a6f1a50e872ca1ccb`.
- Replaced 35 duplicated actor StateManager reflection call sites with `ActorStateManagerTestHelper.SetStateManager(...)`.
- Static check: only the shared helper now contains `typeof(Actor).GetProperty("StateManager", ...)`.
- Static check: no remaining `Thread.Sleep(50)` or touched recent-window `DateTimeOffset.UtcNow` assertions in the reviewed paths.
- `dotnet test tests/Hexalith.EventStore.Admin.UI.Tests/Hexalith.EventStore.Admin.UI.Tests.csproj --filter FullyQualifiedName~Dw5TypeCatalogRenderLoopAtddTests` passed: 2 tests.
- First server targeted run built successfully but exposed stale dead-letter protected-data redaction expectations in this touched file.
- Aligned `DeadLetterMessageCompletenessTests` with the existing `DeadLetterMessage.FromException` safe diagnostic contract.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --filter "FullyQualifiedName~IdempotencyRecordTests|FullyQualifiedName~DeadLetterMessageCompletenessTests"` passed: 11 tests.
- `git diff --check` passed for the touched code and spec paths.

### Completion Notes

- Added a single Dapr actor StateManager injection helper and routed all 35 direct call sites through it.
- Added `FakeEventPublisher.Reset()` and `DaprTestContainerFixture.ResetTestState()` so Dapr collection tests start with clean domain, event, dead-letter, and command-status fake state.
- Wrapped Dapr fixture initialization in failure cleanup that restores `DAPR_HTTP_PORT` and `DAPR_GRPC_PORT` if startup fails after mutation.
- Replaced wall-clock recency assertions with captured before/after bounds and removed the bUnit `Thread.Sleep(50)` by awaiting the component invocation.
- Updated stale dead-letter completeness assertions to expect protected diagnostic redaction instead of raw provider exception text.
- Multi-agent quick-dev review was not launched because delegated sub-agent work was not explicitly requested in this session; this artifact is left `in-review`.

### File List

- `src/Hexalith.EventStore.Testing/Fakes/FakeEventPublisher.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/Dw5TypeCatalogRenderLoopAtddTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/TestUtilities/ActorStateManagerTestHelper.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorGetEventsTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/AggregateActorTestHelper.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/BackpressureTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/DeadLetterRoutingTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/Dw1DrainHardeningAtddTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/Dw8DrainReasonClassifierTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/ETagActorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventDrainRecoveryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventPublicationIntegrationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/EventReplayProjectionActorTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/IdempotencyRecordTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/MultiTenantPublicationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/StateMachineIntegrationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Actors/TombstoningLifecycleTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/AtLeastOnceDeliveryTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Events/PersistThenPublishResilienceTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Fixtures/DaprTestContainerFixture.cs`
- `tests/Hexalith.EventStore.Server.Tests/Integration/ETagActorIntegrationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterMessageCompletenessTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterOriginTracingTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Observability/DeadLetterTraceChainTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/DataPathIsolationTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/SecurityAuditLoggingTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Security/TenantInjectionPreventionTests.cs`
- `tests/Hexalith.EventStore.Server.Tests/Telemetry/EndToEndTraceTests.cs`
- `_bmad-output/implementation-artifacts/spec-test-fixture-isolation-and-actor-harness-cleanup.md`

### Change Log

- 2026-05-20: Implemented approved RV follow-up for actor harness centralization, Dapr fixture reset isolation, and narrow timing-flake cleanup.

