# Continuation 06 verification

**Incomplete and unqualified. No environment was admitted or cut over.**
HEAD remains `e17e7294f540ebba42fe25192998689f0ecd89c1`; runtime changes are
uncommitted. Approved normative SHA-256 remains
`78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
The normative bytes and detached owner approval were preserved. No dependency,
audit policy, Git index/branch/commit or submodule mutation was performed.

## Observed commands

Commands ran from the repository root. Redirection was only to transient
`/tmp/story-6-4-cont06-*.log` files. One Server build was inadvertently started
before the Contracts runner completed; this overlap is disclosed separately and
is not represented as a clean sequential final gate. Other test/build commands
were sequential; no two test runners overlapped.

```bash
aspire start --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
aspire describe --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --format Json
aspire stop --non-interactive --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests' -class '*DaprProjectionLifecycleGatewayTests' -class '*ProjectionDeliveryDocumentationTests' -class '*ProjectionDeliveryRetryWorkerTests' -class '*DaprProjectionDeliveryRetrySchedulerTests' -class '*ProjectionActivationWorkerTests' -class '*QueryRouterSourceGuardTests' -showLiveOutput
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*SourceProof_RealActorAtomicAppend*' -showLiveOutput
dotnet build tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Contracts.Tests/bin/Debug/net10.0/Hexalith.EventStore.Contracts.Tests.dll
git diff --check
```

| Check | Exit | Observed result |
| --- | --- | --- |
| Aspire baseline start/describe/stop | 0 | Resources reported Running/Healthy; exact selected AppHost stopped before edits |
| Server Debug/source build | 0 | 0 warnings/errors |
| Final sequential Server focused runner | 0 | 484 passed, 0 failed/skipped; 12.583 seconds |
| Client Debug/source build | 0 | 10 existing NU1902/NU1903 cryptography advisories; 0 errors |
| Full Client runner | 0 | 1544 passed, 0 failed/skipped; 5.751 seconds |
| DomainService Debug/source build | 0 | 0 warnings/errors; 2.42 seconds |
| Full DomainService runner | 0 | 899 passed, 0 failed/skipped; 105.293 seconds |
| LiveSidecar Debug/source build | 0 | 0 warnings/errors; 6.45 seconds |
| Final pinned source runner | 0 | 2 passed, 0 failed/skipped; 4.365 seconds |
| Contracts Debug/source build | 0 | 0 warnings/errors; 6.19 seconds |
| Contracts full runner | 0 | 2337 passed, 0 failed, 2 package-inventory skips; 270.079 seconds; build overlap disclosed above |
| Tracked whitespace | 0 | `git diff --check` passed |

The final source lane used environment overrides for its isolated daprd binary,
placement port and scheduler port. Full exact control-plane/test command arrays,
ports, exit statuses, official binary/archive SHA-256s and release URLs are in
[the pinned lane record](continuation-06-pinned-source-lane.json). Both owned
control-plane processes exited 0 after termination. Their startup logs identify
Dapr 1.18.2 commit `d2dd3747145e38e4d85db4714e26d7569c9c37db`.
SDK/runtime were 10.0.401/10.0.12; Redis was the existing `redis:6` fixture service,
whose owning container reports Redis 6.2.21.
Installed shared placement/scheduler stayed on `daprio/dapr:1.18.7` and were untouched.

The two Contracts skips are `PackagedReminderApiRunsWithoutWorksTypes` and
`NamedPackagesExposeTrustedEffectContracts`: both explicitly require
`EVENTSTORE_PACKAGE_CONTRACT_DIR`, which was unset. They do not establish release
package-contract acceptance. The runner's tracked consumer-package-authority check
completed within its existing eight-minute deadline. The full Contracts result
records actual execution despite the disclosed Server-build overlap; final
Release/package verification remains a clean separately required gate.

Full Client/DomainService runs preceded the final RetryActivations telemetry-only
addition and the final small guarded cross-route sampling correction. Their
actual private Debug asset identities are retained separately; they are regression
evidence, not a claim that every final source/package gate passed.

## Retained cost evidence and limits

[Production vectors](continuation-06-production-costs.json) retain sanitized
identity-free current/tail/mixed/64-MiB source results. Source sizing now charges
returned envelopes once plus real lookahead. Mixed fixture: 12 ranges, 15 returned
copies, 9 lookaheads, 24 actor event reads/decodes; sizing 28,802,976 <=
response+lookahead 28,803,003. Handler attempts=5, logical model mutations=10.
The exact 64-MiB three-pass fixture reports 396,551,991 sizing/decode bytes,
201,326,592 array-response bytes and 195,225,597 lookahead bytes. One-over
admission still has zero handlers/reservations/model mutations.

Pinned source H=256/257 reports canonical array bytes 327,209/328,597 and
native DataContract response-body bytes 226,118/227,450. Native request bodies
are 374/566 bytes over 5/6 real HTTP invocations. These totals include the explicit
compatibility empty-range call, plus inspection and legacy stream-metadata calls;
they are source API fixtures, not optimized route pass budgets. The process-wide
meter listener sees actual in-process actor work across HTTP contexts. The real
transport observer buffers bodies for observation; no memory/CPU acceptance is
claimed. Canonical and native representations are overlapping measurements, not
independent byte totals to sum.

Full W0–W6, every crash boundary, all proof/control/transport/working bytes and
Release/package gates remain unproven. No acceptance is inferred from these runs.

[Retry vectors](continuation-06-retry-costs.json) retain five observed N=9 cases
with Attempt saturated at 8, 594 shard reads (nine complete 64-shard scans plus
claim/update shard reads) and 18 ledger writes. These use the actual scheduler
over the existing state seam with synthetic guard outcomes; full production/live
W6 semantics and R/R+1 classification remain required. [Source and Debug asset
identities](continuation-06-source-assets.json) bind 160 changed source/doc files
and 50 actual assembly files. Final [process audit](continuation-06-process-audit.json)
found no matching owned test/AppHost/pinned infrastructure processes. Shared
infrastructure and unrelated tooling were preserved.

Final `git diff --check` passed. An explicit byte check found no CR/BOM/trailing
whitespace in all 157 changed/additional C# files. The approved normative digest
was recomputed and matched after all edits.

## Intermediate failures and corrections

- Initial Server build failed CS1501 at the bootstrap's old Count overload after
  the sizing helper changed. Bootstrap now consumes the exact full streamed count;
  later builds are clean.
- Initial nine-activation assertions expected only 64 scan reads per tick. Actual
  scheduler measurements correctly included claim/update shard reads too:
  9*(64+2)=594. The assertion now explicitly charges both categories; no runtime
  scan/CAS work was removed or hidden. The corrected retry/query subset passed
  44/44 and the later broader focused run passed 484/484.
- One subsequent Server build overlapped the pending Contracts full runner.
  This procedural mistake is disclosed; the final build/focused execution was
  repeated sequentially after Contracts finished and passed 484/484. No two test
  runners ran together and no gate was weakened.

Raw logs may contain fixture provider diagnostics and payloads; only sanitized
summaries, measurements and content identities are retained as repository evidence.
The [current-state handoff](continuation-06-current-state.md) lists required
unfinished implementation and qualification work.
