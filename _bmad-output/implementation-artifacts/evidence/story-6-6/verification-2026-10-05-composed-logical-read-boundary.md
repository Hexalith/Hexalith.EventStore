# Story 6.6 composed logical-read boundary — 2026-10-05

Status: **partial implementation; Story 6.6 remains in progress**. No M1–M8 task,
O-row, production-readiness or V2 activation requirement is closed by this record.

## Changes and demonstrated scope

- The logical resolver checks the immutable manifest's aggregate route before
  protection/upcaster callbacks. An envelope and addressed route agreeing with
  each other cannot override the registered contract route.
- Dapr logical pages share one `EventBufferBudget` across retained effective
  owners, protected copies, conservative protection-output reservations and
  bounded metadata snapshots. Failure/disposal releases charged private owners.
  Readable-source bytes are counted separately from upcast output: shrinking
  upcasters cannot hide an oversized readable prefix.
- Logical pages accept expected head/floor pins, reject unavailable retained
  prefixes before event reads, and recheck head/floor/ETag. These are current Dapr
  logical observations, not signed physical or historical-generation proof.
- The production V1 `EventStreamReader` rejects unavailable retained prefixes
  before event reads and rechecks head/floor/ETag before returning replay or an
  already-current snapshot. Its original constructors/interfaces remain intact.
- The current Dapr-only source preflight checks the withdrawn SQL dependency,
  actor save ownership and the dormant V2 writer fence. Five independent mutation
  processes each have a 10-second timeout. Existing Contracts CI runs this gate.
  It does not replace runtime or activation evidence.
- The now-unused Story 6.6 Npgsql central pin was removed from the owning Builds
  repository. Its catalog validator passed; other dependency versions were kept.
- The dedicated live test appends through `AggregateActor`, reads event/metadata
  through public Dapr actor-state GET, retries the same committed command through
  a second sidecar, restarts the first host/sidecar, and inspects the production
  stream. It asserts original application bytes/digest/MessageId and head=1.

## Exact local checks

The retained [output directory](2026-10-05-composed-logical-read/) includes logs,
native process/profile observations, and a SHA-256 source/evidence inventory.

| Command | Result |
| --- | --- |
| `python3 _bmad-output/implementation-artifacts/6-5-integration/verify.py --mutations` | Exit 0; historical AD-13 digest remains `bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050`; 20 obligations/47 follow-ups remain open. |
| `python3 scripts/verify-event-evolution.py --mutations` | Exit 0; all five timed policy mutations rejected by their owning checks; amendment SHA-256 `2c1dc2e80d032a7f1622ab6f00057e9c0298ec2a1114df23e74a1ce28f5feaef`. |
| `python3 scripts/check-deferred-work.py --json` | Exit 0. |
| `pwsh -NoProfile -File Tools/validate-central-package-versions.ps1 -CatalogPath Props/Directory.Packages.props` from `references/Hexalith.Builds` | Exit 0; 304 catalog entries validated. |
| `dotnet build tests/Hexalith.EventStore.Client.Tests/Hexalith.EventStore.Client.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` | Exit 0; zero warnings/errors. |
| `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` | Exit 0; final source, zero warnings/errors. |
| `dotnet build tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Hexalith.EventStore.Server.LiveSidecar.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -m:1` | Exit 0; zero warnings/errors. |
| `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll -class 'Hexalith.EventStore.Client.Tests.Events.*'` | Exit 0; 75 passed, zero failures/skips. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Events.DaprLogicalEventReaderTests -class Hexalith.EventStore.Server.Tests.Events.EventStreamReaderTests -class Hexalith.EventStore.Server.Tests.Events.SnapshotRehydrationTests` | Exit 0; final source, 55 passed, zero failures/skips. |
| `dotnet tests/Hexalith.EventStore.Client.Tests/bin/Debug/net10.0/Hexalith.EventStore.Client.Tests.dll` | Exit 0; 917 passed, zero failures/skips. |
| `dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll` | Exit 0; final source, 3,647 total, zero failures, 25 existing skips. |
| `dotnet build Hexalith.EventStore.slnx --configuration Release -m:1 -warnaserror -p:UseHexalithProjectReferences=false` | Exit 0; final source, zero warnings/errors. |
| `python3 scripts/pack-release-packages.py /tmp/story-6-6-20261005-packages 0.0.0-ci-test` | Exit 0; standard local manifest packing and its existing CI-version normalization; no publication. |
| `python3 scripts/validate-consumer-package-references.py /tmp/story-6-6-20261005-packages --package Hexalith.EventStore.Client --package Hexalith.EventStore.Server` | Exit 0; two isolated package-only consumers, zero warnings/errors. |
| `git diff --check` and `git -C references/Hexalith.Builds diff --check` | Exit 0. |

The package inventory was created before the final internal readable-source
length correction. These ordinary package consumer builds do not establish the
dedicated final-source API/wire/already-compiled evolution matrix.

## Live Development fallback and initial failure

The default live command was:

```bash
dotnet tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll -class Hexalith.EventStore.Server.LiveSidecar.Tests.Events.DaprEventEvolutionLogicalReadbackLiveSidecarTests
```

It exited 1 during fixture initialization: Dapr 1.18.4 reported Redis EOF on
`localhost:6379` and scheduler connection reset. Redis inside `dapr_redis`
answered `PONG`; real RESP PING from the workspace reset, and the Docker bridge
address was unreachable. Shared containers and forwarding listeners were kept.

The fixture now accepts explicit `EVENTSTORE_TEST_REDIS_PORT`,
`EVENTSTORE_TEST_PLACEMENT_PORT` and `EVENTSTORE_TEST_SCHEDULER_PORT` overrides.
Absent values preserve existing defaults; invalid values fail before startup;
configured control-plane ports have no fallback to shared ports. The existing
fixture's second type was extracted into its own file.

Native Redis, placement and scheduler binaries were copied from the existing
Docker containers into `/tmp/story-6-6-native-dapr`. The retained
`run-native-live.py` records the exact launch arguments and cleanup. It uses
reserved random loopback ports, fresh private Redis/etcd directories, real Redis
PING and control-plane health checks, then invokes the same live class plus
`DaprTestInfrastructurePortsTests`.

`python3 /tmp/story-6-6-native-dapr/run-live.py` exited 0: **11 tests passed, zero
failures/skips**. Native Redis was 6.2.21, Dapr runtime/control plane 1.18.4.
Both application hosts and sidecars were exercised; all owned prerequisite
processes stopped. The first native attempt caught an off-by-one test call to
the range API's exclusive lower bound; the corrected `fromSequence=0` call
retained every durability assertion.

This is Development evidence of logical append/readback/retry/restart only. It
does not demonstrate ambiguous same-save event/result/outbox reconciliation,
broker receipts, non-actor control ETags/transactions, abrupt crash qualification,
mixed-fleet evolution, canonical aggregate/projection end-state, or production.
The final readable-length kernel repair was tested in the final Server suite;
the live scenario exercises the unchanged production V1 path.

## Outstanding requirements and preserved external work

The per-domain exact D/V/A/E/F/S/G manifest, authoritative gateway fingerprint,
trusted managed/native loader and complete handler/catalog roots remain absent.
The page reader remains internal and unregistered. Purpose-01 route/page proof,
handler compatibility binding, private durable replay/query sessions, verified
projection generations, subscription carrier/membership/effect receipts, resume
and hold controls, logical capture/redrive safety, diagnostics and the complete
M8 API/wire/compiled-consumer/crash/mixed-fleet matrix remain incomplete. The
array-returning protection and typed Dapr source seams still require their full
ingress/allocation qualification; local reservations do not prove arbitrary
provider allocations are bounded.

`test -f deploy/dapr/production-profile.yaml` exited 1. Production profile,
AD-26 ratification and production component/two-host qualification remain
external owner requirements. No test-local manifest, current application digest
or Development readback supplies that authority. V2 and proof-dependent
operations remain fenced.

No task-authored staging, branch, commit or push occurred. External activity
advanced root HEAD from `ff7f07d1` to `52bcb94f` and recorded the Builds removal
at `ba4ca78`; those commits were preserved. The externally owned
`spec-dapr-infrastructure-boundary-planning-reconciliation.md` appeared and was
subsequently modified during this run; its content was preserved.

## Parent implementation audit

The parent inspected the current slice's source/test diff and the retained test
logs. Every source and evidence entry in `sources-and-evidence.json` matched its
recorded SHA-256. The parent reran `python3 scripts/verify-event-evolution.py
--mutations`, `git diff --check`, and `git -C references/Hexalith.Builds diff
--check`; all exited 0. The production-profile existence check still exited 1.

The complete workspace diff from the preserved workflow baseline
`1329b35e52852952ecb2c94aabf100674e9691e3`, including untracked files, was captured
at `/tmp/story-6-6-baseline-66dbykqv.diff`. It includes earlier implementation and
concurrent external planning work. This record does not claim an exhaustive
completion review of that combined history.

| Story obligation | Audit result |
| --- | --- |
| M1 contracts/registry | Partial local kernel; authoritative domain manifest and loader/catalog closure remain missing. |
| M2 writer/shared reader | V1 reader hardening demonstrated; shared reader remains internal and unregistered; ambiguous same-save recovery is unqualified. |
| M3 control ownership | Actual hold/control participant ownership and Dapr capability/recovery evidence remain incomplete. |
| M4 replay/query | Fixed-head guards demonstrated locally; durable private replay/query sessions and consumer wiring remain incomplete. |
| M5 projections | Verified generation/checkpoint/query visibility and canonical persisted projection end-state remain unqualified. |
| M6 publication/subscription | Carrier, membership and durable effect receipt paths remain incomplete. |
| M7 operator recovery | Resume, hold inventory, capture/redrive safety and diagnostics remain incomplete. |
| M8 final evidence | Local regressions and Development readback passed; final API/wire/compiled-consumer, crash/mixed-fleet and production qualification remain open. |

The I/O matrix has partial local coverage for refusal, owner disposal and V1
duplicate/restart behavior. It lacks the required all-consumer canonical
aggregate/projection end-state, durable-boundary cancellation/crash and mixed-fleet
evidence. Therefore no task checkbox or acceptance criterion is marked complete,
and the workflow does not advance to a completed-story review or V2 activation.
