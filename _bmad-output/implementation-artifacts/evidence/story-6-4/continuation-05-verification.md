# Continuation 05 verification

**Incomplete and unqualified.** No environment was admitted or cut over. These
results are implementation evidence, not acceptance of the full approved matrix.
The receiving agent is the sole active implementer; continue sequential step 03.

HEAD remains `e17e7294f540ebba42fe25192998689f0ecd89c1`. The approved normative
SHA-256 is `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`.
Normative bytes and detached owner approval were preserved. No dependency, audit
policy, Git branch, commit, index or submodule mutation was performed.

## Final observed commands and results

All build/test commands ran individually and sequentially from the repository
root. Independent builds/runners did not overlap. Commands below omit only shell
redirection to transient `/tmp/story-6-4-cont05-*.log` files.

```bash
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class '*ProjectionDeliveryIdempotencyCoordinatorTests' -class '*NamedProjectionDispatchCoordinatorTests' -class '*EventStoreProjectionDeliveryHistoryReaderTests' -class '*ProjectionRebuildProductionPathTests' -class '*ProjectionDeliveryCutoverTests' -class '*ProjectionDeliveryReconcilerTests' -class '*ProjectionDeliveryStateContractTests' -class '*ProjectionDeliveryDiagnosticsTests' -class '*ProjectionEraseCoordinatorTests' -class '*EventPersisterTests' -class '*AggregateMetadataTests' -class '*AggregateActorTests' -class '*AggregateActorInfrastructureFailureTests' -class '*AggregateActorGetEventsTests' -class '*DaprProjectionLifecycleGatewayTests' -class '*ProjectionDeliveryDocumentationTests' -class '*ProjectionDeliveryRetryWorkerTests' -class '*DaprProjectionDeliveryRetrySchedulerTests' -class '*ProjectionActivationWorkerTests' -class '*QueryRouterSourceGuardTests' -showLiveOutput
dotnet build tests/Hexalith.EventStore.DomainService.Tests/Hexalith.EventStore.DomainService.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.DomainService.Tests/bin/Debug/net10.0/Hexalith.EventStore.DomainService.Tests.dll -class '*EventStoreDomainServiceExtensionsTests' -class '*DomainProjectionHandlerCompatibilityTests' -class '*DomainProjectionDispatcherV2Tests' -class '*MixedProjectionCatalogTests' -class '*DomainFencedSharedProjectionIntegrationTests' -class '*DomainPartitionedSharedProjectionRebuildTests'
dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -method '*SourceProof_RealActorAtomicAppend*'
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -method '*GuardedCurrent_ProductionActors*' -method '*GuardedTail_ProductionActor*' -method '*GuardedMixedLargePayloadTail*' -method '*GuardedSourcePass_Exact64MiB*' -method '*SharedShardOccupancy_Charges*' -showLiveOutput
dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1
dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class '*DaprReadModelBatchTests' -class '*ReadModelBatchFingerprintTests' -class '*AdminOperationalIndexHostedServiceTests'
git diff --check
```

| Command | Exit | Observed result |
| --- | --- | --- |
| Server focused Debug/source build | 0 | 0 warnings, 0 errors; 10.82 seconds |
| Server focused runner | 0 | 462 passed, 0 failed, 0 skipped; 12.398 seconds |
| DomainService Debug/source build | 0 | 0 warnings, 0 errors; 7.50 seconds |
| DomainService focused runner | 0 | 200 passed, 0 failed, 0 skipped; 101.236 seconds |
| LiveSidecar Debug/source build | 0 | 0 warnings, 0 errors; 7.99 seconds |
| Live source runner | 0 | 2 passed, 0 failed, 0 skipped; 4.327 seconds |
| Full Server Debug/source build | 0 | 0 warnings, 0 errors; 9.47 seconds |
| Full Server runner | 0 | Total 4822: 4797 passed, 0 failed/errors, 25 skipped; 653.270 seconds |
| Final cost-output runner | 0 | 23 passed, 0 failed/skipped; 4.396 seconds |
| Client Debug/source build | 0 | 10 existing NU1902/NU1903 crypto warnings, 0 errors; 8.04 seconds |
| Client focused runner | 0 | 59 passed, 0 failed/skipped; 0.347 seconds |
| Tracked whitespace | 0 | `git diff --check` passed |

The focused runs include separately proven activation/retry cleanup ownership,
canonical pre-effect fallback, guarded downgraded-detail/index isolation, source
range/count/byte framing and the read-only query freshness gate. Six query tests
use the owning guard/model/lifecycle seam: Current, new-head Stale, returned-version
mismatch, unsupported shared family, LocalOnly and empty Unknown. They assert zero
handlers, ranges, batches, proof writes and model mutations. The public constructor
and lifecycle/query DTO ABI remain preserved.

Earlier passing intermediate Server subsets were 13 cost cases, 119 control/cost
cases, 149 control/cost/actor cases and 42 query/diagnostics/DI cases. These do not
replace the final broader results. Client's final 59 cases include the updated
descriptor/fallback fingerprint assertion and historical absent-descriptor bytes.
Full Client, Contracts, full DomainService, full LiveSidecar and Release/package
gates remain unproven for the latest sources. The Server full suite's 25 unchanged
baseline skips are 4 Dw1PollerCorruptionAtddTests, 13 Dw1ProjectionDeliveryAtddTests
and 8 Dw1DrainHardeningAtddTests. Their existing explicit ATDD red-phase reasons
cover DW1 AC1-5 and AC7-9. Names are retained in the full Server summary; none was
counted as passing or newly disabled here.

## Actual measurements and individual ceilings

The identity-free [cost records](continuation-05-costs.json) retain closed-category
measurements from the production-owner recording lane. The actor and lifecycle
methods, raw model accessors, production delivery store, handler dispatcher and
scheduler own those operations. This lane still substitutes the Dapr transport;
it is not a complete live framework-byte or memory qualification.

- Current H=C in 1,256,257,9999,10000 with R=256/4096: 2 source metadata keys,
  1 proof row, 2 marker keys and 6 other keys = 11 logical reads. Zero source
  envelopes/ranges, hashes, handlers, batches, mutations or proof writes. Proof
  reads fit S=16,384+80H+512min(H,R), and aggregate read bodies fit S+114,688.
  Independent source/marker body caps and long identifiers still need rejection
  vectors; measuring a body is not enforcement of its cap.
- Tail H=10000/C=9999 at P=1/256; H=9771/C=9000 at P=1/256; delta=256,257,512,513
  at P=256: exact 3Q range calls, 3delta envelopes/source hashes/chain extensions/
  route hashes, delta handler attempts, J=delta batches, D=2delta model mutations,
  and 3 successful delivery-row transitions. Assertions separately bound source
  metadata by 3+6Q, proof reads by 3A+5=29 and attempts by 3A+1=25 at A=8,
  2 marker reads per proof read, control reads/mutations by 64+32A+16J+8D, proof
  byte reads/writes by 29S/25S and proof visits by (8A+16)(H+min(H,R)+delta).
  These tests do not establish complete visit coverage, actual A=8/32 exhaustion,
  working-byte limits or every body-size/byte-total category.
- Mixed near-MiB payload tail H=6/C=1: 12 ranges (four greedy pages per pass),
  15 returned events, 9 actual lookahead events and 24 event-key reads/decodes.
  Only the 15 returned copies are hashed/routed; 5 handler attempts and 10 model
  mutations. Source response bodies total 16,406,271 bytes, actual lookahead
  bodies 12,396,732 bytes, actor deserialization bodies 28,802,976 bytes, source
  sizing work 45,209,220 bytes. Separate response serialization and response
  deserialization counters each report 16,406,271 logical array-body bytes.
- Exact source pass 67,108,864 versus 67,108,865 is checked before effects. The
  exact case completes; the one-over case retains C=1, no reservation/work,
  no handler or model mutation. Decoded wire-array exact/over is still pending.
  The exact case measures 99 ranges/returned copies, 96 lookahead copies and
  195 event reads/decodes over three passes; sizing is 597,878,385 bytes against
  the frozen response-plus-lookahead allowance of 396,552,189. One-over rejection
  measures 33 ranges/returned copies, 32 lookaheads, 65 event reads/decodes,
  67,108,865 response bytes and 65,075,200 lookahead bytes; no side effects.
- Scheduler scans include all 64 shards and all 4096 entries, plus a second
  fixture with 128 entries in one shard (4160 total). Actual serialized bytes
  equal the measured sum of complete ledgers. Each work item is <=4096 bytes;
  per-shard bounds are 512+4096I, total fixture <=16,809,984 (uneven adds 262,144).
  The scan returns 64 due items and preserves every pending item without writes.
  Final measured scan bytes are 2,074,816 (4096 items) and 2,107,192 (4160 items).
  Actual scheduler CAS exhaustion and complete cold/cleanup charging remain open.

**Frozen sizing category fails.** For each returned envelope both the actor and
Server history reader execute sizing serialization: actual 2E+Blook instead of
approved B+Blook (E excludes array framing). The mixed test deliberately asserts
the truthful duplicate work: 45,209,220 measured versus 28,803,003 allowed bytes
over its three passes. No W4 sizing qualification is claimed. An additive
actor-owned measured range response can remove the duplicate while preserving
independent Server byte limits and existing APIs; its real transport/body cost
must also be counted. Early oversized-event rejection currently bypasses sizing
measurement and actor deserialization-byte recording after the full key read and
decode, so it must not be reported as zero work. Metadata sizing, complete raw
framework/framing serialization, proof copying/visits and overlapping model/control
byte categories still require a full audit. Do not sum overlapping counters into
a fabricated total. Live costs require exported production measurements or an
equivalent remote seam collector; AsyncLocal does not cross Dapr/HTTP boundaries.

## Live environment and limited proof

The two real Dapr tests append 256/257 events, inspect/recompute source proof,
read bounded pages through actor remoting/Redis and recreate the actor proxy.
They do not establish optimized guard current/tail, route cleanup, full crashes
or W0-W6 live costs. Observed fixture binary
`/home/administrator/.dapr/bin/daprd --version` is **1.18.7**; CI expects **1.18.2**
at `.github/workflows/integration.yml:25`. Bare `daprd --version` returns exit 127
because that binary is not on PATH. The exact fixture binary succeeds. Existing
placement/scheduler containers also use daprio/dapr:1.18.7; Docker is 29.8.2.
SDK 10.0.401 and runtime 10.0.12 ran tests. The installed runtime and unrelated Works
processes/infrastructure were preserved. A following isolated pinned binary lane
may qualify 1.18.2; no such qualification exists here.

## Intermediate failures and corrections

- Initial cost build failed six xUnit1030 diagnostics for ConfigureAwait(false)
  in test methods. Test awaits now use ConfigureAwait(true); later builds clean.
- An initial C=0 replay addition failed CS0136 because a local reused `replay`.
  The local is now `initialReplay`; later builds clean.
- Initial 118-case control run had 35 failures after overlapping SingleEvent and
  FullReplay catalog flags. Historical constructor/old-server invariants correctly
  reject overlap. Flags remain disjoint; fallback uses an optional descriptor.
  The next attempt still had seven failures after blanket fallback diverted active
  partial/initial owners. Fallback now requires fresh C>0 reservation or explicit
  pre-effect rebuild-required result plus unchanged original recovery. Later
  119/149/462 cases passed, retaining original partial/lost-response recovery.
- Initial DomainService focused run had 199 passes/1 failure: route inventory lacked
  `/project/rebuild/reclaim-guarded/v1`. Expected inventory updated; final 200 passed.
- Query DI build failed CS0246 for IActorProxyFactory; added its existing owning
  Dapr.Actors.Client namespace import; later clean.
- Live test build failed CS1674 because ActorProxyFactory is not IDisposable;
  removed `using` ownership. Later clean.
- First live source run had 2 failures (InvalidDataContractException) on source proof
  actor remoting. Added DataContract/DataMember annotations to SourceProofInspection,
  SourceProof and SourceProofBootstrapRequest. Persisted STJ/source-v1 bytes remain
  unchanged; final 2 live cases passed. This failure was invisible to recording seams.

Transient raw logs include provider output/payloads/stacks and are not published as
qualification evidence. Sanitized summaries, cost records, source/asset SHA-256s
and environment identities are retained beside this record. Intermediate logs
overwritten by reruns are represented by the explicit failures above.

Final `git diff --check` passed. A separate byte check of all 152 changed/additional
C# files found no CR bytes or UTF-8 BOM. Normative SHA-256 was recomputed and
matched the approved digest. The final process audit found zero owned EventStore
test/application/sidecar processes. Unrelated .NET/Dapr processes and existing
shared infrastructure were left untouched; see
[the process audit](continuation-05-process-audit.txt).

Full compatibility/partial admission, all crash vectors, complete numeric costs,
pinned live/full affected and Release/package gates remain outstanding; see the
[current-state checkpoint](continuation-05-current-state.md) for exact next work.
