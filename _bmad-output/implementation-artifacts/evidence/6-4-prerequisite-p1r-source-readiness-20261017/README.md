# Story 6.4 prerequisite: P1R current-source successor readiness

This is a local tooling and source-readiness record for proposed EventStore
3.125.0. It is not a package qualification, publication request, P1R acceptance,
or Story 6.4 implementation. The owner source is
`bb986902d3c1b2f3b1c209fe8eb0ffa0292153d9`; the observed workspace Builds
revision is `b7a9c9ec9f57e098b408d3b940929ef4b160985b`.

## Version and source boundary

[Source and release observations](source-and-release-boundary.json) retain the
exact source and tooling hashes, nine commits and 67 changed production files
after published 3.124.0, and the five official NuGet package-index response
hashes. On this observation, all five indexes ended at 3.124.0 and none listed
3.125.0. No `v3.125.0` local tag exists. The prior 3.124.0 package signatures
remain in the [Projects preflight](../../../../../../_bmad-output/implementation-artifacts/evidence/6-4-prerequisite-preflight-20261017/published-family.json);
they bind that old source revision only. The proposed 3.125.0 coordinate is a
release-analysis suggestion, not an immutable selected tuple.

[MSBuild project evaluations](compiler-evaluations.json) record 12 real `dotnet
msbuild -getProperty:DefineConstants -getItem:Compile` evaluations, with no
package restore: Host and Domain across 3.119.0, 3.121.0, 3.122.1, proposed
3.125.0, source, and unenrolled 3.126.0. The evaluated proposed/source paths
select the capability, evolution and folded-snapshot definitions and finite
fixture source items; 3.126.0 selects none. These commands did not compile or
restore the unpublished 3.125.0 package graph. Historical conditions remain
as before. A [focused executable test](test-proposed-project-evaluation.log)
also evaluates Host, Domain and Probe for proposed 3.125.0 and unenrolled
3.126.0, checking the flags, fixture `Compile` items, and package-only
references without restoring packages. The
qualification-only Counter snapshot limits and existing Reminder/evolution
lanes were not changed.

## Verification results

After the review repair, [17 post-review commands](postreview-20261017/results.json) passed: the full executor, qualification, isolation and registration suites; five individual Debug source builds; and seven focused .NET runs, including P1R LiveSidecar 2/2. Their logs are retained in that directory. The earlier isolated Client recovery run remains the source-only live proof; no Client runtime code changed during review.

All commands ran from the EventStore root. The earlier Python suites invoked
`python3 -m unittest discover -s tools/tests -p '<pattern>' -q`.

| Check | Result | Retained output |
| --- | --- | --- |
| `test_p1r_published_executor.py` after final seed guard | 128 passed | [final log](test-executor-final.log) |
| `test_p1r_published_qualification.py` | 61 passed | [log](test_p1r_published_qualification.log) |
| `test_p1r_isolation.py` | 31 passed | [log](test_p1r_isolation.log) |
| `test_p1r_isolation_hardening.py` on stable bytes | 24 passed | [stable log](test_p1r_isolation_hardening-stable.log) |
| `test_p1r_snapshot_registration.py` | 1 passed; real Host/Domain DI startup | [log](test-snapshot-registration.log) |
| `python3 -m unittest tools.tests.test_p1r_snapshot_registration.FixtureSnapshotRegistrationTests.testProposedAndUnenrolledPackageProjectEvaluations -v` | 1 passed; six package-project evaluations, no package restore | [log](test-proposed-project-evaluation.log) |
| Five individual `dotnet build tests/Hexalith.EventStore.<Project>.Tests/Hexalith.EventStore.<Project>.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -p:NuGetAudit=false` | Contracts, Client, DomainService, Server and Server.LiveSidecar succeeded with zero warnings/errors | [Contracts](build-contracts.log), [Client](build-client.log), [DomainService](build-domainservice.log), [Server](build-server.log), [LiveSidecar](build-server.livesidecar.log) |
| Built xUnit v3 assemblies with single-dash `-class` selectors: QueryCallerAuthority, DaprReadModelBatch, FoldedSnapshotCapture/QualificationRegistry, FoldedSnapshotRead/Parity | 1 + 131 + 28 + 3 + 39 + 1 passed, no skips | [Contracts](test-contracts-authority.log), [Client](test-client-DaprReadModelBatchTests.log), [Capture](test-domainservice-FoldedSnapshotCaptureTests.log), [Registry](test-domainservice-FoldedSnapshotQualificationRegistryTests.log), [Read](test-server-FoldedSnapshotReadTests.log), [Parity](test-server-FoldedSnapshotParityTests.log) |
| Built LiveSidecar `P1RRemediationPersistenceTests` with `EVENTSTORE_TEST_DAPR_HOT_RELOAD=false DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false` | 2 passed, no skips; actual source authority effect and retained-state restart | [passing log](test-live-sidecar-p1r-hotreload-disabled.log) |
| Built Client `PendingBatchRecoveryLiveTests` against an invocation-owned Redis container and private Dapr sidecar | 2 passed, no skips; interrupted conditional batch recovery after client restart | [test log](test-client-pending-recovery-isolated.log), [sidecar log](client-live-daprd.log), [ownership and cleanup](client-live-recovery-ownership.json) |

The first LiveSidecar attempt failed both cases during fixture startup because
Dapr 1.18.7 HotReload exhausted the host's inotify budget; neither test body
ran. Its [failure log](test-live-sidecar-p1r.log) remains separate. The private
fixture HotReload override avoided that watcher without changing production
code or the package profile. An isolation-hardening run also failed one
source-hash control while the executor source was edited during that run;
the [interrupted result](test_p1r_isolation_hardening.log) and the later
24-pass stable result are both retained.

Client `PendingBatchRecoveryLiveTests` initially skipped both opt-in cases
because isolated Dapr endpoints were absent; the [skip log](test-client-PendingBatchRecoveryLiveTests.log)
is retained. A subsequent isolated run passed both cases against its own
Redis container and Dapr sidecar, then stopped the sidecar and removed the
owned container and scratch. This proves the source client recovery cases
under Dapr 1.18.7/Redis, not the pinned published profile. The passing P1R
LiveSidecar tests used source Debug/project references with a fixture-scoped
actor namespace on shared Redis and Dapr 1.18.7. The pinned published profile
is Dapr 1.18.2 with PostgreSQL; it was not run here. The
unit witness tests execute the actual strict validator against bound synthetic
receipts, including nine/manual/three-step observations and rehashed tampering.
They are tooling controls, not a real published-package seed.

## Isolation, preservation and review

[Cleanup observations](cleanup-and-preservation.json) record no remaining
`eventstore-live-*` daprd process, removal of the failed fixture component
directory, and the four pre-observed shared Dapr container IDs still running.
Pre-run image IDs and start times were not retained, so exact shared-resource
preservation and private P1R cleanup qualification remain unverified. The
[client live recovery ownership record](client-live-recovery-ownership.json)
independently binds its private Redis container, Dapr binary/configuration,
test result and exact resource absence after teardown. The source tests did
not start the published executor's private Docker runner.

The [independent review](independent-review.md) found that an unenrolled selected
candidate could formerly enter `seed("source", 10, ...)`. The preflight now
requires a finite selected candidate and the exact selected package writer
or source **before** state reset or writer start. Negative tests cover
`3.126.0` with source, `3.125.0` with an older package writer, and unrelated
versions; the 128-test final executor run includes them. The reviewer found
no further code defect in the finite enrollment, strict witness, isolation,
or version-bound restore changes. The reviewer identified the initial skipped
live pending-batch recovery cases; the subsequent owned private run passed
both. Published-profile recovery remains unverified.

`git diff --check` passed. The changed tracked EventStore paths are tooling,
consumer fixture project files, tests and its README; production `src/`, test
projects, package pins and `global.json` were not edited. The workspace
`.gitmodules` and sprint status were not edited. No commit, tag, pin, packet,
acceptance, rollback selection or protected history was changed.

## Concrete successor handoff

1. Land and independently review the exact EventStore source/tooling change,
   then recheck reachable release history and Builds before choosing a version.
2. Obtain separate owner publication authority for an exact source/tag/Builds
   tuple. Require tag/source equality and signed public archives for all five
   EventStore packages; do not reuse old 3.124.0 receipts.
3. Bind immutable archives, locks/assets, loaded Release assemblies and the
   pinned Dapr 1.18.2/PostgreSQL profile in the private published executor.
   Run genuine snapshot creation, restore/append/restart, retained-floor,
   Tenant, authority/recovery, tampering, cleanup and preservation controls.
4. Record independent owner decisions and same-baseline conformance before
   any P1R usability, consumer pin or Story 6.4 gate change. Rollback stays
   `null`; drain/freeze incompatible ingress and recover forward.

This handoff leaves package publication, package qualification, G-2/RTI,
G-4, P2/P3/P4, G-6, production gates and Story 6.4 open.
