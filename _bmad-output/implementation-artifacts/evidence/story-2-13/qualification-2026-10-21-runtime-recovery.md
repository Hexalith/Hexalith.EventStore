# Story 2.13 continuation — isolated runtime recovery

The isolated Dapr candidate corrects the demonstrated reused-endpoint pooled-channel failure under the local two-host qualification profile. Three final-binary restart observations directly retain channel eviction, the next built-in retry, restored actor persistence and subsequent broadcast in about one second. The final distribution class passes all six cases. This supports a local runtime correction; the installed runtime and every production/disposition gate remain unchanged.

The packet starts at `bbfd0fada14ce11e137365637238af4fec2f7f7f`, preserving the existing dirty spec. The frozen baseline remains `f2b480b68a8bd5369b3cdf550340e0fadfb2332b`. The active spec and sprint row remain in-progress, the original story remains backlog, and `accepted_exceptions: []` is preserved. No owner approval, CI seal, NFR5 closure, runtime installation or production transition is supplied.

## Mechanism and correction

The retained [prior diagnosis](qualification-2026-10-21-restart-trace.md) identifies the surviving host's failed remote ETag call despite successful independent TCP probes. Pinned Dapr source `b32a815fa2260983e181b95272ea555fe17d3895` releases the same-address pooled channel with `cancel(false)`, although its retry comment promises destruction. grpc-go `1.83.2` retains reconnect backoff in that channel.

The [controlled source regression](runs-2026-10-21-runtime-recovery/upstream-built-in-backoff-red.json) seeds a real pooled gRPC channel in `TRANSIENT_FAILURE` with a deterministic one-hour backoff, restores its unchanged listener and proves an independent TCP connect succeeds. With the runtime's actual built-in retry initialization, the unpatched router fails after five one-second retries. Placement/security fakes isolate the mechanism; this is not persisted-delivery proof or a production backoff setting.

The [candidate](runs-2026-10-21-runtime-recovery/final-runtime-recovery.patch) destroys the ordinary remote-actor channel only after `Unavailable` while the caller context remains active. It changes no retry count, caller deadline, reminder/stream path or global backoff default. The [same test turns green](runs-2026-10-21-runtime-recovery/candidate-backoff-green.json) on the next built-in attempt, with direct `Shutdown` observation of the old channel. Healthy, permission-denied and internal-error controls retain their channel.

The [final integrated timelines](runs-2026-10-21-runtime-recovery/recovery-timelines.json) bind original manifests and log hashes. In the first final-binary observation, the surviving controller starts the selected ETag invocation at `10:25:53.780` UTC. Its sidecar reports the retained connection-refused error for the reused port `39193`, evicts at `.781`, and recovers on attempt two at `10:25:54.786`. The controller completes and broadcasts afterward. Two independent TCP connects succeed during that unchanged wait. All three final-binary observations retain the same endpoint across restart, initial second-host ownership, Dapr read-model version/ETag reconciliation, authorized rejoin and serialized-boundary/tenant-denial assertions. The signal deadline remains 15 seconds. Historical channel fields were not directly measured, and passing reruns alone are not the basis for this correction.

## Exact local envelope

[Acquisition](runs-2026-10-21-runtime-recovery/runtime-acquisition.json), [build](runs-2026-10-21-runtime-recovery/final-candidate-build.json) and [runtime identity](runs-2026-10-21-runtime-recovery/final-runtime-identity.json) retain the commit-addressed archive, checksum-verified Go `1.26.9` toolchain, patch, unchanged `go.mod`/`go.sum`, compiled dependency hashes and exact commands. The build uses `-mod=readonly`, `CGO_ENABLED=0` and `allcomponents` in a disposable directory. Its explicitly local version is `1.18.7-local-2-13-recovery-observed`; executable SHA-256 is `42ae7bf369c7824f9ae72313d57a0e1590bfb4f7219e478333b3162ddf18601b`.

Only the proof fixture reads `HEXALITH_STORY_2_13_DAPRD_PATH`. It requires an absolute existing path, never falls back on an invalid selection, hashes without loading the entire executable into memory, rejects changes across starts/restarts, and records each host's selected path/hash. Constructor failure creates no component directory. No shipped host, package, AppHost, subscription, broker, issuer or deployment component changes.

Baseline uses explicit-AppHost `aspire run`, `aspire wait eventstore` and retained `aspire describe`. For controlled distribution, the owned auxiliary EventStore host and sidecar stop while the sample topology supplies startup metadata; the two proof hosts own the measured EventStore actors. Tenants resources are Finished, so no whole-topology health pass is claimed. Real projection composition later owns its separate Aspire topology and uses the candidate only for the two proof sidecars.

## Verification

Each linked receipt retains exact arguments, UTC interval, source snapshots, output hash and before/after executable/assembly identities. MSBuild captures use `.source` suffixes with original-path mappings. The Debug graph rebuild changes some .NET assembly hashes; the distribution observations and post-build regressions retain separate binary identities. The candidate Dapr executable remains unchanged throughout.

| Check | Receipt | Result |
| --- | --- | --- |
| Final focused reused endpoint, after capacity restoration | [focused](runs-2026-10-21-runtime-recovery/final-focused-with-restored-capacity.json) | 1 passed; 74.677 s |
| Complete final distribution class, without `-stopOnFail` | [class](runs-2026-10-21-runtime-recovery/final-class-with-restored-capacity.json) | 6 passed; 333.905 s; `10:33:05.021`–`10:38:39.291` UTC |
| Existing upstream router/connection-manager tests and added context controls | [Go regressions](runs-2026-10-21-runtime-recovery/final-upstream-regressions-context-controls.json) | Both packages passed; cancellation/deadline remain bounded and retain the healthy channel |
| Full Debug source-reference graph | [build](runs-2026-10-21-runtime-recovery/final-debug-graph.json) | 0 warnings/errors |
| Existing actor/broadcaster/notification checks | [focused regressions](runs-2026-10-21-runtime-recovery/focused-actor-broadcaster-regressions.json) | 66 passed |
| Real projection signal/detail, persisted readback and tenant denials | [composition](runs-2026-10-21-runtime-recovery/real-projection-composition-regression.json) | 1 passed |
| Ordinary Direct projection regression, including teardown | [Direct](runs-2026-10-21-runtime-recovery/ordinary-direct-regression.json) | 1 passed |
| Existing Redis compatibility baseline | [Redis](runs-2026-10-21-runtime-recovery/retained-redis-baseline-regression.json) | 1 passed |

Earlier identities remain separate. The initial candidate passes focused/class checks but lazily registers its logger, so missing eviction markers are inconclusive. The diagnostic-only logger correction registers before runtime logging options apply. The initial source-test bootstrap omitted built-in policies; its failure is retained beside the corrected five-retry red test. The initial cancellation control incorrectly expected a gRPC status from an internal router returning wrapped context errors; the retained corrected test requires the caller's actual context cause, bounded return and unchanged channel. These test corrections change no runtime behavior.

The first final-binary complete-class attempt retains six startup failures: the inotify ceiling was observed at `1,048,576`, below `1,085,294` accessible per-fd watch lines. The origin of that ceiling change is unknown. The [failure](runs-2026-10-21-runtime-recovery/final-complete-distribution-class.json), [observation](runs-2026-10-21-runtime-recovery/inotify-environment-failure.json) and [temporary restoration](runs-2026-10-21-runtime-recovery/restore-run-start-inotify-ceiling.json) remain intact. Returning to the observed run-start `2,097,152` ceiling is environment-only; focused then full-class qualification passes with unchanged source/binary identities and a stable recorded ceiling.

The separate Contracts validator blocker was not rerun or exempted. Its existing exact command `./tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests` retains 2,348 passed, 1 failed and 2 skipped, with 10,898 package-authority errors in historical MSBuild evidence. The [original receipt](runs-2026-10-21-blackhole/required-contracts-tests.json) remains unchanged.

## Preservation and remaining gates

[Preservation](runs-2026-10-21-runtime-recovery/historical-preservation.json) confirms all 7,564 pre-existing Story 2.13 evidence files retain their hashes. The frozen intent, original story, sprint row, exception register, production graphs and installed daprd remain preserved. The installed executable still hashes to `e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`.

[Closing observations](runs-2026-10-21-runtime-recovery/closing-observations.json) retain no candidate sidecars, matching EventStore hosts, fixture component directories or recorded fixture listeners. Owned Aspire instances stop; shared Redis/placement/scheduler containers remain. The disposable runtime directory remains available for inspection/reproduction. The source-declared empty resource directory is preserved because its initial ownership was not captured.

Restoration to `1,048,576` remains unsafe under the agreed observation rule: closing accessible per-fd watch lines are **1,095,412**, primarily unrelated tooling. The temporary **2,097,152** ceiling remains; inherited/shared-instance overcount is explicitly recorded. No unrelated process or persistent sysctl configuration changes.

The patch closes a shared failed channel; concurrent in-flight users and wider upstream/runtime profiles still require qualification. Reminder/stream recovery is unchanged. The installed runtime is not repaired by this experiment. Production runtime/durable broker/enrolled issuer selection, canonical completion/freshness faults, shipped enrollment, atomic graph transition/rollback and authorized disposition remain open. Story 2.16 retains NFR5 primary ownership; Story 3.17's Redis handoff remains unresolved. No whole-story checkbox or completion/review status advances.

The [packet index](runs-2026-10-21-runtime-recovery/index.json) binds retained bytes; [handoff identities](runs-2026-10-21-runtime-recovery/handoff-identities.json) bind this final report and spec. Recheck with `python3 -B _bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-runtime-recovery/verify-packet.py`. Integrity verification grants no approval or production authority.
