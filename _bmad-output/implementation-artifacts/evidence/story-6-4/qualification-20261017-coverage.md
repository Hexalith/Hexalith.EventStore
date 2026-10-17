# Story 6.4 finite qualification, 2026-10-17

The existing implementation needed no production, test, configuration, dependency or policy changes. All 28 execution frames in the authorized finite pass are retained. Section 10 acceptance remains open: the full Debug DomainService, full Debug Live and full Release Live cohorts failed. No further native attempt, source repair, deployment, cutover, publication or Git mutation was performed.

The approved normative digest remains `78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711`. Entry HEAD was `b99a03dfc29021af16511ee664820e05126b73f8`; an external arrival advanced HEAD to `77d9af18a54d92506e310fe5a45021a395e66f60` before the first fresh producer. Every frame binds its actual root/module graph, 9,643 source hashes, assets and output inventory. All captured source sets are identical before/after and across all 28 frames. External modified gitlinks were preserved.

The [finite audit](qualification-20261017-finite-qualification-audit.json) retains exact argv/environment, exit codes, XML/log identities, native test identities, source/asset/output changes, planned-to-actual aliases and authorized replacements. Its direct test DLL/deps/runtimeconfig bindings all match their explicit producer snapshots and remain stable during native execution. Matching producer snapshots can include reused incremental outputs; they do not assert that every captured directory file was freshly compiled or loaded.

## Native outcomes

| Complete cohort | Debug selected / passed / skips / failed / errors | Release selected / passed / skips / failed / errors |
| --- | --- | --- |
| Server | 5817 / 5792 / 25 / 0 / 0 | 5817 / 5792 / 25 / 0 / 0 |
| DomainService | 978 / 977 / 0 / 1 / 0 | 978 / 978 / 0 / 0 / 0 |
| Client | 1597 / 1597 / 0 / 0 / 0 | 1597 / 1597 / 0 / 0 / 0 |
| Contracts, fresh 14-package feed | 2351 / 2351 / 0 / 0 / 0 | 2351 / 2351 / 0 / 0 / 0 |
| LiveSidecar | 138 / 47 / 1 / 90 / 1 | 192 / 188 / 1 / 3 / 0 |

All native not-run counts are zero. Exact focused Server **642/642**, Domain **110/110** and regular Live **40/40** passed. The separate Release R4096 Current lane passed **5/5**, with fifteen exact H/P/R combinations and no skips/errors/failures.

Both Server configurations retain the exact same 25 existing DW1 skips. Both full Live lanes retain only `ProjectionWatermarkProcessWorkerTests.ApplyPersistedHistoryAsync`; its owning restart test passed in Release and failed with the fixture initialization in Debug. That helper skip cannot waive its owning test. Debug and Release Live use the same method set; fixture failure changes eight theory multiplicities by 54 cases. Native counts are preserved as reported, without substituting the successful cohort's theory expansion.

The first parallel Release restore failed with MSB4166 child-node exits. Its advertised diagnostics directory was already absent at inspection; no engine root cause is claimed. The owning serialized `-m:1` fallback restore/build/package attempt succeeded, and subsequent serialized Release producers succeeded with zero warnings/errors. The preliminary Debug Contracts cohort ran before a fresh package feed existed and failed three package probes. Its original packet remains explicitly preliminary; the fresh package-backed full Debug cohort above passed all 2351 tests. The old/new coordinator boundary and every failed invocation remain retained; no whole-pass retry occurred.

## Workloads and measured observations

The frozen profile remains H=0..10000, P=1..256 with existing defaults, 2-MiB framed pages, independent 64-MiB source/wire pass limits, R=256/4096, A=8/32 and separately measured N=8/9. The scheduler's five CAS attempts and workload occupancy remain distinct from delivery-row A. No configured limit was changed.

The finite audit's `workloadSelectorResults` binds the concrete fresh XML methods for every row below. No selector is missing. An individual passed assertion or numeric row does not turn its enclosing failed cohort green.

| Required scope | Fresh executable/evidence binding |
| --- | --- |
| W0 Current / empty / drift | Server production-current matrix and source bootstrap/empty qualification; actual Live Current matrices and touched-empty zero-effects vector. |
| W1 long small tail / shared routes | Production actor/store/handler cost rows, three-route seven-pass rows and actual Live delta-one tails. |
| W2 multiple pages | Delta-771 P1/P256 production rows, actual retry-worker profile and Live P256 tails. |
| W3 count / framed-byte boundaries | Production boundary cases and actor 8190/8191/8192-byte framing/lookahead assertions. |
| W4 bytes / protection / working roots | Mixed/protected lookahead, exact 64-MiB and one-over source/wire limits, all proof/event working-memory assertions. |
| W4 shared scheduler / cold / CAS | Full scheduler Costs/CasRaces classes, 64x64 and 128-item occupancy, cold/mutation measurements and actual ninth activation. |
| W5 fallback / wider compatibility | Full-prefix legacy/named/generic recovery, original manifest preservation, and existing separately configured streamed rebuilds beyond 64 MiB. |
| W6 transitions / retention / durable boundaries | A8/A32 exhaustion, N8/N9 original-work preservation, R/R+1 complete identities, source/owner changes and owning cancellation/lost-response/completion tests. |
| Domain capabilities / public ABI / ownership | All five prescribed Domain classes execute in the focused and full configurations; the unrelated full Debug process fixture failure remains open. |

The [retained coverage map](continuation-10-coverage-index.md) identifies the twelve section 6 boundary selectors and section 7 preservation selectors. Current full Server XML reruns their owning classes, including history/coordinator/rebuild durability. Direct finalizer assertions remain distinct from owning actor/store and real-sidecar assertions. No extra Cartesian expansion of every fault across every mode is invented; full Live startup/replica gates below remain unproven.

Both full Server logs contain **96** typed JSON observations, including working-root/receipt/depth and actual N9 observations beyond the compact 73-row cost packet. Exact complete inventories are retained in the [Debug](qualification-20261017-server-debug-full-all-observations.json) and [Release](qualification-20261017-server-release-full-all-observations.json) supplements. Independent numerical audits verify 30 Current rows across both R values, eight uniform single-route tails, two shared three-route rows, depth allocations and existing scheduler/retry formulas.

Focused Debug Live retains all **49** observations, including nine NativeAttempts and the two-replica recovery witness. Full Debug Live produced **zero** such observations because initialization failed. Full Release Live retains exactly **48**: fifteen Current, seven LiveCost, eight NativeAttempts, twelve generic recovery and the other six source/empty/cold/activation/typed rows. The missing `two_replica_original_receipt_recovery` NativeAttempts row is **unproven**, not zero. The [observed-row audit](qualification-20261017-live-release-full-numerical.json) checks all fifteen Current and six nonempty tail rows against unchanged ceilings, binds the missing witness to its native XML startup failure, and leaves the full native cohort red. The [R4096 audit](qualification-20261017-live-release-r4096-numerical.json) independently verifies its fifteen exact Current rows, eleven logical reads, zero event/effect work and proof/read-byte bounds.

## Failures and provenance

The full Debug Domain failure is `ProcessReadDeadlineTests.AdapterSlotExhaustionDoesNotBlockAuditedDisposition`: `ProcessReadAdmissionException` escapes while its finally block awaits blocked workers. The helper and fixture were unchanged since entry and outside this Story's change scope. Their precise path commits are respectively `0b38a562cd4fb6191a0c3c78a44b4c48d1f05390` and `31c29e6ea417d6f339beccb44a3ca9f229c8b96f`. Release passing is recorded separately and does not diagnose or waive Debug. See the [configuration comparison](qualification-20261017-domain-configuration-outcomes.json).

Full Debug Live records 89 regular collection initialization failures and one OQ8 initialization failure with daprd watcher ENOSPC, plus one OQ8 evidence-disposal error after initialization cleanup removed the shadow. Full Release records three distinct test failures referencing the same replica startup app/component/time/fatal line: source-guard original-receipt recovery, multi-host admission and event-evolution two-sidecar restart. These are not three separately observed replica launches. See [Debug failure evidence](qualification-20261017-live-debug-full-failure-audit.json) and [shared Release startup identity](qualification-20261017-live-release-full-shared-replica-startup.json). Filesystem/inotify limit probes are post-failure context only; disk/inotify root cause is not established. No sysctl, shared process, HotReload, policy or source change was applied.

All successful Release producer logs still emit Gateway, TestSubscriber and Client.TestContracts Debug output lines. Concrete matching base Release-directory copies and generated configuration identities are disclosed in the [final producer supplement](qualification-20261017-mixed-configuration-final-producer.json). Zero build diagnostics do not establish every dependency's Release configuration. Captured nested publish/RID trees may be inherited; they remain directory/copy inventories, with no fresh-compilation or runtime-loading claim.

Actual versions are [command-bound](qualification-20261017-actual-runtime-versions.json): SDK **10.0.401**, selected native runtime **10.0.12**, regular daprd **1.18.2**, installed OQ8 daprd **1.18.7**, owned Redis **6.2.21** with exact image/settings. Placement/Scheduler reject `--version`; their retained native startup logs identify **1.18.2**. Complete effective dependency versions/types/SHA512 values and raw assets are archived. Only fourteen inherited pre-restore assets from the very first build lack raw copies; their hashes remain retained, and every producer post-restore/after graph and native execution before/after graph has raw asset bytes.

The manifest pack retains exactly fourteen packages, normalized version `999.0.0-ci-test`, 22 internal exact dependency pins and 32 embedded DLL identities. The [archive copies](qualification-20261017-retained-package-bytes.json) preserve all fresh package bytes. These are local pack/consumer results, with no package publication.

## Live copy proof and cleanup

The [full Release Live binding](qualification-20261017-live-release-full-producer-binding.json) separately binds base entries/deps/runtimeconfig, test additional-deps/probing/startup inputs and copied nested inventories. OQ8's owning production matrix passed. Its [8055 native observation bytes](qualification-20261017-live-release-full-oq8-observations.json) are durably retained, including actual 1.18.7/PostgreSQL profile and test-seam declarations. Entry, hosting-startup and additional-deps hashes match. The original helper's full-set comparison sorted filenames too early; the [retained offline correction](qualification-20261017-oq8-runtime-set-ordering-correction.json) uses the fixture's exact sorted `relativePath:sha256` strings and matches both full native shadow-set digests. Original packets and the original false comparison remain unchanged. Failed Debug OQ8 has no native artifact receipt and no invented shadow proof.

The [R4096 binding](qualification-20261017-live-release-r4096-producer-binding.json) binds its actual Release test producer; it launches no OQ8 hosts. The first focused Debug lane uses its in-process Debug owning hosts. Queue labels are clarified by these exact supplemental mappings.

[Physical cleanup](qualification-20261017-owned-resource-cleanup.json) verifies four regular control lanes, all observed native process identities, two OQ8 owned containers/scratch directories, the owned Redis container and its anonymous volume are released. Four proven component/scratch paths were already absent; no manual removal was needed. All control exits are 0/0, ports are closed, and the watchdog recorded no timeout. Shared controls/Redis, historical template work, existing pinned binaries and external processes were preserved.

## Acceptance boundary

Both fresh Server current-policy secret-scanner tests passed. This does not dispose of historical P1R findings or supply human authority. The parent's [P1R decision packet](qualification-20261017-parent/p1r-owner-decision.md) and [authenticated assurance requirements](qualification-20261017-parent/authenticated-assurance-requirements.md) remain separate.

The failed native cohorts, mixed Release graph concern, authenticated exact-head sealed CI, owner assurance and AD-12/NFR8 transition remain open. Story 6.4 remains in progress. No Done/commit transition or overall production readiness is asserted by this finite local qualification.
