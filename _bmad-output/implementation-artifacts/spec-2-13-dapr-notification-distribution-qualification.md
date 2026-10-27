---
title: 'Dapr Notification Distribution Qualification'
type: 'feature'
created: '2026-10-11'
status: 'done'
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
- `src/Hexalith.EventStore/Hexalith.EventStore.csproj`, `src/Hexalith.EventStore.Gateway/Hexalith.EventStore.Gateway.csproj` — both carry the Redis package. `docs/architecture/dapr-infrastructure-exceptions.yaml` holds the register; `docs/concepts/dapr-infrastructure-boundary.md`, `_bmad-output/planning-artifacts/architecture.md` (AD-34 current state, freshness signals, open-issues row) and `prd.md` §8.4 state the disposition.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/evidence/story-2-13/qualification.md` — compare exact Redis, Dapr pub/sub and `bindings.azure.signalr` profiles across negotiation, targeting, fan-out, ordering, recovery, auth, credentials, cost, and rollback; mark unsupported/unrun rows. (Removed under AD-12; summarized in the story's Disposition.)
- [x] `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionRuntimeProofTests.cs`, `tests/Hexalith.EventStore.IntegrationTests/ContractTests/SignalRDaprDistributionFixture.cs` — run two hosts with independent sidecars, shared app identity, per-host clients, projection trigger/readback, signal/detail bounds, and reconnect/duplicate/outage/denial cases. Final run at `c5d1c280d`: 112 passed, 1 failed (full-class sidecar readiness); recorded in the story.
- [x] `src/Hexalith.EventStore/SignalRHub/SignalRServiceCollectionExtensions.cs`, `SignalROptions.cs`, both host `.csproj` files, `src/Hexalith.EventStore.AppHost/Program.cs`, `src/Hexalith.EventStore.AppHost/DaprComponents/pubsub.yaml`, and `deploy/dapr/subscription-projection-changed.yaml` — not applicable: no candidate and profile passed, so nothing changes. These files stay as they are under the accepted exception.
- [x] `docs/concepts/dapr-infrastructure-boundary.md`, `docs/architecture/dapr-infrastructure-exceptions.yaml`, `_bmad-output/implementation-artifacts/2-13-dapr-notification-distribution-qualification.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — record the owner-accepted exception (2026-10-25, single-maintainer-attested) and the 3.17 handoff; story and sprint row `done`.

**Acceptance Criteria:**
- Given both candidates and Redis, when Architecture/Security/Test review the matrix, then every operation has exact-profile evidence or a named gap, alternative, and owner.
- Given two hosts with independent sidecars and authorized clients, when a projection changes, then both hosts' relevant clients receive scoped signals, Dapr readback proves the persisted version, and denied clients receive none.
- Given reconnect, duplicate, outage, and rollback scenarios, when they run under the chosen profile, then bounded failure and honest freshness survive and the prior compatible path can be restored.
- Given completion is requested, when reviewers inspect the packet, then it binds identities, commands, results, UTC interval, scenarios, limits, and an approved disposition.

## Implementation Notes

### Owner disposition — 2026-10-25 (supersedes every continuation note)

The owner chose the frozen intent's exception branch: "If neither passes, record gaps and seek a scoped exception decision." No Dapr candidate passed on an installable runtime and production profile, and the owner accepted a scoped exception for the opt-in Redis backplane. The project has one maintainer, so the decision is labelled `single-maintainer-attested`. The three blocking gaps were tenant-topic enrollment, reused-endpoint restart on stock daprd, and no AD-26 production profile.

This change is documentation and tracking only:

- Move `signalr-replica-distribution` into `accepted_exceptions`. Record its exact paths, package, configuration keys, alternatives, the grants it withholds and a removal trigger.
- Update the boundary guide, the architecture/PRD status sentences and the story's Disposition.
- Mark the sprint row `done`.

No source, test, project, AppHost or deployment file changes. The final run at `c5d1c280d` is the evidence. The only new run is the transport-independent hub and backplane-wiring unit tests on that build: 39 passed. The exception grants no production eligibility, because AD-26 still decides the replica posture.

The dated continuation notes from October 19–25 are superseded. They are in Git history at `c5d1c280d8c1c1a207cd159d817f614e93c93130`.

## Spec Change Log

- 2026-10-25 — Owner decision: accept a scoped exception for the opt-in SignalR Redis backplane instead of another local requalification run. The tasks were rewritten to the exception branch and the stale continuation notes were replaced. Frozen intent is unchanged.
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

### October 25 2696cf21 continuation review

Review covers this continuation's Verification append in the original story,
against run-start `2696cf21b3ed243f75c74feab5b947697e876d74`. The five test
commands ran at `12784fde729a4756564ae388fb6c2217c7323446`; this does not
establish build/source correspondence to that revision. Frozen intent, baseline,
statuses and whole-story gates remain unchanged. Edge-case and verification-gap
reviewers found no additional issues.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| 2696cf21 Blind 1: Platform cohort identity | medium | patch | Build receipts name `c4fad9cd10491547a2b3e94c108442eff38a6edd`; every test receipt names `5fa798f7d860a021feb8cccc35331271e3c9c802`. Distinguished these cohorts in the story. |
| 2696cf21 Blind 2: build-to-test source drift | medium | patch | Comparing build-after with first-test-before finds 13 changed source files and unchanged outputs. Recorded that boundary and denied correspondence of the reused binaries to the later source revision. |
| 2696cf21 Blind 3: auxiliary host exit omitted | low | patch | Both support describes report EventStore Finished with exit 134 and its sidecar exit 0. Retained those codes without inferring their cause from successful stop commands. |
| 2696cf21 Blind 4: process scan coverage | low | patch | The temporary observer matches EventStore paths and Dapr, excluding Tenants application hosts. Narrowed the absence claim to the inspected process families. |
| 2696cf21 Blind 5: catalog outcomes omitted | low | patch | Before/after manifests retain absent new-tenant delivery, two added exact subscriptions, unchanged host identities and both readbacks at version/ETag 2. Added these observations to the story. |

These are documentation corrections from existing receipts and source, requiring
no runtime rerun or behavioral change. Stable test-boundary identities do not
resolve build-to-test drift, historical failures or production/disposition gates.
All five corrections were checked against existing receipts; no new finding was
deferred. Final parent policy checks passed (9,096 tracked entries), as did
`git diff --check`. The original story prefix, frozen intent, spec baseline/status,
sprint file and exception register were preserved.

### October 25 c5d1c280 continuation review

Review covers this continuation's Verification append in the original story at
`c5d1c280d8c1c1a207cd159d817f614e93c93130`. The frozen historical baseline,
intent, statuses and whole-story gates remain unchanged. Edge-case and
verification-gap reviewers found no additional issues.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| c5d1c280 Blind 1: timeout wording | low | patch | The full-distribution log contains `TimeoutException`; clarify that no timeout budget was increased, preserving the failed execution. |
| c5d1c280 Blind 2: executable inventory scope | low | patch | The recorder hashes selected daprd, installed daprd and Aspire CLI only. Name those executables rather than implying Dapr CLI or .NET installation hash coverage. |
| c5d1c280 Blind 3: negative-delivery observation bounds | low | patch | Outage denials use four concurrent one-second reads. Catalog denials use two concurrent one-second detail reads followed by two concurrent one-second signal observations. State those limits beside the results. |
| c5d1c280 Blind 4: catalog topic/hash correspondence | low | patch | The after-rollout manifest binds both subscription hashes to exact added/fresh topic names. Include those names and its relative locator for traceability. |

These documentation corrections use existing receipts and test source. No runtime
tests are repeated. Parent comparison independently confirms all 17,613 measured
source/configuration entries and 550 outputs match the post-build snapshot across
all twelve test boundaries. The full distribution class remains 5 passed / 1
failed despite the passing focused catalog recheck; no cause, correction,
production qualification or completion is inferred. Review logging is the only
change to this spec.

### October 25 owner-disposition review

This review covers the exception disposition in the working tree against `c5d1c280d8c1c1a207cd159d817f614e93c93130`. That is narrower than the frozen baseline, matching the scope of earlier continuation reviews. The earlier subsections describe their own runs and predate this disposition. No intent_gap or bad_spec entry arose, so every surviving entry was patched or deferred.

| Finding | Verdict | Route | Evidence / action |
| --- | --- | --- | --- |
| Verification other 1: sprint row not `done` | low | patch | `sprint-status.yaml:92` still read `in-progress` with stale comments. Comments replaced; the status is synced to `done` in the finalize step. |
| Verification other 2: allowed paths omit key readers | medium | patch | `SignalRRuntimeProofEndpoints.cs:113-120` reads both keys; `ValidateSignalROptions.cs` and `SignalRRuntimeProofIdentity.cs` handle the option. Added all three to `allowed_paths`; corrected the story and architecture wording. |
| Edge 1: same allowed-paths gap | medium | patch | Duplicate of Verification other 2. |
| Edge 2: no exact package or Redis version | medium | patch | The PRD §8.4 exact-version rule applies. Added package `10.0.13`, its Builds pin path and the tested Redis `6.2.21`. |
| Edge 3: Redis outage fail-open not recorded | low | patch | `SignalRServiceCollectionExtensions.cs:52` sets `AbortOnConnectFail = false`; the final run measured HTTP 200 with `FailOpenFailure`. Added `outage_behavior`. |
| Edge 4: earlier Redis proof failures undisclosed | low | patch | The 6ca1b4cb run recorded two readiness-cancellation failures. Disclosed in the Disposition. |
| Edge 5: gaps unowned after `done` | medium | patch | AC4/AC5 partials and harness flakes had no ledger entry. Added one consolidated `deferred-work.md` entry. |
| Edge 6: trigger needs all conditions | low | patch | Now review on any change, remove only when all hold. |
| Edge 7: other docs still say unresolved | medium | patch | Updated `architecture-overview.md` (3 lines), `epics.md:335` and the Story 3.17 AC and task text. |
| Edge 8: acceptance is retroactive under AD-34 | medium | patch | AD-34 requires a decision before introduction; Redis already shipped. Added `retroactive: true` with a note. |
| Edge 9: `1.18.x` generalization | low | patch | Only stock `1.18.7` was tested. Wording corrected in the register and story. |
| Edge 10: env-only key untested | false | reject | `SignalRServiceCollectionExtensions.cs:46-47` falls back to `EVENTSTORE_SIGNALR_REDIS` when the option is null, so no bad outcome occurs. |
| Edge 11: removed runtime/topology requalification clause | medium | patch | Restored package-version, Redis-major and replica-topology changes as review triggers. |
| Edge 12: sprint row | low | patch | Duplicate of Verification other 1. |
| Edge 13: architecture open-issues row says register empty | medium | patch | Row at `architecture.md:566` updated. |
| Edge 14: retained path's outage behavior implied qualified | medium | patch | Same root cause as Edge 3; the AC4 row now states that the Redis path fails open. |
| Edge 15: Azure binding never run | low | patch | Records that it was not run and was excluded by the owner's 2026-10-20 scope decision. |
| Blind 1: tracker statuses disagree | low | patch | Duplicate of Verification other 1. |
| Blind 2: architecture contradictions | medium | patch | Open-issues row fixed; "This rule itself accepts no exception". |
| Blind 3: stale docs and story handoff | medium | patch | Same as Edge 7, plus the story's Verification Handoff rewritten to the current state. |
| Blind 4: allowed paths and "listed above" wording | medium | patch | Same as Verification other 2. |
| Blind 5: operator guides present production backplane | medium | defer | The 2026-05-03 guides already contradicted AD-26, so this is pre-existing; logged in `deferred-work.md`. The register now says `channelPrefix` sharing grants nothing extra. |
| Blind 6: acceptance table overclaims | medium | patch | AC2 notes no Dapr design was selected; AC3 notes the patched-runtime dependency; added the 5/1 class result and the catalog-test recheck. |
| Blind 7: gaps unowned | medium | patch | Duplicate of Edge 5. |
| Blind 8: restart-gap evidence missing from record | medium | patch | Added the `bbfd0fada` restart-trace pointer (verified present), the `1.18.7` scope and the patched-sidecar note. |
| Blind 9: actor defect beyond SignalR | medium | defer | Pre-existing runtime defect; the deferred entry now routes it to AD-26 and the open-issues table. |
| Blind 10: trigger, purpose, single-replica rationale | medium | patch | Added `purpose` and named the probe test in the trigger. |
| Blind 11: credential boundary, versions, status | medium | patch | Added `credential_boundary` and versions. The top-level `status` stays Story 3.17's schema field. |
| Blind 12: stock-runtime probe left undecided | low | patch | The handoff and trigger name it as the probe and state the environment requirement. |
| Blind 13: older subsection contradicted by this diff | low | reject | The fix edits this build's spec; this subsection records the disposition. |
| Blind 14: counts and dates | low | patch / reject | Story run date added. The spec count wording and command results are rejected as spec edits; results are logged below. |
| Blind 15: history-rewrite fragility and wording | medium | patch | The deferred entry now describes the daprd patch and says to report it upstream before the rewrite; "nine runs" and "preceding" wording corrected. |

After patching, the register parsed, `bash scripts/check-committed-files.sh` passed, and `git diff --check` passed.

## Verification

The final runtime results, commands, counts and UTC intervals at `c5d1c280d8c1c1a207cd159d817f614e93c93130` are in the [story's Verification section](2-13-dapr-notification-distribution-qualification.md#verification--october-25-c5d1c280-continuation). No CI run is linked. This disposition changes documentation only.

**Commands:**
- `python3 -c "import yaml; yaml.safe_load(open('docs/architecture/dapr-infrastructure-exceptions.yaml'))"` — the register parses.
- `bash scripts/check-committed-files.sh` — the committed-files guard passes.
- `git diff --check` — no whitespace errors.
- `tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests -class '*ProjectionChangedHubTests' -class '*SignalRBackplaneWiringTests' -class '*SignalRHubEndpointTests'` — 39 passed, 0 failed (2026-10-25 17:36:20–17:36:22 UTC).
