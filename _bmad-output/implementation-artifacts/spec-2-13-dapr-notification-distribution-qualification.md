---
title: 'Dapr Notification Distribution Qualification'
type: 'feature'
created: '2026-10-11'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f2b480b68a8bd5369b3cdf550340e0fadfb2332b'
story_id: '2.13'
story_key: '2-13-dapr-notification-distribution-qualification'
context:
  - _bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/implementation-artifacts/spec-evidence-storage-simplification.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The SignalR Redis backplane remains a provider dependency without an accepted exception. The Dapr callback proves local broadcast, not delivery to authorized clients on every replica.

**Approach:** Compare Dapr pub/sub fan-out and Azure SignalR output binding with Redis. Prove a design through two-host, two-sidecar projection delivery, then transition both host graphs and deployment assets together. If neither passes, record gaps and seek a scoped exception decision.

## Boundaries & Constraints

**Always:** Preserve Story 2.8's `ProjectionChanged` and additive `ProjectionChangedDetail`, authorized joins and rejoin, metadata bounds (16 entries; 2,048 serialized UTF-8 bytes), and Debug-only logging of values. Signals are hints; persisted projection evidence governs freshness and success. Bind identities, limits, review decisions, UTC tests, and rollback in an immutable packet. Follow AD-8/9/12/26 profile gates; Story 2.16 owns NFR5 primary evidence.

**Never:** Treat a single replica, mock, HTTP acceptance, or notification alone as proof. Assume competing consumers reach every hub or the binding supplies negotiation and membership. Add provider fallback, weaken authorization, expose credentials, forge `Current`, retire Redis before rollback proof, provision external service, or accept an exception without authority.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Scoped change | Persisted tenant A projection changes; A and B hosts each hold an authorized A client | Both clients receive signal and detail, then read back the same authoritative projection version; tenant B receives none | Missing replica delivery fails qualification |
| Reconnect or duplicate | Client reconnects/rejoins after auth change; repeated or reordered notice | Reauthorize group; re-query persisted state; no duplicate completion or domain mutation | Denied rejoin receives no tenant data |
| Outage | Sidecar, provider, host, or query unavailable | Bounded/cancellable failure, honest `Stale`, `Degraded`, `Unavailable`, or `Unknown`; restore re-queries | No silent fallback or unbounded retry |
| Denial | Anonymous, wrong/conflicting tenant, forged group, expired identity | Reject before group join or downstream work on both hosts | Safe reason code, no private group/payload disclosure |

</frozen-after-approval>

## Code Map

- `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`, `SignalROptions.cs` — `ConfigureBackplane` chooses Redis; Gateway compiles linked source.
- `src/Hexalith.EventStore/SignalRHub/ProjectionChangedHub.cs`, `IProjectionChangedClient.cs`, `SignalRProjectionChangedBroadcaster.cs` — groups and signal/detail wire contract.
- `src/Hexalith.EventStore/Controllers/ProjectionNotificationController.cs`, `DaprProjectionChangeNotifier.cs` — signed Dapr callback and local broadcast; preserve provenance.
- `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRRedisBackplaneRuntimeProofTests.cs` — client/negative-control patterns; no sidecars or persisted change. `tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs` — two-process sidecar blueprint; shadow host is not proof.
- `src/Hexalith.EventStore.AppHost/Program.cs`, `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`, `deploy/dapr/subscription-projection-changed.yaml` — one-host profile and subscription assets.
- `src/Hexalith.EventStore/Hexalith.EventStore.csproj`, `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj` — both carry the Redis package. `docs/architecture/dapr-infrastructure-exceptions.yaml` has no accepted exception.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md` — compare exact Redis, Dapr pub/sub and `bindings.azure.signalr` profiles across negotiation, targeting, fan-out, ordering, recovery, auth, credentials, cost, and rollback; mark unsupported/unrun rows.
- [ ] `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionRuntimeProofTests.cs`, `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionFixture.cs` — run two hosts with independent sidecars, shared app identity, per-host clients, projection trigger/readback, signal/detail bounds, and reconnect/duplicate/outage/denial cases; retain logs and component identity.
- [ ] `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`, `SignalROptions.cs`, both host `.csproj` files, `src/Hexalith.EventStore.AppHost/Program.cs`, `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`, and `deploy/dapr/subscription-projection-changed.yaml` — change atomically only after a candidate and profile pass; record any additional selected deployment component path before editing it.
- [ ] `docs/concepts/dapr-infrastructure-boundary.md`, `docs/architecture/dapr-infrastructure-exceptions.yaml`, `_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — record disposition and 3.17 handoff; accept an exception only after owner and Security/Operations/Test approval. Otherwise retain backlog.

**Acceptance Criteria:**
- Given both candidates and Redis, when Architecture/Security/Test review the matrix, then every operation has exact-profile evidence or a named gap, alternative, and owner.
- Given two hosts with independent sidecars and authorized clients, when a projection changes, then both hosts' relevant clients receive scoped signals, Dapr readback proves the persisted version, and denied clients receive none.
- Given reconnect, duplicate, outage, and rollback scenarios, when they run under the chosen profile, then bounded failure and honest freshness survive and the prior compatible path can be restored.
- Given completion is requested, when reviewers inspect the packet, then it binds identities, commands, results, UTC interval, scenarios, limits, and an approved disposition.

## Implementation Notes

### Current continuation: October 25 unchanged local qualification

The October 23 evidence-storage decision in architecture AD-12 supersedes the
packet-writing instructions in the dated continuations below. Record new commands,
results, source revision and CI run identity in the original story's Verification
section; do not restore deleted evidence files or introduce new committed packets
or packet validators. The dated continuation scopes and their historical failures
remain historical context, including both failed projection regressions and the
Redis binary-identity ambiguity.

The former frontmatter context report was deleted by
`6a0b6441f6d78a3007678a1d0ec336e9cb8ff961`. Its complete historical contents were
read without restoring it using
`git show bbfd0fada14ce11e137365637238af4fec2f7f7f:_bmad-output/implementation-artifacts/evidence/story-2-13/qualification-2026-10-21-restart-trace.md`.
The replacement context entry binds the current evidence-storage policy.

This continuation verifies the unchanged, explicitly selected retirement runtime
and the existing application proofs. Passing current runs cannot establish the
cause or resolution of the historical failures. Implement a correction only after
reproducing and establishing its mechanism. Keep the installed runtime, dependencies,
production graphs, frozen intent, original story's backlog status, in-progress spec/sprint
row and empty exception register unchanged.

### Active continuation: shared-channel concurrency safety

Measured continuation decision: the first concurrency packet demonstrates that
retained eviction cancels held actor/reminder/stream RPC attempts. Its evaluated
TransientFailure-only ResetConnectBackoff alternative passes fixed-loopback
controls and all six distribution cases, but a pinned grpc-go resolver-churn race
probe reports concurrent map access and a fatal error. Preserve that candidate
and all measurements as limited/rejected wider-profile evidence. This supports
evaluating a second runtime-only correction in a second disposable tree and
`evidence/story-2-13/runs-2026-10-21-concurrency-retirement/`, without dependency
updates or patches. The source-supported design may mark the failed pooled
channel retired, exclude it from new leases, and close it only after the last
active lease releases; the current caller must release exactly once. Qualify pool
lifetime/concurrency and cancellation-guard discrimination, then the unchanged
focused reused-endpoint case, complete distribution class without stopOnFail and
relevant regressions. No ResetConnectBackoff call belongs in that candidate.
The initial candidate's successes cannot erase either measured failure.


Resume from `evidence/story-2-13/qualification-2026-10-21-runtime-recovery.md`,
`evidence/story-2-13/review-2026-10-21-runtime-recovery/README.md` and
`evidence/story-2-13/closing-after-review-2026-10-21.json`. The existing working
tree is expressly authorized; preserve all prior work. This continuation only
qualifies shared-channel concurrency safety of the isolated Dapr correction.

The retained runtime destroys an Unavailable pooled actor channel. In a new
disposable source tree, use controlled overlapping calls against the retained
candidate source to measure whether that action interrupts other active actor
invocations, remote reminders or actor streams. Distinguish a transport failure
from Unavailable returned by a healthy server. Preserve the original acquired
source, candidate source/binaries, reports and evidence packets byte-for-byte.
Record new evidence only under `evidence/story-2-13/runs-2026-10-21-concurrency-safety/`
and a new qualification report; `start-preservation.json` and `spec-before.source`
already record parent start state. Never stage, commit, push or create a branch.

Only evaluate an isolated correction after pinned runtime/dependency sources and
measured failures support it. Retain complete before-build source inventories,
exact patch, unchanged dependency/toolchain identities and binary hashes. Preserve
caller cancellation/deadline semantics and the discriminating cancelled-Unavailable
control from the retained review supplement (the old controls alone missed that
regression). Keep retry/cancellation budgets, actor persistence before broadcast,
authorization, serialized metadata bounds and the 15-second signal wait unchanged.
The installed runtime and production graphs must remain unchanged. Launch-identity
hardening is explicitly deferred unless required to establish trustworthy evidence.

After any supported correction run focused concurrency controls and the reused-
endpoint case, then the entire distribution class without stopping on failure,
then relevant Go runtime and existing application regressions. Bind failures and
passes to exact source/runtime/binary identities; successful reruns alone establish
no resolution. Fresh-endpoint replacement is comparison evidence only. Preserve
existing .NET binary identities unless a necessary rebuild is supported and
recorded separately. Keep the existing Contracts validator blocker separate; do
not weaken or exempt it. Reassess inotify restoration to 1,048,576 by observing
accessible watches without disturbing unrelated processes; retain blockers and
every ceiling change. Use the permitted Aspire workflow for lifecycle operations
and capture exact owned-process/resource identities and cleanup observations.

The spec and sprint row remain in-progress, original story backlog, and
`accepted_exceptions: []`. No completion/review advancement, production transition,
runtime installation, owner approval or qualification/disposition gate is implied
by local qualification. Whole-story gated tasks remain unchecked. This user scope
overrides any generic workflow instruction to complete gated production work.


### Active continuation: isolated reused-endpoint runtime correction

The user resumes from `evidence/story-2-13/qualification-2026-10-21-restart-trace.md`
and authorizes local evaluation of an isolated Dapr runtime or configuration
correction for the reused-endpoint remote-actor recovery failure. Use the retained
failures, successful TCP probes and pinned runtime sources to establish the pooled
gRPC reconnect/backoff mechanism before implementing a correction. Preserve the
installed runtime, production graphs, dependencies, historical packets and frozen
baseline. A candidate runtime may be built in a disposable local directory and
selected explicitly for the test fixture only; retain exact source revision,
patch, toolchain/dependency and executable hashes, command receipts, failures and
passes in a new packet. A fresh internal endpoint remains comparison evidence and
does not resolve reused-endpoint recovery. Do not infer resolution from passing
reruns alone. Preserve authorization, serialized metadata bounds, cancellation
budgets, actor-persistence-before-broadcast ordering and the 15-second signal
deadline. Run the focused reused-endpoint case first, then the complete distribution
class without `-stopOnFail`, then relevant regressions. Report the existing
Contracts validator blocker separately. Reassess restoration of the temporary
inotify ceiling to 1,048,576 without disturbing unrelated processes and retain any
restoration blocker. Keep this spec and its sprint row in-progress, the original
story backlog and `accepted_exceptions: []`. Production qualification, approval,
rollback and disposition tasks remain gated; this continuation must stop short of
whole-story completion and must not advance a completion/review status merely
because its local evaluation finishes. Record supported corrections and unresolved
gaps candidly, including any inability to build or qualify an isolated runtime.

### Active continuation: October 21 restart timeout investigation

The user explicitly resumed the existing dirty working tree from
`evidence/story-2-13/qualification-2026-10-21-blackhole.md`. Preserve that work
and every historical packet. This continuation focuses only on the unresolved
first-host signal timeout after the second replica restarts. Use the existing
failure capture to reproduce and trace Dapr delivery →
`ProjectionNotificationController` → `ETagActor.RegenerateAsync` → SignalR
broadcast. Establish the cause before changing behavior. Implement a fix only
when justified by retained evidence; do not infer resolution from passing
reruns alone. Preserve authorization, metadata bounds, caller cancellation
budgets, and the 15-second signal deadline. Retain failures and passes on their
exact source and binary identities in a new continuation packet (never rewrite
the blackhole packet). Include actor/controller/broadcaster sources in new
identity captures. Run the focused restart case, then the full distribution
class. Report the existing Contracts validator blocker separately. Production
graph/profile changes remain gated; keep the active spec and sprint row
in-progress and the original backlog story uncompleted until all remaining
qualification and approval requirements pass. Do not execute gated production
tasks or claim whole-story completion during this focused continuation.

- The [qualification packet](evidence/story-2-13/qualification.md) records a passing test-only two-host, two-sidecar exact-topic Dapr proof, a real projection-triggered signal with projection-backed readback, and a scoped Redis exception proposal that has **not** been accepted.
- The shipped Redis Streams wildcard subscription did not deliver concrete tenant topics. Dynamic tenant subscription, real projection-triggered detail, full outage/freshness and rollback behavior, and owner approvals remain open. No production profile or backplane dependency changed; the original story remains backlog.
- The ordinary Aspire regression test body passed, but collection teardown failed while restoring the shared writer-protocol marker. The packet records the exact result and unresolved lock interference. The local inotify limit also remains elevated while unrelated processes hold more watches than the original limit.
- The [2026-10-19 continuation](evidence/story-2-13/qualification-2026-10-19.md) records a separate run-start revision, current runtime identities, expanded two-host denial/bounds/reconciliation scenarios, retained component bytes and redacted logs, and the unresolved serialized-versus-key/value metadata ceiling. It does not change the frozen baseline, accept an exception, or authorize the atomic runtime transition. Historical teardown and machine-state observations above remain historical; current results and cleanup are bound by the continuation.
- The [2026-10-20 continuation](evidence/story-2-13/qualification-2026-10-20.md) enforces the user-confirmed serialized metadata contract and records passing final-source tests: 93 focused cases, both two-host Dapr scenarios, real-projection composition, ordinary Direct regression, and the retained Redis baseline. Replica restart now proves fresh authorization/rejoin, persisted Dapr reconciliation, and per-host rejection of three oversized serialized vectors beside an exact-boundary control. Exact tested spec/profile bytes, superseded failures, commands and assembly identities are retained separately. Dynamic topics, real projection detail, production profile, broader outage/freshness, transition/rollback and owner disposition gates remain open; the original story remains backlog.
- The [network-fault continuation](evidence/story-2-13/qualification-2026-10-20-network.md) isolates both sidecars' Redis connections through a disposable loopback proxy. The selected test paths are `SignalRDaprDistributionFixture.cs`, `SignalRDaprDistributionRuntimeProofTests.cs`, and the new `SignalRDaprNetworkFaultProxy.cs`; generated component files remain fixture-only. The user identified self-hosted Kubernetes at `192.168.1.30` as the MVP production target and excluded Azure/AWS from MVP. Cloud binding behavior remains a comparison-only unrun candidate, not a service-provisioning prerequisite. Read-only cluster observations and local network-fault qualification do not select or authorize a production notification profile.
- The same continuation evaluates a fixture-only exact-topic catalog rollout for a previously absent tenant. It retains before/after subscriptions, reloads both sidecars, preserves host/client identities, and uses an unpublished fresh control topic after reload. Operator-managed catalog rollout is distinct from automatic tenant discovery; no shipped subscription, catalog, or deployment path changes. Before the additive detail slice, `ProjectionResponse` and actor-owned `ProjectionState` carried no scoped detail, and the post-persistence calls in `EventReplayProjectionActor` selected only the signal overload.
- The story authorizes the additive real-detail implementation choice; no separate owner decision is required merely to choose an optional domain-provided hint. Selected paths before that slice are `Contracts/Projections/ProjectionResponse.cs`, `Server/Actors/ProjectionState.cs`, `EventReplayProjectionActor.cs`, `.ReplayPublication.cs`, a new `.Notifications.cs` partial, `Server/Projections/ProjectionUpdateOrchestrator.cs` and `.LegacyReplay.cs`, the sample `CounterProjectionHandler.cs`, the real-projection integration proof, and their focused contract/state/actor/sample compatibility tests. Null detail fields must preserve legacy JSON and DataContract shape, bounded metadata must be copied before actor persistence, scope must not alter trusted tenant/projection routing, and authoritative candidate fingerprints remain derived from model bytes alone. No provider/profile transition is selected by this slice.

The final local continuation passed the full Debug graph, 186 focused detail/compatibility cases, real two-host projection signal/detail/readback and tenant denials, ordinary Direct regression, and 182 supporting client/credential cases. The four-scenario network/catalog/runtime run also passed against its separately retained earlier source/binary identity. Optional legacy projection-response detail is implemented; named v2 outcomes are unchanged. Remaining gates are the selected production runtime/durable broker/enrolled issuer, shipped enrollment, canonical completion/freshness fault profile, atomic graph transition/rollback and authorized disposition. Story status and whole-task checkboxes remain open; no exception or production profile is accepted.

The [October 21 local continuation](evidence/story-2-13/qualification-2026-10-21-blackhole.md) adds silent bidirectional traffic discard beside connection reset in the existing test-only network proxy and distribution runtime proof. Both cases pass under a declared auxiliary AppHost/sample topology, with unchanged caller cancellation/readback limits, fresh two-host delivery and tenant denials. The full Debug source-reference graph passes. An unchanged focused replica-restart run passes, but two broader runs time out at the first-host post-restart signal; that intermittent delivery gap and the separate standalone startup failure remain retained. Run-start revision is `7217b5bf740b80ae92858378785b496e6b564285`; the frozen baseline is unchanged. Current Story 3.21 inputs still select no production broker/profile. Production runtime/broker/issuer, canonical completion/freshness, atomic transition/rollback and authorized disposition remain required gates; no whole-story checkbox or completion status is advanced.

The local restart diagnostic phase adds failure-only evidence retention in the existing fixture/runtime proof and targeted controller receipt logging in `SignalRDaprDistributionNode.cs`. Failed captures are explicitly separate from passed assertions; the signal wait remains 15 seconds and capture has its own bounded budget. New fixture/runner captures retain MSBuild inputs with a `.source` suffix and explicit original-path mappings. Earlier new-packet file names may be transformed with recorded aliases while their bytes and emitted manifests remain unchanged; historical packets remain untouched.

## Spec Change Log

- 2026-10-25 — Replaced the deleted frontmatter report with the approved evidence-storage
  policy and documented its read-only historical retrieval. New unchanged-runtime
  qualification results belong in the original story's Verification section under
  AD-12. Prior failures and all production/disposition gates remain open.

The [shared-channel concurrency evaluation](evidence/story-2-13/qualification-2026-10-21-concurrency-retirement.md)
reproduces eviction cancelling held actor/reminder/stream RPC attempts and rejects
a reset alternative after a pinned grpc-go resolver-churn data race/fatal error.
A second disposable runtime uses pool retirement and final-lease closure; focused
race/context controls, reused-endpoint recovery, the complete six-case class,
post-class Go/churn checks, 66 focused application checks and Redis pass. Initial
watcher startup failures remain retained beside a measured external ceiling drop.
Real projection composition and ordinary Direct projection fail and remain
unresolved; this is not overall qualification. Historical packets, original
runtime/source subjects, production graphs, frozen intent and accepted-exception
register remain preserved. The active spec/sprint stay in-progress and original
story backlog; launch-identity hardening and all production/disposition gates
remain deferred/gated. No staging, commit or push was performed by this task.


The [October 21 isolated runtime evaluation](evidence/story-2-13/qualification-2026-10-21-runtime-recovery.md)
retains a deterministic pooled-backoff red/green regression and a disposable,
explicitly selected Dapr candidate. Three final-binary reused-endpoint traces
observe eviction, the unchanged built-in retry, restored actor persistence and
broadcast in about one second. The final distribution class passes all six
cases; upstream router/manager controls, the Debug graph, 66 focused checks,
real projection composition, Direct and retained Redis regressions pass on their
recorded identities. An externally observed inotify ceiling drop and all startup
failures remain retained; original-ceiling restoration remains blocked by
unrelated watch use. The installed runtime, production graphs, historical packets,
frozen baseline, original backlog story and accepted-exception register stay
unchanged. Local evaluation advances no whole-story checkbox or completion/review
status; production qualification, rollback and authorized disposition remain gated.


The [October 21 restart routing continuation](evidence/story-2-13/qualification-2026-10-21-restart-trace.md)
reproduces the surviving host's failed remote ETag invocation after restart.
Both controlled reused-endpoint failures retain successful independent TCP
connects during the unchanged 15-second signal wait; the additive fresh internal
endpoint profile passes twice with initial second-host actor ownership proved.
Pinned runtime/pool/backoff sources support retained gRPC reconnect state, with
no direct channel-state capture or runtime repair claimed. The final full class
records 5 passed and 1 failed of 6, and the Debug graph plus 66 focused existing
checks pass. Same-endpoint recovery and all production/disposition gates remain
open; this continuation advances no whole-story checkbox or completion status.

- 2026-10-20 — The user resolved the metadata accounting conflict: "Enforce the serialized-byte limit in the approved story spec." The frozen intent is unchanged. Compact default System.Text.Json metadata sizing now includes punctuation and escaping across producer, receiver and broadcaster, with Contracts-owned ceilings. The [October 20 continuation](evidence/story-2-13/qualification-2026-10-20.md) separates the observed pre-fix gap from final-source qualification and replica restart evidence. Profile/owner gates and the retained Redis disposition remain unresolved.
- 2026-10-20 — The user clarified: "production is on local 192.168.1.30 kubernetes server. Azure or AWS is not MVP." This narrows the target; it approves no issuer, broker, runtime upgrade, cluster mutation, or production transition. The frozen intent and required candidate comparison are unchanged.

## Review Triage Log

Review scope is the authorized local reused-endpoint continuation from run-start
`bbfd0fada14ce11e137365637238af4fec2f7f7f`; the frozen story baseline is preserved.
The original runtime packet/report remain immutable. Review does not advance status
or production authority. Follow-up evidence belongs in a separate review supplement.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| Blind 1: whitespace selector falls back | low | Whitespace/empty selection deliberately uses the installed default. The report's invalid-selection wording can be read more broadly; clarify this interface in the supplement, without changing tested fixture behavior. |
| Blind 2: healthy-server Unavailable evicts | medium | The patch deliberately classifies by status, not connection state; a server-originated Unavailable also evicts. This wider behavior remains a runtime qualification gate. Reject a wider recovery redesign in this local experiment; no installed/production runtime is changed. |
| Blind 3: shared-channel concurrent users | medium | Pool destruction closes the channel and can interrupt concurrent users. The original report explicitly retains this unqualified risk. Broader runtime/concurrency qualification and production selection remain gated by the user's intent; no production safety claim or exception is supplied. |
| Blind 4: incomplete extracted-tree identity | medium | Build receipts bind the archive, router and manifests, rather than every extracted file. Add a complete read-only post-build tree comparison to the supplement; it cannot retroactively supply a pre-build observation. |
| Blind 5: timeline correlation | false | Each of the three retained measured windows has exactly one first-controller actor invocation and one retry error naming the selected reused endpoint, followed by recovery and the matching request completion. No competing invocation is present in these captures. Wider concurrent profiles remain unqualified. |
| Blind 6: handoff not checked by verifier | medium | The original verifier checks indexed packet bytes but does not compare external handoff documents. Add explicit successor handoff checks while preserving the original handoff and its spec snapshot. |
| Blind 7: candidate provenance cross-checks | medium | Equal host hashes alone do not mechanically compare final scenario/build/patch/runtime records. Add those comparisons in the separate review verifier. |
| Blind 8: optional final artifacts | medium | Removing the original index/timelines skips their guarded checks. Require final artifacts in the successor verifier and validate the final class independently. |
| Blind 9: optimized Python removes assertions | medium | PYTHONOPTIMIZE can suppress original assertions. The successor verifier must explicitly reject optimized execution and run the original verifier with optimization disabled. |
| Edge 1: closing active shared users | medium | Same demonstrated close semantics and gated limitation as Blind 3. Reject a pool-retirement redesign in this scoped local continuation; concurrent production behavior remains unqualified. |
| Edge 2: executable hash/start race | maybe-false | No executable mutation occurred in retained before/after observations, but they do not exclude a change-and-restore race. A concurrent mutation experiment or atomic launch/image-identity mechanism would settle this unverified medium risk; defer that fixture hardening. |
| Edge 3: recorder spawn error | low | OSError would bypass the receipt writer for an absent executable, but all 28 recorded commands actually spawned. Reject added generic recorder recovery complexity for this immutable one-off packet. |
| Edge 4: recorder command hang | low | The recorder has no global watchdog, but no command hung; scenario and call budgets remain tested. Reject new generic process-tree timeout/cleanup machinery in this continuation. |
| Verification 1: cancellation predicate survives mutation | medium | A temporary overlay proves existing controls pass with the ctx.Err guard removed. The new deterministic cancelled-Unavailable control passes the candidate and rejects that mutation. Retain its exact source, commands and logs in the separate supplement; no runtime or application code changes. |


### Shared-channel continuation review — 2026-10-21

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| B1 | medium | patch | Draft verifier requests missing receipt names. Preserve the draft; canonical integrity checks and a separate qualification gate will use actual filenames. Failed regressions must still fail qualification. |
| B2 | medium | patch | Final closing bindings were pending during review; finish inventory, spec snapshot and preservation checks before closure. |
| B3 | medium | patch | Old recorders omit effective Go overlays before/after execution. Preserve old receipts and rerun final Go checks using complete overlay capture on unchanged product source. |
| B4 | medium | defer | Some regression source snapshots omit application fixtures/configuration. Application binaries are hash-bound, but that is not full source provenance. Retain a retrospective observation and require fuller future qualification inputs; do not claim pre-run capture. |
| B5 | medium | patch | Retained recorders use assertions for immutability. Actual runs used ordinary Python and no overwrite is observed. Preserve old tools and use unconditional exclusive creation for new records. |
| B6 | medium | patch | Existing concurrency barrier starts after retirement. Add an initially-active retirement/acquisition/final-release race control via overlay. |
| B7 | medium | patch | Post-retirement cancellation/deadline cases were absent. Add live caller controls and verify replacement-channel users stay healthy. |
| B8 | medium | patch | Counter-only Close cannot prove pool lock availability. Add a blocking Close control and simultaneous acquisition. |
| B9 | medium | patch | Manifest-only PID discovery misses failed starts. Supplement with an independent workspace product-path scan; retain launch-identity deferral. |
| B10 | medium | patch | Timeline helper permits empty persistence lists. Actual four lists are nonempty; canonical checks must reparse actor-correlated events and require persistence before broadcast. |
| E1 | medium | patch | Duplicate B1: missing filenames are real. The two projection failures must remain failures and qualification incomplete. |
| E2 | low | patch | Abandoned first-candidate finalizer targets an uncreated report. Preserve and explicitly retire it; use the closing directory instead. |
| G1 | medium | patch | Pre-verified gap: extra-release router mutant passes old controls. Retain/rerun the second-lease control and mutant with complete identities. |
| G2 | medium | patch | Duplicate B1/E1: planned checker cannot read actual receipt names. Its all-pass qualification gate remains unsatisfied; no waiver. |


Review follow-up retains passing additional lifetime controls and a full Go race replay with complete overlay identities in `evidence/story-2-13/concurrency-closing-2026-10-21/review-lifetime-controls/`. The caller-lease mutant is rejected, persistence is checked against raw actor-correlated logs, and retired tooling is documented separately. B4 remains deferred: retrospective application fixture snapshots cannot provide pre-run provenance. The canonical qualification gate remains failed for both projection regressions; no status or authority advances.


Closing audit: the Redis test body passes, but its receipt records nine assembly/deps before/after differences, so stable-binary compatibility remains unqualified. Later workspace output drift is separately retained; its writer is unattributed and current user outputs are preserved. The final qualification gate fails for the two projection regressions and this identity ambiguity. Complete raw-log, overlay and historical preservation checks do not grant qualification or approval.

### October 25 unchanged local continuation review

Review covers this continuation's documentation changes against run-start
`749767072b8c2ea2abc8a23540ca65471d078c49`; the frozen story baseline is preserved.
The edge-case and verification-gap reviewers found no additional issues. This is
a local documentation review, not a whole-story qualification or owner approval.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| October 25 Blind 1: fallback output provenance | medium | patch | Added nine captured assembly/deps identities, build/reuse provenance and the earlier pair's missing deps/sample capture. The story now distinguishes execution stability from complete source correspondence. |
| October 25 Blind 2: disposable runtime reconstruction | medium | patch | Added the upstream revision and six historical Git retrieval references for the patch, dependencies, toolchain and build command. Missing current inputs and the inability to reproduce the runtime are explicit. |
| October 25 Blind 3: auxiliary topology commands | medium | patch | Added the exact start, readiness and resource-stop sequence, process/resource names, and the retained in-run state observation. Missing separate wait/stop receipts are explicit. |
| October 25 Blind 4: support service identities | medium | patch | Added Redis, placement, scheduler and Aspire CLI identities. The story labels their inspection as retrospective and states that absent pre-run captures cannot be supplied retroactively. |

### October 25 current-workspace requalification review

Review covers the new Verification section in the original story, measured against
run-start `aa46e2526ab33b2f4b220478c8acc097f7ad01b0` and its recorded checkouts.
The frozen baseline and all prior findings remain unchanged. The edge-case and
verification-gap reviewers found no additional issues. This local documentation
review advances no whole-story status, production qualification or approval.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| Current-workspace Blind 1: logical readback observations omitted | medium | patch | The real-projection manifest retains both counts, equal ETags, host/sidecar PIDs, connection ownership and tenant-denial results. Added a compact summary to the story so logical readback remains assessable after temporary output expires. |
| Current-workspace Blind 2: generated component identities omitted | medium | patch | Component kinds alone do not bind tested configuration bytes. Added the real-projection manifest's pub/sub, state-store and subscription hashes, explicitly limited to that scenario. |
| Current-workspace Blind 3: focused test runner hashes omitted | low | patch | The 102-test command uses a separate executable. Added its DLL and dependency-manifest hashes from matching before/after entries in `focused-server.json`. |
| Current-workspace Blind 4: Redis command template differs | low | patch | The generated Redis report contains a static `dotnet test` template. Clarified that it was not executed and that the captured direct executable invocation is authoritative; retained report bytes remain unchanged. |

All four corrections are documentation additions sourced from existing receipts;
no runtime tests were repeated. The production/disposition gates and missing Go
requalification remain open. Review logging is the only change to this spec.

### October 25 current-revision continuation review

Review covers only this run's Verification append in the original story. Results
remain bound to `340b1d1a9f5f259d70ade0aa88e969d5a5e855c5`; concurrent commit
`245ded36b78b960b57728f845f9a8aae62275908` is outside the tested revision and
reviewed change. The frozen baseline, statuses and whole-story gates are preserved.
The edge-case and verification-gap reviewers found no additional issues.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| Current-revision Blind 1: sequential-command claim | low | patch | The in-distribution support-state receipt records read-only inspection during the distribution test. Limit sequencing to the build and five test commands and identify the concurrent observations. |
| Current-revision Blind 2: reused-endpoint observations omitted | medium | patch | Both restart manifests prove initial second-host actor ownership, unchanged internal ports and persisted version/ETag 1 to 2. Retain these observations beside the pass counts so recovery evidence survives temporary output expiry. |
| Current-revision Blind 3: fault-effect and cancellation observations omitted | medium | patch | The reset and blackhole manifests retain actual disconnect/reconnect/discard counters, bounded cancelled reads and restored/final persisted versions. Add their compact measurements and preserve the synthetic-state/freshness limits. |
| Current-revision Blind 4: tenant-denial observation window omitted | low | patch | The real-projection proof checks four other-tenant channels concurrently using one-second reads. State that bound beside the negative delivery flags. |

These are documentation corrections using existing measurements; they require no
runtime rerun and confer no production qualification or approval. Review logging
is the only change to this spec.

### October 25 d41a3c55 continuation review

Review covers this run's Verification append in the original story against
`d41a3c55a684491d02f61c5ea98e0f74332b5d4d`. The frozen baseline, statuses and
whole-story gates remain unchanged. The edge-case and verification-gap reviewers
found no additional issues.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| d41a3c55 Blind 1: environment missing from copyable commands | low | patch | The temporary recorder sets all four variables for every command; only the proof fixture consumes the candidate selector. Add explicit exports and correct the integration-only wording so a copied invocation preserves the measured settings. |
| d41a3c55 Blind 2: restart process identities compressed | medium | patch | Both restart manifests retain original and replacement host/sidecar PIDs plus unchanged internal gRPC ports. A labeled table makes the replacement and focused/full-class distinction assessable without temporary captures. |
| d41a3c55 Blind 3: projection log correlation omitted | low | patch | The real-projection manifest binds synthetic tenant, topic, aggregate and correlation values to the reported connections and readback. Record those test-generated identifiers while retaining the shared-topology limitation. |
| d41a3c55 Blind 4: metadata boundary results omitted | medium | patch | Both restart captures record 2,048 serialized bytes accepted on both hosts and 2,129/2,050/2,060-byte vectors rejected. State those measured outcomes beside the unchanged contract limits. |

These corrections use existing command receipts and scenario captures. No runtime
tests were repeated for documentation edits. Review logging is the only change to
this spec; local passes supply no CI result, production qualification or approval.

### October 25 7012502b continuation review

Review covers only this continuation's Verification append in the original story,
against run-start `7012502bc094484eb2d4687badb98cb3b5e52b73`. Concurrent
payload-protection changes are outside this review. The frozen baseline, intent,
statuses and whole-story gates remain unchanged. The edge-case and verification-gap
reviewers found no additional issues.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| 7012502b Blind 1: drift chronology | low | patch | The build's after-status already records the concurrent owner-delivery spec edit, outside the measured source inventory. Corrected its chronology separately from the later payload-protection source edits. |
| 7012502b Blind 2: focused readback identity | medium | patch | Added the focused restart manifest's exact scenario and logical read-model key beside version/ETag 1 to 2, preserving the synthetic-state limitation. |
| 7012502b Blind 3: copied-output replay | medium | patch | Added receipt-backed preparation/comparison replay steps, without another measured execution or source-correspondence claim. Parent verification confirmed all 682 copied file identities and the recorded zero exit. |
| 7012502b Blind 4: lifecycle command sequence | low | patch | Added the existing receipts' ordered sample waits, both failed stops and recovery commands/results without rerunning the topology. |
| 7012502b Parent 1: cleanup wording | low | patch | Limited cleanup claims to this continuation's actions and preserved the observed unrelated process start times. |

These are documentation corrections from existing measurements. The six-case
distribution run remains identity-ambiguous, the copied 102-control result remains
a separately identified output subject, and the unrun regressions and earlier Go
failures remain open. No production qualification, approval or status advancement
is supplied.

Final parent checks passed the tracked-file policy (9,050 entries), whitespace,
copied-output inventory, frozen-intent preservation, unchanged sprint status and
append-only story history. Concurrent HEAD
`fd083a91b67a3bca10016f1f645da913c7ef526e` was observed afterward; it is outside
the tested revision and this continuation's reviewed changes. No runtime tests
were repeated for documentation corrections.

### October 25 6ca1b4cb continuation review

Review covers only this continuation's Verification append in the original story,
against run-start `6ca1b4cb9d422017138541036b3c23397265de98`. The frozen baseline,
intent, statuses and whole-story gates remain unchanged. Edge-case and
verification-gap reviewers found no additional issues. Command counts, timestamps,
output hashes and failure descriptions matched the local receipts.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| 6ca1b4cb Blind 1: outage publication outcomes omitted | medium | patch | Reset and blackhole manifests retain `DaprException` after 70.4399 ms and cancellation after 1,999.8448 ms, respectively. Add these measurements beside read cancellation bounds so publication failure can be assessed independently. |
| 6ca1b4cb Blind 2: catalog restart requirement compressed | low | patch | `ReloadSidecarsAsync` stops both sidecars and then starts both. Name that sequence explicitly rather than implying live subscription refresh. |
| 6ca1b4cb Blind 3: fresh-endpoint comparison identities omitted | low | patch | The fresh-endpoint manifest records original host/sidecar PIDs 63452/63565, replacements 64827/80045 and internal port 34867 to 44529. Retain them beside the reused-endpoint rows without claiming that comparison resolves reused-endpoint failures. |
| 6ca1b4cb Blind 4: client dependency versions omitted | medium | patch | The integration dependency manifest's recorded hash still matches current bytes and names Dapr.Client 1.18.10 and Microsoft.AspNetCore.SignalR.Client 10.0.13. Add those exercised client identities without claiming complete dependency coherence. |
| 6ca1b4cb Blind 5: final documentation check results implicit | low | patch | The implementation handoff reports both post-append checks passed. State those outcomes explicitly and run the narrow policy/whitespace checks again after review corrections. |

These corrections use existing receipts and source; no runtime rerun or behavioral
change is required. The stable real-projection failures, both Redis failures and
all historical/production gates remain open. This local documentation review
advances no completion/review status or approval. Final parent policy checks
passed (9,091 tracked entries), as did `git diff --check`. Parent preservation
checks confirmed the original story prefix, frozen intent, spec baseline/status,
sprint row and exception register; run-start HEAD remained unchanged.

### October 25 dee84974 continuation review

Review covers only this continuation's Verification append in the original story,
against run-start `dee8497458bfc92e094ac66fb977b2eddabd6c93`. The frozen baseline,
intent, statuses and whole-story gates remain unchanged. Edge-case and
verification-gap reviewers found no additional issues.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| dee84974 Blind 1: shared-endpoint topology understated | medium | patch | The in-distribution process receipt includes six additional auxiliary app identities on placement/scheduler endpoints 50005/50006. Corrected the participant list; bounded observations still establish no exclusive topology. |
| dee84974 Blind 2: scenario capture mapping omitted | low | patch | Nine manifests exist, including two reused-endpoint runs and catalog before/after captures. Added their exact temporary relative paths beside the measurements. |
| dee84974 Blind 3: Redis comparison observations omitted | medium | patch | The existing report records distinct origin/target PIDs, 21 ms signal delivery, the disabled-backplane control and a 5,747.1851 ms fail-open diagnostic. Added those observations with transport-only and source-drift limits. |
| dee84974 Blind 4: denial coverage summary imprecise | low | patch | Passing source includes malformed-tenant and replacement-identity denied-rejoin assertions. Enumerated exercised denials and distinguished the unestablished conflicting-claim and in-place revocation cases; no missing test is claimed fixed. |
| dee84974 Blind 5: inherited Contracts blocker unidentified | low | patch | Read-only historical retrieval identifies the Release command, 2,348 passes/1 failure/2 skips and 10,898 package-authority errors in retained MSBuild evidence. Added that identity without asserting a current Contracts failure or rerun. |

These corrections use existing receipts and test source; no runtime tests were
repeated. Parent checks independently confirmed all 550 measured output identities
match across all ten test boundaries. Concurrent commit
`8fb04342d487e8ec1df80a15a75f967227201294` was observed after the measured commands;
it is outside the tested revision and this continuation's reviewed changes.
Review logging is the only change to this spec. Local passes supply no CI result,
historical failure resolution, production qualification or approval.

## Verification

The October 25 executed commands, counts, UTC intervals and local-only limitations
are recorded in the [original story's Verification section](2-13-dapr-notification-distribution-qualification.md#verification--october-25-unchanged-local-continuation).
They do not replace historical failures, a CI run, Go requalification, the failed
complete source-reference dependency graph, or the gated production disposition.

**Commands:**
- `aspire run` then `aspire describe` — establish baseline resource state before code edits.
- `dotnet build tests/Hexalith.EventStore.IntegrationTests/Hexalith.EventStore.IntegrationTests.csproj -c Debug` — compile the focused proof project; invoke the built xUnit v3 executable with `-class` for focused runtime proof.
- `dotnet build Hexalith.EventStore.slnx -c Debug -m:1` — verify both host graphs; run relevant test projects individually and record exact commands/results in the packet.
- `git diff --check` — no whitespace errors.
