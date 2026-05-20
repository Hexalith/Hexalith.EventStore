# Post-Epic Deferred DW12: Consistency Actor-State Contract and Dispatcher Fix

Status: backlog

Context created: 2026-05-20
Source proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-20-admin-ui-manual-retest-residuals.md`
Source evidence: `_bmad-output/test-artifacts/admin-ui-manual-tests-restants-apres-corrections-2026-05-20.md`

## Story

As an EventStore operator running consistency checks,
I want the checker to read event and metadata state through a supported contract,
so that existing actor-state data is not reported as missing and UI refreshes remain dispatcher-safe.

## Scope

This story covers:

- CC-4 / Issues 16 and 17: Consistency check reports false-positive sequence and metadata anomalies for the seeded Counter stream.
- The related Blazor Server dispatcher exception observed in `Consistency.razor`.

This story does not cover:

- Projection detail contract gaps (DW11).
- Tenant lifecycle timeout root cause (DW13).
- Deferred snapshot/backup/compaction UX policy (DW14).

## Evidence

Observed after `Run Check` on `tenant-a / counter`:

- `Streams Checked = 1`.
- `Anomalies Found = 20`.
- 18 `sequencecontinuity` anomalies: missing events at sequence 1..18.
- 1 `metadataconsistency` anomaly: aggregate metadata is missing.
- 1 `projectionpositions` warning: domain-specific projection position validation is not granular.

Redis verification from the manual evidence:

- `admin:stream-activity:all` contains `tenant-a / counter / counter-1`, `eventCount = 18`, `lastEventSequence = 18`.
- Raw keys such as `tenant-a:counter:counter-1:events:*` are absent.
- Actor-state keys exist under the DAPR actor-state format, for example:

  ```text
  eventstore||AggregateActor||tenant-a:counter:counter-1||tenant-a:counter:counter-1:events:1
  ```

Code evidence:

- `DaprConsistencyCommandService` reconstructs raw event keys as `{tenant}:{domain}:{aggregateId}:events:{sequence}`.
- `DaprConsistencyCommandService` reconstructs metadata keys as `{tenant}:{domain}:{aggregateId}:metadata`.
- Project context says DAPR actor state must go through `IActorStateManager`; admin tooling should not guess actor-state backend key formats.
- `Consistency.razor` had dispatcher-related `StateHasChanged()` evidence in async completion paths.

## Acceptance Criteria

1. Consistency checks no longer report missing events 1..N for streams whose events exist under the supported EventStore storage/actor contract.

2. `DaprConsistencyCommandService` stops reconstructing raw keys such as:

   ```text
   {tenant}:{domain}:{aggregateId}:events:{sequence}
   {tenant}:{domain}:{aggregateId}:metadata
   ```

   unless that format is explicitly proven to be the supported contract for the current implementation.

3. Preferred implementation reads through an EventStore/admin stream-read contract or an admin-maintained consistency index, not by bypassing actor isolation.

4. Sequence continuity and metadata consistency tests cover the `tenant-a/counter/counter-1` shape with 18 events and no false positives.

5. Projection-position warnings are either made granular or explicitly labeled as coarse-grained warnings, not unexplained anomalies.

6. `Consistency.razor` uses `await InvokeAsync(StateHasChanged)` in async completion/finally paths that can run outside the Blazor renderer dispatcher.

7. Manual retest for Issues 16 and 17 returns no unexplained anomaly cluster for the seeded Counter stream.

## Expected File Touches

Likely implementation files:

- `src/Hexalith.EventStore.Admin.Server/Services/DaprConsistencyCommandService.cs`
- `src/Hexalith.EventStore.Admin.UI/Pages/Consistency.razor`

Likely tests:

- `tests/Hexalith.EventStore.Admin.Server.Tests/Services/DaprConsistencyCommandServiceTests.cs`
- `tests/Hexalith.EventStore.Admin.UI.Tests/Pages/ConsistencyPageTests.cs` if present, otherwise add targeted bUnit coverage in the existing Admin UI test project.

## Validation

Run targeted tests first:

```powershell
$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$dotnetDir;$dotnetDir\tools;$env:PATH"
dotnet test tests/Hexalith.EventStore.Admin.Server.Tests --configuration Release --filter "FullyQualifiedName~DaprConsistencyCommandServiceTests" -m:1
dotnet test tests/Hexalith.EventStore.Admin.UI.Tests --configuration Release --filter "FullyQualifiedName~Consistency" -m:1
```

After implementation, restart Aspire and rerun manual Issues 16 and 17.

## Tasks

- [ ] Add regression tests for actor-state-backed stream continuity without false positives.
- [ ] Add or update metadata consistency tests for the same stream shape.
- [ ] Replace unsupported raw-key reads with a supported read/index contract.
- [ ] Fix dispatcher-unsafe `StateHasChanged()` paths in `Consistency.razor`.
- [ ] Run targeted tests and record results.
- [ ] Rerun manual Issues 16 and 17 validation.

