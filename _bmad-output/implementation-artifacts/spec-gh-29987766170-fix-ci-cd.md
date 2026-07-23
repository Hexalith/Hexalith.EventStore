---
title: 'Fix live-sidecar fixture environment propagation'
type: 'bugfix'
created: '2026-07-23'
status: 'done'
baseline_commit: '6044b995fe40f8feb282c59db8ff5feb0df7c70a'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions run `29987766170`, job `89143363003`, failed all 49 live-sidecar tests because `DaprTestContainerFixture` configured idempotency digest keys while its DI-registered `IHostEnvironment` remained `Production`. Current `main` fixed the identical Oq8 fixture, but replacement run `30018569124` still failed the 48 tests using the shared fixture.

**Approach:** Set `EnvironmentName = "Testing"` in `WebApplicationOptions` when creating both the primary and replica `WebApplicationBuilder` instances, matching the proven Oq8 correction so the environment is available to startup validation.

## Boundaries & Constraints

**Always:** Keep the change fixture-only; correct both primary and replica builders; preserve Release/package-mode CI behavior; retain the production rejection of configuration-backed digest keys.

**Ask First:** Any change to production idempotency validation, workflow-level environment variables, `.github/workflows/integration.yml`, or files beyond the shared live-sidecar fixture.

**Never:** Weaken or bypass `ValidateIdempotencyAdmissionOptions`; broadly force the CI runner into a test environment; switch the integration lane to project references; modify submodules or unrelated Story 1.20 proof artifacts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Primary fixture startup | Configuration digest key with the primary in-process host | DI resolves environment `Testing`; host startup proceeds into live-sidecar execution | Startup must still surface unrelated validation or infrastructure failures |
| Replica fixture startup | Multi-host test starts the replica after the primary | Replica DI also resolves `Testing`; failover/idempotency tests execute | Replica cleanup and existing failure propagation remain unchanged |
| Production application startup | Configuration-backed digest key outside Development/Test/Testing | Startup remains rejected by the production safety validator | Existing `OptionsValidationException` contract is preserved |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` -- creates the primary and replica in-process hosts and supplies their configuration-backed test digest key.
- `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` -- golden example already corrected on `main` by setting the environment during builder creation.
- `src/Hexalith.EventStore.Server/Configuration/ValidateIdempotencyAdmissionOptions.cs` -- production safety boundary that must remain unchanged.
- `.github/workflows/integration.yml` -- authoritative Release/package-mode command for the 49-test live-sidecar lane; no edit expected.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs` -- pass `EnvironmentName = "Testing"` through `WebApplicationOptions` at creation of both builders and remove the ineffective post-creation mutations.

**Acceptance Criteria:**
- Given the shared fixture's configuration-backed digest key, when the primary host starts, then startup validation observes `Testing` and a representative primary-host test executes past fixture initialization.
- Given a multi-host live-sidecar scenario, when the replica host starts, then its startup validation observes `Testing` and the replica-owned test executes past fixture initialization.
- Given the exact Integration Tests workflow lane in Release/package mode, when the full project runs, then all 49 live-sidecar tests pass.
- Given a production host configured with a configuration-backed digest key, when startup validation runs, then the existing production-rejection test still passes.

## Spec Change Log

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -warnaserror -m:1` -- expected: project builds with zero warnings or errors.
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false -warnaserror -m:1` -- expected: the production-guard test assembly builds with zero warnings or errors.
- `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method "Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.AggregateActorIntegrationTests.ProcessCommandAsync_NewAggregate_ActivatesActorAndReturnsAccepted"` -- expected: primary fixture initializes and the focused test passes.
- `dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method "Hexalith.EventStore.Server.LiveSidecar.Tests.Actors.IdempotencyAdmissionLiveSidecarTests.MultiHostAdmission_PrimaryHostRemovedBeforeExecution_ExecutesAndReplaysExactlyOnceOnReplica"` -- expected: primary/replica fixture path passes.
- `dotnet test tests/Hexalith.EventStore.Server.LiveSidecar.Tests/ --configuration Release --no-restore -p:UseHexalithProjectReferences=false --logger "trx;LogFileName=live-sidecar-results.trx" --results-directory TestResults/live-sidecar --collect:"XPlat Code Coverage"` -- expected: workflow-equivalent lane reports 49 passed and zero failed.
- `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -method "Hexalith.EventStore.Server.Tests.Configuration.EventStoreServerServiceCollectionExtensionsTests.AddEventStoreServerRejectsConfiguredDigestKeyInProductionAtHostStartAsync"` -- expected: production guard test passes.

## Suggested Review Order

**Host construction**

- Configure the primary environment during builder creation so DI observes `Testing`.
  [`DaprTestContainerFixture.cs:730`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L730)

- Apply the same creation-time environment contract to the failover replica.
  [`DaprTestContainerFixture.cs:881`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L881)

**Regression enforcement**

- Verify the primary service provider resolves the exact approved environment.
  [`DaprTestContainerFixture.cs:769`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L769)

- Enforce the same exact DI contract before the replica starts.
  [`DaprTestContainerFixture.cs:914`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L914)

- Fail fixture initialization immediately when either DI environment drifts.
  [`DaprTestContainerFixture.cs:929`](../../tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/DaprTestContainerFixture.cs#L929)
