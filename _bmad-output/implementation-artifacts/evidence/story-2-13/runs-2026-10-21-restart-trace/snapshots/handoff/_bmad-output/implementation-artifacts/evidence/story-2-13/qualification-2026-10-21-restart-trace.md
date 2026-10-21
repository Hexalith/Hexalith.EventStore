# Story 2.13 continuation — replica restart routing diagnosis

The same-endpoint restart profile still fails qualification. The retained
failure is between the surviving host's ETag actor invocation and the restored
replica, after successful Dapr delivery and before SignalR broadcast. A fresh
sidecar-internal gRPC endpoint passes as a separately identified fixture
profile; it is not a runtime repair or a production-profile decision.

This continuation starts from `ae917d3e4dbab20b8982c5222a13611c3e3faf35` with the
existing dirty tree preserved. The frozen baseline remains
`f2b480b68a8bd5369b3cdf550340e0fadfb2332b`. The original backlog story remains
backlog; the active spec and sprint row remain in-progress. No exception,
production transition, NFR5 closure, owner approval or CI seal is supplied.

## Established failure and mechanism evidence

The [instrumented broad failure](runs-2026-10-21-restart-trace/instrumented-distribution.json)
first localizes the failed hop. At `08:22:31.873` UTC the surviving host
receives the post-restart callback and starts `ETagActor.RegenerateAsync`.
At `08:22:36.882` it reports `DaprApiException`; it never starts the associated
SignalR broadcast. The restored second host receives the same phase,
regenerates and persists its ETag, and broadcasts successfully. Its actor also
owned this projection before stop. This run did not capture the underlying
Dapr error, so it establishes the failing hop, not the complete internal cause.
The [derived timeline](runs-2026-10-21-restart-trace/instrumented-failure-timeline.json)
binds its raw log hashes.

The final fixture makes initial ownership explicit. It tries at most 16 exact
fixture topics under one unchanged 15-second setup bound. Setup publications
carry `EntityId=actor-owner-selection`; both local broadcasts finish before
measured clients connect. The selected actor must have a post-persistence ETag
log on the second proof host. The packet retains candidate count, selected
actor, owning process and both endpoint identities. This routing observation
is diagnostic; the existing Dapr read-model version/ETag assertions still
provide persisted correctness evidence.

The [focused comparison](runs-2026-10-21-restart-trace/selected-owner-focused-pair.json)
and [complete final class](runs-2026-10-21-restart-trace/final-distribution-class.json)
each reproduce the same-endpoint failure. In the focused failure:

- Second-host ownership is selected on candidate 4. Its internal endpoint stays
  at port `39611` across the new sidecar process.
- The restored internal listener is logged at `08:51:43.370` UTC. The surviving
  controller receives the measured callback at `08:51:43.626`.
- The surviving sidecar reports `Unavailable`, with a retained connection-refused
  error for that same advertised endpoint. After about five seconds the
  controller receives `ERR_ACTOR_INVOKE_METHOD`, `IsTransient=false`, and returns
  the existing HTTP 500 retry response. The associated first-host broadcast
  never starts within the 15-second signal deadline.
- Five independent TCP connects to that exact advertised endpoint all succeed,
  near offsets 0, 1, 3, 5 and 10 seconds. Each connect has a 250 ms bound, sends
  zero payload bytes and runs beside the original signal wait. These are
  transport diagnostics, not Dapr correctness assertions.

The complete-class failure selects candidate 1 and repeats the same observation
at reused port `38693`, including five successful TCP probes. The two fresh
endpoint cases select candidate 1 and pass all existing restart/rejoin,
serialized-boundary, tenant-denial and persisted readback assertions. Their
ports change `45457 → 33815` and `46057 → 44071`. Candidate counts and actor
identities differ between runs; this is an operating-profile comparison, not a
matched-load performance measurement. The [restart trace summary](runs-2026-10-21-restart-trace/restart-trace-summary.json)
retains the selected events and original manifest/source/binary identities.

The observed failure class is a same-endpoint Dapr remote-actor recovery gap:
current TCP reachability and successful local actor execution coexist with the
survivor's failed remote gRPC invocation. The retained runtime source supports
pooled-channel reconnect/backoff as its mechanism:

- Dapr [v1.18.7 actor router](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/actors/router/router.go)
  obtains a remote connection and always defers `cancel(false)`. Its retry
  comment says an unavailable connection is destroyed, but this path does not
  request destruction.
- The pinned [connection manager](https://github.com/dapr/dapr/blob/b32a815fa2260983e181b95272ea555fe17d3895/pkg/api/grpc/manager/manager.go)
  keys the remote pool by address; `false` releases the connection while `true`
  destroys it. The pool reuses a released channel without requiring Ready.
- The pinned runtime's actor-not-found policy has five one-second retries.
  Its actual grpc-go `v1.83.2` dependency retains stateful reconnect backoff,
  whose default delay can grow to 120 seconds. A new address uses a different
  pool entry. Exact upstream bytes, retrieval URLs and hashes are retained in
  [upstream](runs-2026-10-21-restart-trace/upstream/).

These observations strongly support retained gRPC reconnect/backoff state;
channelz/heap state was not captured, so a particular internal channel field is
not directly measured. Earlier blackhole-packet failures lacking this detailed
trace are not retroactively assigned that complete mechanism. No retry budget,
caller cancellation, actor/broadcast ordering or runtime dependency was changed.
A runtime correction and same-endpoint requalification remain required.

## Changes and exact envelope

The only product-source additions are value-safe Debug diagnostics in
`ProjectionNotificationController.cs` and `ETagActor.cs`: actor invocation
start/completion, regeneration/persistence start and Dapr error code/transience.
Authorization, validation, metadata accounting and delivery behavior retain
their order and limits.

The existing test fixture/node/proof adds UTC/process-stamped captured lines,
actor runtime metadata, explicit actor/controller/broadcaster sources and the
actual host-path Server/Dapr assembly hashes. Internal gRPC port replacement
is opt-in in an additive fresh-endpoint test. The original reused-endpoint test
remains and still fails. Both keep the original three-minute scenario budget,
15-second signal wait, serialized metadata limits and Dapr readback assertions.
The TCP observer is confined to the test fixture path and makes at most five
zero-payload connections; it adds no product/provider fallback.

The local runtime is SDK `10.0.402`, .NET `10.0.13`, Dapr CLI `1.18.2` and daprd
`1.18.7`. The [structural Go build-info observation](runs-2026-10-21-restart-trace/runtime-build-metadata-corrected.json)
identifies daprd's full VCS revision
`b32a815fa2260983e181b95272ea555fe17d3895`, with `vcs.modified=false`, and grpc-go
`v1.83.2`. The [upstream tag identity](runs-2026-10-21-restart-trace/upstream/tag-identity.json)
matches that full revision. The runtime executable SHA-256 remains
`e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488`
at closure. The Go CLI was unavailable; the exact blocked command and partial
snapshot remain, and `read-go-buildinfo.py` reproduces the structural extraction
without installing a toolchain. An earlier generic marker extraction is retained
with its explicit correction.

Required baseline used explicit-AppHost `aspire run`, `aspire wait eventstore`
and sanitized `aspire describe`. The initial attempt could not start sidecars
because inotify watches were exhausted. After a temporary environment-only
limit increase, auxiliary EventStore/sample resources ran; Tenants hosts stayed
Finished, so no whole-topology health pass is claimed. The auxiliary sample
sidecar later exited because its source-declared empty resource directory had
disappeared; that failure and slower broad run stay separately retained.

For the final controlled pair/class, only that empty directory was restored and
its owned sample sidecar restarted. Owned auxiliary EventStore host/sidecar were
stopped through Aspire resource commands. Thus sample supplies startup metadata
while the two proof hosts supply EventStore actor ownership. The sanitized
[controlled topology](runs-2026-10-21-restart-trace/endpoint-control-support-state.json)
records this difference. No AppHost source, deployed component, cluster, broker,
issuer, Redis backplane setting or package graph was transitioned.

## Verification and retained failures

Every command receipt retains exact arguments, UTC interval, source snapshots,
output hash and before/after binary identities. Runtime test inputs and binaries
remain stable within each test command; superseded source identities are separate.
Early unchanged runs predate expanded host-Server hash capture; their
supplemental in-run hashes are labelled as such and do not claim earlier
before/after observations.

| Check | Receipt | Result |
| --- | --- | --- |
| Final complete distribution class | [final-distribution-class.json](runs-2026-10-21-restart-trace/final-distribution-class.json) | **6 total: 5 passed, 1 failed**, no skipped/errors; 340.216 s; `08:53:50.532`–`08:59:30.935` UTC |
| Final focused reused/fresh pair | [selected-owner-focused-pair.json](runs-2026-10-21-restart-trace/selected-owner-focused-pair.json) | **1 passed, 1 failed**; 164.725 s; reused endpoint fails, fresh endpoint passes |
| Full Debug source-reference graph | [final-debug-graph.json](runs-2026-10-21-restart-trace/final-debug-graph.json) | Passed; 0 warnings/errors; 41.0 s |
| Existing focused actor/broadcaster/notification checks | [focused-actor-broadcaster-tests.json](runs-2026-10-21-restart-trace/focused-actor-broadcaster-tests.json) | **66 passed**, none failed/skipped/errors |
| Earlier instrumented broad failure | [instrumented-distribution.json](runs-2026-10-21-restart-trace/instrumented-distribution.json) | Post-restart actor invocation failure; first-host signal timeout; retained separately |
| Earlier unchanged focused/full runs | [unchanged-restart-with-capacity.json](runs-2026-10-21-restart-trace/unchanged-restart-with-capacity.json), [unchanged-distribution.json](runs-2026-10-21-restart-trace/unchanged-distribution.json) | 1 and 5 passed; no cause inferred from passing reruns |
| Earlier Debug-sidecar focused/full runs | [routing-focused-restart.json](runs-2026-10-21-restart-trace/routing-focused-restart.json), [routing-distribution.json](runs-2026-10-21-restart-trace/routing-distribution.json) | 1 and 5 passed, with auxiliary actor ownership and sample-sidecar limitation retained |

The final full class deliberately omits `-stopOnFail`. Its sole failure is the
reused-endpoint first-host signal timeout; reset, blackhole, synthetic delivery,
exact-topic catalog and the fresh-endpoint comparison all complete and pass.
The final focused pair runs before that complete class on the same final source
and binary identity. No new real-domain projection composition or canonical
UI completion/freshness proof is claimed by this continuation.

The existing Contracts validator blocker is separate and was not rerun here.
The blackhole packet's exact retained Release command
`./tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests`
reported 2,348 passed, 1 failed and 2 skipped: the package-authority validator
reported 10,898 errors in already tracked historical retained MSBuild evidence.
Its [original receipt](runs-2026-10-21-blackhole/required-contracts-tests.json)
remains unchanged. This continuation retains MSBuild snapshots with `.source`
suffixes and changes no validator exemption or historical packet.

## Preservation, cleanup and disposition

The [preservation check](runs-2026-10-21-restart-trace/historical-preservation-corrected.json)
confirms all **1,798** pre-existing evidence files retain their starting hashes;
tracked historical packet diffs are empty. Its earlier overbroad check included
10 mutable source/doc/config entries and is retained with the explicit correction.
The exact starting identity list is retained as
[baseline-identities.json](runs-2026-10-21-restart-trace/baseline-identities.json)
and matches the preservation check's `baselineIdentityFileSha256`.
The local packet verifier is
`python3 -B _bmad-output/implementation-artifacts/evidence/story-2-13/runs-2026-10-21-restart-trace/verify-packet.py`.

[Closure observations](runs-2026-10-21-restart-trace/closing-observations.json)
find no fixture sidecar, temporary component directory or listening final-class
port. Owned AppHost instances stop through Aspire; the source-declared empty
resource directory is removed only after its users are gone and it is empty.
Shared Redis/placement/scheduler containers are preserved.

The temporary inotify change cannot yet be restored under the agreed condition:
observed watch lines are **1,075,173**, above the original **1,048,576** limit.
The temporary limit remains **2,097,152**; no persistent sysctl configuration or
unrelated process was changed. The [closing watch observation](runs-2026-10-21-restart-trace/inotify-closing-before-restore.json)
records the counting method and possible inherited-instance overcount.

The same-endpoint runtime recovery gap remains open, and fresh endpoint replacement
is a distinct local fixture envelope. A qualified runtime correction, selected
production runtime/durable broker/enrolled issuer, shipped enrollment,
canonical completion/freshness faults, atomic both-host transition/rollback and
authorized disposition remain outstanding. Story 2.16 retains NFR5 primary
ownership. Keep `accepted_exceptions: []`, the original Story 2.13 backlog,
the active spec/sprint row in-progress and the Redis paths in Story 3.17's
unresolved handoff. This packet supplies diagnosis and bounded comparison,
not whole-story completion.
