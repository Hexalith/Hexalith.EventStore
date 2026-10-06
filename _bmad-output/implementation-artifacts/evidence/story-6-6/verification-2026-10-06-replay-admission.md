# Story 6.6 replay admission and cancellation verification

**Status:** Verified local safety slice; parent Story 6.6 remains in progress.
No M1–M8 task or activation obligation closes from this report.

## Implemented behavior

- Legacy replay captures caller references and admits the entire source before
  domain callbacks, private payload copies or sorting. Admission enforces the
  100,000-event ceiling, 64 MiB readable-payload budget, 256 MiB accounted budget
  and 512 KiB canonical Web-JSON metadata ceiling. The 8,192-byte charge per
  event can make the accounted budget reject a smaller count. Invalid Unicode
  refuses safely. This establishes no authenticated raw-source proof or bound
  on an arbitrary domain state graph.
- Private eligible payload copies survive mutation by earlier domain callbacks
  and are cleared on disposal or exceptional capture exits; caller bytes remain
  unchanged. Encoded metadata and retained string capacity are both accounted.
- Hold/Limit/Conflict are additive enum values 8/9/10. Nullable `ReasonCode`
  preserves the eight-member result constructor and deconstruction; absent
  optional members remain omitted from legacy JSON. Admin maps valid outcomes
  to 503/422/409, with `Retry-After: 30` for Hold. Unsupported reasons or
  contradictory status/state/timeline/progress shapes produce a safe 500.
- Built-in processing/replay interfaces carry the original cancellation token
  across binding, Apply, serialization and Handle boundaries. Binding failures
  after cancellation become cancellation rather than a reconstruction result.
  Reflection cancellation is unwrapped. An already-started legacy async Handle
  is awaited; its fault is observed rather than abandoning the task.
- The V1 producer snapshots all result references before a result getter or
  serializer can replace later entries. Serialized source bytes are privately
  copied before serializer callbacks, charged to the budget and cleared after
  use. Partial produced payloads are cleared on failure.

Pre-existing cancellation work was preserved with the user's authorization.
Concurrent Story 6.1 work changed shared files and Git state during this run;
its changes and staged evidence were preserved. This report does not review or
close that separate remediation. The final source and assembly hashes are in
[the local capture](replay-admission-2026-10-06/capture.json).

## Validation

Commands and retained outputs are indexed by the local capture. Tests executed
the built xUnit assemblies, rather than a solution-level test command.

| Check | Result |
| --- | --- |
| New command/historical-converter controls before the fix | 14 failures reproduced; cancellation paths then passed |
| New contradictory typed-result controls before the fix | 15 failures reproduced; all 42 controller replay tests then passed |
| Final Release Client suite | 1,056 total; zero failures/skips |
| Final Release DomainService suite | 427 total; zero failures/skips |
| Final Release replay wire/ABI and paged-contract classes | 7 total; zero failures/skips |
| Post-fixture-rename Client cancellation/admission classes | 70 total; zero failures/skips |
| Final Release Server suite | 3,769 total; one content-scan failure and 25 existing DW1 ATDD skips |
| Final Release solution build | Passed with `-m:1 -warnaserror -p:UseHexalithProjectReferences=false`; zero warnings/errors |
| Client Release rebuild after fixture rename | Passed; zero warnings/errors |
| Current Dapr-only source preflight with mutations | Passed; all 11 prohibited mutations rejected |
| Historical approval preflight | Passed earlier in this run; describes historical approval, not current activation |
| Release package inventory | All 14 packages packed; manifest inventory validation passed |
| Package compatibility retry with supplied inventory | Two tests passed without skips; includes three isolated reminder consumers |
| OCI provenance retry outside sandbox | One test passed without skips |

The broader Debug Contracts run executed 2,252 tests: one OCI publish failed
because the sandbox denied access to `mcr.microsoft.com`, and two package tests
skipped because no inventory was supplied. The isolated OCI retry and both
inventory-backed package tests subsequently passed. These retries do not
claim a new full Contracts collection run against every concurrent change.

Initial sandbox MSBuild runs exited without diagnostics. Debug builds against
stale Release restore assets also reported CS1704/MSB3243 duplicate references
for `Hexalith.Commons.UniqueIds`. Restoring the selected project graph outside
the sandbox resolved those build failures; final builds were warning-free.

## Exact remaining repository gate failure

`dotnet tests/Hexalith.EventStore.Server.Tests/bin/Release/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*SecretsProtectionTests' -method '*TrackedReusableContent_DoesNotContainUsableSecrets'`
still exits 1. The guard reports only
`_bmad-output/implementation-artifacts/evidence/6-1-p1r-remediation/source-candidate.diff:2590`.
It contains a preserved cancellation fixture assignment, not authentication
material. The current fixture was renamed and no longer flags. The copied line
belongs to another run's SHA-bound source evidence and was left intact; its
owner must reconcile that evidence and the guard without invalidating its
bindings. No guard exception or bypass was introduced.

## Acceptance and authority still open

The [loader-policy comparison](dependency-loader-options-2026-10-06.md) is a
proposal only. The [loader decision](dependency-loader-decision.md) still
requires an owner-selected policy for explicit loads and nested effects.
Authoritative per-domain manifest/catalog closure remains absent. The exact
production profile `deploy/dapr/production-profile.yaml` is still absent, and
this run establishes no two-host Dapr production qualification or provider
attestation. V2 and proof-dependent activation remain fenced.

The [parent spec](../../spec-6-6-event-versioning-and-upcasting-implementation.md)
retains its frozen intent, baseline and all eight open task groups. The full
acceptance matrix and formal completion review have not passed. No Git
mutation, remote publication or deployment was performed by this run.
