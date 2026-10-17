# Story 6.4 finite qualification handoff — in progress

The one authorized section 10 qualification pass ended after **28 native execution frames**. Whole-candidate acceptance is **blocked** by the full Debug DomainService, full Debug Live and full Release Live results below. No production, test, dependency, configuration or policy change was justified by the observed failures. Story 6.4 and sprint remain in progress; the 41 checked implementation tasks do not establish acceptance.

The [native ledger](../qualification-20261017-gates.json) records exact commands, environment, exit status, source/module graph, effective assets, output hashes, logs and native XML for every frame. The [agent coverage](../qualification-20261017-coverage.md) and [finite audit](../qualification-20261017-finite-qualification-audit.json) map all planned slots, aliases and bounded replacements. The parent independently audited actual native outcomes and hashes rather than treating execution coverage as success.

## Candidate and preservation boundary

Entry HEAD was `b99a03dfc29021af16511ee664820e05126b73f8`. An external arrival advanced it to `77d9af18a54d92506e310fe5a45021a395e66f60` before the first fresh producer. Every native frame binds the latter revision and its actual root-declared module identities. The original wrapper baseline remains `e17e7294f540ebba42fe25192998689f0ecd89c1`; no historical result was relabeled as a new candidate.

The [source/task audit](source-and-task-audit.json) verifies all **9,643 captured source-input hashes** are unchanged on disk and identical before/after all 28 frames. The incremental tracked implementation/configuration/product-documentation diff from entry is empty. New qualification evidence and mutable wrapper notes are recorded separately. This resume preserves the prior five review rounds; it does not claim a new review of every historical baseline change. The [latest-resume enrollment audit](latest-resume-regression-enrollment.json) matches all 45 resume cases by exact native type/method/display name in both fresh full Server configurations.

Approved normative SHA-256 remains `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`. The [preservation audit](preservation-after.json) checks the frozen wrapper intent, all pre-existing wrapper lines, 11,027 P1R files, 409 historical Story 6.4 files and 15 protected specification/policy/configuration files against the entry manifest. Mutable qualification notes are added without rewriting historical restrictions or results.

## Fresh complete-suite outcomes

Counts are selected / passed / skipped / failed / runner errors. Every reported not-run count is zero; an initialization failure can prevent theory cases from materializing and is not evidence they passed.

| Complete suite | Debug | Release |
| --- | --- | --- |
| Server | 5817 / 5792 / 25 / 0 / 0 | 5817 / 5792 / 25 / 0 / 0 |
| DomainService | 978 / 977 / 0 / 1 / 0 | 978 / 978 / 0 / 0 / 0 |
| Client | 1597 / 1597 / 0 / 0 / 0 | 1597 / 1597 / 0 / 0 / 0 |
| Contracts with fresh packages | 2351 / 2351 / 0 / 0 / 0 | 2351 / 2351 / 0 / 0 / 0 |
| LiveSidecar | 138 / 47 / 1 / 90 / 1 | 192 / 188 / 1 / 3 / 0 |

The 25 Server skips exactly match the existing declared set. The only full Live skip is `ProjectionWatermarkProcessWorkerTests.ApplyPersistedHistoryAsync`, whose owning restart test retains its actual outcome. The full Live method sets match; eight theories account for the 54-case difference between the failed Debug initialization and Release expansion. See the [native/matrix audit](native-and-matrix-audit.json) and [cohort comparison](oq8-native-shadow-and-cohort-audit.json).

Focused Server **642/642**, DomainService **110/110**, and Debug Live **40/40** passed. The final Release R4096 supplement passed **5/5**, with fifteen exact Current observations and clean control exits. These individual results do not replace failed complete cohorts.

## Producer, package and numerical evidence

The first exact parallel Release restore failed with MSB4166; its named temporary diagnostics directory was already absent on inspection, so its engine cause remains unproven. The prescribed bounded serialized restore/build/package fallback passed. Subsequent coherent Release restores/builds passed with zero warnings/errors. A premature Debug Contracts invocation without a fresh package inventory failed three package probes; that original cohort remains preliminary, alongside the subsequent fresh package-backed 2351/2351 cohort. The coordinator transition occurred between completed native commands and did not restart the whole pass.

The [package audit](package-inventory-audit.json) independently verifies all fourteen immutable local `999.0.0-ci-test` archives, their 32 embedded DLLs and fourteen durable identical archive copies after both Contracts cohorts. These are pack/consumer checks; nothing was published.

The [Live audit](live-binding-and-budget-audit.json) independently checks actual native XML/log/vector bindings, inventory consistency, Current and tail budgets across focused Debug, failed full Debug, failed full Release and R4096. The full Release lane emitted 48 observations: all fifteen Current rows and seven LiveCost rows remain measured, but the missing replica-recovery NativeAttempts witness is explicitly **unproven**. No missing row is converted to zero or borrowed from the earlier 49-observation focused pass. The [working-root/retry audit](working-roots-and-retry-audit.json) distinguishes measured retained phases from cumulative allocations and verifies the exact separate N8/N9 accounting in both full Server cohorts. Every frozen matrix row has executed passing witnesses; the full Live startup/replica gates remain failed.

Actual [version evidence](../qualification-20261017-actual-runtime-versions.json) records SDK 10.0.401 and native runtime 10.0.12. Regular controls/sidecars use pinned Dapr 1.18.2 and task-owned Redis 6.2.21 with captured image/settings/component identities. OQ8 uses its separately recorded Dapr 1.18.7/PostgreSQL profile. Its owning production-matrix case passed with declared test seams. The [parent shadow audit](oq8-native-shadow-and-cohort-audit.json) independently recomputes both complete copied runtime-set hashes using the native sorted `relativePath:sha256` protocol; both match the durable native receipt. The original incorrect filename-order comparison and its correction remain retained. This establishes that test profile's copy identity, not unmodified production qualification or a CI seal.

## Unrelated failures and evidence limits

- **DomainService Debug:** `ProcessReadDeadlineTests.AdapterSlotExhaustionDoesNotBlockAuditedDisposition` throws `ProcessReadAdmissionException` from the `finally` await of blocked workers. The unchanged external helper/fixture provenance and exact native failure are in the [blocker packet](unrelated-domain-debug-blocker.json). The [Debug/Release comparison](domain-debug-release-comparison.json) proves their source hashes match; Release passing neither diagnoses the admission exhaustion nor proves every Debug body assertion passed. No external process-feature fix or retry was made.
- **Full Debug Live:** all 90 failed case messages contain daprd file-watcher ENOSPC; the native collection also reports one OQ8 evidence-disposal error after its failed initialization cleanup. No native OQ8 artifact receipt exists for that lane. The [parent failure packet](live-debug-full-blocker.json) retains the actual 138-case result, matching earlier focused Debug output inventory, exact owning commands and separate native/outer logs.
- **Full Release Live:** three distinct tests refer to the same failed replica startup app/component/time/fatal line: source-guard receipt recovery, multi-host admission, and two-sidecar event evolution. The primary fixture continued and other measured assertions passed. The [failure grouping](../qualification-20261017-live-release-full-shared-replica-startup.json) preserves these as three test failures from one observed startup failure. Filesystem/inotify snapshots are post-failure context; disk or inotify exhaustion is not established as the root cause. No shared settings, processes or environment were repaired.
- **Mixed Release output provenance:** the zero-warning producers emit Gateway, TestSubscriber and Client.TestContracts Debug output lines. The [final producer supplement](../qualification-20261017-mixed-configuration-final-producer.json) binds concrete matching base copies/configuration identities. No claim that every dependency is Release is made. Nested publish/RID inventories can be inherited or unused; unchanged post-producer bytes alone do not prove fresh compilation or loading.
- **Capture limits:** fourteen inherited assets before the first implicit restore retain hashes without raw byte copies; every producer post-restore/after graph and native execution asset graph has raw bytes archived. The eight-second Debug fixture failure did not retain complete dynamic daprd child argv; owning test/control commands and startup logs remain available, and no dynamic ports were reconstructed. Remaining observed child argv/start-tick identities are retained explicitly.

## Owner decisions and formal gates

The [P1R decision](p1r-owner-decision.md) is **prepared, not approved**. Its exact scope manifest SHA-256 is `59dc0f17416f980a5c2b071f659d959fb88c5fee8c744fb4d55430d837953efe`: **77 unique files / 98 historical reported locations**, with overlapping cohorts accounted for by exact file/line hashes. It separately identifies the 3,188-path historical landing, complete preserved P1R inventory and current scanner policy. Both fresh Server scanner tests pass under that preserved policy; neither pass retrospectively approves bulk ingestion or erases historical red results. No credential was authenticated, tested or declared inert/usable.

The [authenticated-assurance requirements](authenticated-assurance-requirements.md) identify the remaining exact-head/workflow-bound CI seal, current authenticated owner-role registry, applicable assurance level and subject-bound owner attestation at least 24 hours after the last authored change to that exact subject. Stories 9.1/9.2 remain backlog and the local phase-4 matrix/validator artifacts are absent. No required seal was retrieved or supplied here, no remote transition was dispatched, and no timer eligibility, role grant or approval was invented. The October 15 specification approval and October 17 qualification authorization supply none of these runtime acceptance approvals.

## Cleanup and result

The [agent cleanup receipt](../qualification-20261017-owned-resource-cleanup.json) and [independent physical checks](owned-resource-cleanup-audit.json) verify recorded process identities have ended, controls/ports and owned Redis/container volume are released, both owned PostgreSQL container hashes are absent, and known scratch paths are gone. Shared resources and historical tool/template paths were preserved. The queue and helpers ended without a deadline timeout.

The [parent verification audit](parent-verification-audit.json) binds this handoff and the independent checks. Story 6.4 remains **in progress**. This pass performed no staging, commit, push, publication, deployment or operational maintenance cutover.
