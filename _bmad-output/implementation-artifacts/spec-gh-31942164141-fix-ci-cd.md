---
title: 'Fix CI/CD CS8620 nullability errors in projection tests'
type: 'bugfix'
created: '2026-08-16'
status: 'done'
review_loop_iteration: 0
baseline_commit: '226a9e815e0755e6c118d4ae8a338b426dc5bdb3'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** GitHub Actions push workflows CI [31942164141](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31942164141) and Integration Tests [31942163828](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/31942163828) fail in Release build mode with compiler error `CS8620` due to nullability mismatches in NSubstitute `.Returns(...)` calls across `DaprProjectionDeliveryRetrySchedulerTests.cs` and `ProjectionDeliveryCheckpointStoreTests.cs`.

**Approach:** Fix the mock `.Returns((T?)null)` calls in `ProjectionDeliveryCheckpointStoreTests.cs` and `DaprProjectionDeliveryRetrySchedulerTests.cs` to `.Returns((T)null!)` so that `Task<T>` return types align cleanly with NSubstitute's generic `Returns<T>` extension without nullability warnings, and verify the Release build and all tests pass locally.

## Boundaries & Constraints

**Always:** Fix the nullability mismatches using standard NSubstitute `.Returns((T)null!)` or aligned type patterns matching the method signature. Validate Release builds with `TreatWarningsAsErrors=true`. Ensure all tests pass.

**Ask First:** Modifying runtime production code or Dapr SDK contracts when the issue is confined to test mock configuration.

**Never:** Disable `TreatWarningsAsErrors` or suppress compiler warnings with pragmas in solution/project configuration. Mutate submodule pointers or nested submodules. Bypass commitlint or git hooks.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Release build | `dotnet build Hexalith.EventStore.slnx -c Release` | 0 Warnings, 0 Errors; build succeeds | Fail closed on any compiler warning/error |
| Server unit tests | `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Release` | All unit tests pass including checkpoint and retry scheduler tests | Assertions pass |
| Integration tests | `dotnet test tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Release` | All integration tests pass | Assertions pass |

</frozen-after-approval>

## Code Map

- `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryCheckpointStoreTests.cs:78,80,139` -- `GetStateAsync<ProjectionCheckpoint>` and `GetStateAsync<ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker>` mock setups using `.Returns((T?)null)` causing CS8620.
- `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionDeliveryRetrySchedulerTests.cs:150` -- `GetStateAsync<ProjectionDeliveryRetryLedger>` mock setup using `.Returns((ProjectionDeliveryRetryLedger?)null)` causing CS8620.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryCheckpointStoreTests.cs` -- Replace `.Returns((ProjectionCheckpoint?)null)` and `.Returns((ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker?)null)` at lines 78, 80, 139 with `.Returns((ProjectionCheckpoint)null!)` and `.Returns((ProjectionCheckpointTracker.ProjectionCheckpointMigrationMarker)null!)`.
- [x] `tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionDeliveryRetrySchedulerTests.cs` -- Replace `.Returns((ProjectionDeliveryRetryLedger?)null)` at line 156 with `.Returns((ProjectionDeliveryRetryLedger)null!)`.
- [x] `tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj` -- Build in Release configuration to verify 0 warnings and 0 errors.
- [x] `Hexalith.EventStore.slnx` -- Build full solution in Release configuration to verify zero regressions.

**Acceptance Criteria:**
- Given Release configuration (`-c Release`), when `dotnet build Hexalith.EventStore.slnx` runs, then build exits with code 0 and 0 errors / 0 warnings.
- Given `ProjectionDeliveryCheckpointStoreTests` and `DaprProjectionDeliveryRetrySchedulerTests`, when tests execute, then all tests pass.

## Spec Change Log

_None._

## Design Notes

NSubstitute's `Returns<T>(this Task<T> value, T returnThis)` extension method infers `T` from `returnThis`. When passing `(ProjectionCheckpoint?)null`, `T` is inferred as nullable `ProjectionCheckpoint?`, causing the `value` parameter to expect `Task<ProjectionCheckpoint?>`. Because `Task<T>` is invariant and `GetStateAsync<ProjectionCheckpoint>` returns `Task<ProjectionCheckpoint>`, C# flags error `CS8620` (nullability mismatch of reference types). Casting to `(ProjectionCheckpoint)null!` keeps `T` inferred as non-nullable `ProjectionCheckpoint`, matching `Task<ProjectionCheckpoint>` without warnings while returning `null` at runtime.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release` -- expected: Build succeeded with 0 errors and 0 warnings.
- `dotnet test tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Release --no-build` -- expected: All tests pass.
- `dotnet build Hexalith.EventStore.slnx --configuration Release` -- expected: Build succeeded across all projects.

## Suggested Review Order

- Align absent-state mock returns with non-nullable task signatures using null-forgiveness.
  [`ProjectionDeliveryCheckpointStoreTests.cs:78`](../../tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryCheckpointStoreTests.cs#L78)

- Ensure migration marker and post-erasure absent setups avoid CS8620.
  [`ProjectionDeliveryCheckpointStoreTests.cs:139`](../../tests/Hexalith.EventStore.Server.Tests/Projections/ProjectionDeliveryCheckpointStoreTests.cs#L139)

- Correct legacy ledger absent mock return in shard retry test.
  [`DaprProjectionDeliveryRetrySchedulerTests.cs:156`](../../tests/Hexalith.EventStore.Server.Tests/Projections/DaprProjectionDeliveryRetrySchedulerTests.cs#L156)

