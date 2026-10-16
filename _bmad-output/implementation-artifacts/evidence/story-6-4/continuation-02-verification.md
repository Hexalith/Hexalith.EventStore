# Story 6.4 continuation 02 verification

The implementation remains incomplete and unqualified. These are development
regressions, not the complete W0–W6 cost/preservation/live evidence or permission
to activate any existing environment. No task or acceptance criterion is done.

HEAD remains `e17e7294f540ebba42fe25192998689f0ecd89c1`. The approved normative
SHA-256 remains `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`;
the owner approval statement digest remains
`657f0dc25b18b826b37ab3be53273043fa98ca61a40923c9a913922816bf531f`.
No commits, branch/staging/push, dependency/submodule updates or cutover execution
occurred. Fixture activation occurred only in deterministic recording state.

## Commands and observed results

All commands ran from the repository root. Builds used the pinned SDK and
Debug/source mode. No audit settings or dependency versions were changed.

| Command / retained log | Exit / result |
| --- | --- |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1` — `continuation-02-server-build.log` | 0; 0 warnings/errors, 9.86 seconds; final normalized source. |
| Approved Server focused class set plus lifecycle and documentation — `continuation-02-server-focused.log` | 0; 364 tests, 0 failures/skips, 7.549 seconds; final focused rerun. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll` — `continuation-02-server-full.log` | 0; 4,747 total, 0 failed, 25 tracked skips, 739.331 seconds. |
| `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1` — `continuation-02-client-build.log` | 0; 0 errors, 10 NU1902/NU1903 warnings from centrally pinned Pkcs/Xml 10.0.12 packages. |
| `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*DaprReadModelBatchTests'` — `continuation-02-client-batch-tests.log` | 0; 24 passed, 0 failed/skipped. |
| `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll` — `continuation-02-client-full.log` | 0; 1,534 passed, 0 failed/skipped, 6.936 seconds. |
| `dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1` — `continuation-02-domain-build.log` | 0; 0 warnings/errors, including the endpoint metadata fix. |
| Approved DomainService focused classes — `continuation-02-domain-tests.log` | 0; 105 passed, 0 failed/skipped, 95.113 seconds. |
| `dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll` — `continuation-02-domain-full-initial.log` | 1; 893 total, 2 failed, 0 skipped, 119.930 seconds. See retained failure details below. |
| `dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -method '*MapEventStoreDomainService_MapsCanonicalEndpointsUnchanged' -method '*ExhaustedWorkersRejectPromptlyAndRecoverAfterRelease'` — `continuation-02-domain-isolated.log` | 0; both previously failing tests passed, 0 failures/skips, 0.457 seconds. |
| Sequential full DomainService rerun — `continuation-02-domain-full.log` | 0; 893 passed, 0 failed/skipped, 122.826 seconds. |
| `dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1` — `continuation-02-contracts-build.log` | 0; 0 warnings/errors. |
| `dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll` — `continuation-02-contracts-full.log` | 0; 2,339 total, 0 failed, 2 package-inventory skips, 348.072 seconds. |
| `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1` — `continuation-02-live-build.log` | 0; 0 warnings/errors. Live suites were not executed in this continuation. |
| `git diff --check` and direct normative/approval digest verification | Passed; approved bytes unchanged. |

Exact Server focused command:

```bash
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests' -class '*DaprProjectionLifecycleGatewayTests' -class '*ProjectionDeliveryDocumentationTests'
```

Exact DomainService focused command:

```bash
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*DomainProjectionHandlerCompatibilityTests' -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests' -class '*DomainFencedSharedProjectionIntegrationTests' -class '*DomainPartitionedSharedProjectionRebuildTests'
```

The initial DomainService full run overlapped other broad development runs.
After they completed, validation resumed one build/suite at a time. Both
initial failures and subsequent results are retained. The endpoint metadata
failure was real: the new catalog parameters were inferred as a second body
parameter when endpoint mapping was used without full registration. The fix
adds explicit `[FromServices]` binding to all three source-guard operations and
updates the exact route inventory to retain every existing endpoint plus the
three additive operations. Route policy assertions remain intact.

The worker-admission failure occurred in
`ProcessReadDeadlineTests.ExhaustedWorkersRejectPromptlyAndRecoverAfterRelease`,
at its `Task.WhenAll` cleanup with `ProcessReadAdmissionException`. It passed
in the isolated rerun. No process read production code or unrelated assertion
was changed to hide that failure; the sequential full rerun decides its gate.

## Declared skips and gates still unproven

The exact 25 existing Server ATDD skip names and reasons are retained in
`continuation-02-server-full.log`: 4 poller-corruption, 13 delivery-hardening
and 8 drain-hardening red-phase cases. No new skip was introduced or removed.

The Contracts package-only skips were exactly:

- `Packaging.PackagedReminderApiTests.PackagedReminderApiRunsWithoutWorksTypes`.
- `Packaging.TrustedEffectPackageContractTests.NamedPackagesExposeTrustedEffectContracts`.

Both report `EVENTSTORE_PACKAGE_CONTRACT_DIR is not set; no release package
inventory was supplied.` Their Release/package inventory lane remains
unproven. The exit-0 Debug result does not establish those package contracts.

Live Dapr/Redis workload qualification, the full W0–W6 quantitative vector and
every section-6 crash point, and approved Release/package restore/build/suite
commands remain required. The recorded local runtime is 1.18.7 rather than
the pinned 1.18.2 lane and remains unqualified. Existing pinned crypto advisory
warnings prevent a zero-warning claim. No dependency update, audit suppression,
skip suppression or weakened assertion was used to manufacture acceptance.

Broad runner captures were audited and sanitized: only runner outcomes,
failure summaries, skip names/reasons and final counts are retained. Synthetic
host logs, provider details and stack traces were omitted, with the original
raw capture SHA-256 recorded in each sanitized broad log. Baseline host warnings
are not evidence about the new payload-free projection telemetry.

## Final sequential result

The sequential full DomainService rerun passed all 893 tests with no skips.
The two initial failures remain retained. The final Server rebuild passed
with zero warnings/errors; its focused result is retained in the log above.
No affected test process or fixture sidecar is left running by this continuation.
Compiler/build-server processes and unrelated workspace processes were preserved.

Full source-file and built test-asset SHA-256 identities are retained in
`continuation-02-identities.json`. The full Server run predates the final
small endpoint-inventory/maintenance validation refinements; the final rebuild
and focused set validate those source refinements. These are development
regressions and do not substitute for the unimplemented mode and final gates.
